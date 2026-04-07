using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class UGCRuntimeSession
	{
		public UGCMapManifestData Manifest { get; private set; }
		public IReadOnlyDictionary<string, UGCTriggerEventData> TriggerMap => triggerMap;

		private readonly Dictionary<string, UGCTriggerEventData> triggerMap = new Dictionary<string, UGCTriggerEventData>();
		private readonly HashSet<string> onceExecuted = new HashSet<string>();
		private readonly Dictionary<string, float> cooldownUntil = new Dictionary<string, float>();
		private readonly Dictionary<string, int> lastExecutedFrame = new Dictionary<string, int>();
		private readonly HashSet<string> runningTriggers = new HashSet<string>();
		private float sessionStartTime;

		public bool TryLoadSamples(string manifestFileName, string triggerFileName, out string error)
		{
			if (!UGCJsonLoader.TryLoadMapManifestFromSample(manifestFileName, out UGCMapManifestData manifest, out error))
				return false;

			if (!UGCJsonLoader.TryLoadTriggerEventsFromSample(triggerFileName, out List<UGCTriggerEventData> triggerEvents, out error))
				return false;

			Manifest = manifest;
			sessionStartTime = Time.time;
			triggerMap.Clear();
			onceExecuted.Clear();
			cooldownUntil.Clear();
			lastExecutedFrame.Clear();
			runningTriggers.Clear();

			foreach (UGCTriggerEventData evt in triggerEvents)
			{
				if (triggerMap.ContainsKey(evt.id))
				{
					error = $"Duplicate trigger id found: {evt.id}";
					return false;
				}

				triggerMap[evt.id] = evt;
			}

			if (manifest.triggers != null)
			{
				HashSet<string> triggerRefs = new HashSet<string>();
				for (int i = 0; i < manifest.triggers.Count; i++)
				{
					string triggerId = manifest.triggers[i];
					if (!triggerRefs.Add(triggerId))
					{
						error = $"Duplicate trigger reference in manifest: {triggerId}";
						return false;
					}

					if (!triggerMap.ContainsKey(triggerId))
					{
						error = $"Manifest references unknown trigger id: {triggerId}";
						return false;
					}
				}
			}

			error = null;
			return true;
		}

		public bool TryExecuteTrigger(string triggerId, bool ignoreConditions, out string error)
		{
			if (!triggerMap.TryGetValue(triggerId, out UGCTriggerEventData evt))
			{
				error = $"Trigger not found: {triggerId}";
				return false;
			}

			if (!evt.enabled)
			{
				error = $"Trigger is disabled: {triggerId}";
				return false;
			}

			if (evt.once && onceExecuted.Contains(triggerId))
			{
				error = $"Trigger already executed once: {triggerId}";
				return false;
			}

			if (cooldownUntil.TryGetValue(triggerId, out float blockedUntil) && Time.time < blockedUntil)
			{
				error = $"Trigger is in cooldown: {triggerId}";
				return false;
			}

			if (lastExecutedFrame.TryGetValue(triggerId, out int frame) && frame == Time.frameCount)
			{
				error = $"Trigger already executed this frame: {triggerId}";
				return false;
			}

			if (!runningTriggers.Add(triggerId))
			{
				error = $"Trigger is already running: {triggerId}";
				return false;
			}

			try
			{
				if (!ignoreConditions && !TryEvaluateConditions(evt, out error))
					return false;

				if (!TryPreflightActions(evt, out error))
					return false;

				for (int i = 0; i < evt.actions.Count; i++)
				{
					if (!UGCActionExecutor.TryExecute(evt.actions[i], out error))
					{
						error = $"{triggerId} action[{i}] {error}";
						return false;
					}
				}

				if (evt.once)
					onceExecuted.Add(triggerId);

				if (evt.cooldownSec > 0f)
					cooldownUntil[triggerId] = Time.time + evt.cooldownSec;

				lastExecutedFrame[triggerId] = Time.frameCount;

				error = null;
				return true;
			}
			finally
			{
				runningTriggers.Remove(triggerId);
			}
		}

		private bool TryPreflightActions(UGCTriggerEventData evt, out string error)
		{
			if (evt?.actions == null || evt.actions.Count == 0)
			{
				error = null;
				return true;
			}

			for (int i = 0; i < evt.actions.Count; i++)
			{
				if (!UGCActionExecutor.TryPreflight(evt.actions[i], out error))
				{
					error = $"action[{i}] {error}";
					return false;
				}
			}

			error = null;
			return true;
		}

		private bool TryEvaluateConditions(UGCTriggerEventData evt, out string error)
		{
			if (evt.conditions == null || evt.conditions.Count == 0)
			{
				error = null;
				return true;
			}

			bool isAll = evt.match == "all";
			bool hasAnyTrue = false;

			for (int i = 0; i < evt.conditions.Count; i++)
			{
				bool isTrue = TryEvaluateCondition(evt.conditions[i]);
				hasAnyTrue |= isTrue;

				if (isAll && !isTrue)
				{
					error = $"Condition failed at index {i}.";
					return false;
				}
			}

			if (!isAll && !hasAnyTrue)
			{
				error = "No condition matched (match=any).";
				return false;
			}

			error = null;
			return true;
		}

		private bool TryEvaluateCondition(UGCConditionData condition)
		{
			if (condition == null)
				return false;

			switch (condition.type)
			{
				case "OnEnterZone":
				{
					string zoneId = condition.target?.id;
					string actorId = GetParamString(condition.@params, "actor", "player");
					return UGCConditionRuntime.HasEnteredZone(zoneId, actorId);
				}
				case "ElapsedTime":
				{
					float required = GetParamFloat(condition.@params, "sec", 0f);
					return Time.time - sessionStartTime >= required;
				}
				default:
					return false;
			}
		}

		private static string GetParamString(JObject obj, string key, string defaultValue)
		{
			if (obj == null || !obj.TryGetValue(key, out JToken token))
				return defaultValue;

			return token.Value<string>() ?? defaultValue;
		}

		private static float GetParamFloat(JObject obj, string key, float defaultValue)
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
	}
}
