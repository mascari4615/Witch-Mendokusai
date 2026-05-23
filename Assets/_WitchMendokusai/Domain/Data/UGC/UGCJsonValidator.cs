using System.Collections.Generic;

namespace WitchMendokusai
{
	public static class UGCJsonValidator
	{
		public const int CURRENT_SCHEMA_VERSION = 1;
		private const string ZONE_KIND = "Zone";
		private const string DOOR_KIND = "Door";
		private const string PLATFORM_KIND = "Platform";
		private const string CHECKPOINT_KIND = "Checkpoint";
		private const string HAZARD_KIND = "Hazard";
		private const float EPSILON = 0.0001f;

		public static bool TryValidateTriggerEvent(UGCTriggerEventData data, out string error)
		{
			if (data == null)
			{
				error = "Trigger event is null.";
				return false;
			}

			if (data.schemaVersion != CURRENT_SCHEMA_VERSION)
			{
				error = $"Unsupported trigger schemaVersion: {data.schemaVersion}";
				return false;
			}

			if (string.IsNullOrWhiteSpace(data.id))
			{
				error = "Trigger event id is required.";
				return false;
			}

			if (data.match != "all" && data.match != "any")
			{
				error = $"Trigger event '{data.id}' has invalid match value: {data.match}";
				return false;
			}

			if (data.conditions == null)
				data.conditions = new List<UGCConditionData>();

			if (data.actions == null || data.actions.Count == 0)
			{
				error = $"Trigger event '{data.id}' must have at least one action.";
				return false;
			}

			for (int i = 0; i < data.conditions.Count; i++)
			{
				if (TryValidateCondition(data.conditions[i], i, out error) == false)
					return false;
			}

			for (int i = 0; i < data.actions.Count; i++)
			{
				if (TryValidateAction(data.actions[i], i, out error) == false)
					return false;
			}

			error = null;
			return true;
		}

		public static bool TryValidateManifest(UGCMapManifestData data, out string error)
		{
			if (data == null)
			{
				error = "Map manifest is null.";
				return false;
			}

			if (data.schemaVersion != CURRENT_SCHEMA_VERSION)
			{
				error = $"Unsupported manifest schemaVersion: {data.schemaVersion}";
				return false;
			}

			if (string.IsNullOrWhiteSpace(data.mapId))
			{
				error = "Map manifest mapId is required.";
				return false;
			}

			if (data.spawnPoints == null || data.spawnPoints.Count == 0)
			{
				error = $"Map manifest '{data.mapId}' must have at least one spawn point.";
				return false;
			}

			if (TryValidateUniqueIds(data.spawnPoints, p => p?.id, "spawnPoints", out error) == false)
				return false;

			if (TryValidateUniqueIds(data.checkpoints, p => p?.id, "checkpoints", out error) == false)
				return false;

			if (TryValidateUniqueIds(data.objects, p => p?.id, "objects", out error) == false)
				return false;

			if (TryValidateUniqueIds(data.zones, p => p?.id, "zones", out error) == false)
				return false;

			if (data.triggers != null)
			{
				HashSet<string> uniqueTriggers = new();
				for (int i = 0; i < data.triggers.Count; i++)
				{
					string triggerId = data.triggers[i];
					if (string.IsNullOrWhiteSpace(triggerId))
					{
						error = $"triggers[{i}] is empty.";
						return false;
					}

					if (uniqueTriggers.Add(triggerId) == false)
					{
						error = $"Duplicate trigger reference found: {triggerId}";
						return false;
					}
				}
			}

			error = null;
			return true;
		}

		private static bool TryValidateUniqueIds<T>(List<T> entries, System.Func<T, string> getId, string context, out string error)
		{
			if (entries == null)
			{
				error = null;
				return true;
			}

			HashSet<string> uniqueIds = new();
			for (int i = 0; i < entries.Count; i++)
			{
				string id = getId(entries[i]);
				if (string.IsNullOrWhiteSpace(id))
				{
					error = $"{context}[{i}] id is required.";
					return false;
				}

				if (uniqueIds.Add(id) == false)
				{
					error = $"Duplicate id found in {context}: {id}";
					return false;
				}
			}

			error = null;
			return true;
		}

		private static bool TryValidateTarget(UGCTargetRef target, string context, out string error)
		{
			if (target == null)
			{
				error = $"{context} target is null.";
				return false;
			}

			if (string.IsNullOrWhiteSpace(target.kind) || string.IsNullOrWhiteSpace(target.id))
			{
				error = $"{context} target.kind and target.id are required.";
				return false;
			}

			error = null;
			return true;
		}

		private static bool TryValidateCondition(UGCConditionData condition, int index, out string error)
		{
			if (condition == null)
			{
				error = $"condition[{index}] is null.";
				return false;
			}

			switch (condition.type)
			{
				case "OnEnterZone":
					if (TryValidateTarget(condition.target, $"condition[{index}]", out error) == false)
						return false;

					if (IsKind(condition.target.kind, ZONE_KIND) == false)
					{
						error = $"condition[{index}] expects target.kind '{ZONE_KIND}' but got '{condition.target.kind}'.";
						return false;
					}

					error = null;
					return true;

				case "ElapsedTime":
					if (condition.target != null && (string.IsNullOrWhiteSpace(condition.target.kind) == false || string.IsNullOrWhiteSpace(condition.target.id) == false))
					{
						error = $"condition[{index}] type 'ElapsedTime' should not define target.";
						return false;
					}

					error = null;
					return true;

				default:
					error = $"condition[{index}] has unsupported type: {condition.type}";
					return false;
			}
		}

		private static bool TryValidateAction(UGCActionData action, int index, out string error)
		{
			if (action == null)
			{
				error = $"action[{index}] is null.";
				return false;
			}

			if (TryValidateTarget(action.target, $"action[{index}]", out error) == false)
				return false;

			switch (action.type)
			{
				case "SetDoorState":
					if (IsKind(action.target.kind, DOOR_KIND) == false)
					{
						error = $"action[{index}] expects target.kind '{DOOR_KIND}' but got '{action.target.kind}'.";
						return false;
					}

					error = null;
					return true;

				case "MovePlatform":
					if (IsKind(action.target.kind, PLATFORM_KIND) == false)
					{
						error = $"action[{index}] expects target.kind '{PLATFORM_KIND}' but got '{action.target.kind}'.";
						return false;
					}

					error = null;
					return true;

				case "ActivateCheckpoint":
					if (IsKind(action.target.kind, CHECKPOINT_KIND) == false)
					{
						error = $"action[{index}] expects target.kind '{CHECKPOINT_KIND}' but got '{action.target.kind}'.";
						return false;
					}

					error = null;
					return true;

					case "ToggleHazard":
						if (IsKind(action.target.kind, HAZARD_KIND) == false)
						{
							error = $"action[{index}] expects target.kind '{HAZARD_KIND}' but got '{action.target.kind}'.";
							return false;
						}

						error = null;
						return true;

				default:
					error = $"action[{index}] has unsupported type: {action.type}";
					return false;
			}
		}

		public static bool TryValidateSeed(UGCSeedManifestData manifest, out string error)
		{
			if (manifest == null)
			{
				error = "Manifest is null";
				return false;
			}

			if (manifest.schemaVersion != CURRENT_SCHEMA_VERSION)
			{
				error = $"Schema version mismatch: expected {CURRENT_SCHEMA_VERSION}, got {manifest.schemaVersion}";
				return false;
			}

			if (manifest.seedId < 0)
			{
				error = $"Invalid seedId: {manifest.seedId} (must be >= 0)";
				return false;
			}

			SeedSaveData seedData = manifest.seedData;

			if (string.IsNullOrWhiteSpace(seedData.name))
			{
				error = "Seed name is required and must not be empty";
				return false;
			}

			if (seedData.octaves < 1 || seedData.octaves > 8)
			{
				error = $"Invalid octaves: {seedData.octaves} (must be 1~8)";
				return false;
			}

			if (seedData.frequency < EPSILON)
			{
				error = $"Invalid frequency: {seedData.frequency} (must be > {EPSILON})";
				return false;
			}

			if (seedData.persistence < 0.0f || seedData.persistence > 1.0f)
			{
				error = $"Invalid persistence: {seedData.persistence} (must be 0~1)";
				return false;
			}

			if (seedData.lacunarity < 1.0f)
			{
				error = $"Invalid lacunarity: {seedData.lacunarity} (must be >= 1.0)";
				return false;
			}

			if (seedData.biomeFrequency < EPSILON)
			{
				error = $"Invalid biomeFrequency: {seedData.biomeFrequency} (must be > {EPSILON})";
				return false;
			}

			error = null;
			return true;
		}

		private static bool IsKind(string actual, string expected)
		{
			return string.Equals(actual, expected, System.StringComparison.OrdinalIgnoreCase);
		}
	}
}
