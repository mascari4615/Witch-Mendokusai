using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace WitchMendokusai
{
	// 이 파일의 모델은 전부 UGC json 을 Json.NET 으로 읽고 쓰기 위한 것이다 — Unity 직렬화는 건드리지
	// 않으므로 [Serializable] 을 달지 않는다(달면 JObject 같은 필드마다 "저장 안 된다"는 경고가 뜬다).
	// 배경 정본 = Domain/Data/Save/GameData.cs 주석.

	public class UGCTargetRef
	{
		public string kind;
		public string id;
	}

	public class UGCConditionData
	{
		public string type;
		public UGCTargetRef target;
		public JObject @params;
	}

	public class UGCActionData
	{
		public string type;
		public UGCTargetRef target;
		public JObject @params;
	}

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

	public class UGCVector3Data
	{
		public float x;
		public float y;
		public float z;
	}

	public class UGCSpawnPointData
	{
		public string id;
		public UGCVector3Data position;
		public UGCVector3Data rotation;
	}

	public class UGCCheckpointData
	{
		public string id;
		public UGCVector3Data position;
		public bool isDefault;
	}

	public class UGCMapObjectData
	{
		public string id;
		public string prefabId;
		public UGCVector3Data position;
		public UGCVector3Data rotation;
		public UGCVector3Data scale;
		public List<string> tags = new();
	}

	public class UGCZoneData
	{
		public string id;
		public UGCVector3Data position;
		public UGCVector3Data scale;
	}

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
	public class SeedSaveData
	{
		public string name;
		public int octaves = 4;
		public float frequency = 0.01f;
		public float persistence = 0.5f;
		public float lacunarity = 2f;
		public float biomeFrequency = 0.005f;
	}

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
