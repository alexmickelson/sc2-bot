using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Web.Services;

public class LinuxHeadlessClientService : IHostedService, IDisposable
{
    private Process? _process;
    private readonly StringBuilder _logBuffer = new();
    private const int MaxLogLength = 50000;
    private CancellationTokenSource? _logWatcherCts;
    private const string StateFileName = "headlessclient.json";
    private const string LogFileName = "sc2_headless.log";
    
    public event Action<string>? OnLogReceived;
    public event Action? OnStateChanged;

    public bool IsRunning
    {
        get
        {
            if (_process == null) return false;
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

    public LinuxHeadlessClientService()
    {
        var rootDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
        _workingDirectory = rootDir;
        
        ExecutablePath = Path.Combine(rootDir, "StarCraftII/Versions/Base75689/SC2_x64");
        DataDir = Path.Combine(rootDir, "StarCraftII");
        EglPath = "/nix/store/z88avybj8n2svi9wv1hl937k2k3mbc2d-libglvnd-1.7.0/lib/libEGL.so";

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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Do NOT kill process on app stop, to allow it to survive restarts
        _logWatcherCts?.Cancel();
        return Task.CompletedTask;
    }

    public void StartProcess()
    {
        if (IsRunning) return;

        try
        {
            var logPath = Path.Combine(_workingDirectory, LogFileName);
            var arguments = $"-listen {Host} -port {Port} -eglpath {EglPath} -dataDir {DataDir} -tempDir {TempDir} -displayMode 0 -windowwidth 1024 -windowheight 768 -windowx 0 -windowy 0";
            
            // Use sh -c exec to replace shell with process, and redirect output to file
            var shellArgs = $"-c \"exec '{ExecutablePath}' {arguments} > '{logPath}' 2>&1\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = shellArgs,
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _process = new Process { StartInfo = startInfo };
            _process.EnableRaisingEvents = true;
            _process.Exited += (s, e) => HandleProcessExit();

            Log($"Starting process: {ExecutablePath} {arguments}");
            Log($"Logging to: {logPath}");
            
            if (_process.Start())
            {
                // Save state
                var state = new HeadlessClientState { Pid = _process.Id, LogPath = logPath, StartTime = DateTime.Now };
                var json = JsonSerializer.Serialize(state);
                File.WriteAllText(Path.Combine(_workingDirectory, StateFileName), json);

                StartWatchingLogs(logPath);
                OnStateChanged?.Invoke();
            }
            else
            {
                Log("Failed to start process.");
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
        if (File.Exists(statePath)) File.Delete(statePath);
        
        _logWatcherCts?.Cancel();
        OnStateChanged?.Invoke();
    }

    private void StartWatchingLogs(string logPath)
    {
        _logWatcherCts?.Cancel();
        _logWatcherCts = new CancellationTokenSource();
        var token = _logWatcherCts.Token;

        Task.Run(async () =>
        {
            try
            {
                // Wait for file to exist
                while (!File.Exists(logPath) && !token.IsCancellationRequested)
                {
                    await Task.Delay(100, token);
                }

                using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);

                // Read existing content first? Or just tail?
                // Let's read existing content to populate buffer
                var existing = await reader.ReadToEndAsync();
                if (!string.IsNullOrEmpty(existing))
                {
                    foreach (var line in existing.Split('\n'))
                    {
                        if (!string.IsNullOrWhiteSpace(line)) Log(line, false);
                    }
                    OnLogReceived?.Invoke(Logs); // Trigger update for bulk load
                }

                while (!token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null)
                    {
                        Log(line);
                    }
                    else
                    {
                        await Task.Delay(100, token);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Error watching logs: {ex.Message}");
            }
        }, token);
    }

    public void KillProcess()
    {
        if (_process != null)
        {
            Log("Stopping process...");
            try
            {
                _process.Kill(true); // Kill entire process tree
                _process.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                Log($"Error killing process: {ex.Message}");
            }
        }
        
        HandleProcessExit();
    }

    public void RestartProcess()
    {
        KillProcess();
        StartProcess();
    }

    private void Log(string? message, bool notify = true)
    {
        if (string.IsNullOrEmpty(message)) return;
        
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
        
        if (notify) OnLogReceived?.Invoke(timestampedMessage);
    }

    public void Dispose()
    {
        _logWatcherCts?.Cancel();
        // Do NOT kill process on dispose
    }
}

public class HeadlessClientState
{
    public int Pid { get; set; }
    public string LogPath { get; set; } = "";
    public DateTime StartTime { get; set; }
}
