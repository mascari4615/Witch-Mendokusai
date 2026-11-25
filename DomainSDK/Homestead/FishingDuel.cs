using System;

namespace WitchMendokusai.DomainSDK.Homestead
{
	public enum FishingPhase
	{
		Waiting = 0,
		Bite = 1,
		Duel = 2,
		Caught = 3,
		Snapped = 4,
		Escaped = 5,
	}

	// 낚시 한 번. 기다림, 입질, 실 감기
	// - 감으면 실이 팽팽해지고 가까워짐. 놓으면 느슨해지고 멀어짐
	// - 고기가 몸부림칠 때 감으면 실이 빨리 팽팽해져 끊어짐
	// - 팽팽함이 알맞은 구간이면 더 빨리 감김
	public sealed class FishingDuel
	{
		private const float PERFECT_LIMIT = 0.92f;
		private const float SURGE_WARNING_SECONDS = 0.35f;
		private const float SURGE_SLACK_SHARE = 0.35f;
		private const float SURGE_REEL_SHARE = 0.25f;
		private const float JITTER_MIN = 0.7f;
		private const float JITTER_SPAN = 0.6f;

		private readonly HomesteadTuning tuning;
		private readonly Random random;
		private readonly float ease;
		private readonly float pullRate;
		private float timer;
		private bool strained;

		public FishDef Fish { get; }
		public FishingPhase Phase { get; private set; } = FishingPhase.Waiting;
		public float Tension { get; private set; }
		public float Progress { get; private set; }
		public bool IsSurging { get; private set; }
		// 몸부림 직전. 화면이 먼저 알리는 짧은 예고
		public bool IsSurgeNear => Phase == FishingPhase.Duel && IsSurging == false && timer <= SURGE_WARNING_SECONDS;
		public bool IsSweet => Tension >= tuning.SweetFrom && Tension <= tuning.SweetTo;
		// 봇과 안내가 쓰는 감기 상한
		public float TensionCeiling => tuning.SweetTo;
		public bool IsOver => Phase >= FishingPhase.Caught;
		// 실이 한 번도 위험하지 않았음
		public bool IsPerfect => Phase == FishingPhase.Caught && strained == false;

		internal FishingDuel(FishDef fish, HomesteadTuning tuning, int rodTier, Random random)
		{
			Fish = fish;
			this.tuning = tuning;
			this.random = random;
			ease = 1f - Math.Min(0.6f, rodTier * tuning.RodEase);
			pullRate = 1f + rodTier * tuning.RodPull;
			timer = Between(tuning.BiteDelayMin, tuning.BiteDelayMax);
		}

		private float Between(float from, float to) => from + (float)random.NextDouble() * (to - from);

		private float Jitter() => JITTER_MIN + (float)random.NextDouble() * JITTER_SPAN;

		// 누른 순간. 기다리는 중이면 놀라 달아나 다시 기다림, 입질이면 실 감기 시작
		public void Press()
		{
			if (Phase == FishingPhase.Waiting)
			{
				timer = Between(tuning.BiteDelayMin, tuning.BiteDelayMax);
			}
			else if (Phase == FishingPhase.Bite)
			{
				Phase = FishingPhase.Duel;
				Progress = tuning.StartProgress;
				Tension = tuning.SweetFrom;
				timer = tuning.CalmSeconds * Jitter();
			}
		}

		public void Step(float seconds, bool reeling)
		{
			switch (Phase)
			{
				case FishingPhase.Waiting:
					timer -= seconds;
					if (timer <= 0f)
					{
						Phase = FishingPhase.Bite;
						timer = tuning.BiteWindow;
					}
					break;
				case FishingPhase.Bite:
					timer -= seconds;
					// 놓치면 벌 없이 다시 기다림
					if (timer <= 0f)
					{
						Phase = FishingPhase.Waiting;
						timer = Between(tuning.BiteDelayMin, tuning.BiteDelayMax);
					}
					break;
				case FishingPhase.Duel:
					StepDuel(seconds, reeling);
					break;
			}
		}

		private void StepDuel(float seconds, bool reeling)
		{
			timer -= seconds;
			if (timer <= 0f)
			{
				IsSurging = IsSurging == false;
				// 변덕이 크면 잠잠한 시간이 짧음
				timer = (IsSurging ? tuning.SurgeSeconds : tuning.CalmSeconds * (1.2f - 0.7f * Fish.Temper)) * Jitter();
			}

			float strain = 0f;
			if (IsSurging)
			{
				strain += tuning.SurgeTension * Fish.Strength * ease * (reeling ? 1f : SURGE_SLACK_SHARE);
			}
			strain += reeling ? tuning.ReelTension : -tuning.SlackTension;
			Tension = Math.Max(0f, Tension + strain * seconds);
			if (Tension > PERFECT_LIMIT)
			{
				strained = true;
			}
			if (Tension >= 1f)
			{
				Phase = FishingPhase.Snapped;
				return;
			}

			if (reeling)
			{
				float pull = tuning.ReelProgress * pullRate * (IsSweet ? tuning.SweetBonus : 1f) * (IsSurging ? SURGE_REEL_SHARE : 1f);
				Progress += pull * seconds;
			}
			else
			{
				Progress -= tuning.DriftProgress * (0.4f + Fish.Strength) * (IsSurging ? 1.5f : 1f) * seconds;
			}

			if (Progress >= 1f)
			{
				Progress = 1f;
				Phase = FishingPhase.Caught;
			}
			else if (Progress <= 0f)
			{
				Progress = 0f;
				Phase = FishingPhase.Escaped;
			}
		}
	}
}
