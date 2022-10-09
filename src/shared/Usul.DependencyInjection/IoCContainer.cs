using System.Diagnostics.Contracts;
using Autofac;

namespace Usul.DependencyInjection;

public class IoCContainer : Disposable, IIoCContainer
{
    private readonly IContainer _container;
    private readonly ILifetimeScope _lifetimeScope;

    internal IoCContainer(IContainer container)
    {
        Contract.Requires(container is not null);
        _container = container!;
        _lifetimeScope = _container.BeginLifetimeScope();
    }

    public TInterface Resolve<TInterface>() where TInterface : notnull =>
        _lifetimeScope.Resolve<TInterface>();

    protected override void DisposeResources()
    {
        _lifetimeScope.Dispose();
        _container.Dispose();
    }

    protected override async ValueTask DisposeResourcesAsync()
    {
        await _lifetimeScope.DisposeAsync();
        await _container.DisposeAsync();
    }

}

