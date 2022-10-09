using Usul.SerialPort.Default;

namespace Usul.Providers.SerialPort.Default;

public static class ProviderManagerExtensions
{
    public static ProviderManagerBuilder AddSerialPort(this ProviderManagerBuilder builder) =>
        builder.Register<ISerialPortProvider, SerialPortProvider>();
    
}

