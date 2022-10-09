using System.Diagnostics.Contracts;
using Usul.Providers;
using Usul.Providers.SerialPort;

namespace Usul.SerialPort.Default;

public class SerialPortProvider : Provider, ISerialPortProvider
{
    public IList<string> GetPortNames() =>
        Port.GetPortNames();

    public virtual ISerialPort Open(SerialPortConfiguration configuration)
    {
        Contract.Requires(configuration is not null);
        return new Usul.SerialPort.Default.SerialPort(this, configuration!);
    }
}

