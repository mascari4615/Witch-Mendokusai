using UnityEngine;

namespace WitchMendokusai
{
	// 강약 짝 겹치기. 같은 구간에 강한 적 1종과 약한 적 1종을 각자 초당 생성 수로
	[CreateAssetMenu(fileName = nameof(PairOverlapPattern), menuName = "WM/Wave Pattern/" + nameof(PairOverlapPattern))]
	public class PairOverlapPattern : WavePattern
	{
		[field: SerializeField] public Monster StrongMonster { get; private set; }
		[field: SerializeField, Tooltip("강한 적 초당 생성 수"), Min(0f)] public float StrongPerSecond { get; private set; } = 0.5f;
		[field: SerializeField] public Monster WeakMonster { get; private set; }
		[field: SerializeField, Tooltip("약한 적 초당 생성 수"), Min(0f)] public float WeakPerSecond { get; private set; } = 3f;
		[field: SerializeField, Tooltip("지속 초"), Min(0.1f)] public float Duration { get; private set; } = 60f;
		[field: SerializeField, Tooltip("숨 고르기 패턴과 최종 보스 뒤 생성률 배율을 따름")] public bool FollowPeriodicRate { get; private set; } = true;

		public override WavePatternRun CreateRun() => new Run(this);

		public override void Validate() => RequireMonsters(new[] { StrongMonster, WeakMonster }, nameof(StrongMonster) + ", " + nameof(WeakMonster));

		private class Run : WavePatternRun
		{
			private readonly PairOverlapPattern pattern;
			private float strongDebt;
			private float weakDebt;

			public Run(PairOverlapPattern pattern) => this.pattern = pattern;

			public override bool Tick(IWavePatternHost host, float localSeconds, float deltaSeconds)
			{
				if (localSeconds >= pattern.Duration)
					return true;
				float rate = pattern.FollowPeriodicRate ? host.PeriodicRateMultiplier : 1f;
				Spawn(host, pattern.StrongMonster, pattern.StrongPerSecond * rate, ref strongDebt, deltaSeconds);
				Spawn(host, pattern.WeakMonster, pattern.WeakPerSecond * rate, ref weakDebt, deltaSeconds);
				return false;
			}

			private static void Spawn(IWavePatternHost host, Monster monster, float perSecond, ref float debt, float deltaSeconds)
			{
				int requested = WavePatternRules.TakeSpawns(ref debt, host.ScaleSpawnRate(perSecond), deltaSeconds);
				int count = host.AllowedCount(SpawnClass.Periodic, requested);
				for (int i = 0; i < count; i++)
					host.SpawnAroundPlayer(monster);
			}
		}
	}
}
