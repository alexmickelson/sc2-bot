using Microsoft.AspNetCore.Components;
using Web.Services;

namespace Web.Components;

public abstract class ReliableIDisposable : ComponentBase, IDisposable
{
  [Inject]
  protected TrackingCircuitHandler CircuitHandler { get; set; } = default!;

  private readonly CancellationTokenSource _disposalCts = new();
  private CancellationTokenSource? _linkedCts;

  protected CancellationToken ComponentToken
  {
    get
    {
      if (_linkedCts == null)
      {
        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
          _disposalCts.Token,
          CircuitHandler.CircuitToken
        );
      }
      return _linkedCts.Token;
    }
  }

  public virtual void Dispose()
  {
    _disposalCts.Cancel();
    _disposalCts.Dispose();
    _linkedCts?.Dispose();
    GC.SuppressFinalize(this);
  }
}
