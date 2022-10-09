namespace Usul.Providers;

public interface IProviderResource : IDisposable, IAsyncDisposable
{
    IProvider Provider { get; }

}

