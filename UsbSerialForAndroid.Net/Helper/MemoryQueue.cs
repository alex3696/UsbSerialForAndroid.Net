using System;

namespace UsbSerialForAndroid.Net.Helper;

/// <summary>
/// simple fifo
/// </summary>
public class MemoryQueue
{
    public MemoryQueue(int capacity = 256)
    {
        _mem = new byte[capacity];
        _r = 0;
        _w = 0;
    }
    public uint Length => _w - _r;
    public uint EmptySpace => (uint)_mem.Length - (_w - _r);
    public void Write(Span<byte> src)
    {
        if (0 >= src.Length)
            return;
        var wempty = (uint)_mem.Length - _w;
        if (src.Length > _r + wempty)
            throw new OverflowException($"EmptySpace {EmptySpace}, data length is {src.Length}");
        if (src.Length > wempty)
            MemMove();
        src.CopyTo(_mem.AsSpan((int)_w));
        _w += (uint)src.Length;
    }
    public void Read(Span<byte> dst)
    {
        if (dst.Length > _w - _r)
            throw new ArgumentOutOfRangeException($"{dst.Length} is greater then data {_w - _r}");
        _mem.AsSpan((int)_r, dst.Length).CopyTo(dst);
        _r += (uint)dst.Length;
    }
    public void Write(byte[] src, int soffset = 0, int slen = 0)
    {
        if (0 >= slen)
            slen = src.Length - soffset;
        Write(src.AsSpan(soffset, slen));
    }
    public void Read(byte[] dst, int offset = 0, int len = 0)
    {
        if (0 >= len)
            len = dst.Length - offset;
        Read(dst.AsSpan(offset, len));
    }

    readonly byte[] _mem;
    uint _r;
    uint _w;
    void MemMove()
    {
        if (0 < _r)
        {
            _w -= _r;//dataLen
            Array.Copy(_mem, _r, _mem, 0, _w);
            _r = 0;
        }
    }
}
