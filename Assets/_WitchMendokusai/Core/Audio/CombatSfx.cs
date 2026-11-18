using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	public static class CombatSfx
	{
		private static CombatSfxTable table;
		private static readonly Dictionary<CombatSfxEvent, float> lastPlayedTimes = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetState()
		{
			table = null;
			lastPlayedTimes.Clear();
		}

		public static void Play(CombatSfxEvent sfxEvent, Vector3? position = null)
		{
			if (table == null)
			{
				table = Resources.Load<CombatSfxTable>(nameof(CombatSfxTable));
				if (table == null)
				{
					Debug.LogError($"{nameof(CombatSfxTable)} not found");
					return;
				}
			}

			if (table.TryGetEntry(sfxEvent, out CombatSfxTable.Entry entry) == false || string.IsNullOrEmpty(entry.EventPath))
				return;

			float now = Time.unscaledTime;
			if (lastPlayedTimes.TryGetValue(sfxEvent, out float lastTime) && now - lastTime < entry.MinIntervalSeconds)
				return;
			lastPlayedTimes[sfxEvent] = now;

			if (AudioManager.TryGetExistingInstance(out AudioManager audioManager) == false)
				return;

			if (entry.PlayAtPosition && position.HasValue)
			{
				try
				{
					RuntimeManager.PlayOneShot(entry.EventPath, position.Value);
				}
				catch (EventNotFoundException)
				{
					Debug.LogWarning($"[CombatSfx] FMOD event not found: '{entry.EventPath}', sfx skip");
				}
				return;
			}
			audioManager.PlaySfx(entry.EventPath);
		}
	}
}
