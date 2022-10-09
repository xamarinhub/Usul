namespace Usul.Providers.SerialPort;

public interface ISerialPortProvider : IProvider
{
    IList<string> GetPortNames();

    ISerialPort Open(SerialPortConfiguration configuration);
}

