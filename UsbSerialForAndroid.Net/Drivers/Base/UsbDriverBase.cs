using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Java.Nio;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
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

        public FilterDataFn? FilterData;
        public delegate int FilterDataFn(Span<byte> src, Span<byte> dst);

        protected UsbRequest? _usbWriteRequest;
        protected UsbRequest? _usbReadRequest;
        protected TaskCompletionSource<UsbRequest>? _tcsRead;
        protected TaskCompletionSource<UsbRequest>? _tcsWrite;

        protected TaskCompletionSource? _tcsFilterBufers;
        protected TaskCompletionSource? _tcsFillBuf;
        protected CancellationTokenSource? _readerExit;
        protected List<NetDirectByteBuffer> _allBuffers = [];
        protected ConcurrentQueue<NetDirectByteBuffer> _emptyBuffers = new();
        protected ConcurrentQueue<NetDirectByteBuffer> _fillBuffers = new();
        protected ConcurrentQueue<NetDirectByteBuffer> _filterBufers = new();

        protected Task? _dispatchTask;
        protected Task? _readTask;
        protected Task? _filterTask;
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
            int readBufLen = 512;
            int readBufCount = DefaultBufferLength / readBufLen;
            for (int i = 0; i < readBufCount; i++)
            {
                var newBuf = new NetDirectByteBuffer(readBufLen);
                _allBuffers.Add(newBuf);
                _emptyBuffers.Enqueue(newBuf);
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
                Interlocked.Exchange(ref _readerExit, null)?.Dispose();
                // await exit all tasks
                if (null != _dispatchTask)
                    await _dispatchTask;
                if (null != _readTask)
                    await _readTask;
                if (null != _filterTask)
                    await _filterTask;
                // clear all tasks
                _filterTask = null;
                _dispatchTask = null;
                _readTask = null;
                // clear all buffers
                _emptyBuffers.Clear();
                _fillBuffers.Clear();
                _filterBufers.Clear();
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
            NetDirectByteBuffer? buf = null;
            try
            {
                ArgumentNullException.ThrowIfNull(FilterData);
                while (!ct.IsCancellationRequested)
                {
                    if (_filterBufers.TryDequeue(out buf))
                    {
                        var data = buf.NetBuffer.AsSpan(0, buf.Position);
                        buf.Position = FilterData(data, data);
                        if (0 < buf.Position)
                        {
                            _fillBuffers.Enqueue(buf);
                            _tcsFillBuf?.TrySetResult();
                        }
                        else
                            _emptyBuffers.Enqueue(buf);
                    }
                    else
                    {
                        TaskCompletionSource waitData = new();
                        using (ct.Register(() => waitData.TrySetCanceled(ct)))
                        {
                            var task = waitData.Task;
                            Interlocked.Exchange(ref _tcsFilterBufers, waitData);
                            await task.WaitAsync(ct);
                            Interlocked.Exchange(ref _tcsFilterBufers, null);
                        }
                    }
                }
                Logger.Trace($"[USBDRIVER]: exit ProcessFilter");
            }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
            }
            finally
            {
                if (null != buf)
                    _emptyBuffers.Enqueue(buf);
            }
        }
        protected virtual async Task UsbDispatchAsync(CancellationToken ct = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(UsbDeviceConnection);
                while (!ct.IsCancellationRequested)
                {
                    UsbRequest? response = await UsbDeviceConnection.RequestWaitAsync();
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
                        _tcsRead?.TrySetResult(response);
                        continue;
                    }
                    if (ReferenceEquals(response, _usbWriteRequest))
                    {
                        //Logger.Trace($"[USBDRIVER]: _tcsWrite");
                        _tcsWrite?.TrySetResult(response);
                        continue;
                    }
                }
                Logger.Trace($"[USBDRIVER]: exit UsbDispatchAsync");
            }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
            }
        }
        protected virtual async Task InternalUsbReadAsync(CancellationToken ct = default)
        {
            ConcurrentQueue<NetDirectByteBuffer> dataQueue;
            NetDirectByteBuffer? buf = null;
            NetDirectByteBuffer? emptyBuf = null;
            try
            {
                ArgumentNullException.ThrowIfNull(_usbReadRequest);
                dataQueue = (null == FilterData) ? _fillBuffers : _filterBufers;
                while (!_emptyBuffers.TryDequeue(out buf))
                    ct.ThrowIfCancellationRequested();
                while (!ct.IsCancellationRequested)
                {
                    buf.Rewind();
                    using var crReg = ct.Register(() => _usbReadRequest?.Cancel());
                    //Logger.Trace($"[USBDRIVER]: read requested");
                    Interlocked.Exchange(ref _tcsRead, new());
                    _usbReadRequest.QueueReq((ByteBuffer)buf);
                    UsbRequest? response = await _tcsRead.Task.WaitAsync(ct);
                    Interlocked.Exchange(ref _tcsRead, null);
                    //Logger.Trace($"[USBDRIVER]: read received");
                    ct.ThrowIfCancellationRequested();
                    if (0 < buf.Position)
                    {
                        while (!_emptyBuffers.IsEmpty)
                        {
                            ct.ThrowIfCancellationRequested();
                            if (_emptyBuffers.TryDequeue(out emptyBuf))
                            {
                                dataQueue.Enqueue(buf);
                                var dataNotify = (null == FilterData) ? _tcsFillBuf : _tcsFilterBufers;
                                dataNotify?.TrySetResult();
                                buf = emptyBuf;
                                break;
                            }
                        }
                    }
                }
                Logger.Trace($"[USBDRIVER]: exit InternalUsbReadAsync");
            }
            catch (OperationCanceledException)
            {
                _usbReadRequest?.Cancel();
                Logger.Trace($"[USBDRIVER]: exit InternalUsbReadAsync");
            }
            catch (Exception ex)
            {
                Logger.Error($"[USBDRIVER]: crash {ex}");
            }
            finally
            {
                if (null != buf)
                    _emptyBuffers.Enqueue(buf);
                if (null != emptyBuf)
                    _emptyBuffers.Enqueue(emptyBuf);
            }
        }
        public virtual async Task<int> ReadAsync(byte[] dstBuf, int offset, int count, CancellationToken ct = default)
        {
            int readed = 0;
            NetDirectByteBuffer? buf = default;

            //try peek already buffered data,
            //if there is none, do async wait and try peek again
            while (!_fillBuffers.TryPeek(out buf))
            {
                ct.ThrowIfCancellationRequested();
                TaskCompletionSource waitData = new();
                using (ct.Register(() => waitData.TrySetCanceled(ct)))
                {
                    var task = waitData.Task;
                    Interlocked.Exchange(ref _tcsFillBuf, waitData);
                    await task.WaitAsync(ct);
                    Interlocked.Exchange(ref _tcsFillBuf, null);
                }
            }
            // get buffered data, not more than the requested size
            while (null != buf)
            {
                if (count < buf.Position)
                {
                    var qty = buf.Position;
                    var data = (byte[])buf;
                    data.AsSpan(0, count).CopyTo(dstBuf.AsSpan(offset));
                    data.AsSpan(count, qty).CopyTo(data.AsSpan());
                    buf.Position = qty - count;
                    break;
                }
                var r = buf.Position;
                ((byte[])buf).AsSpan(0, r).CopyTo(dstBuf.AsSpan(offset));
                offset += r;
                count -= r;
                readed += r;
                try
                {
                    while (!_fillBuffers.TryDequeue(out buf))
                        ct.ThrowIfCancellationRequested();
                }
                finally
                {
                    if (null != buf)
                    {
                        _emptyBuffers.Enqueue(buf);
                        buf = null;
                    }
                }
                while (false == _fillBuffers.IsEmpty && false == _fillBuffers.TryPeek(out buf))
                    ct.ThrowIfCancellationRequested();
            }
            return readed;

        }
        public virtual async Task<int> WriteAsync(byte[] wbuf, int offset, int count, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(_usbWriteRequest);
            using var buf = ByteBuffer.Wrap(wbuf, offset, count);
            using var crReg = ct.Register(() => _usbWriteRequest?.Cancel());
            Interlocked.Exchange(ref _tcsWrite, new());
            //Logger.Trace($"[USBDRIVER]: write requested");
            _usbWriteRequest.QueueReq(buf);
            UsbRequest response = await _tcsWrite.Task.WaitAsync(ct);
            Interlocked.Exchange(ref _tcsWrite, null);
            ct.ThrowIfCancellationRequested();
            int nwrite = buf.Position();
            return nwrite;
        }

    }
}