namespace Usul.Providers.SerialPort;

public interface ISerialPortProvider : IProvider
{
    Task<IList<string>> GetPortNamesAsync();

    Task<ISerialPort> OpenAsync(SerialPortConfiguration configuration);
}

