namespace Usul.Providers;

public abstract class ProviderResource : Disposable, IProviderResource
{
    protected ProviderResource(IProvider provider) =>
        Provider = provider;
    
    public IProvider Provider { get; init; }
}

