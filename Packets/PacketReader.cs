using System;
using System.Text;

namespace NetLib{
	public class PacketReader : IDisposable{
		protected byte[] _data;
		protected int _readPos = 0;
		protected bool _isDisposed = false;

		public PacketReader() => _data = new byte[0];
		public PacketReader(byte[] packet) => _data = packet;

		public int UnreadLength => _data.Length-_readPos;

		public void AppendData(byte[] data){
			int unreadlen = UnreadLength;
			byte[] newdata = new byte[unreadlen+data.Length];
			Array.Copy(data, newdata, unreadlen);
			Array.Copy(data, 0, newdata, unreadlen, data.Length);
			_data = newdata;
			_readPos = 0;
		}

		public bool Skip(int numBytes){
			if(_readPos+numBytes <= _data.Length){
				_readPos += numBytes;
				return true;
			}
			return false;
		}

		public byte PeekByte(){
			if(_readPos + sizeof(byte) <= _data.Length)
				return _data[_readPos];
			else throw new PacketException("Could not read byte");
		}
		public byte NextByte(){
			byte value = PeekByte();
			_readPos += sizeof(byte);
			return value;
		}
		public sbyte PeekSByte(){
			if(_readPos + sizeof(sbyte) <= _data.Length)
				return (sbyte)_data[_readPos];
			else throw new PacketException("Could not read sbyte");
		}
		public sbyte NextSByte(){
			sbyte value = PeekSByte();
			_readPos += sizeof(sbyte);
			return value;
		}

		public byte[] PeekBytes(int numBytes){
			if(_readPos + numBytes <= _data.Length){
				byte[] bytes = new byte[numBytes];
				Array.Copy(_data, _readPos, bytes, 0, numBytes);
				return bytes;
			}
			else throw new PacketException("Could not read byte[]");
		}
		public byte[] NextBytes(int numBytes){
			byte[] bytes = PeekBytes(numBytes);
			_readPos += numBytes;
			return bytes;
		}

		public short PeekShort(){
			if(_readPos + sizeof(short) <= _data.Length)
				return BitConverter.ToInt16(_data, _readPos);
			else throw new PacketException("Could not read short");
		}
		public short NextShort(){
			short value = PeekShort();
			_readPos += sizeof(short);
			return value;
		}
		public ushort PeekUShort(){
			if(_readPos + sizeof(ushort) <= _data.Length)
				return BitConverter.ToUInt16(_data, _readPos);
			else throw new PacketException("Could not read ushort");
		}
		public ushort NextUShort(){
			ushort value = PeekUShort();
			_readPos += sizeof(ushort);
			return value;
		}

		public int PeekInt(){
			if(_readPos + sizeof(int) <= _data.Length)
				return BitConverter.ToInt32(_data, _readPos);
			else throw new PacketException("Could not read int");
		}
		public int NextInt(){
			int value = PeekInt();
			_readPos += sizeof(int);
			return value;
		}
		public uint PeekUInt(){
			if(_readPos + sizeof(uint) <= _data.Length)
				return BitConverter.ToUInt32(_data, _readPos);
			else throw new PacketException("Could not read uint");
		}
		public uint NextUInt(){
			uint value = PeekUInt();
			_readPos += sizeof(uint);
			return value;
		}

		public long PeekLong(){
			if(_readPos + sizeof(long) <= _data.Length)
				return BitConverter.ToInt64(_data, _readPos);
			else throw new PacketException("Could not read long");
		}
		public long NextLong(){
			long value = PeekLong();
			_readPos += sizeof(long);
			return value;
		}
		public ulong PeekULong(){
			if(_readPos + sizeof(ulong) <= _data.Length)
				return BitConverter.ToUInt64(_data, _readPos);
			else throw new PacketException("Could not read ulong");
		}
		public ulong NextULong(){
			ulong value = PeekULong();
			_readPos += sizeof(ulong);
			return value;
		}

		public float PeekFloat(){
			if(_readPos + sizeof(float) <= _data.Length)
				return BitConverter.ToSingle(_data, _readPos);
			else throw new PacketException("Could not read float");
		}
		public float NextFloat(){
			float value = PeekFloat();
			_readPos += sizeof(float);
			return value;
		}

		public double PeekDouble(){
			if(_readPos + sizeof(double) <= _data.Length)
				return BitConverter.ToDouble(_data, _readPos);
			else throw new PacketException("Could not read double");
		}
		public double NextDouble(){
			double value = PeekDouble();
			_readPos += sizeof(double);
			return value;
		}

		public string PeekString() => PeekString(Encoding.ASCII, out int _);
		public string PeekString(Encoding encoding) => PeekString(encoding, out int _);
		public string PeekString(Encoding encoding, out int numBytes){
			if(_readPos + sizeof(int) <= _data.Length){
				try{
					numBytes = PeekInt();
					return encoding.GetString(_data, _readPos + sizeof(int), numBytes);
				}catch(Exception ex){
					throw new PacketException($"Could not read string value: {ex}");
				}
			}else
				throw new PacketException("Could not read string value");
		}
		public string NextString() => NextString(Encoding.ASCII);
		public string NextString(Encoding encoding){
			string value = PeekString(encoding, out int numBytes);
			_readPos += sizeof(int) + numBytes;
			return value;
		}

		public bool PeekBool(){
			if(_readPos + sizeof(bool) <= _data.Length){
				bool value = BitConverter.ToBoolean(_data, _readPos);
				_readPos += sizeof(bool);
				return value;
			}else
				throw new PacketException("Could not read bool");
		}
		public bool NextBool(){
			bool value = PeekBool();
			_readPos += sizeof(bool);
			return value;
		}

		protected virtual void Dispose(bool disposing){
			if(!_isDisposed){
				if(disposing){
					_data = null;
				}
				_isDisposed = true;
			}
		}

		public void Dispose(){
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~PacketReader() => Dispose(false);
	}
}