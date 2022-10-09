using System.Diagnostics.Contracts;

namespace Usul.Providers.SerialPort.Maui;

public class SerialPortProvider : Provider, ISerialPortProvider
{
    public IList<string> GetPortNames() =>
        Port.GetPortNames();

    public virtual ISerialPort Open(SerialPortConfiguration configuration)
    {
        Contract.Requires(configuration is not null);
        return new Providers.SerialPort.Maui.SerialPort(this, configuration!);
    }
}

