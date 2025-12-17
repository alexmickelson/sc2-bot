using Web.Models;

namespace Web.Services;

public class ClientGroup : IDisposable
{
  public required string Key { get; init; }
  public required WebPlayerInfo PlayerInfo { get; init; }
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
    // var player1Info = new PlayerInfo(1, 5000, 5100);
    // var ws1 = new WebSocketService();
    // _groups["player1"] = new ClientGroup
    // {
    //   Key = "player1",
    //   PlayerInfo = player1Info,
    //   WebSocketService = ws1,
    //   SC2Client = new SC2Client(ws1, player1Info),
    //   LinuxHeadlessClientService = new LinuxHeadlessClientService(player1Info),
    // };
  }

  public ClientGroup GetOrCreateGroup(string key)
  {
    if (_groups.TryGetValue(key, out var group))
    {
      return group;
    }

    var port = GetNextAvailablePort();
    var player1Info = new WebPlayerInfo(1, port);
    var ws1 = new WebSocketService();
    _groups[key] = new ClientGroup
    {
      Key = key,
      PlayerInfo = player1Info,
      WebSocketService = ws1,
      SC2Client = new SC2Client(ws1, player1Info),
      LinuxHeadlessClientService = new LinuxHeadlessClientService(player1Info),
    };
    return _groups[key];
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

  public void Dispose()
  {
    foreach (var group in _groups.Values)
    {
      group.Dispose();
    }
  }
}
