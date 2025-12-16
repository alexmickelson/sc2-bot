using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Web.Models;

namespace Web.Services;

public class LinuxHeadlessClientService : IDisposable
{
  private Process? _process;
  private readonly StringBuilder _logBuffer = new();
  private const int MaxLogLength = 50000;
  private CancellationTokenSource? _logWatcherCts;
  private readonly string StateFileName;
  private readonly string LogFileName;
  private readonly string PidFileName;

  public event Action<string>? OnLogReceived;
  public event Action? OnStateChanged;

  public bool IsRunning
  {
    get
    {
      if (_process == null)
        return false;
      try
      {
        return !_process.HasExited;
      }
      catch
      {
        // If we can't access the process (e.g. access denied or other issue), assume it's not running or we lost control
        return false;
      }
    }
  }
  public string Logs => _logBuffer.ToString();

  private readonly string _workingDirectory;

  public string ExecutablePath { get; set; }
  public string DataDir { get; set; }
  public string EglPath { get; set; }
  public string TempDir { get; set; } = "/tmp/sc2_temp";
  public int Port { get; set; } = 5000;
  public string Host { get; set; } = "127.0.0.1";

  public LinuxHeadlessClientService(PlayerInfo playerInfo)
  {
    var rootDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
    _workingDirectory = rootDir;

    ExecutablePath = Path.Combine(rootDir, "StarCraftII/Versions/Base75689/SC2_x64");
    DataDir = Path.Combine(rootDir, "StarCraftII");
    EglPath = FindLibEGL();

    Port = playerInfo.ClientPort;

    if (playerInfo.PlayerNumber == 2)
    {
      StateFileName = "headlessclient_p2.json";
      LogFileName = "sc2_headless_p2.log";
      PidFileName = "sc2_client_p2.pid";
      TempDir = "/tmp/sc2_temp_p2";
    }
    else
    {
      StateFileName = "headlessclient.json";
      LogFileName = "sc2_headless.log";
      PidFileName = "sc2_client.pid";
    }

    RecoverState();
  }

  private void RecoverState()
  {
    var statePath = Path.Combine(_workingDirectory, StateFileName);
    if (File.Exists(statePath))
    {
      try
      {
        var json = File.ReadAllText(statePath);
        var state = JsonSerializer.Deserialize<HeadlessClientState>(json);
        if (state != null)
        {
          try
          {
            var proc = Process.GetProcessById(state.Pid);
            // Verify it might be our process? Hard to be 100% sure without checking process name/path
            // but for now assume if it exists it's ours.
            if (!proc.HasExited)
            {
              _process = proc;
              _process.EnableRaisingEvents = true;
              _process.Exited += (s, e) => HandleProcessExit();

              Log($"Recovered process with PID {state.Pid}");
              StartWatchingLogs(state.LogPath);
            }
            else
            {
              Log($"Process {state.Pid} has exited.");
              File.Delete(statePath);
            }
          }
          catch (ArgumentException)
          {
            // Process not found
            Log($"Process {state.Pid} not found.");
            File.Delete(statePath);
          }
        }
      }
      catch (Exception ex)
      {
        Log($"Error recovering state: {ex.Message}");
      }
    }
  }

  public void StartProcess()
  {
    if (IsRunning)
      return;

    try
    {
      var logPath = Path.Combine(_workingDirectory, LogFileName);
      var pidFile = Path.Combine(_workingDirectory, PidFileName);

      // Clear previous logs
      lock (_logBuffer)
      {
        _logBuffer.Clear();
      }
      OnStateChanged?.Invoke();

      if (File.Exists(pidFile))
        File.Delete(pidFile);
      if (File.Exists(logPath))
        File.Delete(logPath);

      var arguments =
        $"-listen {Host} -port {Port} -eglpath {EglPath} -dataDir {DataDir} -tempDir {TempDir} -displayMode 0 -windowwidth 1024 -windowheight 768 -windowx 0 -windowy 0"; // Use nohup and backgrounding to detach completely
      // echo $! > pidFile writes the PID to a file
      var shellArgs =
        $"-c \"nohup '{ExecutablePath}' {arguments} > '{logPath}' 2>&1 & echo $! > '{pidFile}'\"";

      var startInfo = new ProcessStartInfo
      {
        FileName = "/bin/sh",
        Arguments = shellArgs,
        WorkingDirectory = _workingDirectory,
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        UseShellExecute = false,
        CreateNoWindow = true,
      };

      Log($"Starting process: {ExecutablePath} {arguments}");
      Log($"Logging to: {logPath}");

      using var starter = new Process { StartInfo = startInfo };
      starter.Start();
      starter.WaitForExit();

      if (starter.ExitCode == 0)
      {
        // Wait briefly for the pid file to be written
        Thread.Sleep(100);

        if (File.Exists(pidFile))
        {
          var pidText = File.ReadAllText(pidFile).Trim();
          File.Delete(pidFile); // Cleanup

          if (int.TryParse(pidText, out int pid))
          {
            try
            {
              _process = Process.GetProcessById(pid);
              _process.EnableRaisingEvents = true;
              _process.Exited += (s, e) => HandleProcessExit();

              // Save state
              var state = new HeadlessClientState
              {
                Pid = pid,
                LogPath = logPath,
                StartTime = DateTime.Now,
              };
              var json = JsonSerializer.Serialize(state);
              File.WriteAllText(Path.Combine(_workingDirectory, StateFileName), json);

              StartWatchingLogs(logPath);
              OnStateChanged?.Invoke();
            }
            catch (Exception ex)
            {
              Log($"Failed to attach to process {pid}: {ex.Message}");
            }
          }
          else
          {
            Log($"Failed to parse PID from file: {pidText}");
          }
        }
        else
        {
          Log("Failed to start process: PID file not created.");
        }
      }
      else
      {
        Log($"Failed to start process. Exit code: {starter.ExitCode}");
      }
    }
    catch (Exception ex)
    {
      Log($"Error starting process: {ex.Message}");
    }
  }

  private void HandleProcessExit()
  {
    Log("Process exited.");
    var statePath = Path.Combine(_workingDirectory, StateFileName);
    if (File.Exists(statePath))
      File.Delete(statePath);

    _logWatcherCts?.Cancel();
    OnStateChanged?.Invoke();
  }

  private void StartWatchingLogs(string logPath)
  {
    _logWatcherCts?.Cancel();
    _logWatcherCts = new CancellationTokenSource();
    var token = _logWatcherCts.Token;

    Task.Run(
      async () =>
      {
        try
        {
          // Wait for file to exist
          while (!File.Exists(logPath) && !token.IsCancellationRequested)
          {
            await Task.Delay(100, token);
          }

          // Open with FileShare.ReadWrite to allow writing while reading
          using var stream = new FileStream(
            logPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
          );
          using var reader = new StreamReader(stream);

          // Read existing content
          string? line;
          while ((line = await reader.ReadLineAsync()) != null)
          {
            Log(line, false);
          }
          OnLogReceived?.Invoke(Logs); // Trigger update for bulk load

          // Tail the file
          while (!token.IsCancellationRequested)
          {
            line = await reader.ReadLineAsync();
            if (line != null)
            {
              Log(line);
            }
            else
            {
              // Wait for more data
              await Task.Delay(500, token);

              // If the file was deleted/recreated, we might need to handle that?
              // For now assume log file is stable for the process lifetime.
            }
          }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          Log($"Error watching logs: {ex.Message}");
        }
      },
      token
    );
  }

  public void KillProcess()
  {
    if (_process != null)
    {
      Log("Stopping process...");
      try
      {
        // Try graceful shutdown first (SIGTERM)
        Process.Start("kill", _process.Id.ToString())?.WaitForExit();

        if (!_process.WaitForExit(5000)) // Wait 5 seconds for cleanup
        {
          Log("Process did not exit gracefully, forcing kill...");
          _process.Kill(true); // Kill entire process tree
          _process.WaitForExit(2000);
        }
      }
      catch (Exception ex)
      {
        Log($"Error killing process: {ex.Message}");
        try
        {
          _process.Kill(true);
        }
        catch { }
      }
    }

    HandleProcessExit();

    lock (_logBuffer)
    {
      _logBuffer.Clear();
    }
    OnStateChanged?.Invoke();
  }

  public void RestartProcess()
  {
    KillProcess();
    StartProcess();
  }

  private void Log(string? message, bool notify = true)
  {
    if (string.IsNullOrEmpty(message))
      return;

    // If message already has timestamp (from file), maybe don't add another?
    // But our file is raw output, so we should add timestamp.
    var timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";

    lock (_logBuffer)
    {
      if (_logBuffer.Length > MaxLogLength)
      {
        _logBuffer.Remove(0, _logBuffer.Length - MaxLogLength + 1000); // Trim
      }
      _logBuffer.AppendLine(timestampedMessage);
    }

    if (notify)
      OnLogReceived?.Invoke(timestampedMessage);
  }

  public void Dispose()
  {
    _logWatcherCts?.Cancel();
    // Do NOT kill process on dispose
  }

  private string FindLibEGL()
  {
    var psi = new ProcessStartInfo
    {
      FileName = "/bin/bash",
      RedirectStandardOutput = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };
    psi.ArgumentList.Add("-c");
    psi.ArgumentList.Add(
      "if [ ! -z \"$LD_LIBRARY_PATH\" ]; then for p in ${LD_LIBRARY_PATH//:/ }; do f=\"$p/libEGL.so\"; [[ -e \"$f\" ]] && echo \"$f\" && break; done; fi"
    );

    using var p = Process.Start(psi);
    string output = p.StandardOutput.ReadToEnd().Trim();
    p.WaitForExit();
    return output;
  }
}

public class HeadlessClientState
{
  public int Pid { get; set; }
  public string LogPath { get; set; } = "";
  public DateTime StartTime { get; set; }
}
