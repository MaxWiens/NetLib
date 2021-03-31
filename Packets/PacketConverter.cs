using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLib {
	public abstract class PacketConverter {
		public abstract byte[] Write(params object[] args);
		public abstract object[] Read(PacketReader reader);
	}
}
