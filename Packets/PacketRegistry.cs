using System;
using System.Collections.Generic;
using System.Linq;
namespace NetLib {
	internal class PacketRegistry {
		private readonly Dictionary<string, PacketType> _packets = new Dictionary<string, PacketType>();
		private readonly List<PacketType> _packetList = new List<PacketType>();

		public int PacketTypeCount => _packetList.Count;

		public PacketRegistry(IEnumerable<PacketTypeData> packetTypeData) {
			foreach(PacketTypeData d in (from packet in packetTypeData orderby packet select packet)) {
				switch(Register(d)) {
					case RegisterResult.Full:
						throw new PacketException("PacketRegistry became full");
					case RegisterResult.NameAlreadyRegistered:
						throw new PacketException($"Packet \"{d.Name}\"");
				}
			}
		}

		public PacketType GetPacket(ushort id)
			=> id < _packetList.Count ? _packetList[id] : null;

		public PacketType GetPacket(string name)
			=> _packets.TryGetValue(name, out PacketType packet) ? packet : null;

		public ushort GetID(string name)
			=> _packets.TryGetValue(name, out PacketType packet) ? packet.ID : (ushort)0;

		private RegisterResult Register(PacketTypeData packetData){
			if(_packets.ContainsKey(packetData.Name))
				return RegisterResult.NameAlreadyRegistered;
			int count = _packetList.Count;
			if(count <= ushort.MaxValue){
				PacketType packetType = new PacketType(packetData.Name, (ushort)count, packetData.Converter);
				_packetList.Add(packetType);
				_packets.Add(packetData.Name, packetType);
				return RegisterResult.Successful;
			}else{
				return RegisterResult.Full;
			}
		}

		public enum RegisterResult : byte {
			Successful,
			Full,
			NameAlreadyRegistered
		}
	}
}