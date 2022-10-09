using Microsoft.Extensions.DependencyInjection;

namespace Usul.Providers;

public static class ProviderManagerExtensions
{
    public static IServiceCollection AddProviderManager(
        this IServiceCollection serviceCollection,
        Func<ProviderManagerBuilder, ProviderManagerBuilder> providers)
    {
        var builder = new ProviderManagerBuilder();
        builder = providers(builder);

        var providerManager = builder.Build();
        serviceCollection.AddSingleton(_ => providerManager);

        return serviceCollection;
    }
}

