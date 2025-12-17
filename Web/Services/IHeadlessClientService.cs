namespace Web.Services;

public interface IHeadlessClientService : IDisposable
{
  event Action<string>? OnLogReceived;
  event Action? OnStateChanged;

  bool IsRunning { get; }
  string Logs { get; }

  string ExecutablePath { get; set; }
  string DataDir { get; set; }
  string EglPath { get; set; }
  string TempDir { get; set; }
  int Port { get; set; }
  string Host { get; set; }

  void StartProcess();
  void KillProcess();
  void RestartProcess();
}
