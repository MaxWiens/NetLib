using System;

namespace NetLib {
	public static class Client_NetLibPackets {
		public static PacketTypeData[] GetPacketData()=> new PacketTypeData[] {
			new PacketTypeData(PacketNames.Welcome, WelcomeConverter),
			new PacketTypeData(PacketNames.ServerFull)
		};

		private static object[] WelcomeConverter(PacketReader packetReader)
			=> new object[] { packetReader.NextByte() };
	}
}