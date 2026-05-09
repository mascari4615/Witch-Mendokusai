using Newtonsoft.Json.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public static class UGCActionExecutor
	{
		private const string DoorKind = "Door";
		private const string PlatformKind = "Platform";
		private const string CheckpointKind = "Checkpoint";
		private const string HazardKind = "Hazard";

		public sealed class MovePlatformCommand
		{
			public string routeId;
			public float speed;
			public bool loop;
		}

		public static bool TryExecute(UGCActionData action, out string error)
		{
			if (action == null)
			{
				error = "Action is null.";
				return false;
			}

			if (!TryGetExpectedKind(action.type, out string expectedKind, out error))
				return false;

			if (!TryResolveTarget(action, expectedKind, out GameObject target, out error))
			{
				return false;
			}

			switch (action.type)
			{
				case "SetDoorState":
					return TrySetDoorState(action, target, out error);
				case "MovePlatform":
					return TryMovePlatform(action, target, out error);
				case "ActivateCheckpoint":
					return TryActivateCheckpoint(action, target, out error);
				case "ToggleHazard":
					return TryToggleHazard(action, target, out error);
				default:
					error = $"Unsupported action type: {action.type}";
					return false;
			}
		}

		public static bool TryPreflight(UGCActionData action, out string error)
		{
			if (action == null)
			{
				error = "Action is null.";
				return false;
			}

			if (!TryGetExpectedKind(action.type, out string expectedKind, out error))
				return false;

			if (!TryResolveTarget(action, expectedKind, out GameObject target, out error))
			{
				return false;
			}

			switch (action.type)
			{
				case "SetDoorState":
					return TryRequireTargetKind(action, DoorKind, out error);
				case "MovePlatform":
					if (!TryRequireTargetKind(action, PlatformKind, out error))
						return false;

					if (string.IsNullOrWhiteSpace(GetString(action.@params, "routeId", string.Empty)))
					{
						error = $"MovePlatform action on '{target.name}' requires params.routeId.";
						return false;
					}

					error = null;
					return true;
				case "ActivateCheckpoint":
					return TryRequireTargetKind(action, CheckpointKind, out error);
				case "ToggleHazard":
					return TryRequireTargetKind(action, HazardKind, out error);
				default:
					error = $"Unsupported action type: {action.type}";
					return false;
			}
		}

		private static bool TrySetDoorState(UGCActionData action, GameObject target, out string error)
		{
			if (!TryRequireTargetKind(action, DoorKind, out error))
				return false;

			bool isOpen = GetBool(action.@params, "isOpen", true);

			Animator animator = target.GetComponent<Animator>();
			if (animator != null)
				animator.SetBool("IsOpen", isOpen);

			target.SendMessage("UGC_SetDoorState", isOpen, SendMessageOptions.DontRequireReceiver);
			UGCLog.Info($"Action SetDoorState executed. target={target.name}, isOpen={isOpen}");

			error = null;
			return true;
		}

		private static bool TryMovePlatform(UGCActionData action, GameObject target, out string error)
		{
			if (!TryRequireTargetKind(action, PlatformKind, out error))
				return false;

			MovePlatformCommand command = new()
			{
				routeId = GetString(action.@params, "routeId", string.Empty),
				speed = GetFloat(action.@params, "speed", 1f),
				loop = GetBool(action.@params, "loop", false),
			};

			if (string.IsNullOrWhiteSpace(command.routeId))
			{
				error = $"MovePlatform action on '{target.name}' requires params.routeId.";
				return false;
			}

			target.SendMessage("UGC_MovePlatform", command, SendMessageOptions.DontRequireReceiver);
			UGCLog.Info($"Action MovePlatform executed. target={target.name}, route={command.routeId}, speed={command.speed}, loop={command.loop}");

			error = null;
			return true;
		}

		private static bool TryActivateCheckpoint(UGCActionData action, GameObject target, out string error)
		{
			if (!TryRequireTargetKind(action, CheckpointKind, out error))
				return false;

			bool setAsRespawn = GetBool(action.@params, "setAsRespawn", true);
			target.SendMessage("UGC_ActivateCheckpoint", setAsRespawn, SendMessageOptions.DontRequireReceiver);
			UGCLog.Info($"Action ActivateCheckpoint executed. target={target.name}, setAsRespawn={setAsRespawn}");

			error = null;
			return true;
		}

		private static bool TryToggleHazard(UGCActionData action, GameObject target, out string error)
		{
			if (!TryRequireTargetKind(action, HazardKind, out error))
				return false;

			bool enabled = GetBool(action.@params, "enabled", true);
			target.SendMessage("UGC_SetHazardEnabled", enabled, SendMessageOptions.DontRequireReceiver);
			UGCLog.Info($"Action ToggleHazard executed. target={target.name}, enabled={enabled}");

			error = null;
			return true;
		}

		private static bool GetBool(JObject obj, string key, bool defaultValue)
		{
			if (obj == null || !obj.TryGetValue(key, out JToken token))
				return defaultValue;

			return token.Type switch
			{
				JTokenType.Boolean => token.Value<bool>(),
				JTokenType.Integer => token.Value<int>() != 0,
				JTokenType.String => bool.TryParse(token.Value<string>(), out bool parsed) ? parsed : defaultValue,
				_ => defaultValue,
			};
		}

		private static float GetFloat(JObject obj, string key, float defaultValue)
		{
			if (obj == null || !obj.TryGetValue(key, out JToken token))
				return defaultValue;

			return token.Type switch
			{
				JTokenType.Float => token.Value<float>(),
				JTokenType.Integer => token.Value<int>(),
				JTokenType.String => float.TryParse(token.Value<string>(), out float parsed) ? parsed : defaultValue,
				_ => defaultValue,
			};
		}

		private static string GetString(JObject obj, string key, string defaultValue)
		{
			if (obj == null || !obj.TryGetValue(key, out JToken token))
				return defaultValue;

			return token.Value<string>() ?? defaultValue;
		}

		private static bool TryRequireTargetKind(UGCActionData action, string expectedKind, out string error)
		{
			if (action?.target == null)
			{
				error = $"Action '{action?.type}' has invalid target.";
				return false;
			}

			if (!string.Equals(action.target.kind, expectedKind, System.StringComparison.OrdinalIgnoreCase))
			{
				error = $"Action '{action.type}' expects target kind '{expectedKind}' but got '{action.target.kind}'.";
				return false;
			}

			error = null;
			return true;
		}

		private static bool TryGetExpectedKind(string actionType, out string expectedKind, out string error)
		{
			switch (actionType)
			{
				case "SetDoorState":
					expectedKind = DoorKind;
					error = null;
					return true;
				case "MovePlatform":
					expectedKind = PlatformKind;
					error = null;
					return true;
				case "ActivateCheckpoint":
					expectedKind = CheckpointKind;
					error = null;
					return true;
				case "ToggleHazard":
					expectedKind = HazardKind;
					error = null;
					return true;
				default:
					expectedKind = null;
					error = $"Unsupported action type: {actionType}";
					return false;
			}
		}

		private static bool TryResolveTarget(UGCActionData action, string expectedKind, out GameObject target, out string error)
		{
			target = null;

			if (action?.target == null || string.IsNullOrWhiteSpace(action.target.id))
			{
				error = $"Action '{action?.type}' has invalid target.";
				UGCLog.Warn($"[ActionExec] {error}");
				return false;
			}

			UGCLog.Info($"[ActionExec] Resolving target: id={action.target.id}, expectedKind={expectedKind}");
			if (!UGCObjectRegistry.TryResolve(action.target.id, expectedKind, out target, out error))
			{
				UGCLog.Warn($"[ActionExec] Target resolution failed: {error}");
				return false;
			}

			UGCLog.Info($"[ActionExec] Target resolved: name={target.name}");
			return true;
		}
	}

	public static class UGCObjectRegistry
	{
		private const string DoorKind = "Door";
		private const string PlatformKind = "Platform";
		private const string CheckpointKind = "Checkpoint";
		private const string ZoneKind = "Zone";
		private const string HazardKind = "Hazard";

		private sealed class RegisteredObject
		{
			public string kind;
			public GameObject gameObject;
		}

		private static readonly System.Collections.Generic.Dictionary<string, RegisteredObject> registry = new System.Collections.Generic.Dictionary<string, RegisteredObject>();

		public static void Clear()
		{
			registry.Clear();
		}

		public static void Register(string id, string kind, GameObject gameObject)
		{
			if (string.IsNullOrWhiteSpace(id) || gameObject == null)
				return;

			if (registry.TryGetValue(id, out RegisteredObject existing) && existing.gameObject != null && existing.gameObject != gameObject)
				UGCLog.Warn($"Duplicate UGC registration for '{id}'. Replacing '{existing.gameObject.name}' with '{gameObject.name}'.");

			registry[id] = new RegisteredObject
			{
				kind = kind,
				gameObject = gameObject,
			};
		}

		public static void Unregister(string id, GameObject gameObject)
		{
			if (string.IsNullOrWhiteSpace(id) || !registry.TryGetValue(id, out RegisteredObject existing))
				return;

			if (gameObject == null || existing.gameObject == gameObject)
				registry.Remove(id);
		}

		public static bool TryResolve(string id, string expectedKind, out GameObject gameObject, out string error)
		{
			gameObject = null;

			if (string.IsNullOrWhiteSpace(id))
			{
				error = "UGC object id is required.";
				return false;
			}

			if (registry.TryGetValue(id, out RegisteredObject existing))
			{
				if (existing.gameObject == null)
				{
					registry.Remove(id);
				}
				else if (MatchesKind(existing.gameObject, expectedKind))
				{
					gameObject = existing.gameObject;
					error = null;
					return true;
				}
				else
				{
					error = $"UGC object '{id}' does not match expected kind '{expectedKind}'.";
					return false;
				}
			}

			gameObject = GameObject.Find(id);
			if (gameObject == null)
			{
				error = $"UGC object not found: {id}";
				return false;
			}

			if (!MatchesKind(gameObject, expectedKind))
			{
				error = $"UGC object '{id}' does not match expected kind '{expectedKind}'.";
				return false;
			}

			Register(id, expectedKind, gameObject);
			error = null;
			return true;
		}

		private static bool MatchesKind(GameObject gameObject, string expectedKind)
		{
			if (string.IsNullOrWhiteSpace(expectedKind))
				return true;

			switch (expectedKind)
			{
				case DoorKind:
					return gameObject.GetComponent<UGCTestDoorReceiver>() != null;
				case PlatformKind:
					return gameObject.GetComponent<UGCTestPlatformReceiver>() != null;
				case CheckpointKind:
					return gameObject.GetComponent<UGCTestCheckpointReceiver>() != null;
					case HazardKind:
						return gameObject.GetComponent<UGCTestHazardReceiver>() != null;
				case ZoneKind:
					return gameObject.GetComponent<UGCTriggerZone>() != null;
				default:
					return true;
			}
		}
	}
}
