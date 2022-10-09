namespace Usul.Providers.SerialPort.Default;

public class SerialPortProvider : Provider, ISerialPortProvider
{
    public Task<IList<string>> GetPortNamesAsync() =>
        Task.FromResult<IList<string>>(Port.GetPortNames());

    public virtual Task<ISerialPort> OpenAsync(SerialPortConfiguration configuration) =>
        Task.FromResult<ISerialPort>(new SerialPort(this, configuration));
}

