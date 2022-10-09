using Usul.DependencyInjection;
using Usul.Patterns;

namespace Usul.Providers;

public class ProviderManagerBuilder : Builder<IProviderManager>
{
    private readonly IoCContainerBuilder _containerBuilder = new ();

    public ProviderManagerBuilder Register<TInterface, TImplementation>() 
        where TInterface : notnull 
        where TImplementation : class
    {
        _containerBuilder.Register<TInterface, TImplementation>();
        return this;
    }

    public override IProviderManager Build()
    {
        var container = _containerBuilder.Build();
        return new ProviderManager(container);
    }
        
}

