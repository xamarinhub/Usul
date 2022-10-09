namespace Usul.Providers.SerialPort.Maui;

internal partial class SerialPortProviderMaui : ISerialPortProvider
{
    public partial Task<IList<string>> GetPortNamesAsync();

    public partial Task<ISerialPort> OpenAsync(SerialPortConfiguration configuration);

    public partial ValueTask DisposeAsync();
}

