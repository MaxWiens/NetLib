using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System;

namespace NetLib {
	public class Server : IDisposable {
		public event Action<byte> ClientConnected;
		public event Action<byte> ClientDisconnected;

		private readonly PacketRegistry _packetRegistry;
		public ushort MaxClients { get; private set;}
		public int Port { get; private set; }
		public bool IsRunning {get; private set;}

		private ThreadManager _threadManager;
		private Stack<byte> _avilableClientIDs;
		private Dictionary<byte,Client> _clients;
		private Dictionary<IPEndPoint,Client> _endpointClients;
		private TcpListener _tcpListener;
		private UdpClient _udpSocket;
		private bool _isDisposed = false;
		private readonly ushort _welcomeID;
		private readonly ushort _serverFullID;

		public Server(IEnumerable<PacketTypeData> customPacketTypes){
			List<PacketTypeData> allPacketData = new List<PacketTypeData>(Server_NetLibPackets.GetPacketData());
			allPacketData.AddRange(customPacketTypes);
			_packetRegistry = new PacketRegistry(allPacketData);
			_welcomeID = _packetRegistry.GetID(PacketNames.Welcome);
			_serverFullID = _packetRegistry.GetID(PacketNames.ServerFull);
		}

		/// <summary>
		/// Starts server
		/// </summary>
		/// <param name="port">Port to send and receive messages from</param>
		/// <param name="maxClients">Max number of clients to support. Max 256</param>
		public Action Start(int port, ushort maxClients){
			#if DEBUG
				Globals.DebugLog?.Invoke($"Starting server on port {port}, max clients {maxClients}...");
			#endif

			if(maxClients > byte.MaxValue)
				throw new ArgumentOutOfRangeException($"Max clients greater than max value {byte.MaxValue}");

			_clients = new Dictionary<byte, Client>();
			_endpointClients = new Dictionary<IPEndPoint, Client>();
			_avilableClientIDs = new Stack<byte>(MaxClients);
			_threadManager = new ThreadManager();
			MaxClients = maxClients;
			Port = port;

			_tcpListener = new TcpListener(IPAddress.Any, Port);
			_tcpListener.Start();
			_tcpListener.BeginAcceptTcpClient(TCPConnectCallback, null);

			_udpSocket = new UdpClient(Port);
			_udpSocket.BeginReceive(UDPRecvCallback, null);

			IsRunning = true;

			#if DEBUG
				Globals.DebugLog?.Invoke("Server started!");
			#endif
			return _threadManager.Update;
		}

		public void Stop(){
			if(IsRunning){
				List<byte> clientIDs = new List<byte>(_clients.Keys);
				foreach(byte clientId in clientIDs){
					Disconnect(clientId);
				}

				_endpointClients = null;

				_avilableClientIDs.Clear();
				_avilableClientIDs = null;

				_threadManager = null;

				MaxClients = 0;
				Port = 0;
				_tcpListener.Stop();
				_tcpListener = null;

				_udpSocket.Close();
				IsRunning = false;
			}
		}

		public void Disconnect(byte clientIdx){
			if(_clients != null && _clients.TryGetValue(clientIdx, out Client client)){
				client.Dispose();
				_endpointClients.Remove(client.IPEndPoint);
				_clients.Remove(clientIdx);
				#if DEBUG
					Globals.DebugLog?.Invoke($"Client {clientIdx} disconnected");
				#endif
				_avilableClientIDs.Push(clientIdx);
				_threadManager.ExecuteOnMainThread(()=>ClientDisconnected?.Invoke(clientIdx));
			}
		}

		/// <summary>
		/// Sends a packet to a client using TCP
		/// </summary>
		/// <param name="clientID">Client to send packet to</param>
		/// <param name="packetData">Packet to send</param>
		public void SendTCP(byte clientID, byte[] packetData){
			if(_clients.TryGetValue(clientID, out Client client)){
				try{
					client.TcpStream.BeginWrite(packetData, 0, packetData.Length, null, null);
				}catch (Exception ex){
					#if DEBUG
						Globals.DebugLog?.Invoke($"Error sending data to client {clientID} using TCP: {ex}");
					#endif
				}
			}else{
				#if DEBUG
					Globals.DebugLog?.Invoke($"Attempt to send packet to {clientID} who doesn't exist");
				#endif
			}
		}

		/// <summary>
		/// Sends a packet to all clients using TCP
		/// </summary>
		/// <param name="packetData">Packet to send</param>
		public void SendTCPAll(byte[] packetData){
			foreach(Client client in _clients.Values){
				try{
					client.TcpStream.BeginWrite(packetData, 0, packetData.Length, null, null);
				}catch (Exception ex){
					#if (DEBUG)
						Globals.DebugLog?.Invoke($"Error sending data to client {client.ClientIndex} using TCP: {ex}");
					#endif
				}
			}
		}

		/// <summary>
		/// Sends packet data to all clients connected to server excluding one
		/// </summary>
		/// <param name="packetData">Data to send</param>
		/// <param name="clientID">Client to exclude</param>
		public void SendTCPAll(byte[] packetData, byte clientID){
			foreach(Client client in _clients.Values){
				if(client.ClientIndex != clientID){
					try{
						client.TcpStream.BeginWrite(packetData, 0, packetData.Length, null, null);
					}catch (Exception ex){
						#if (DEBUG)
							Globals.DebugLog?.Invoke($"Error sending data to client {client.ClientIndex} using TCP: {ex}");
						#endif
					}
				}
			}
		}

		/// <summary>
		/// Sends a packet to a client using UDP
		/// </summary>
		/// <param name="clientIdx">Client to send packet to</param>
		/// <param name="packet">Packet to send</param>
		public void SendUDP(byte clientIdx, byte[] packet){
			if(_clients.TryGetValue(clientIdx, out Client client)){
				try{
					_udpSocket.BeginSend(packet, packet.Length, client.IPEndPoint, null, null);
				}catch (Exception ex){
					#if DEBUG
						Globals.DebugLog?.Invoke($"Error sending data to client {clientIdx} using TCP: {ex}");
					#endif
				}
			}else{
				#if DEBUG
					Globals.DebugLog?.Invoke($"Attempt to send packet to {clientIdx} who doesn't exist");
				#endif
			}
		}

		public void SendUDPAll(byte[] data){
			foreach(Client client in _clients.Values){
				try{
					_udpSocket.BeginSend(data, data.Length, client.IPEndPoint, null, null);
				}catch (Exception ex){
					#if DEBUG
						Globals.DebugLog?.Invoke($"Error sending data to client {client.ClientIndex} using TCP: {ex}");
					#endif
				}
			}
		}

		private void SendWelcome(byte clientIdx){
			using(PacketBuilder pb = new PacketBuilder(_welcomeID)){
				pb.Write(clientIdx);
				SendTCP(clientIdx, pb.Build());
			}
		}

		private void SendServerFull(IPEndPoint userEndpoint){
			using(PacketBuilder pb = new PacketBuilder(_serverFullID)){
				byte[] packet = pb.Build();
				_udpSocket.BeginSend(packet, packet.Length, userEndpoint, null, null);
			}
		}

		/// <summary>
		/// Sets up new client to server
		/// </summary>
		/// <param name="result">Result of the connection</param>
		private void TCPConnectCallback(IAsyncResult result){
			try{
				if(_tcpListener == null) return;
				TcpClient socket = _tcpListener.EndAcceptTcpClient(result);
				_tcpListener.BeginAcceptTcpClient(TCPConnectCallback, null); // continue to listen for annother connection asynchronously
				#if DEBUG
					Globals.DebugLog?.Invoke($"Incoming connection from {socket.Client.RemoteEndPoint}");
				#endif
				int count = _clients.Count;
				// add client to clients array
				if(count < MaxClients){
					byte newClientIdx;
					if(_avilableClientIDs.Count > 0){
						newClientIdx = _avilableClientIDs.Pop();
					}else{
						newClientIdx = (byte)count;
					}

					#if DEBUG
						Globals.DebugLog?.Invoke($"Creating Client idx: {newClientIdx} for {socket.Client.RemoteEndPoint}");
					#endif
					Client c = new Client(socket, newClientIdx, this);
					_clients.Add(newClientIdx, c);
					_endpointClients.Add((IPEndPoint)socket.Client.RemoteEndPoint, c);
					// send welcome message
					SendWelcome(newClientIdx);
					_threadManager.ExecuteOnMainThread(()=>ClientConnected?.Invoke(newClientIdx));
				}else{
					#if DEBUG
						Globals.DebugLog?.Invoke($"{socket.Client.RemoteEndPoint} failed to connect. Server full!");
					#endif

					// send server full packet
					SendServerFull((IPEndPoint)socket.Client.RemoteEndPoint);
					socket.Close();
					socket.Dispose();
				}
			}catch(Exception ex){
				#if DEBUG
					Globals.DebugLog?.Invoke($"Error in TCP connect callback : {ex}");
				#endif
			}
		}

		/// <summary>
		/// Recieves UDP data
		/// </summary>
		/// <param name="result">Result of the recieve</param>
		private void UDPRecvCallback(IAsyncResult result){
			try{
				IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
				// This can throw an socket exception, Fix this
				byte[] data = _udpSocket.EndReceive(result, ref endPoint);
				_udpSocket.BeginReceive(UDPRecvCallback, null); // continue to listen for more data asynchronously

				if(data.Length <= 0) return;

				using(PacketReader pr = new PacketReader(data)){
					if(_endpointClients.TryGetValue(endPoint, out Client client)){
						// handle data
						int packetlength = pr.NextInt();
						if(pr.UnreadLength >= packetlength){
							byte[] packetBytes = pr.NextBytes(packetlength);
							_threadManager.ExecuteOnMainThread(()=>{
								using(PacketReader packetReader = new PacketReader(packetBytes)){
									PacketType packetType = _packetRegistry.GetPacket((ushort)packetReader.NextShort());
									packetType?.InvokeRecieved(client.ClientIndex, packetReader);
								}
							});
						}
						else throw new Exception("Error, impartial udp packet?!?");

					}
				}
			}catch(Exception ex){
				#if DEBUG
					Globals.DebugLog?.Invoke($"Error receiving UDP data: {ex}");
				#endif
				return;
			}
			//throw new NotImplementedException("finish this");
		}

		protected virtual void Dispose(bool isDisposing){
			if(!_isDisposed){
				if(isDisposing){
					if(_clients != null){
						foreach(Client c in _clients.Values)
							c.Dispose();
					}
					_udpSocket.Close();
					_udpSocket.Dispose();
				}
				_isDisposed = true;
			}
		}

		public void Dispose(){
			Stop();
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~Server() => Dispose(false);

		public IEnumerable<byte> ClientIdxs => _clients.Keys;

		private class Client : IDisposable {
			public readonly string EndPointStr;
			public readonly IPEndPoint IPEndPoint;
			public readonly byte ClientIndex;
			public NetworkStream TcpStream;
			private TcpClient _socket;
			private byte[] _tcpRecvBuffer = null;
			private PacketReader _packetReader = new PacketReader();
			private Server _server;
			private bool _isDisposed = false;

			/// <summary>
			/// Initalize and connect client
			/// </summary>
			/// <param name="socket">Client Socket to connect to</param>
			public Client(TcpClient socket, byte index, Server server){
				//IPEndPoint e = new IPEndPoint()
				ClientIndex = index;
				_socket = socket;
				_server = server;

				EndPointStr = _socket.Client.RemoteEndPoint.ToString();
				IPEndPoint = _socket.Client.RemoteEndPoint as IPEndPoint;
				if(IPEndPoint == null){
					throw new Exception("CASTING IPENDPOINT WEIRDNESS");
				}
				_socket.ReceiveBufferSize = Globals.DATA_BUFFER_SIZE;
				_socket.SendBufferSize = Globals.DATA_BUFFER_SIZE;

				TcpStream = _socket.GetStream();

				_tcpRecvBuffer = new byte[Globals.DATA_BUFFER_SIZE];

				TcpStream.BeginRead(_tcpRecvBuffer, 0, Globals.DATA_BUFFER_SIZE, TCPReceiveCallback, null);
			}

			private void TCPReceiveCallback(IAsyncResult result){
				try{
					int byteLength = TcpStream.EndRead(result);
					if(byteLength <= 0){
						_server.Disconnect(ClientIndex);
						return;
					}
					byte[] data = new byte[byteLength];
					Array.Copy(_tcpRecvBuffer, data, byteLength);

					TCPHandleData(data);
					TcpStream.BeginRead(_tcpRecvBuffer, 0, Globals.DATA_BUFFER_SIZE, TCPReceiveCallback, null);
				}catch(Exception ex){
					#if DEBUG
						Globals.DebugLog?.Invoke($"Error receiving TCP data: {ex}");
					#endif
					_server.Disconnect(ClientIndex);
				}
			}

			private void TCPHandleData(byte[] packet){
				int packetlength = 0;

				//append the latest packet/partial packet to the current packet reader
				_packetReader.AppendData(packet);

				// read first int of data (should be the length of the packet sent)
				if(_packetReader.UnreadLength >= sizeof(int)){
					packetlength = _packetReader.PeekInt();
					if(packetlength <= 0) return;
					_packetReader.Skip(sizeof(int));
				}

				while(packetlength > 0 && packetlength <= _packetReader.UnreadLength){
					byte[] packetBytes = _packetReader.NextBytes(packetlength);
					//throw new NotImplementedException("Finish This");
					_server._threadManager.ExecuteOnMainThread(()=>{
						using(PacketReader pr = new PacketReader(packetBytes)){
							_server._packetRegistry.GetPacket(pr.NextUShort())?.InvokeRecieved(ClientIndex, pr);
						}
					});

					packetlength = 0;
					if(_packetReader.UnreadLength >= sizeof(int)){
						packetlength  = _packetReader.PeekInt();
						if(packetlength <= 0) return;
						_packetReader.Skip(sizeof(int));
					}
				}
			}

			protected virtual void Dispose(bool isDisposing){
				if(!_isDisposed){
					if(isDisposing){
						_socket.Dispose();
						TcpStream.Dispose();
						_packetReader.Dispose();
					}
					_isDisposed = true;
				}
			}

			public void Dispose(){
				TcpStream.Close();
				_socket.Close();
				_tcpRecvBuffer = null;
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			~Client() => Dispose(false);
		}
	}
}
