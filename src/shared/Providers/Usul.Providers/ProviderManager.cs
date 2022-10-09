using System.Diagnostics.Contracts;
using Usul.DependencyInjection;

namespace Usul.Providers;

public class ProviderManager : Disposable, IProviderManager
{
    private readonly IIoCContainer _container;

    internal ProviderManager(IIoCContainer container)
    {
        Contract.Requires(container is not null);
        _container = container!;
    }

    public TProvider GetProvider<TProvider>() where TProvider : IProvider =>
        _container.Resolve<TProvider>();
    
}

