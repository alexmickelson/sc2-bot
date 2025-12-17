using System.Net.WebSockets;
using System.Text.Json;
using Google.Protobuf;
using SC2APIProtocol;
using Web.Models;

namespace Web.Services;

public class SC2Client : IDisposable
{
  private readonly WebSocketService _webSocketService;
  private readonly string _url;

  // Store request/response pairs
  public List<RequestResponsePair> History { get; } = new();
  public event System.Action? OnHistoryUpdated;
  public event System.Action? OnGameStateChanged;
  public event System.Action? OnConnectionStateChanged;

  public WebSocketState ConnectionState => _webSocketService.WebSocketState;
  public Status CurrentStatus { get; private set; } = Status.Unknown;

  public SC2Client(WebSocketService webSocketService, WebPlayerInfo playerInfo)
  {
    _webSocketService = webSocketService;
    _url = $"ws://127.0.0.1:{playerInfo.ClientPort}/sc2api";
  }

  public async Task ConnectAsync()
  {
    await _webSocketService.ConnectAsync(_url);
    OnConnectionStateChanged?.Invoke();
  }

  public async Task<Response> SendRequestAsync(Request request)
  {
    var bytes = request.ToByteArray();
    Console.WriteLine($"Sending Request: {request}");
    var responseBytes = await _webSocketService.SendReceiveAsync(bytes);
    var response = Response.Parser.ParseFrom(responseBytes);

    if (response.Status != CurrentStatus)
    {
      CurrentStatus = response.Status;
      OnGameStateChanged?.Invoke();
    }

    // Log to history
    History.Add(new RequestResponsePair(request, response, DateTime.Now));

    if (History.Count > 130)
    {
      History.RemoveRange(0, History.Count - 100);
    }

    OnHistoryUpdated?.Invoke();

    return response;
  }

  public async Task MoveCameraAsync(int x, int y)
  {
    var request = new Request
    {
      Action = new RequestAction
      {
        Actions =
        {
          new SC2APIProtocol.Action
          {
            ActionFeatureLayer = new ActionSpatial
            {
              CameraMove = new ActionSpatialCameraMove
              {
                CenterMinimap = new PointI { X = x, Y = y },
              },
            },
          },
        },
      },
    };
    await SendRequestAsync(request);
  }

  public async Task<Status> PingAsync()
  {
    var request = new Request { Ping = new RequestPing() };
    var response = await SendRequestAsync(request);
    return response.Status;
  }

  public async Task<ResponseObservation> GetObservationAsync()
  {
    var request = new Request { Observation = new RequestObservation() };
    var response = await SendRequestAsync(request);
    return response.Observation;
  }

  public async Task<ResponseAvailableMaps> GetAvailableMapsAsync()
  {
    var request = new Request { AvailableMaps = new RequestAvailableMaps() };
    var response = await SendRequestAsync(request);
    // Console.WriteLine(JsonSerializer.Serialize(response));
    return response.AvailableMaps;
  }

  public async Task<ResponseGameInfo> GetGameInfoAsync()
  {
    var request = new Request { GameInfo = new RequestGameInfo() };
    var response = await SendRequestAsync(request);
    return response.GameInfo;
  }

  public async Task DisconnectAsync()
  {
    await _webSocketService.DisconnectAsync();
    OnConnectionStateChanged?.Invoke();
  }

  public void Dispose()
  {
    _webSocketService.Dispose();
    OnConnectionStateChanged?.Invoke();
  }
}

public record RequestResponsePair(Request Request, Response Response, DateTime Timestamp);
