using System.Text;

namespace Olbrasoft.VirtualAssistant.Service.Services;

public class SingleInstanceLockManager : ISingleInstanceLockManager
{
    private readonly object _lock = new();
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
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_lockFile != null)
                return true;

            FileStream? stream = null;
            try
            {
                stream = new FileStream(
                    LockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                var pid = Environment.ProcessId.ToString();
                stream.SetLength(0);
                var bytes = Encoding.UTF8.GetBytes(pid);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();

                _lockFile = stream;
                return true;
            }
            catch (IOException)
            {
                stream?.Dispose();
                return false;
            }
            catch
            {
                stream?.Dispose();
                throw;
            }
        }
    }

    public void Release()
    {
        lock (_lock)
        {
            if (_lockFile == null)
                return;

            try
            {
                _lockFile.Dispose();
                _lockFile = null;

                if (File.Exists(LockFilePath))
                {
                    File.Delete(LockFilePath);
                }
            }
            catch
            {
                _lockFile = null;
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Release();
            }

            _disposed = true;
        }
    }
}
