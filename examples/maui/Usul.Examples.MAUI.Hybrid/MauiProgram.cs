using Usul.Examples.MAUI.Hybrid.Data;
using Usul.Providers;
using Usul.Providers.SerialPort.Maui;

namespace Usul.Examples.MAUI.Hybrid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
	builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        builder.Services.AddProviderManager(b => b.AddSerialPort() );
        builder.Services.AddSingleton<WeatherForecastService>();

        return builder.Build();
    }
}
