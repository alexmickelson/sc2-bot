using System.Text.Json;
using Web.Models;

namespace Web.Services;

public class ClientGroup : IDisposable
{
  public required string Key { get; init; }
  public required WebPlayerInfo PlayerInfo { get; init; }
  public required WebSocketService WebSocketService { get; init; }
  public required SC2Client SC2Client { get; init; }
  public required IHeadlessClientService HeadlessClientService { get; init; }

  public void Dispose()
  {
    SC2Client.Dispose();
    WebSocketService.Dispose();
    HeadlessClientService.Dispose();
  }
}

public class ClientManager : IDisposable
{
  private readonly Dictionary<string, ClientGroup> _groups = new();
  private readonly string _keysFilePath;

  public ClientManager()
  {
    var rootDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
    var stateDir = Path.Combine(rootDir, "clientStateData");
    if (!Directory.Exists(stateDir))
    {
      Directory.CreateDirectory(stateDir);
    }
    _keysFilePath = Path.Combine(stateDir, "clientKeys.json");
    LoadKeys();
  }

  public event Action? OnGroupChanged;

  public ClientGroup GetOrCreateGroup(string key)
  {
    if (_groups.TryGetValue(key, out var group))
    {
      return group;
    }

    var port = GetNextAvailablePort();
    var player1Info = new WebPlayerInfo(key, port);
    var ws1 = new WebSocketService();
    var newGroup = new ClientGroup
    {
      Key = key,
      PlayerInfo = player1Info,
      WebSocketService = ws1,
      SC2Client = new SC2Client(ws1, player1Info),
      HeadlessClientService = new LinuxHeadlessClientService(player1Info),
    };
    _groups[key] = newGroup;
    SaveKeys();
    OnGroupChanged?.Invoke();

    return newGroup;
  }

  private int GetNextAvailablePort()
  {
    var port = 5010;
    while (_groups.Values.Any(g => g.PlayerInfo.ClientPort == port))
    {
      port += 10;
    }
    return port;
  }

  public IEnumerable<ClientGroup> GetAllGroups()
  {
    return _groups.Values;
  }

  public void RemoveGroup(string key)
  {
    if (_groups.TryGetValue(key, out var group))
    {
      group.Dispose();
      group.HeadlessClientService.KillProcess();
      _groups.Remove(key);
      SaveKeys();
      OnGroupChanged?.Invoke();
    }
  }

  private void SaveKeys()
  {
    try
    {
      var keys = _groups.Keys.ToList();
      var json = JsonSerializer.Serialize(keys);
      File.WriteAllText(_keysFilePath, json);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error saving client keys: {ex.Message}");
    }
  }

  private void LoadKeys()
  {
    if (!File.Exists(_keysFilePath))
      return;

    try
    {
      var json = File.ReadAllText(_keysFilePath);
      var keys = JsonSerializer.Deserialize<List<string>>(json);
      if (keys != null)
      {
        foreach (var key in keys)
        {
          GetOrCreateGroup(key);
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error loading client keys: {ex.Message}");
    }
  }

  public void Dispose()
  {
    foreach (var group in _groups.Values)
    {
      group.Dispose();
    }
  }
}
