namespace Usul.Providers.SerialPort;

public interface ISerialPort : IDisposable
{
    #region String
    string ReadExisting();

    string ReadLine();

    void WriteLine(string message);

    void Write(string message);
    #endregion

    #region Binary
    void Read(byte[] buffer, int offset = 0, int count = -1);

    void Write(byte[] buffer, int offset = 0, int count = -1);
    #endregion
}
