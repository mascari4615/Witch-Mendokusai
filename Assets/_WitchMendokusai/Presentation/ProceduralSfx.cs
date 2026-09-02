using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai.Presentation
{
	/// <summary>
	/// Idle 임시 효과음
	/// 프로젝트 FMOD 뱅크에 이미 있는 짧은 효과음 재사용
	/// 전용 사운드 준비 뒤 이벤트 경로만 교체하는 경계
	/// </summary>
	public sealed class ProceduralSfx
	{
		private const string BLIP_EVENT = "event:/SFX/Monster/Hit";
		private const string CLICK_EVENT = "event:/SFX/UI/Click";
		private const string GOOD_EVENT = "event:/SFX/LevelUp";
		private const string SWEEP_EVENT = "event:/SFX/Equip";
		private const string TICK_EVENT = "event:/SFX/EXP";

		private readonly float minGap;
		private readonly float volume;
		private bool muted;
		private float lastBlipAt;
		private float lastTickAt;

		public ProceduralSfx(GameObject host, float volume = 0.35f, float minSecondsBetweenBlips = 0.06f)
		{
			_ = host;
			minGap = minSecondsBetweenBlips;
			this.volume = Mathf.Clamp01(volume);
		}

		public bool Muted
		{
			get => muted;
			set => muted = value;
		}

		public void Blip(int step)
		{
			if (Time.unscaledTime - lastBlipAt < minGap)
			{
				return;
			}

			lastBlipAt = Time.unscaledTime;
			float pitch = Mathf.Clamp(0.92f + Mathf.Clamp(step - 1, 0, 8) * 0.025f, 0.92f, 1.12f);
			Play(BLIP_EVENT, 0.35f, pitch);
		}

		public void Tick(float minGapSeconds)
		{
			if (Time.unscaledTime - lastTickAt < minGapSeconds)
			{
				return;
			}

			lastTickAt = Time.unscaledTime;
			Play(TICK_EVENT, 0.12f);
		}

		public void Click()
		{
			Play(CLICK_EVENT, 0.7f);
		}

		public void Good()
		{
			Play(GOOD_EVENT, 0.6f);
		}

		public void Sweep()
		{
			Play(SWEEP_EVENT, 0.5f);
		}

		private void Play(string eventPath, float gain, float pitch = 1f)
		{
			if (muted)
			{
				return;
			}

			try
			{
				EventInstance instance = RuntimeManager.CreateInstance(eventPath);
				instance.setVolume(volume * gain);
				instance.setPitch(pitch);
				instance.start();
				instance.release();
			}
			catch (EventNotFoundException)
			{
				Debug.LogWarning("[IdleSfx] FMOD event not found: " + eventPath);
			}
		}
	}
}
