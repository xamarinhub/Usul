using Autofac;
using Usul.Patterns;

namespace Usul.DependencyInjection;

public class IoCContainerBuilder : Builder<IIoCContainer>
{
    private readonly ContainerBuilder _containerBuilder = new();

    public void Register<TInterface, TImplementation>()
        where TInterface : notnull
        where TImplementation : class =>
        _containerBuilder.RegisterType<TImplementation>().As<TInterface>();
    
    public override IIoCContainer Build() =>
        new IoCContainer(_containerBuilder.Build());
}

