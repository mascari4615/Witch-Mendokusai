using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public static class UiSfx
	{
		private static UiSfxTable table;
		private static readonly Dictionary<UiSfxEvent, float> lastPlayedTimes = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetState()
		{
			table = null;
			lastPlayedTimes.Clear();
		}

		public static void Play(UiSfxEvent sfxEvent)
		{
			if (table == null)
			{
				table = Resources.Load<UiSfxTable>(nameof(UiSfxTable));
				if (table == null)
				{
					Debug.LogError($"{nameof(UiSfxTable)} not found");
					return;
				}
			}

			if (table.TryGetEntry(sfxEvent, out UiSfxTable.Entry entry) == false || string.IsNullOrEmpty(entry.EventPath))
				return;

			float now = Time.unscaledTime;
			if (lastPlayedTimes.TryGetValue(sfxEvent, out float lastTime) && now - lastTime < entry.MinIntervalSeconds)
				return;
			lastPlayedTimes[sfxEvent] = now;

			if (AudioManager.TryGetExistingInstance(out AudioManager audioManager))
				audioManager.PlaySfx(entry.EventPath);
		}
	}
}
