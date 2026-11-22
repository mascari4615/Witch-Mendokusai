using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	// 월드 효과음의 단일 입구. 사건 이름을 CombatSfxTable 의 FMOD 경로, 최소 간격, 동시 발음 상한으로 바꿔 재생
	public static class CombatSfx
	{
		private const float DEFAULT_PITCH = 1f;
		private const float DEFAULT_VOLUME = 1f;

		private static CombatSfxTable table;
		private static readonly Dictionary<CombatSfxEvent, float> lastPlayedTimes = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetState()
		{
			table = null;
			lastPlayedTimes.Clear();
		}

		public static void Play(CombatSfxEvent sfxEvent, Vector3? position = null)
			=> Play(sfxEvent, position, DEFAULT_PITCH, DEFAULT_VOLUME);

		// 음높이와 음량은 FMOD setPitch, setVolume 배수 (1 이 원래 값). 경험치 줍기처럼 상황에 따라 바뀌는 소리용
		public static void Play(CombatSfxEvent sfxEvent, Vector3? position, float pitch, float volume)
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
			// 뱅크가 안 올라온 실행 (MPPM 가상 플레이어 등) 은 무음으로 넘어감
			if (AudioManager.TryGetExistingInstance(out AudioManager audioManager) == false || audioManager.AudioReady == false)
				return;

			bool atPosition = entry.PlayAtPosition && position.HasValue;
			// 수백 마리가 한꺼번에 내는 소리는 동시 발음 상한이 간격까지 맡음
			if (atPosition && entry.MaxVoices > 0)
			{
				SfxVoiceGate.TryPlay(entry.EventPath, position.Value, entry.MaxVoices, entry.MinIntervalSeconds, pitch, volume);
				return;
			}

			float now = Time.unscaledTime;
			if (lastPlayedTimes.TryGetValue(sfxEvent, out float lastTime) && now - lastTime < entry.MinIntervalSeconds)
				return;
			lastPlayedTimes[sfxEvent] = now;

			try
			{
				EventInstance instance = RuntimeManager.CreateInstance(entry.EventPath);
				if (atPosition)
					instance.set3DAttributes(position.Value.To3DAttributes());
				instance.setPitch(pitch);
				instance.setVolume(volume);
				instance.start();
				instance.release();
			}
			catch (EventNotFoundException)
			{
				Debug.LogWarning($"[CombatSfx] FMOD event not found: '{entry.EventPath}', sfx skip");
			}
		}
	}
}
