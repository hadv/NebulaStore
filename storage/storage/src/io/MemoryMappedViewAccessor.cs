using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// Enhanced memory-mapped view accessor with performance monitoring and safety features.
/// </summary>
public class MemoryMappedViewAccessor : IMemoryMappedViewAccessor
{
    private readonly System.IO.MemoryMappedFiles.MemoryMappedViewAccessor _accessor;
    private readonly MemoryMappedFileStatistics _statistics;
    private readonly long _offset;
    private readonly long _size;
    private readonly MemoryMappedFileAccess _access;
    private volatile bool _isDisposed;

    public MemoryMappedViewAccessor(
        System.IO.MemoryMappedFiles.MemoryMappedViewAccessor accessor,
        long offset,
        long size,
        MemoryMappedFileAccess access,
        MemoryMappedFileStatistics statistics)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        _offset = offset;
        _size = size;
        _access = access;
    }

    public long Offset => _offset;
    public long Size => _size;
    public MemoryMappedFileAccess Access => _access;
    public bool IsValid => !_isDisposed && _accessor != null;

    public byte ReadByte(long position)
    {
        ThrowIfDisposed();
        ValidatePosition(position, 1);
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var value = _accessor.ReadByte(position);
            stopwatch.Stop();
            _statistics.RecordAccess(stopwatch.Elapsed);
            return value;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordPageFault();
            throw;
        }
    }

    public void WriteByte(long position, byte value)
    {
        ThrowIfDisposed();
        ValidateWriteAccess();
        ValidatePosition(position, 1);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _accessor.Write(position, value);
            stopwatch.Stop();
            _statistics.RecordAccess(stopwatch.Elapsed);
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordPageFault();
            throw;
        }
    }

    public int ReadArray(long position, byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset >= buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
        
        ValidatePosition(position, count);
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var actualCount = Math.Min(count, (int)(_size - position));
            _accessor.ReadArray(position, buffer, offset, actualCount);
            stopwatch.Stop();
            _statistics.RecordAccess(stopwatch.Elapsed);
            return actualCount;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordPageFault();
            throw;
        }
    }

    public void WriteArray(long position, byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        ValidateWriteAccess();
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset >= buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
        
        ValidatePosition(position, count);
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _accessor.WriteArray(position, buffer, offset, count);
            stopwatch.Stop();
            _statistics.RecordAccess(stopwatch.Elapsed);
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordPageFault();
            throw;
        }
    }

    public int ReadInt32(long position)
    {
        ThrowIfDisposed();
        ValidatePosition(position, sizeof(int));
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var value = _accessor.ReadInt32(position);
            stopwatch.Stop();
            _statistics.RecordAccess(stopwatch.Elapsed);
            return value;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordPageFault();
            throw;
        }
    }

    public void WriteInt32(long position, int value)
    {
        ThrowIfDisposed();
        ValidateWriteAccess();
        ValidatePosition(position, sizeof(int));
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _accessor.Write(position, value);
            stopwatch.Stop();
            _statistics.RecordAccess(stopwatch.Elapsed);
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordPageFault();
            throw;
        }
    }

    public long ReadInt64(long position)
    {
        ThrowIfDisposed();
        ValidatePosition(position, sizeof(long));
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var value = _accessor.ReadInt64(position);
            stopwatch.Stop();
            _statistics.RecordAccess(stopwatch.Elapsed);
            return value;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordPageFault();
            throw;
        }
    }

    public void WriteInt64(long position, long value)
    {
        ThrowIfDisposed();
        ValidateWriteAccess();
        ValidatePosition(position, sizeof(long));
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _accessor.Write(position, value);
            stopwatch.Stop();
            _statistics.RecordAccess(stopwatch.Elapsed);
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordPageFault();
            throw;
        }
    }

    public void Flush()
    {
        ThrowIfDisposed();
        if (_access == MemoryMappedFileAccess.Read) return;
        
        try
        {
            _accessor.Flush();
        }
        catch
        {
            // Ignore flush errors
        }
    }

    private void ValidatePosition(long position, int size)
    {
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position), "Position cannot be negative");
        
        if (position + size > _size)
            throw new ArgumentOutOfRangeException(nameof(position), "Position and size exceed view bounds");
    }

    private void ValidateWriteAccess()
    {
        if (_access == MemoryMappedFileAccess.Read)
            throw new InvalidOperationException("Cannot write to read-only view");
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MemoryMappedViewAccessor));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _accessor?.Dispose();
        _statistics.RecordViewDisposed();
    }
}
