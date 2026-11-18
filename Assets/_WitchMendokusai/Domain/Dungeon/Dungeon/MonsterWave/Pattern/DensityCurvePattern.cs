using UnityEngine;

namespace WitchMendokusai
{
	// 고점 뒤 저점. 구간 밀도를 곡선으로. 자기 생성 곡선과 기존 주기 웨이브 배율 곡선 둘
	// 자기 생성은 자기 곡선과 최종 보스 뒤 배율만 따름
	[CreateAssetMenu(fileName = nameof(DensityCurvePattern), menuName = "WM/Wave Pattern/" + nameof(DensityCurvePattern))]
	public class DensityCurvePattern : WavePattern
	{
		[field: SerializeField, Tooltip("자기 생성에서 고를 적. 비우면 자기 생성 없음")] public Monster[] Monsters { get; private set; }
		[field: SerializeField, Tooltip("지속 초"), Min(0.1f)] public float Duration { get; private set; } = 90f;
		[field: SerializeField, Tooltip("가로: 패턴 시작부터 초, 세로: 초당 생성 수")] public AnimationCurve SpawnsPerSecond { get; private set; } = new(new Keyframe(0f, 2f), new Keyframe(30f, 8f), new Keyframe(45f, 0f), new Keyframe(90f, 0f));
		[field: SerializeField, Tooltip("가로: 패턴 시작부터 초, 세로: 기존 주기 웨이브와 짝 패턴 생성률 배율. 0 이면 멈춤")] public AnimationCurve PeriodicRate { get; private set; } = new(new Keyframe(0f, 1f), new Keyframe(30f, 1.5f), new Keyframe(45f, 0.2f), new Keyframe(90f, 1f));

		public override WavePatternRun CreateRun() => new Run(this);

		// 적이 비면 배율 곡선만 쓰는 패턴. 빈 칸만 검사
		public override void Validate()
		{
			if (Monsters != null && Monsters.Length > 0)
				RequireMonsters(Monsters, nameof(Monsters));
		}

		private class Run : WavePatternRun
		{
			private readonly DensityCurvePattern pattern;
			private float debt;

			public Run(DensityCurvePattern pattern) => this.pattern = pattern;

			public override bool Tick(IWavePatternHost host, float localSeconds, float deltaSeconds)
			{
				if (localSeconds >= pattern.Duration)
					return true;
				if (pattern.Monsters == null || pattern.Monsters.Length == 0)
					return false;

				float perSecond = host.ScaleSpawnRate(Mathf.Max(0f, pattern.SpawnsPerSecond.Evaluate(localSeconds))) * host.FinalBossRateMultiplier;
				int count = host.AllowedCount(SpawnClass.Periodic, WavePatternRules.TakeSpawns(ref debt, perSecond, deltaSeconds));
				for (int i = 0; i < count; i++)
					host.SpawnAroundPlayer(pattern.Monsters[Random.Range(0, pattern.Monsters.Length)]);
				return false;
			}

			public override float PeriodicRateMultiplier(float localSeconds) => Mathf.Max(0f, pattern.PeriodicRate.Evaluate(localSeconds));
		}
	}
}
