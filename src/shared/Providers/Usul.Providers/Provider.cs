namespace Usul.Providers;

public abstract class Provider : IProvider
{
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return DisposeResourcesAsync();
    }

    protected virtual ValueTask DisposeResourcesAsync() =>
        ValueTask.CompletedTask;
}

