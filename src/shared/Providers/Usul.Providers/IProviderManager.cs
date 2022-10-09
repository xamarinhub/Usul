namespace Usul.Providers;

public interface IProviderManager : IDisposable, IAsyncDisposable
{
    TProvider GetProvider<TProvider>() where TProvider : IProvider;
}

