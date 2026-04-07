using System.Collections.Generic;

namespace WitchMendokusai
{
	public static class UGCConditionRuntime
	{
		private static readonly HashSet<string> enteredZoneActors = new();

		public static void Clear()
		{
			enteredZoneActors.Clear();
		}

		public static void RegisterZoneEnter(string zoneId, string actorId)
		{
			if (string.IsNullOrWhiteSpace(zoneId) || string.IsNullOrWhiteSpace(actorId))
				return;

			enteredZoneActors.Add(BuildKey(zoneId, actorId));
		}

		public static bool HasEnteredZone(string zoneId, string actorId)
		{
			if (string.IsNullOrWhiteSpace(zoneId) || string.IsNullOrWhiteSpace(actorId))
				return false;

			return enteredZoneActors.Contains(BuildKey(zoneId, actorId));
		}

		private static string BuildKey(string zoneId, string actorId)
		{
			return $"{zoneId}::{actorId}";
		}
	}
}
