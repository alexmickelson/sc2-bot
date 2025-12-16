using System.Net.WebSockets;

namespace Web.Services;

public class WebSocketService : IDisposable
{
  private ClientWebSocket? _webSocket;
  private CancellationTokenSource? _cts;
  private readonly SemaphoreSlim _lock = new(1, 1);

  public WebSocketState WebSocketState => _webSocket?.State ?? WebSocketState.None;

  public async Task ConnectAsync(string url)
  {
    await _lock.WaitAsync();
    try
    {
      if (_webSocket != null)
      {
        if (_webSocket.State == WebSocketState.Open)
          return;

        try
        {
          _webSocket.Dispose();
        }
        catch { }
      }

      _cts?.Dispose();

      _webSocket = new ClientWebSocket();
      _cts = new CancellationTokenSource();
      await _webSocket.ConnectAsync(new Uri(url), _cts.Token);
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task<byte[]> SendReceiveAsync(byte[] requestBytes)
  {
    await _lock.WaitAsync();
    try
    {
      if (_webSocket == null || _webSocket.State != WebSocketState.Open)
      {
        throw new InvalidOperationException("Not connected to WebSocket.");
      }

      await _webSocket.SendAsync(
        new ArraySegment<byte>(requestBytes),
        WebSocketMessageType.Binary,
        true,
        _cts!.Token
      );

      using var ms = new MemoryStream();
      var buffer = new byte[1024 * 32];
      WebSocketReceiveResult result;
      do
      {
        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
        if (result.MessageType == WebSocketMessageType.Close)
        {
          await _webSocket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            string.Empty,
            CancellationToken.None
          );
          throw new WebSocketException("WebSocket closed by server.");
        }
        ms.Write(buffer, 0, result.Count);
      } while (!result.EndOfMessage);

      return ms.ToArray();
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task DisconnectAsync()
  {
    _cts?.Cancel();
    if (
      _webSocket != null
      && (
        _webSocket.State == WebSocketState.Open
        || _webSocket.State == WebSocketState.CloseReceived
        || _webSocket.State == WebSocketState.CloseSent
      )
    )
    {
      try
      {
        await _webSocket.CloseAsync(
          WebSocketCloseStatus.NormalClosure,
          "Client disconnecting",
          CancellationToken.None
        );
      }
      catch (Exception)
      {
        // Ignore errors during close
      }
    }
  }

  public void Dispose()
  {
    _cts?.Cancel();
    _webSocket?.Dispose();
    _lock.Dispose();
  }
}
