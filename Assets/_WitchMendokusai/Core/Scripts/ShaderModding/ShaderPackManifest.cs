using System;

namespace WitchMendokusai
{
	[Serializable]
	public class ShaderPackManifest
	{
		public int schemaVersion;
		public string name;
		public string author;
		public string version;
		public string description;
		public ShaderPackSlotInfo[] slots;
	}

	[Serializable]
	public class ShaderPackSlotInfo
	{
		public string id;
		public string assetName;
		public string blendMode;
		public int priority;
	}
}
