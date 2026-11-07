using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public static class UiSfx
	{
		private static UiSfxTable table;
		private static readonly Dictionary<UiSfxEvent, float> lastPlayedTimes = new();
		private static float lastAnyPlayedTime = float.NegativeInfinity;

		public static event Action<UiSfxEvent> Played = delegate { };

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetState()
		{
			table = null;
			lastPlayedTimes.Clear();
			lastAnyPlayedTime = float.NegativeInfinity;
			Played = delegate { };
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
			// 확인과 탭 직후의 포커스 이동음은 생략
			if (sfxEvent == UiSfxEvent.FocusMove && now - lastAnyPlayedTime < entry.MinIntervalSeconds)
				return;
			lastPlayedTimes[sfxEvent] = now;
			lastAnyPlayedTime = now;
			Played.Invoke(sfxEvent);

			if (AudioManager.TryGetExistingInstance(out AudioManager audioManager))
				audioManager.PlaySfx(entry.EventPath);
		}
	}
}
