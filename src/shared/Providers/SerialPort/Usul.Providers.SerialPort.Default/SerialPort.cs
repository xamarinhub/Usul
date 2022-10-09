using System.Diagnostics.Contracts;
using Usul.Providers;
using Usul.Providers.SerialPort;

namespace Usul.SerialPort.Default;

public class SerialPort : ProviderResource, ISerialPort
{
    private readonly Port _port; 

    internal SerialPort(IProvider provider, SerialPortConfiguration configuration)
        : base(provider)
    {
        Contract.Requires(configuration is not null);
        /*
        _port = new Port(
            configuration!.Name, 
            configuration.BaudRate, 
            configuration.Parity.ToParity(), 
            configuration.DataBits, 
            configuration.StopBits.ToStopBits());
        */
        _port = new Port(configuration!.Name, 115200);
        _port!.Open();
    }

    #region String

    public string ReadExisting() => _port.ReadExisting();

    public virtual string ReadLine() =>
        _port.ReadLine();

    public virtual void WriteLine(string message) =>
        _port.WriteLine(message);

    public virtual void Write(string message) =>
        _port.Write(message);

    #endregion

    #region Binary
    public virtual void Read(byte[] buffer, int offset = 0, int count = -1) =>
        _port.Read(buffer, offset, count >= 0 ? count : buffer.Length);

    public virtual void Write(byte[] buffer, int offset = 0, int count = -1) =>
        _port.Write(buffer, offset, count >= 0 ? count : buffer.Length);
    #endregion

    #region Protected Methods

    protected void Close()
    {
        _port.Close();
        _port.Dispose();
    }

    protected override void DisposeResources()
    {
        Close();
        base.DisposeResources();
    }

    protected override ValueTask DisposeResourcesAsync()
    {
        Close();
        return base.DisposeResourcesAsync();
    }

    #endregion
}

