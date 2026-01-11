using System.Text;

namespace Olbrasoft.VirtualAssistant.Service.Services;

public class SingleInstanceLockManager : ISingleInstanceLockManager
{
    private FileStream? _lockFile;
    private bool _disposed;

    public string LockFilePath { get; }

    public SingleInstanceLockManager(string lockFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        LockFilePath = lockFilePath;
    }

    public bool TryAcquire()
    {
        if (_lockFile != null)
            return true;

        try
        {
            _lockFile = new FileStream(
                LockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            var pid = Environment.ProcessId.ToString();
            _lockFile.SetLength(0);
            var bytes = Encoding.UTF8.GetBytes(pid);
            _lockFile.Write(bytes, 0, bytes.Length);
            _lockFile.Flush();

            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Release()
    {
        try
        {
            _lockFile?.Dispose();
            _lockFile = null;

            if (File.Exists(LockFilePath))
            {
                File.Delete(LockFilePath);
            }
        }
        catch
        {
            // Ignore cleanup errors - best effort
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Release();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
