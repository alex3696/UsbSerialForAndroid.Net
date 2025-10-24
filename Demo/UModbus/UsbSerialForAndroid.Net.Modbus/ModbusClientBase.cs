using System;
using System.Linq;
using UsbSerialForAndroid.Net.Modbus.Enums;
using UsbSerialForAndroid.Net.Modbus.Exceptions;
using UsbSerialForAndroid.Net.Modbus.Extensions;

namespace UsbSerialForAndroid.Net.Modbus
{
    public abstract class ModbusClientBase
    {
        private readonly object _lock = new object();
        protected abstract void Write(byte[] writeData);
        protected abstract byte[] Read();
        public string? DriverId { get; set; }
        public ushort TransactionId { get; private set; }
        public int ReceivedBufferSize { get; set; } = 1024 * 512;
        public int LoopReadTimeout { get; set; } = 3000;
        private T EnqueueExecute<T>(Func<T> func)
        {
            lock (_lock)
            {
                return func.Invoke();
            }
        }
        private Result NoLockExecute(byte[] writeData)
        {
            var result = new Result() { SendData = writeData };
            Write(writeData);
            int offset = 0;
            var data = new byte[ReceivedBufferSize];
            while (true)
            {
                var buffer = Read();
                if (buffer.Length > 0)
                {
                    Array.Copy(buffer, 0, data, offset, buffer.Length);
                    offset += buffer.Length;
                    var readData = data.Slice(0, offset);
                    var payload = ExtractPayload(writeData, readData);
                    if (payload != null && payload.Length > 0)
                    {
                        result.ReceivedData = readData;
                        result.Payload = payload;
                        result.EndTime = DateTime.Now;
                        return result;
                    }
                }

                ThrowLoopTimeoutException(result.StartTime);
            }
        }
        /// <summary>
        /// Extract payload data
        /// </summary>
        /// <param name="writeData"></param>
        /// <param name="readData"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private byte[]? ExtractPayload(byte[] writeData, byte[] readData)
        {
            var data = ValidateReceivedModbusRtuData(writeData, readData);
            if (data.Length != 0)
            {
                return data;
            }
            return default;
        }
        private void ThrowLoopTimeoutException(DateTime inLoopTime)
        {
            if ((DateTime.Now - inLoopTime).TotalMilliseconds > LoopReadTimeout)
                throw new Exception($"Loop read data timed out.DriverId:{DriverId}");
        }
        /// <summary>
        /// Get exception code information
        /// </summary>
        /// <param name="code">Error code</param>
        /// <returns></returns>
        public static string GetExceptionCodeMessage(byte code) => code switch
        {
            0x01 => $"Illegal function,error code:{code}",
            0x02 => $"Invalid data address,error code:{code}",
            0x03 => $"Illegal data values,error codes:{code}",
            0x04 => $"Slave device failure,error code:{code}",
            0x05 => $"Acknowledge,error code:{code}",
            0x06 => $"Slave device busy,error code:{code}",
            0x07 => $"Negative acknowledge,error code:{code}",
            0x08 => $"Memory parity error,error code:{code}",
            0x0A => $"Gateway path unavailable,error code:{code}",
            0x0B => $"Gateway target failed,error code:{code}",
            _ => $"Unknown error,error code:{code}",
        };
        /// <summary>
        /// PDU = function code + data
        /// </summary>
        /// <param name="unitId">unique identification Id</param>
        /// <param name="funcCode">function code</param>
        /// <param name="readBeginAddress">Read the starting address</param>
        /// <param name="readLength">Read length</param>
        /// <param name="writeBeginAddress">Write the starting address</param>
        /// <param name="writeLength">Write length</param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        private static byte[] CreatePDU(byte unitId, FuncCode funcCode, ushort readBeginAddress = 0, ushort readLength = 0, ushort writeBeginAddress = 0, ushort writeLength = 0)
        {
            byte[] pdu = [];
            switch (funcCode)
            {
                case FuncCode.ReadCoils:
                case FuncCode.ReadDiscreteInputs:
                    {
                        ValidateLengthRange(funcCode, readLength);
                        pdu = new byte[6];
                        pdu[0] = unitId; //单元标识符设备地址
                        pdu[1] = (byte)funcCode; //功能码
                        pdu[2] = (byte)(readBeginAddress >> 8); //起始地址高位
                        pdu[3] = (byte)readBeginAddress; //起始地址低位
                        pdu[4] = (byte)(readLength >> 8); //读数据长度高位
                        pdu[5] = (byte)readLength; //读数据长度低位
                    }
                    break;
                case FuncCode.ReadHoldingRegisters:
                case FuncCode.ReadInputRegisters:
                    {
                        ValidateLengthRange(funcCode, readLength);
                        pdu = new byte[6];
                        pdu[0] = unitId; //单元标识符设备地址
                        pdu[1] = (byte)funcCode; //功能码
                        pdu[2] = (byte)(readBeginAddress >> 8); //起始地址高位
                        pdu[3] = (byte)readBeginAddress; //起始地址低位
                        pdu[4] = (byte)(readLength >> 8); //读数据长度高位
                        pdu[5] = (byte)readLength; //读数据长度低位
                    }
                    break;
                case FuncCode.WriteSingleCoil:
                case FuncCode.WriteSingleRegister:
                    {
                        ValidateLengthRange(funcCode, writeLength);
                        pdu = new byte[6];
                        pdu[0] = unitId; //设备地址
                        pdu[1] = (byte)funcCode; //功能码
                        pdu[2] = (byte)(writeBeginAddress >> 8); //起始地址高位
                        pdu[3] = (byte)writeBeginAddress; //起始地址低位
                        //写单线圈 WriteSingleCoil ON=0xFF00 OFF=0x0000
                        //写单寄存器 WriteSingleRegister 数据为实际转换值
                        pdu[4] = 0; //写入数据高位
                        pdu[5] = 0; //写入数据低位；
                    }
                    break;
                case FuncCode.WriteMultipleCoils:
                    {
                        ValidateLengthRange(funcCode, writeLength);
                        byte dataLen = writeLength % 8 > 0
                                ? (byte)(writeLength / 8 + 1)
                                : (byte)(writeLength / 8);
                        pdu = new byte[7 + dataLen];
                        pdu[0] = unitId;
                        pdu[1] = (byte)funcCode;
                        pdu[2] = (byte)(writeBeginAddress >> 8);
                        pdu[3] = (byte)writeBeginAddress;
                        pdu[4] = (byte)(writeLength >> 8);
                        pdu[5] = (byte)writeLength;
                        pdu[6] = dataLen;
                    }
                    break;
                case FuncCode.WriteMultipleRegisters:
                    {
                        byte dataLen = (byte)(writeLength * 2);
                        ValidateLengthRange(funcCode, dataLen);
                        pdu = new byte[7 + dataLen];
                        pdu[0] = unitId;
                        pdu[1] = (byte)funcCode;
                        pdu[2] = (byte)(writeBeginAddress >> 8);
                        pdu[3] = (byte)writeBeginAddress;
                        pdu[4] = (byte)(writeLength >> 8);
                        pdu[5] = (byte)writeLength;
                        pdu[6] = dataLen;
                    }
                    break;
                case FuncCode.ReadWriteMultipleRegisters:
                    {
                        if (readLength > 124 || readLength < 1)
                            throw new ArgumentOutOfRangeException(nameof(readLength), readLength, "Range of 1-124.");
                        if (writeLength > 122 || writeLength < 2)
                            throw new ArgumentOutOfRangeException(nameof(writeLength), writeLength, "Range of 1-124.");
                        byte dataLen = (byte)(writeLength * 2);
                        pdu = new byte[7 + dataLen + 4];
                        pdu[0] = unitId;
                        pdu[1] = (byte)funcCode;

                        pdu[2] = (byte)(readBeginAddress >> 8);
                        pdu[3] = (byte)readBeginAddress;
                        pdu[4] = (byte)(readLength >> 8);
                        pdu[5] = (byte)readLength;

                        pdu[6] = (byte)(writeBeginAddress >> 8);
                        pdu[7] = (byte)writeBeginAddress;
                        pdu[8] = (byte)(writeLength >> 8);
                        pdu[9] = (byte)writeLength;

                        pdu[10] = dataLen;
                    }
                    break;
            }
            return pdu;
        }
        /// <summary>
        /// Create ADU = address field + PDU
        /// </summary>
        /// <param name="rw">read/write/read write</param>
        /// <param name="connectMode">Connection mode</param>
        /// <param name="unitId"></param>
        /// <param name="funcCode"></param>
        /// <param name="readBeginAddress"></param>
        /// <param name="readLength"></param>
        /// <param name="writeBeginAddress"></param>
        /// <param name="writeLength"></param>
        /// <param name="writeData"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private static byte[] CreateADU(ReadOrWrite rw, byte unitId, FuncCode funcCode, ushort readBeginAddress = 0, ushort readLength = 0, ushort writeBeginAddress = 0, ushort writeLength = 0, byte[]? writeData = default)
        {
            byte[] adu = [];
            switch (rw)
            {
                case ReadOrWrite.Read:
                    {
                        byte[] pdu = CreatePDU(unitId, funcCode, readBeginAddress, readLength);
                        byte[] crc = pdu.ToCrc();
                        adu = pdu.Combine(crc);
                    }
                    break;
                case ReadOrWrite.Write:
                    {
                        ArgumentNullException.ThrowIfNull(writeData);

                        byte[] pdu = CreatePDU(unitId, funcCode, writeBeginAddress: writeBeginAddress, writeLength: writeLength);
                        Array.Copy(writeData, 0, pdu, pdu.Length - writeData.Length, writeData.Length);
                        byte[] crc = pdu.ToCrc();
                        adu = pdu.Combine(crc);
                    }
                    break;
                case ReadOrWrite.ReadWrite:
                    {
                        if (writeData == null)
                            throw new ArgumentNullException(nameof(writeData));

                        byte[] pdu = CreatePDU(unitId, funcCode, readBeginAddress, readLength, writeBeginAddress, writeLength);
                        Array.Copy(writeData, 0, pdu, pdu.Length - writeData.Length, writeData.Length);
                        byte[] crc = pdu.ToCrc();
                        adu = pdu.Combine(crc);
                    }
                    break;
            }
            return adu;
        }
        /// <summary>
        /// Check the modbus RTU return data
        /// </summary>
        /// <param name="sendData"></param>
        /// <param name="receiveData"></param>
        /// <returns></returns>
        /// <exception cref="ReceivedException"></exception>
        private byte[] ValidateReceivedModbusRtuData(byte[] sendData, byte[] receiveData)
        {
            if (receiveData[0] == sendData[0])
            {
                if (receiveData[1] == sendData[1])
                {
                    //正确回复
                    var funcCode = (FuncCode)receiveData[1];
                    switch (funcCode)
                    {
                        case FuncCode.ReadCoils:
                        case FuncCode.ReadDiscreteInputs:
                        case FuncCode.ReadHoldingRegisters:
                        case FuncCode.ReadInputRegisters:
                        case FuncCode.ReadWriteMultipleRegisters:
                            {
                                //读数据会返回数据长度和数据
                                //设备Id+功能码+数据长度+数据+CRC
                                if (receiveData[2] + 5 == receiveData.Length)
                                {
                                    //返回长度一致后校验CRC，不正常抛异常
                                    ValidateCrcFromRtu(sendData, receiveData, receiveData.Length);

                                    //正确返回，直接返回
                                    return receiveData.Slice(3, receiveData[2]);
                                }
                                break;
                            }
                        case FuncCode.WriteSingleCoil:
                        case FuncCode.WriteSingleRegister:
                        case FuncCode.WriteMultipleCoils:
                        case FuncCode.WriteMultipleRegisters:
                            {
                                //写入数据会将写入的报文原样返回
                                //正确返回，直接返回
                                if (
                                    sendData[2] == receiveData[2]
                                    && sendData[3] == receiveData[3]
                                    && sendData[4] == receiveData[4]
                                    && sendData[5] == receiveData[5]
                                )
                                {
                                    //返回长度一致后校验CRC，不正常抛异常
                                    ValidateCrcFromRtu(sendData, receiveData, receiveData.Length);

                                    //正确返回，直接返回
                                    return receiveData.Slice(0, receiveData.Length);
                                }
                                break;
                            }
                    }
                }
                else if (receiveData[1] == sendData[1] + 0x80)
                {
                    //错误回复，有错误码，抛异常
                    byte abnormalCode = receiveData[1];
                    byte errorCode = receiveData[2];
                    throw new ModbusErrorCodeException(abnormalCode, errorCode, sendData, receiveData, DriverId);
                }
                else
                {
                    throw new ReceivedException("Returns data parsing exception", sendData, receiveData, DriverId);
                }
            }
            else
            {
                throw new ReceivedException("The function codes for sending and replying are inconsistent", sendData, receiveData, DriverId);
            }

            return Array.Empty<byte>();
        }
        /// <summary>
        /// Verify the CRC of RTU message
        /// </summary>
        /// <param name="sendData"></param>
        /// <param name="receiveData"></param>
        /// <param name="offset"></param>
        /// <exception cref="ReceivedException"></exception>
        private void ValidateCrcFromRtu(byte[] sendData, byte[] receiveData, int offset)
        {
            var crc = receiveData.Slice(0, offset - 2).ToCrc();
            if (receiveData[offset - 2] != crc[0] && receiveData[offset - 1] != crc[1])
                throw new ReceivedException("Returns data CRC error", sendData, receiveData, DriverId);
        }
        /// <summary>
        /// Verify the length of read and write data
        /// </summary>
        /// <param name="funcCode"></param>
        /// <param name="length"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private static void ValidateLengthRange(FuncCode funcCode, int length)
        {
            switch (funcCode)
            {
                case FuncCode.ReadCoils:
                case FuncCode.ReadDiscreteInputs:
                    if (length < 1 || length > 1992)
                        throw new ArgumentOutOfRangeException(nameof(length), length, "Range of 1-1992.");
                    break;
                case FuncCode.ReadHoldingRegisters:
                case FuncCode.ReadInputRegisters:
                    if (length < 1 || length > 124)
                        throw new ArgumentOutOfRangeException(nameof(length), length, "Range of 1-124.");
                    break;
                case FuncCode.WriteSingleCoil:
                case FuncCode.WriteSingleRegister:
                    if (length != 1)
                        throw new ArgumentOutOfRangeException(nameof(length), length, "Must of 1.");
                    break;
                case FuncCode.WriteMultipleCoils:
                    if (length < 2 || length > 1960)
                        throw new ArgumentOutOfRangeException(nameof(length), length, "Range of 2-1960.");
                    break;
                case FuncCode.WriteMultipleRegisters:
                    if (length < 2 || length > 122)
                        throw new ArgumentOutOfRangeException(nameof(length), length, "Range of 2-122.");
                    break;
                case FuncCode.ReadWriteMultipleRegisters:
                    break;
            }
        }
        /// <summary>
        /// Read
        /// </summary>
        /// <param name="funcCode"></param>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private Result ReadBytes(FuncCode funcCode, byte unitId, ushort beginAddress, ushort length)
        {
            return EnqueueExecute(() =>
            {
                var sendData = CreateADU(ReadOrWrite.Read, unitId, funcCode, beginAddress, length);
                return NoLockExecute(sendData);
            });
        }
        /// <summary>
        /// Read continuous bool
        /// </summary>
        /// <param name="funcCode"></param>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private Result<bool[]> ReadBooleans(FuncCode funcCode, byte unitId, ushort beginAddress, ushort length)
        {
            var result = ReadBytes(funcCode, unitId, beginAddress, length);
            var values = result.Payload.ToBooleans().Slice(0, length);
            return result.ToResult(values);
        }
        /// <summary>
        /// Read multiple coils FC01
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public Result<bool[]> ReadCoils(byte unitId, ushort beginAddress, ushort length)
        {
            return ReadBooleans(FuncCode.ReadCoils, unitId, beginAddress, length);
        }
        /// <summary>
        /// Read multiple discrete input/output FC02
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public Result<bool[]> ReadDiscreteInputs(byte unitId, ushort beginAddress, ushort length)
        {
            return ReadBooleans(FuncCode.ReadDiscreteInputs, unitId, beginAddress, length);
        }
        /// <summary>
        /// Read the continuous holding register FC03
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public Result<byte[]> ReadHoldingRegisters(byte unitId, ushort beginAddress, ushort length)
        {
            var result = ReadBytes(FuncCode.ReadHoldingRegisters, unitId, beginAddress, length);
            return result.ToResult(result.Payload);
        }
        /// <summary>
        /// Read the single hold register FC03
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public Result<T> ReadHoldingRegister<T>(byte unitId, ushort beginAddress, ByteFormat format = ByteFormat.AB) where T : struct
        {
            var result = ReadHoldingRegisters<T>(unitId, beginAddress, 1, format);
            var value = result.Value.FirstOrDefault();
            return result.ToResult(value);
        }
        /// <summary>
        /// Read the continuous holding register FC03
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="length"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public Result<T[]> ReadHoldingRegisters<T>(byte unitId, ushort beginAddress, byte length, ByteFormat format = ByteFormat.AB) where T : struct
        {
            ValidateTypeFromByteFormat<T>(format);
            int wordSize = GetWordSize(format);
            var result = ReadHoldingRegisters(unitId, beginAddress, (byte)(length * wordSize));
            var values = GetValuesFromByteFormat<T>(result.Payload, format);
            return result.ToResult(values);
        }
        /// <summary>
        /// Read the continuous input register FC04
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public Result<byte[]> ReadInputRegisters(byte unitId, ushort beginAddress, ushort length)
        {
            var result = ReadBytes(FuncCode.ReadInputRegisters, unitId, beginAddress, length);
            return result.ToResult(result.Payload);
        }
        /// <summary>
        /// Read the single input register FC04
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public Result<T> ReadInputRegister<T>(byte unitId, ushort beginAddress, ByteFormat format = ByteFormat.AB) where T : struct
        {
            var result = ReadInputRegisters<T>(unitId, beginAddress, 1, format);
            var value = result.Value.FirstOrDefault();
            return result.ToResult(value);
        }
        /// <summary>
        /// Read the continuous input register FC04
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="length"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public Result<T[]> ReadInputRegisters<T>(byte unitId, ushort beginAddress, byte length, ByteFormat format = ByteFormat.AB) where T : struct
        {
            ValidateTypeFromByteFormat<T>(format);
            int wordSize = GetWordSize(format);
            var result = ReadBytes(FuncCode.ReadInputRegisters, unitId, beginAddress, (byte)(length * wordSize));
            var values = GetValuesFromByteFormat<T>(result.Payload, format);
            return result.ToResult(values);
        }
        /// <summary>
        /// Verify the byte type
        /// </summary>
        /// <param name="format"></param>
        /// <exception cref="ArgumentException"></exception>
        private void ValidateTypeFromByteFormat<T>(ByteFormat format) where T : struct
        {
            T t = default;
            if (t is byte)
                throw new ArgumentException("The type is incorrect. The minimum unit of a single read is 2 bytes", nameof(T));
            switch (format)
            {
                case ByteFormat.AB:
                    if (t is not short && t is not ushort)
                        throw new ArgumentException("Type error, only 2-byte built-in value types or arrays are supported", nameof(T));
                    break;
                case ByteFormat.ABCD:
                case ByteFormat.CDAB:
                case ByteFormat.BADC:
                case ByteFormat.DCBA:
                    if (t is not int && t is not uint && t is not float)
                        throw new ArgumentException("Type error, only 4-byte built-in value types or arrays are supported", nameof(T));
                    break;
                case ByteFormat.ABCDEFGH:
                case ByteFormat.GHEFCDAB:
                case ByteFormat.BADCFEHG:
                case ByteFormat.HGFEDCBA:
                    if (t is not long && t is not ulong && t is not double)
                        throw new ArgumentException("Type error, only 8-byte built-in value types or arrays are supported", nameof(T));
                    break;
            }
        }
        /// <summary>
        /// Get the size of a word in byte format
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        private static int GetWordSize(ByteFormat format) => format switch
        {
            ByteFormat.AB => 1,
            ByteFormat.ABCD or ByteFormat.CDAB or ByteFormat.BADC or ByteFormat.DCBA => 2,
            ByteFormat.ABCDEFGH
            or ByteFormat.GHEFCDAB
            or ByteFormat.BADCFEHG
            or ByteFormat.HGFEDCBA => 4,
            _ => 1,
        };
        /// <summary>
        /// Get the value according to the byte format
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="NullReferenceException"></exception>
        private T[] GetValuesFromByteFormat<T>(byte[] payload, ByteFormat format) where T : struct =>
            format switch
            {
                ByteFormat.AB => GetValuesFromByteFormat16<T>(payload),
                ByteFormat.ABCD or ByteFormat.CDAB or ByteFormat.BADC or ByteFormat.DCBA => GetValuesFromByteFormat32<T>(payload, format),
                ByteFormat.ABCDEFGH
                or ByteFormat.GHEFCDAB
                or ByteFormat.BADCFEHG
                or ByteFormat.HGFEDCBA => GetValuesFromByteFormat64<T>(payload, format),
                _ => throw new Exception("ByteFormat error"),
            };
        /// <summary>
        /// Get the value of 16 bytes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <returns></returns>
        private T[] GetValuesFromByteFormat16<T>(byte[] payload)
            where T : struct
        {
            T t = default;
            switch (t)
            {
                case short:
                    if (payload.ToInt16Array() is T[] int16Array)
                        return int16Array;
                    break;
                case ushort:
                    if (payload.ToUInt16Array() is T[] uint16Array)
                        return uint16Array;
                    break;
                default:
                    break;
            }
            throw new ArgumentException("Type not supported", nameof(T));
        }
        /// <summary>
        /// Get the value of 32 bytes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        private T[] GetValuesFromByteFormat32<T>(byte[] payload, ByteFormat format)
            where T : struct
        {
            T t = default;
            switch (t)
            {
                case int:
                    if (payload.ToInt32Array(format) is T[] int32Array)
                        return int32Array;
                    break;
                case uint:
                    if (payload.ToUInt32Array(format) is T[] uint32Array)
                        return uint32Array;
                    break;
                case float:
                    if (payload.ToFloatArray(format) is T[] floatArray)
                        return floatArray;
                    break;
                default:
                    break;
            }
            throw new ArgumentException("Type not supported", nameof(T));
        }
        /// <summary>
        /// Get a 64-byte value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        private T[] GetValuesFromByteFormat64<T>(byte[] payload, ByteFormat format)
            where T : struct
        {
            T t = default;
            switch (t)
            {
                case long:
                    if (payload.ToInt64Array(format) is T[] int64Array)
                        return int64Array;
                    break;
                case ulong:
                    if (payload.ToUInt64Array(format) is T[] uint64Array)
                        return uint64Array;
                    break;
                case double:
                    if (payload.ToDoubleArray(format) is T[] doubleArray)
                        return doubleArray;
                    break;
                default:
                    break;
            }
            throw new ArgumentException("Type not supported", nameof(T));
        }
        public static byte[] SingleCoilConverterToBytes(bool state) =>
            state ? new byte[] { 0xFF, 0x00 } : new byte[] { 0x00, 0x00 };
        public static bool SingleCoilConverterToBoolean(byte[] buffer)
        {
            if (buffer.Length != 2)
                throw new ArgumentOutOfRangeException(nameof(buffer.Length), buffer.Length, "The length of the array must be 2");
            return buffer[0] switch
            {
                0 when buffer[1] == 0 => false,
                0xFF when buffer[1] == 0 => true,
                _ => throw new ArgumentException("data format error", nameof(buffer)),
            };
        }
        private Result WriteMultipleValues(FuncCode funcCode, byte unitId, ushort beginAddress, ushort length, byte[] writeData)
        {
            return EnqueueExecute(() =>
            {
                var sendData = CreateADU(ReadOrWrite.Write, unitId, funcCode, writeBeginAddress: beginAddress, writeLength: length, writeData: writeData);
                return NoLockExecute(sendData);
            });
        }
        private Result WriteMultipleCoils(byte unitId, ushort beginAddress, ushort length, byte[] writeData)
        {
            int writeDataLen = length % 8 > 0 ? length / 8 + 1 : length / 8;
            if (writeDataLen != writeData.Length)
                throw new FormatException("The written data does not match the write length. The minimum write length is 1 byte");
            return WriteMultipleValues(FuncCode.WriteMultipleCoils, unitId, beginAddress, length, writeData);
        }
        /// <summary>
        /// Write to a single coil FC05
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="value"></param>
        public Result WriteSingleCoil(byte unitId, ushort beginAddress, bool value)
        {
            var writeData = SingleCoilConverterToBytes(value);
            return WriteMultipleValues(FuncCode.WriteSingleCoil, unitId, beginAddress, 1, writeData);
        }
        /// <summary>
        /// Write to multiple coils FC15
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="writeData"></param>
        public Result WriteMultipleCoils(byte unitId, ushort beginAddress, params bool[] writeData)
        {
            var buffer = writeData.ToBytes();
            return WriteMultipleCoils(unitId, beginAddress, (ushort)writeData.Length, buffer);
        }
        /// <summary>
        /// Write to a single register FC06
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="value"></param>
        public Result WriteSingleRegister(byte unitId, ushort beginAddress, short value)
        {
            return WriteMultipleValues(FuncCode.WriteSingleRegister, unitId, beginAddress, 1, value.ToBytes(false));
        }
        /// <summary>
        /// Write to a single register FC06
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="value"></param>
        public Result WriteSingleRegister(byte unitId, ushort beginAddress, ushort value)
        {
            return WriteMultipleValues(FuncCode.WriteSingleRegister, unitId, beginAddress, 1, value.ToBytes(false));
        }
        /// <summary>
        /// Write into a series of consecutive registers FC16
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="beginAddress"></param>
        /// <param name="writeData"></param>
        public Result WriteMultipleRegisters<T>(byte unitId, ushort beginAddress, params T[] writeData) where T : struct
        {
            ValidateWriteType(typeof(T));
            var data = writeData.ToBytesFromValues(false);
            int wordSize = writeData.Select(c => GetWordSizeFromValueType(c)).Sum();
            return WriteMultipleValues(FuncCode.WriteMultipleRegisters, unitId, beginAddress, (ushort)wordSize, data);
        }
        /// <summary>
        /// Read/write multi-register FC23
        /// </summary>
        /// <param name="unitId">unique identification Id</param>
        /// <param name="readBeginAddress">Read the starting address</param>
        /// <param name="readLength">Read the address length</param>
        /// <param name="writeBeginAddress">Write the starting address</param>
        /// <param name="writeData">Write data with a minimum write unit of 1Word=2Byte</param>
        /// <returns></returns>
        public Result<ushort[]> ReadWriteMultipleRegisters(byte unitId, ushort readBeginAddress, ushort readLength, ushort writeBeginAddress, ushort[] writeData)
        {
            return EnqueueExecute(() =>
            {
                var sendData = CreateADU(ReadOrWrite.ReadWrite, unitId, FuncCode.ReadWriteMultipleRegisters, readBeginAddress, readLength, writeBeginAddress, (ushort)writeData.Length, writeData.ToBytes(false));
                var result = NoLockExecute(sendData);
                var value = result.Payload.ToUInt16Array(true);
                return result.ToResult(value);
            });
        }
        /// <summary>
        /// Verify the write type
        /// </summary>
        /// <param name="type"></param>
        /// <exception cref="ArgumentException"></exception>
        private void ValidateWriteType(Type type)
        {
            var name = type.FullName;
            if (name == typeof(byte).FullName)
                throw new ArgumentException("Type error. The minimum unit for a single write is 2 bytes", nameof(type));
            if (name != typeof(short).FullName
                && name != typeof(ushort).FullName
                && name != typeof(int).FullName
                && name != typeof(uint).FullName
                && name != typeof(float).FullName
                && name != typeof(long).FullName
                && name != typeof(ulong).FullName
                && name != typeof(double).FullName)
                throw new ArgumentException("Type error, unsupported data type", nameof(type));
        }
        /// <summary>
        /// Get the character size of the value type
        /// </summary>
        /// <typeparam name="T">Only basic types are supported</typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private int GetWordSizeFromValueType<T>(T t) where T : struct => t switch
        {
            short or ushort => 1,
            int or uint or float => 2,
            long or ulong or double => 4,
            _ => throw new ArgumentException($"Unsupported types `{nameof(T)}`", nameof(t)),
        };
    }
}
