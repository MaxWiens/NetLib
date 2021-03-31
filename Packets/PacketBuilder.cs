using System;
using System.Collections.Generic;
using System.Text;

namespace NetLib {
	public class PacketBuilder : IDisposable {
		private List<byte> _data;
		private bool _isDisposed = false;

		public PacketBuilder(ushort packetType){
			_data = new List<byte>();
			Write(packetType);
		}

		public void Write(byte data) => _data.Add(data);
		public void Write(sbyte data) => _data.Add((byte)data);
		public void Write(IEnumerable<byte> data) => _data.AddRange(data);
		public void Write(short data) => _data.AddRange(BitConverter.GetBytes(data));
		public void Write(ushort data) => _data.AddRange(BitConverter.GetBytes(data));
		public void Write(int data) => _data.AddRange(BitConverter.GetBytes(data));
		public void Write(uint data) => _data.AddRange(BitConverter.GetBytes(data));
		public void Write(long data) => _data.AddRange(BitConverter.GetBytes(data));
		public void Write(ulong data) => _data.AddRange(BitConverter.GetBytes(data));
		public void Write(float data) => _data.AddRange(BitConverter.GetBytes(data));
		public void Write(double data) => _data.AddRange(BitConverter.GetBytes(data));
		public void Write(string data, Encoding encoding){
			byte[] bytes = encoding.GetBytes(data);
			_data.AddRange(BitConverter.GetBytes(bytes.Length));
			_data.AddRange(bytes);
		}
		public void Write(string data) => Write(data, Encoding.ASCII);
		public void Write(bool data) => _data.AddRange(BitConverter.GetBytes(data));

		public byte[] Build(){
			byte[] packet = new byte[sizeof(int)+_data.Count];
			BitConverter.GetBytes(_data.Count).CopyTo(packet, 0);
			_data.CopyTo(packet, sizeof(int));
			return packet;
		}

		/// <summary>
		/// Inserts clientIdx infront of the entire packet including the packet body length
		/// Used for sending UDP packets from the client to server
		/// </summary>
		/// <param name="clientIdx"></param>
		/// <returns></returns>
		public byte[] Build(byte clientIdx){
			byte[] packet = new byte[sizeof(byte)+sizeof(int)+_data.Count];
			packet[0] = clientIdx;
			BitConverter.GetBytes(_data.Count).CopyTo(packet, sizeof(byte));
			_data.CopyTo(packet, sizeof(byte)+sizeof(int));
			return packet;
		}

		protected virtual void Dispose(bool disposing){
			if(!_isDisposed){
				if(disposing)
					_data = null;
				_isDisposed = true;
			}
		}

		public void Dispose(){
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~PacketBuilder() => Dispose(false);

		}
}