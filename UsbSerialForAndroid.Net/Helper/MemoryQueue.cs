using System;
using System.Diagnostics;
using System.Text;

namespace UsbSerialForAndroid.Net.Helper;

/*
begin - B
length - C

// filled 0 Continuous
0 1 2 3 4 5 6 7 8 9
. . . . . . . . . . 
  B 
  C=0

// filled 9 Continuous
0 1 2 3 4 5 6 7 8 9
* * * * * * * * * . 
B 
C=9

// filled 10 Continuous
0 1 2 3 4 5 6 7 8 9
* * * * * * * * * * 
B 
C=10

// filled 9
0 1 2 3 4 5 6 7 8 9
* * . * * * * * * *  
      B 
      C=9

// filled 10 Continuous
0 1 2 3 4 5 6 7 8 9
* * * * * * * * * * 
      B 
      C=10

// filled 4
0 1 2 3 4 5 6 7 8 9
* * . . . . . . * *  
                B 
                C=4

// filled 4 Continuous
0 1 2 3 4 5 6 7 8 9
. . * * * * . . . . 
    B
    C = 4
*/

/// <summary>
/// simple fifo Queue
/// </summary>
[DebuggerDisplay("{DebugString,nq}")]
public class MemoryQueue
{
    public MemoryQueue(int capacity = 256)
    {
        _buf = new byte[capacity];
    }
    public int Capacity => _buf.Length;
    public int Length => _len;
    public int EmptySpace => _buf.Length - _len;
    public bool IsContinuous => _buf.Length >= _pos + _len;

    public int Free(int count)
    {
        count = int.Min(count, _len);
        _pos += count;
        if (_buf.Length <= _pos)
            _pos -= _buf.Length;
        _len -= count;
        if (0 == _len)// reduce the number of copy calls
            _pos = 0;
        return count;
    }
    public void OverflowWrite(Span<byte> src)
    {
        if (src.Length >= _buf.Length)
            src = src.Slice(src.Length - _buf.Length, _buf.Length);
        if (EmptySpace < src.Length)
            Free(src.Length - EmptySpace);
        Write(src);
    }
    public void Write(Span<byte> src)
    {
        if (0 >= src.Length)
            return;
        if (src.Length > EmptySpace)
            throw new OverflowException($"EmptySpace {EmptySpace}, src length is {src.Length}");
        int tail = _pos + _len;
        if (_buf.Length <= tail)
            tail -= _buf.Length;
        int wLen = int.Min(src.Length, _buf.Length - tail);
        src.Slice(0, wLen).CopyTo(_buf.AsSpan(tail));
        _len += wLen;
        if (src.Length > wLen)
        {
            src.Slice(wLen).CopyTo(_buf.AsSpan(0));
            _len += src.Length - wLen;
        }
    }
    public int Read(Span<byte> dst)
    {
        int totalRead = int.Min(dst.Length, _len);
        int rLen = int.Min(totalRead, _buf.Length - _pos);
        _buf.AsSpan(_pos, rLen).CopyTo(dst);
        if (totalRead > rLen)
            _buf.AsSpan(0, totalRead - rLen).CopyTo(dst.Slice(rLen));
        _pos += totalRead;
        if (_buf.Length <= _pos)
            _pos -= _buf.Length;
        _len -= totalRead;
        if (0 == _len)// reduce the number of copy calls
            _pos = 0;
        return totalRead;
    }
    public int TouchRead(Span<byte> dst)
    {
        // use regular read but restore pointers
        int pos = _pos;
        int len = _len;
        int ret = Read(dst);
        _pos = pos;
        _len = len;
        return ret;
    }
#if DEBUG
    public string DebugString
    {
        get
        {
            StringBuilder sb = new(Length * 4);
            sb.Append('[');
            int llen, rlen;
            rlen = _pos + _len;
            if (rlen > _buf.Length) // !IsContinuous
            {
                llen = rlen - _buf.Length;
                rlen = _buf.Length;
            }
            else
                llen = -1;
            for (int i = 0; i < _buf.Length; i++)
            {
                sb.Append(i == _pos ? '+' : ' ');
                if (i < llen || (i >= _pos && i < rlen))
                    sb.AppendFormat("{0:x2} ", _buf[i]);
                else
                    sb.Append("__ ");
            }
            sb.Append(']');
            return sb.ToString();
            //TouchRead(dst);
            //return $"[{BitConverter.ToString(dst, 0, Length)}]";
        }
    }
#endif

    readonly byte[] _buf;
    int _pos = 0;
    int _len = 0;
}
