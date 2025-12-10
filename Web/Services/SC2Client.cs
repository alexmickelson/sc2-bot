using System.Net.WebSockets;
using System.Text.Json;
using Google.Protobuf;
using SC2APIProtocol;

namespace Web.Services;

public class SC2Client : IDisposable
{
  private ClientWebSocket? _webSocket;
  private readonly string _url;
  private CancellationTokenSource? _cts;
  
  // Store request/response pairs
  public List<RequestResponsePair> History { get; } = new();
  public event System.Action? OnHistoryUpdated;

  public bool IsConnected => _webSocket?.State == WebSocketState.Open;

  public SC2Client(string url = "ws://127.0.0.1:5000/sc2api")
  {
    _url = url;
  }

  public async Task ConnectAsync()
  {
    if (_webSocket != null && _webSocket.State == WebSocketState.Open) return;

    _webSocket = new ClientWebSocket();
    _cts = new CancellationTokenSource();
    await _webSocket.ConnectAsync(new Uri(_url), _cts.Token);
  }

  public async Task<Response> SendRequestAsync(Request request)
  {
    if (_webSocket == null || _webSocket.State != WebSocketState.Open)
    {
      throw new InvalidOperationException("Not connected to SC2.");
    }

    var bytes = request.ToByteArray();
    await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, _cts!.Token);

    using var ms = new MemoryStream();
    var buffer = new byte[1024 * 32]; 
    WebSocketReceiveResult result;
    do
    {
        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
        ms.Write(buffer, 0, result.Count);
    } while (!result.EndOfMessage);

    ms.Seek(0, SeekOrigin.Begin);
    var response = Response.Parser.ParseFrom(ms);

    // Log to history
    History.Add(new RequestResponsePair(request, response, DateTime.Now));
    OnHistoryUpdated?.Invoke();

    return response;
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

  public void Dispose()
  {
    _cts?.Cancel();
    _webSocket?.Dispose();
  }
}

public record RequestResponsePair(Request Request, Response Response, DateTime Timestamp);
