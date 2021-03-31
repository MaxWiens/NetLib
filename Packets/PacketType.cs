using System;
using System.Collections.Generic;
using System.Text;


// Packets are formatted like the following:
//
// Client to Server Packets using TCP and all Server to client packets (TCP & UDP)
// int packet body length (added automatically with PacketBuilder.Build)
// ushort packet type (what method to call as in PacketHandler call)
// bytes... body
//
// Client to Server Packets using UDP
// int clientIndex
// int packet body length
// ushort packet type (what method to call as in PacketHandler)
// bytes... body
//
// Note: when packets are sent, multiple packets are sent so the bytes of a read

namespace NetLib {
	public delegate void ClientPacketHandler(PacketReader packetReader);
	public delegate void ServerPacketHandler(byte sendingClientIdx, PacketReader packetReader);

	public delegate void PacketAction(byte sender, params object[] args);

	public class PacketType {
		public readonly string Name;
		public readonly ushort ID;
		public readonly Func<PacketReader, object[]> PacketReader;
		public event PacketAction OnPacketRecieved;
		public PacketType(string name, ushort id, Func<PacketReader, object[]> packetReader = null){
			PacketReader = packetReader;
			Name = name;
			ID = id;
		}
		public void InvokeRecieved(byte sender, PacketReader reader) {
			if(OnPacketRecieved != null) {
				if(PacketReader != null) OnPacketRecieved(sender, args: PacketReader(reader));
				else OnPacketRecieved(sender);
			}
		}
	}
}