using System;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public struct FarmRuntimeData
	{
		public long PlantedUnixTime;

		public bool IsEmpty => PlantedUnixTime == 0;

		public static FarmRuntimeData Empty => new() { PlantedUnixTime = 0 };

		public static FarmRuntimeData Planted()
			=> new() { PlantedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };

		public long ElapsedSeconds
			=> DateTimeOffset.UtcNow.ToUnixTimeSeconds() - PlantedUnixTime;

		public string ToJson() => JsonUtility.ToJson(this);

		public static FarmRuntimeData FromJson(string json)
		{
			if (string.IsNullOrEmpty(json))
				return Empty;
			return JsonUtility.FromJson<FarmRuntimeData>(json);
		}
	}
}
