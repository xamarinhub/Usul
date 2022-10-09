namespace Usul;

public abstract class Disposable : IDisposable, IAsyncDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        DisposeResources();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return DisposeResourcesAsync();
    }

    protected virtual void DisposeResources()
    {

    }

    protected virtual ValueTask DisposeResourcesAsync() =>
        ValueTask.CompletedTask;
}

