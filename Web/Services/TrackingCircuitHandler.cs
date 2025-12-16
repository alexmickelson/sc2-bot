using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Web.Services;

public class TrackingCircuitHandler : CircuitHandler, IDisposable
{
  private readonly CancellationTokenSource _cts = new();
  public CancellationToken CircuitToken => _cts.Token;

  public event EventHandler? ConnectionDown;
  public event EventHandler? ConnectionUp;

  public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
  {
    ConnectionUp?.Invoke(this, EventArgs.Empty);
    return Task.CompletedTask;
  }

  public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
  {
    ConnectionDown?.Invoke(this, EventArgs.Empty);
    _cts.Cancel();
    return Task.CompletedTask;
  }

  public void Dispose()
  {
    _cts.Dispose();
  }
}
