using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace NetLib {
	public class Client : IDisposable {
		public enum ClientState : byte {
			NotInitalized,
			Disconnected,
			Connecting,
			Connected
		}
		public enum DisconnectReason : byte {
			UserDisconnected,
			LostConnection,
			Error,
			ServerFull,
			Custom
		}

		public event Action<DisconnectReason,string> Disconnected;
		public event Action ConnectionFinalized;

		private readonly PacketRegistry _packetRegistry;
		public byte ClientIdx {get; private set;}
		public ClientState State { get; private set; } = ClientState.Disconnected;

		private ThreadManager _threadManager = new ThreadManager();

		private TcpClient _tcpSocket;
		private NetworkStream _tcpStream;
		private byte[] _tcpRecvBuffer;
		private readonly PacketReader _tcpPacketReader;

		private UdpClient _udpSocket;
		private IPEndPoint _udpEndPoint;

		private bool _isDisposed = false;

		public Client(IEnumerable<PacketTypeData> customPacketTypes) {
			List<PacketTypeData> allPacketData = new List<PacketTypeData>(Client_NetLibPackets.GetPacketData());
			allPacketData.AddRange(customPacketTypes);
			_packetRegistry = new PacketRegistry(allPacketData);
			_tcpPacketReader = new PacketReader();
			_tcpRecvBuffer = new byte[Globals.DATA_BUFFER_SIZE];
			
			
			throw new NotImplementedException("finish this");
			// add welcome listener to FinalizeConnection method
			// _packets.Welcome += FinalizeConnection;
		}

		public Action Connect(IPAddress ip, int port) {
			_tcpSocket = new TcpClient{
				ReceiveBufferSize = Globals.DATA_BUFFER_SIZE,
				SendBufferSize = Globals.DATA_BUFFER_SIZE
			};

			// connect to udp
			_udpEndPoint = new IPEndPoint(ip, port);

			// connect to tcp
			_tcpRecvBuffer = new byte[Globals.DATA_BUFFER_SIZE];
			State = ClientState.Connecting;
			_tcpSocket.BeginConnect(ip, port, (IAsyncResult result)=>{
					_tcpSocket.EndConnect(result);
					if(!_tcpSocket.Connected) return;
					_tcpStream = _tcpSocket.GetStream();
					_tcpStream.BeginRead(_tcpRecvBuffer, 0, Globals.DATA_BUFFER_SIZE, TCPReceiveCallback, null);
				}, _tcpSocket);
			return _threadManager.Update;
		}

		private void FinalizeConnection(byte clientIdx){
			ClientIdx = clientIdx;
			State = ClientState.Connected;

			_udpSocket = new UdpClient(((IPEndPoint)_tcpSocket.Client.LocalEndPoint).Port);
			_udpSocket.Connect(_udpEndPoint);
			_udpSocket.BeginReceive(UDPReceiveCallback, null);
			_threadManager.ExecuteOnMainThread(()=>ConnectionFinalized?.Invoke());
		}

		public void Disconnect(DisconnectReason reason, string description){
			if(State == ClientState.Disconnected)
				return;

			_tcpSocket.Close();
			if(State == ClientState.Connected){
				_udpSocket.Close();
			}
			_tcpStream = null;
			_tcpRecvBuffer = null;
			State = ClientState.Disconnected;
			_threadManager.ExecuteOnMainThread(()=>Disconnected?.Invoke(reason, description));
		}

		public bool AddListener(string packetName, PacketAction packet) {
			throw new NotImplementedException();
		}

		public bool RemoveListener(string packetName, PacketAction packet) {
			throw new NotImplementedException();
		}

		#region TCP
		public void SendTCP(byte[] packet){
			if(State == ClientState.Connected){
				try{
					_tcpStream.BeginWrite(packet, 0, packet.Length, null, null);
				}catch(Exception ex){
					#if DEBUG
						Globals.DebugLog?.Invoke($"Error sending TCP message to Server: {ex}");
					#endif
				}
			}else{
				#if DEBUG
					Globals.DebugLog?.Invoke($"Error sending TCP message to Server: not connected to server");
				#endif
			}
		}

		protected void TCPReceiveCallback(IAsyncResult result){
			try{
				int readLength = _tcpStream.EndRead(result);
				if(readLength <=0){
					Disconnect(DisconnectReason.LostConnection, "Connection ended");
					return;
				}
				byte[] data = new byte[readLength];
				Array.Copy(_tcpRecvBuffer, data, readLength);

				TCPHandleData(data);

				_tcpStream.BeginRead(_tcpRecvBuffer, 0, Globals.DATA_BUFFER_SIZE, TCPReceiveCallback, null);
			}catch{
				Disconnect(DisconnectReason.Error, "Error reading TCP message");
			}
		}

		public void TCPHandleData(byte[] data){
			int packetlength = 0;
			//append the latest packet/partial packet to the current packet reader
			_tcpPacketReader.AppendData(data);

			// read first int of data (should be the length of the packet sent)
			if(_tcpPacketReader.UnreadLength >= sizeof(int)){
				packetlength = _tcpPacketReader.PeekInt();
				if(packetlength <= 0) return;
				_tcpPacketReader.Skip(sizeof(int));
			}

			while(packetlength > 0 && packetlength <= _tcpPacketReader.UnreadLength){
				byte[] packetBytes = _tcpPacketReader.NextBytes(packetlength);

				_threadManager.ExecuteOnMainThread(()=>{
					using(PacketReader pr = new PacketReader(packetBytes)){
						ushort packetType = pr.NextUShort();
						_packetRegistry.GetPacket(packetType)?.InvokeRecieved(0, pr);
					}
				});

				packetlength = 0;
				if(_tcpPacketReader.UnreadLength >= sizeof(int)){
					packetlength  = _tcpPacketReader.PeekInt();
					if(packetlength <= 0) return;
					_tcpPacketReader.Skip(sizeof(int));
				}
			}
		}
		#endregion

		#region UDP
		public void SendUDP(byte[] packet){
			if(State == ClientState.Connected){
				try{
					_udpSocket.BeginSend(packet, packet.Length, null, null);
				}catch(Exception ex){
					#if DEBUG
						Globals.DebugLog?.Invoke($"Error sending TCP message to Server: {ex}");
					#endif

				}
			}else{
				#if DEBUG
					Globals.DebugLog?.Invoke($"Error sending TCP message to Server: not connected to server");
				#endif
			}
		}

		private void UDPReceiveCallback(IAsyncResult result){
			try{
				byte[] data = _udpSocket.EndReceive(result, ref _udpEndPoint);
				_udpSocket.BeginReceive(UDPReceiveCallback, null);

				// Did the server send a packet that is too small?
				if(data.Length < sizeof(int)){
					Disconnect(DisconnectReason.LostConnection, "Connection ended");
					return;
				}else{
					UDPHandleData(data);
				}
			}catch{
				Disconnect(DisconnectReason.Error, "Error reading UDP message");
			}
		}

		private void UDPHandleData(byte[] data){
			byte[] packetBody;
			using(PacketReader pr = new PacketReader(data)){
				int packetLength = pr.NextInt();
				packetBody = pr.NextBytes(packetLength);
			}

			_threadManager.ExecuteOnMainThread(()=>{
				using(PacketReader pr = new PacketReader(packetBody)){
					ushort packetType = pr.NextUShort();
					_packetRegistry.GetPacket(packetType)?.InvokeRecieved(0, pr);
				}
			});
		}
		#endregion

		protected virtual void Dispose(bool isDisposing){
			if(!_isDisposed){
				if(isDisposing){
					_tcpPacketReader?.Dispose();
					_tcpSocket?.Dispose();
					_tcpStream?.Dispose();
					_udpSocket?.Dispose();
					_tcpRecvBuffer=null;
				}
				_isDisposed = true;
			}
		}

		public void Dispose(){
			Disconnect(DisconnectReason.UserDisconnected, string.Empty);
			GC.SuppressFinalize(this);
		}

		~Client() => Dispose(false);
	}
}
