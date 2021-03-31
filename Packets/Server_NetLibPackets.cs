
namespace NetLib {
	public static class Server_NetLibPackets {
		public static PacketTypeData[] GetPacketData()=> new PacketTypeData[]{
			new PacketTypeData(PacketNames.Welcome),
			new PacketTypeData(PacketNames.ServerFull)
		};
	}
}