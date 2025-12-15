using System.Diagnostics;
using System.Text;

namespace Web.Services;

public class LinuxHeadlessClientService : IHostedService, IDisposable
{
    private Process? _process;
    private readonly StringBuilder _logBuffer = new();
    private const int MaxLogLength = 50000;
    
    public event Action<string>? OnLogReceived;
    public event Action? OnStateChanged;

    public bool IsRunning => _process != null && !_process.HasExited;
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
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // We don't auto-start on app launch unless desired. 
        // For now, let's just initialize.
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        KillProcess();
        return Task.CompletedTask;
    }

    public void StartProcess()
    {
        if (IsRunning) return;

        try
        {
            var arguments = $"-listen {Host} -port {Port} -eglpath {EglPath} -dataDir {DataDir} -tempDir {TempDir} -displayMode 0 -windowwidth 1024 -windowheight 768 -windowx 0 -windowy 0";

            var startInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = arguments,
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Set environment variables if needed
            // startInfo.EnvironmentVariables["LD_LIBRARY_PATH"] = ...

            _process = new Process { StartInfo = startInfo };
            
            _process.OutputDataReceived += (sender, e) => Log(e.Data);
            _process.ErrorDataReceived += (sender, e) => Log(e.Data);
            _process.Exited += (sender, e) => 
            {
                Log("Process exited.");
                OnStateChanged?.Invoke();
            };
            _process.EnableRaisingEvents = true;

            Log($"Starting process: {ExecutablePath} {arguments}");
            
            if (_process.Start())
            {
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
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

    public void KillProcess()
    {
        if (_process != null && !_process.HasExited)
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
        _process = null;
        OnStateChanged?.Invoke();
    }

    public void RestartProcess()
    {
        KillProcess();
        StartProcess();
    }

    private void Log(string? message)
    {
        if (string.IsNullOrEmpty(message)) return;
        
        var timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
        
        lock (_logBuffer)
        {
            if (_logBuffer.Length > MaxLogLength)
            {
                _logBuffer.Remove(0, _logBuffer.Length - MaxLogLength + 1000); // Trim
            }
            _logBuffer.AppendLine(timestampedMessage);
        }
        
        OnLogReceived?.Invoke(timestampedMessage);
    }

    public void Dispose()
    {
        KillProcess();
    }
}
