using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	// 같은 이벤트 경로의 동시 발음 수와 시작 간격을 코드에서 제한
	// 몬스터 피격과 사망처럼 수백 마리가 한꺼번에 내는 소리가 겹쳐 커지고 뭉개지는 것을 막음
	// 상한 값은 CombatSfxTable 의 maxVoices 한 곳. FMOD 이벤트의 Max Instances 대신 코드로 둔 것은 값이 표에서 보이게 하려는 것
	public static class SfxVoiceGate
	{
		private sealed class Voices
		{
			public readonly List<EventInstance> Playing = new();
			public float LastStartTime = float.NegativeInfinity;
		}

		private static readonly Dictionary<string, Voices> voicesByPath = new();
		private static readonly System.Predicate<EventInstance> isFinished = IsFinished;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetState() => voicesByPath.Clear();

		// 자리가 있으면 위치 재생하고 true. 상한이 차 있거나 간격 안이면 새 소리를 내지 않고 false
		public static bool TryPlay(string eventPath, Vector3 position, int maxVoices, float minIntervalSeconds, float pitch, float volume)
		{
			if (voicesByPath.TryGetValue(eventPath, out Voices voices) == false)
			{
				voices = new Voices();
				voicesByPath[eventPath] = voices;
			}

			voices.Playing.RemoveAll(isFinished);
			float now = Time.unscaledTime;
			if (CanStart(voices.Playing.Count, maxVoices, now, voices.LastStartTime, minIntervalSeconds) == false)
				return false;

			EventInstance instance;
			try
			{
				instance = RuntimeManager.CreateInstance(eventPath);
			}
			catch (EventNotFoundException)
			{
				// 뱅크 미로드나 경로 오타가 피해 처리까지 끊지 않게 (PlayOneShot 과 같은 처리)
				Debug.LogWarning($"[SfxVoiceGate] FMOD event not found: '{eventPath}', sfx skip");
				return false;
			}
			instance.set3DAttributes(position.To3DAttributes());
			instance.setPitch(pitch);
			instance.setVolume(volume);
			instance.start();
			// release 해도 끝날 때까지 소리는 남고, 끝나면 핸들이 무효가 되어 IsFinished 가 거둠
			instance.release();
			voices.Playing.Add(instance);
			voices.LastStartTime = now;
			return true;
		}

		public static bool CanStart(int playingCount, int maxVoices, float now, float lastStartTime, float minIntervalSeconds)
			=> playingCount < maxVoices && now - lastStartTime >= minIntervalSeconds;

		private static bool IsFinished(EventInstance instance)
		{
			if (instance.isValid() == false)
				return true;
			instance.getPlaybackState(out PLAYBACK_STATE state);
			return state == PLAYBACK_STATE.STOPPED;
		}
	}
}
