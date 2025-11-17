using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Java.Nio;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;
using UsbSerialForAndroid.Net.Extensions;
using UsbSerialForAndroid.Net.Helper;
using UsbSerialForAndroid.Net.Logging;

namespace UsbSerialForAndroid.Net.Drivers
{
    /// <summary>
    /// USB driver base class
    /// </summary>
    public abstract class UsbDriverBase
    {
        private static readonly UsbManager usbManager = GetUsbManager();
        public const byte XON = 17;
        public const byte XOFF = 19;
        public const int DefaultTimeout = 1000;
        public const int DefaultBufferLength = 1024 * 4;
        public const int DefaultBaudRate = 9600;
        public const byte DefaultDataBits = 8;
        public const StopBits DefaultStopBits = StopBits.One;
        public const Parity DefaultParity = Parity.None;
        public const int DefaultUsbInterfaceIndex = 0;
        /// <summary>
        /// flow control
        /// </summary>
        public FlowControl FlowControl { get; protected set; }
        /// <summary>
        /// Data Terminal Ready Enable
        /// </summary>
        public bool DtrEnable { get; protected set; }
        /// <summary>
        /// Request To Send Enable
        /// </summary>
        public bool RtsEnable { get; protected set; }
        /// <summary>
        /// USB interface index to use
        /// </summary>
        public int UsbInterfaceIndex { get; set; } = DefaultUsbInterfaceIndex;
        /// <summary>
        /// USB manager
        /// </summary>
        public static UsbManager UsbManager => usbManager;
        /// <summary>
        /// USB device
        /// </summary>
        public UsbDevice UsbDevice { get; private set; }
        /// <summary>
        /// USB device connection
        /// </summary>
        public UsbDeviceConnection? UsbDeviceConnection { get; protected set; }
        /// <summary>
        /// USB interface
        /// </summary>
        public UsbInterface? UsbInterface { get; protected set; }
        /// <summary>
        /// read endpoint
        /// </summary>
        public UsbEndpoint? UsbEndpointRead { get; protected set; }
        /// <summary>
        /// write endpoint
        /// </summary>
        public UsbEndpoint? UsbEndpointWrite { get; protected set; }
        /// <summary>
        /// read timeout
        /// </summary>
        public int ReadTimeout { get; set; } = DefaultTimeout;
        /// <summary>
        /// write timeout
        /// </summary>
        public int WriteTimeout { get; set; } = DefaultTimeout;
        /// <summary>
        /// Control timeout
        /// </summary>
        public int ControlTimeout { get; set; } = DefaultTimeout;
        /// <summary>
        /// is connected
        /// </summary>
        public bool Connected => TestConnection();
        /// <summary>
        /// USB driver base class
        /// </summary>
        /// <param name="_usbDevice"></param>
        protected UsbDriverBase(UsbDevice _usbDevice)
        {
            UsbDevice = _usbDevice;
        }
        /// <summary>
        /// Get usbManager
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">UsbManager is null exception</exception>
        private static UsbManager GetUsbManager()
        {
            var usebService = Application.Context.GetSystemService(Context.UsbService);
            return usebService is UsbManager manager
                ? manager
                : throw new NullReferenceException("UsbManager is null");
        }
        /// <summary>
        /// open the usb device
        /// </summary>
        /// <param name="baudRate">baudRate</param>
        /// <param name="dataBits">dataBits</param>
        /// <param name="stopBits">stopBits</param>
        /// <param name="parity">parity</param>
        public abstract void Open(int baudRate, byte dataBits, StopBits stopBits, Parity parity);
        /// <summary>
        /// Set DTR enabled
        /// </summary>
        /// <param name="value">true=enabled</param>
        public abstract void SetDtrEnabled(bool value);
        /// <summary>
        /// Set RTS enabled
        /// </summary>
        /// <param name="value">true=enabled</param>
        public abstract void SetRtsEnabled(bool value);
        /// <summary>
        /// close the usb device
        /// </summary>
        public virtual async void Close()
        {
            Logger.Trace($"[USBDRIVER]: Close");
            await DeinitBuffersAsync();
            UsbEndpointRead?.Dispose(); UsbEndpointRead = null;
            UsbEndpointWrite?.Dispose(); UsbEndpointWrite = null;
            UsbDeviceConnection?.ReleaseInterface(UsbInterface);
            UsbInterface?.Dispose(); UsbInterface = null;
            UsbDeviceConnection?.Close(); UsbDeviceConnection = null;
            Logger.Trace($"[USBDRIVER]: Close - Ok");
        }
        /// <summary>
        /// sync write
        /// </summary>
        /// <param name="buffer">write data</param>
        /// <exception cref="BulkTransferException">write failed exception</exception>
        public virtual void Write(byte[] buffer)
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            int result = UsbDeviceConnection.BulkTransfer(UsbEndpointWrite, buffer, 0, buffer.Length, WriteTimeout);
            if (result < 0)
                throw new BulkTransferException("Write failed", result, UsbEndpointWrite, buffer, 0, buffer.Length, WriteTimeout);
        }
        /// <summary>
        /// sync read
        /// </summary>
        /// <returns>The read data is returned after the read succeeds. Null data is returned after the read fails</returns>
        public virtual byte[]? Read()
        {
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferLength);
            try
            {
                int result = UsbDeviceConnection.BulkTransfer(UsbEndpointRead, buffer, 0, DefaultBufferLength, ReadTimeout);
                return result >= 0
                    ? buffer.AsSpan().Slice(0, result).ToArray()
                    : default;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        /// <summary>
        /// async write
        /// </summary>
        /// <param name="buffer">write data</param>
        /// <returns></returns>
        /// <exception cref="BulkTransferException">Write failed exception</exception>
        public virtual Task WriteAsync(byte[] buffer)
        {
            return WriteAsync(buffer, 0, buffer.Length);
        }
        /// <summary>
        /// async read
        /// </summary>
        /// <returns>The read data is returned after the read succeeds. Null data is returned after the read fails</returns>
        public virtual async Task<byte[]?> ReadAsync()
        {
            var dest = ArrayPool<byte>.Shared.Rent(DefaultBufferLength);
            try
            {
                int len = await ReadAsync(dest, 0, dest.Length);
                return dest.AsSpan(0, len).ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(dest);
            }
        }
        /// <summary>
        /// get the interface of the current USB device
        /// </summary>
        /// <param name="usbDevice">USB device</param>
        /// <returns>UsbInterface array</returns>
        public static UsbInterface[] GetUsbInterfaces(UsbDevice usbDevice)
        {
            var array = new UsbInterface[usbDevice.InterfaceCount];
            for (int i = 0; i < usbDevice.InterfaceCount; i++)
            {
                array[i] = usbDevice.GetInterface(i);
            }
            return array;
        }
        /// <summary>
        /// test connection
        /// </summary>
        /// <returns>true=connected</returns>
        public bool TestConnection()
        {
            try
            {
                ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
                byte[] buf = new byte[2];
                const int request = 0;//GET_STATUS
                int len = UsbDeviceConnection.ControlTransfer(UsbAddressing.DirMask, request, 0, 0, buf, buf.Length, 100);
                return len == 2;
            }
            catch
            {
                return false;
            }
        }

        public ILogger Logger = new NullLogger();

        public int ReadHeaderLength = 0;
        public FilterDataFn? FilterData;
        public delegate int FilterDataFn(Span<byte> src, Span<byte> dst);

        protected UsbRequest? _usbWriteRequest;
        protected UsbRequest? _usbReadRequest;

        NetDirectByteBuffer? _current = null;
        protected CancellationTokenSource? _readerExit;
        protected List<NetDirectByteBuffer> _allBuffers = [];

        protected Task? _dispatchTask;
        protected Task? _readTask;
        protected Task? _filterTask;

        Channel<UsbRequest>? _writecChannel;
        Channel<UsbRequest>? _readChannel;

        Channel<NetDirectByteBuffer>? _emptyChannel;
        Channel<NetDirectByteBuffer>? _filterChannel;
        Channel<NetDirectByteBuffer>? _dataChannel;

        protected void InitAsyncBuffers()
        {
            Logger.Trace($"[USBDRIVER]: Init");
            ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
            ArgumentNullException.ThrowIfNull(UsbEndpointWrite);
            ArgumentNullException.ThrowIfNull(UsbEndpointRead);
            _usbReadRequest = new();
            _usbReadRequest.Initialize(UsbDeviceConnection, UsbEndpointRead);
            _usbWriteRequest = new();
            _usbWriteRequest.Initialize(UsbDeviceConnection, UsbEndpointWrite);
            _writecChannel = Channel.CreateBounded<UsbRequest>(new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            _readChannel = Channel.CreateBounded<UsbRequest>(new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            _emptyChannel = Channel.CreateUnbounded<NetDirectByteBuffer>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
            _filterChannel = Channel.CreateUnbounded<NetDirectByteBuffer>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });
            _dataChannel = Channel.CreateUnbounded<NetDirectByteBuffer>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });
            int readBufLen = 512;
            int readBufCount = DefaultBufferLength / readBufLen;
            for (int i = 0; i < readBufCount; i++)
            {
                var newBuf = new NetDirectByteBuffer(readBufLen);
                _allBuffers.Add(newBuf);
                while (!_emptyChannel.Writer.TryWrite(newBuf))
                    Task.Yield();
            }
            _readerExit = new();
            if (null != FilterData)
                _filterTask = Task.Run(() => ProcessFilterAsync(_readerExit.Token));
            _readTask = Task.Run(() => InternalUsbReadAsync(_readerExit.Token));
            _dispatchTask = Task.Run(() => UsbDispatchAsync(_readerExit.Token));
            Logger.Trace($"[USBDRIVER]: Init - Ok");
        }
        protected async Task DeinitBuffersAsync()
        {
            Logger.Trace($"[USBDRIVER]: Deinit");
            try
            {
                // cancel all tasks
                _readerExit?.Cancel();
                _writecChannel?.Writer.Complete();
                _readChannel?.Writer.Complete();
                _emptyChannel?.Writer.Complete();
                _filterChannel?.Writer.Complete();
                _dataChannel?.Writer.Complete();
                Interlocked.Exchange(ref _readerExit, null)?.Dispose();
                // await exit all tasks // clear all tasks
                if (null != _dispatchTask)
                {
                    await _dispatchTask;
                    _dispatchTask = null;
                }
                if (null != _readTask)
                {
                    await _readTask;
                    _readTask = null;
                }
                if (null != _filterTask)
                {
                    await _filterTask;
                    _filterTask = null;
                }
                // clear all buffers
                _writecChannel = null;
                _readChannel = null;
                _emptyChannel = null;
                _filterChannel = null;
                _dataChannel = null;
                foreach (var item in _allBuffers)
                    item.Dispose();
                _allBuffers.Clear();
                // clear requests
                Interlocked.Exchange(ref _usbReadRequest, null)?.Dispose();
                Interlocked.Exchange(ref _usbWriteRequest, null)?.Dispose();
                Logger.Trace($"[USBDRIVER]: Deinit - Ok");
            }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
            }
        }
        protected virtual async Task ProcessFilterAsync(CancellationToken ct = default)
        {
            NetDirectByteBuffer? buf;
            ChannelWriter<NetDirectByteBuffer>? emptyQueue;
            try
            {
                ArgumentNullException.ThrowIfNull(FilterData);
                ArgumentNullException.ThrowIfNull(_emptyChannel);
                ArgumentNullException.ThrowIfNull(_filterChannel);
                ArgumentNullException.ThrowIfNull(_dataChannel);
                var inData = _filterChannel.Reader;
                var outData = _dataChannel.Writer;
                emptyQueue = _emptyChannel.Writer;
                while (!ct.IsCancellationRequested)
                {
                    if (!inData.TryRead(out buf))
                        buf = await inData.ReadAsync(ct);
                    var data = buf.MemBuffer.Span.Slice(0, buf.Position);
                    buf.Position = FilterData(data, data);
                    if (0 < buf.Position)
                        await outData.WriteAsync(buf, ct);
                    else
                        await emptyQueue.WriteAsync(buf, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
                return;
            }
            Logger.Trace($"[USBDRIVER]: exit ProcessFilter");
        }
        protected virtual async Task UsbDispatchAsync(CancellationToken ct = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
                ArgumentNullException.ThrowIfNull(_writecChannel);
                ArgumentNullException.ThrowIfNull(_readChannel);
                var wr = _writecChannel.Writer;
                var read = _readChannel.Writer;
                UsbRequest? response;
                while (!ct.IsCancellationRequested)
                {
                    response = await UsbDeviceConnection.RequestWaitAsync();
                    // ArgumentNullException.ThrowIfNull(response);
                    if (null == response)
                    {
                        Logger.Trace($"[USBDRIVER]: WARN response is null");
                        if (!TestConnection())
                            await Task.Run(Close, ct);
                        continue;
                    }
                    if (ReferenceEquals(response, _usbReadRequest))
                    {
                        //Logger.Trace($"[USBDRIVER]: _tcsRead");
                        await read.WriteAsync(response, ct);
                        continue;
                    }
                    if (ReferenceEquals(response, _usbWriteRequest))
                    {
                        //Logger.Trace($"[USBDRIVER]: _tcsWrite");
                        await wr.WriteAsync(response, ct);
                        continue;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
                return;
            }
            Logger.Trace($"[USBDRIVER]: exit UsbDispatchAsync");
        }
        protected virtual async Task InternalUsbReadAsync(CancellationToken ct = default)
        {
            ChannelWriter<NetDirectByteBuffer> outQueue;
            ChannelReader<NetDirectByteBuffer> emptyQueue;
            NetDirectByteBuffer? buf = null;
            try
            {
                ArgumentNullException.ThrowIfNull(_usbReadRequest);
                ArgumentNullException.ThrowIfNull(_readChannel);
                ArgumentNullException.ThrowIfNull(_emptyChannel);
                ArgumentNullException.ThrowIfNull(_filterChannel);
                ArgumentNullException.ThrowIfNull(_dataChannel);
                var reader = _readChannel.Reader;
                emptyQueue = _emptyChannel.Reader;
                outQueue = (null == FilterData) ? _dataChannel.Writer : _filterChannel.Writer;
                while (!ct.IsCancellationRequested)
                {
                    if (null == buf && !emptyQueue.TryRead(out buf))
                        buf = await emptyQueue.ReadAsync(ct);
                    buf.Rewind();
                    //Logger.Trace($"[USBDRIVER]: read requested");
                    _usbReadRequest.QueueReq((ByteBuffer)buf);
                    _ = await reader.ReadAsync(ct);
                    ct.ThrowIfCancellationRequested();
                    if (ReadHeaderLength < buf.Position)
                    {
                        //Logger.Trace($"[USBDRIVER]: received {buf.Position}");
                        await outQueue.WriteAsync(buf, ct);
                        buf = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _usbReadRequest?.Cancel();
            }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
                return;
            }
            Logger.Trace($"[USBDRIVER]: exit InternalUsbReadAsync");
        }
        public virtual async Task<int> ReadAsync(byte[] dstBuf, int offset, int count, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(_emptyChannel);
            ArgumentNullException.ThrowIfNull(_dataChannel);
            ChannelWriter<NetDirectByteBuffer> emptyQueue = _emptyChannel.Writer;
            ChannelReader<NetDirectByteBuffer> dataQueue = _dataChannel.Reader;
            int readed = 0;
            NetDirectByteBuffer? buf = null;
            Interlocked.Exchange(ref buf, _current);
            if (null == buf && !dataQueue.TryRead(out buf))
                buf = await dataQueue.ReadAsync(ct);
            // get buffered data, not more than the requested size
            while (!ct.IsCancellationRequested && null != buf)
            {
                //try peek already buffered data,
                //if there is none, do async wait and try peek again

                if (count < buf.Position)
                {
                    var data = buf.MemBuffer.Span;
                    data.Slice(0, count).CopyTo(dstBuf.AsSpan(offset));
                    readed += count;
                    // move data
                    var qty = buf.Position - count;
                    data.Slice(count, qty).CopyTo(data.Slice(0, qty));
                    buf.Position = qty;
                    Interlocked.Exchange(ref _current, buf);
                    return readed;
                }
                //_current = null;
                var r = buf.Position;
                buf.MemBuffer.Span.Slice(0, r).CopyTo(dstBuf.AsSpan(offset));
                offset += r;
                count -= r;
                readed += r;
                await emptyQueue.WriteAsync(buf, ct);
                dataQueue.TryRead(out buf);
            }
            return readed;
        }
        public virtual async Task<int> WriteAsync(byte[] wbuf, int offset, int count, CancellationToken ct = default)
        {
            try
            {
                // TODO: split wbuf into 512 bytes and send in pieces to be able to cancel
                ArgumentNullException.ThrowIfNull(_writecChannel);
                ArgumentNullException.ThrowIfNull(_usbWriteRequest);
                var reader = _writecChannel.Reader;
                // it`s not copy its just wrapper
                using var buf = new NetDirectByteBuffer(wbuf, offset, count);
                //Logger.Trace($"[USBDRIVER]: write requested");
                _usbWriteRequest.QueueReq((ByteBuffer)buf);
                _ = await reader.ReadAsync(ct);
                ct.ThrowIfCancellationRequested();
                return buf.Position;
            }
            catch (Exception)
            {
                _usbWriteRequest?.Cancel();
                throw;
            }
        }
    }
}