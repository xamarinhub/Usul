using Usul.Providers.SerialPort.Default;

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui;

#if !ANDROID
internal partial class SerialPortProviderMaui
{
    private readonly SerialPortProvider _provider = new();

    public partial Task<IList<string>> GetPortNamesAsync() => _provider.GetPortNamesAsync();

    public partial Task<ISerialPort> OpenAsync(SerialPortConfiguration configuration) => _provider.OpenAsync(configuration);

    public partial ValueTask DisposeAsync() => _provider.DisposeAsync();
}
#endif

