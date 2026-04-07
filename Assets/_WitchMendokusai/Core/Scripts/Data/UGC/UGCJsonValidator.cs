using System.Collections.Generic;

namespace WitchMendokusai
{
	public static class UGCJsonValidator
	{
		public const int CurrentSchemaVersion = 1;
		private const string ZoneKind = "Zone";
		private const string DoorKind = "Door";
		private const string PlatformKind = "Platform";
		private const string CheckpointKind = "Checkpoint";
		private const string HazardKind = "Hazard";

		public static bool TryValidateTriggerEvent(UGCTriggerEventData data, out string error)
		{
			if (data == null)
			{
				error = "Trigger event is null.";
				return false;
			}

			if (data.schemaVersion != CurrentSchemaVersion)
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
				if (!TryValidateCondition(data.conditions[i], i, out error))
					return false;
			}

			for (int i = 0; i < data.actions.Count; i++)
			{
				if (!TryValidateAction(data.actions[i], i, out error))
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

			if (data.schemaVersion != CurrentSchemaVersion)
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

			if (!TryValidateUniqueIds(data.spawnPoints, p => p?.id, "spawnPoints", out error))
				return false;

			if (!TryValidateUniqueIds(data.checkpoints, p => p?.id, "checkpoints", out error))
				return false;

			if (!TryValidateUniqueIds(data.objects, p => p?.id, "objects", out error))
				return false;

			if (!TryValidateUniqueIds(data.zones, p => p?.id, "zones", out error))
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

					if (!uniqueTriggers.Add(triggerId))
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

				if (!uniqueIds.Add(id))
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
					if (!TryValidateTarget(condition.target, $"condition[{index}]", out error))
						return false;

					if (!IsKind(condition.target.kind, ZoneKind))
					{
						error = $"condition[{index}] expects target.kind '{ZoneKind}' but got '{condition.target.kind}'.";
						return false;
					}

					error = null;
					return true;

				case "ElapsedTime":
					if (condition.target != null && (!string.IsNullOrWhiteSpace(condition.target.kind) || !string.IsNullOrWhiteSpace(condition.target.id)))
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

			if (!TryValidateTarget(action.target, $"action[{index}]", out error))
				return false;

			switch (action.type)
			{
				case "SetDoorState":
					if (!IsKind(action.target.kind, DoorKind))
					{
						error = $"action[{index}] expects target.kind '{DoorKind}' but got '{action.target.kind}'.";
						return false;
					}

					error = null;
					return true;

				case "MovePlatform":
					if (!IsKind(action.target.kind, PlatformKind))
					{
						error = $"action[{index}] expects target.kind '{PlatformKind}' but got '{action.target.kind}'.";
						return false;
					}

					error = null;
					return true;

				case "ActivateCheckpoint":
					if (!IsKind(action.target.kind, CheckpointKind))
					{
						error = $"action[{index}] expects target.kind '{CheckpointKind}' but got '{action.target.kind}'.";
						return false;
					}

					error = null;
					return true;

					case "ToggleHazard":
						if (!IsKind(action.target.kind, HazardKind))
						{
							error = $"action[{index}] expects target.kind '{HazardKind}' but got '{action.target.kind}'.";
							return false;
						}

						error = null;
						return true;

				default:
					error = $"action[{index}] has unsupported type: {action.type}";
					return false;
			}
		}

		private static bool IsKind(string actual, string expected)
		{
			return string.Equals(actual, expected, System.StringComparison.OrdinalIgnoreCase);
		}
	}
}
