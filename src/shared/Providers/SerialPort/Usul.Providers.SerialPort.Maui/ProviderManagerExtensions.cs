namespace Usul.Providers.SerialPort.Maui;

public static class ProviderManagerExtensions
{
    public static ProviderManagerBuilder AddSerialPort(this ProviderManagerBuilder builder) =>
        builder.Register<ISerialPortProvider, SerialPortProvider>();
    
}

