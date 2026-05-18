using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace WitchMendokusai
{
	[Serializable]
	public class UGCTargetRef
	{
		public string kind;
		public string id;
	}

	[Serializable]
	public class UGCConditionData
	{
		public string type;
		public UGCTargetRef target;
		public JObject @params;
	}

	[Serializable]
	public class UGCActionData
	{
		public string type;
		public UGCTargetRef target;
		public JObject @params;
	}

	[Serializable]
	public class UGCTriggerEventData
	{
		public int schemaVersion;
		public string id;
		public bool enabled;
		public List<string> tags = new();
		public string match;
		public List<UGCConditionData> conditions = new();
		public List<UGCActionData> actions = new();
		public float cooldownSec;
		public bool once;
	}

	[Serializable]
	public class UGCVector3Data
	{
		public float x;
		public float y;
		public float z;
	}

	[Serializable]
	public class UGCSpawnPointData
	{
		public string id;
		public UGCVector3Data position;
		public UGCVector3Data rotation;
	}

	[Serializable]
	public class UGCCheckpointData
	{
		public string id;
		public UGCVector3Data position;
		public bool isDefault;
	}

	[Serializable]
	public class UGCMapObjectData
	{
		public string id;
		public string prefabId;
		public UGCVector3Data position;
		public UGCVector3Data rotation;
		public UGCVector3Data scale;
		public List<string> tags = new();
	}

	[Serializable]
	public class UGCZoneData
	{
		public string id;
		public UGCVector3Data position;
		public UGCVector3Data scale;
	}

	[Serializable]
	public class UGCMapManifestData
	{
		public int schemaVersion;
		public string mapId;
		public int version;
		public string author;
		public string mode;
		public int seed;
		public List<UGCSpawnPointData> spawnPoints = new();
		public List<UGCCheckpointData> checkpoints = new();
		public List<UGCMapObjectData> objects = new();
		public List<UGCZoneData> zones = new();
		public List<string> triggers = new();
		public List<string> tags = new();
		public JObject meta;
	}

	// 월드젠 노이즈 시드 — TerrainParameters 의 직렬화 서브셋 (FBM 파라미터). 기본값 = TerrainParameters 기본값 정합.
	[Serializable]
	public class SeedSaveData
	{
		public string name;
		public int octaves = 4;
		public float frequency = 0.01f;
		public float persistence = 0.5f;
		public float lacunarity = 2f;
		public float biomeFrequency = 0.005f;
	}

	[Serializable]
	public class UGCSeedManifestData
	{
		public int schemaVersion;
		public int seedId;
		public int version;
		public string author;
		public SeedSaveData seedData;
		public List<string> tags = new();
		public JObject meta;
	}
}
