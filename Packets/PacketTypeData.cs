using System;
namespace NetLib{
	public class PacketTypeData : IComparable, IComparable<PacketTypeData> {
		public string Name;
		public Func<PacketReader,object[]> Converter = null;

		public PacketTypeData(string name){
			Name = name;
		}

		public PacketTypeData(string name, Func<PacketReader,object[]> converter){
			Name = name;
			Converter = converter;
		}

		public int CompareTo(object obj){
			if(obj == null) return 1;
			PacketTypeData d;
			if((d = obj as PacketTypeData) != null){
				return d.Name.CompareTo(Name);
			}
			throw new ArgumentException("Object is not of type PacketTypeData");
		}

		public int CompareTo(PacketTypeData other)
			=> Name.CompareTo(other.Name);
	}
}
