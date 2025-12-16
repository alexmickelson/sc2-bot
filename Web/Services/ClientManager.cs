using Web.Models;

namespace Web.Services;

public class ClientGroup : IDisposable
{
  public required string Key { get; init; }
  public required PlayerInfo PlayerInfo { get; init; }
  public required WebSocketService WebSocketService { get; init; }
  public required SC2Client SC2Client { get; init; }
  public required LinuxHeadlessClientService LinuxHeadlessClientService { get; init; }

  public void Dispose()
  {
    SC2Client.Dispose();
    WebSocketService.Dispose();
    LinuxHeadlessClientService.Dispose();
  }
}

public class ClientManager : IDisposable
{
  private readonly Dictionary<string, ClientGroup> _groups = new();

  public ClientManager()
  {
    // Initialize player1
    var player1Info = new PlayerInfo(1, 5000, 5100);
    var ws1 = new WebSocketService();
    _groups["player1"] = new ClientGroup
    {
      Key = "player1",
      PlayerInfo = player1Info,
      WebSocketService = ws1,
      SC2Client = new SC2Client(ws1, player1Info),
      LinuxHeadlessClientService = new LinuxHeadlessClientService(player1Info),
    };

    // Initialize player2
    var player2Info = new PlayerInfo(2, 6000, 6100);
    var ws2 = new WebSocketService();
    _groups["player2"] = new ClientGroup
    {
      Key = "player2",
      PlayerInfo = player2Info,
      WebSocketService = ws2,
      SC2Client = new SC2Client(ws2, player2Info),
      LinuxHeadlessClientService = new LinuxHeadlessClientService(player2Info),
    };
  }

  public ClientGroup GetGroup(string key)
  {
    if (_groups.TryGetValue(key, out var group))
    {
      return group;
    }
    throw new KeyNotFoundException($"Client group '{key}' not found.");
  }

  public ClientGroup Player1 => GetGroup("player1");
  public ClientGroup Player2 => GetGroup("player2");

  public void Dispose()
  {
    foreach (var group in _groups.Values)
    {
      group.Dispose();
    }
  }
}
