using UnityEngine;

namespace WitchMendokusai
{
	// 움직이는 벽. 여러 줄이 한 방향으로 직진, 빈 줄 묶음이 파마다 옆으로 움직여 빠져나갈 틈
	[CreateAssetMenu(fileName = nameof(MovingWallPattern), menuName = "WM/Wave Pattern/" + nameof(MovingWallPattern))]
	public class MovingWallPattern : WavePattern
	{
		[field: SerializeField] public Monster[] Monsters { get; private set; }
		[field: SerializeField, Tooltip("한 파의 줄 수"), Min(1)] public int LaneCount { get; private set; } = 21;
		[field: SerializeField, Tooltip("한 파의 빈 줄 수"), Min(0)] public int GapCount { get; private set; } = 2;
		[field: SerializeField, Tooltip("파마다 빈 줄 묶음이 움직일 수 있는 최대 줄 수"), Min(0)] public int GapStepLanes { get; private set; } = 2;
		[field: SerializeField, Tooltip("줄 간격"), Min(0.1f)] public float LaneSpacing { get; private set; } = 1f;
		[field: SerializeField, Tooltip("파 수"), Min(1)] public int WaveCount { get; private set; } = 5;
		[field: SerializeField, Tooltip("파 간격 초"), Min(0f)] public float WaveInterval { get; private set; } = 0.5f;
		[field: SerializeField, Tooltip("플레이어에서 벽 출발선까지 거리"), Min(1f)] public float StartDistance { get; private set; } = 14f;
		[field: SerializeField, Tooltip("진행 방향을 판마다 무작위로")] public bool RandomDirection { get; private set; } = true;
		[field: SerializeField, Tooltip("무작위가 아닐 때 진행 방향 각도. 0 은 +x, 반시계"), Range(0f, 360f)] public float DirectionDegrees { get; private set; } = 0f;
		[field: SerializeField, Tooltip("반대편에서도 마주 오는 벽")] public bool BothSides { get; private set; } = true;
		[field: SerializeField, Tooltip("한 개체 직진 수명 초. 끝나면 드롭 없이 사라짐"), Min(0.1f)] public float Lifetime { get; private set; } = 12f;
		[field: SerializeField, Tooltip("절대 상한에 걸리면 남은 줄은 생략")] public SpawnClass SpawnClass { get; private set; } = SpawnClass.Event;

		public override WavePatternRun CreateRun() => new Run(this);

		public override void Validate() => RequireMonsters(Monsters, nameof(Monsters));

		private class Run : WavePatternRun
		{
			private readonly MovingWallPattern pattern;
			private bool started;
			private Vector3 center;
			private Vector3 forward;
			private int nextWave;
			private int gapStart;

			public Run(MovingWallPattern pattern) => this.pattern = pattern;

			public override bool Tick(IWavePatternHost host, float localSeconds, float deltaSeconds)
			{
				if (started == false)
				{
					// 벽 중심은 시작 순간 플레이어 위치에 고정. 그 자리를 쓸고 지나감
					started = true;
					center = host.PlayerPosition;
					float degrees = pattern.RandomDirection ? Random.Range(0f, 360f) : pattern.DirectionDegrees;
					forward = new Vector3(Mathf.Cos(degrees * Mathf.Deg2Rad), 0f, Mathf.Sin(degrees * Mathf.Deg2Rad));
					gapStart = WavePatternRules.InitialGapStart(pattern.LaneCount, pattern.GapCount);
				}

				while (nextWave < pattern.WaveCount && localSeconds >= nextWave * pattern.WaveInterval)
				{
					SpawnWave(host, forward, false);
					if (pattern.BothSides)
						SpawnWave(host, -forward, true);
					gapStart = WavePatternRules.StepGapStart(gapStart, pattern.LaneCount, pattern.GapCount, pattern.GapStepLanes, (min, max) => Random.Range(min, max + 1));
					nextWave++;
				}
				return nextWave >= pattern.WaveCount;
			}

			// 반대편 벽은 옆 방향 축이 뒤집혀서 줄 번호를 거꾸로 써야 빈 줄이 같은 자리
			private void SpawnWave(IWavePatternHost host, Vector3 direction, bool mirrored)
			{
				Vector3 side = new(-direction.z, 0f, direction.x);
				Vector3 origin = center - direction * pattern.StartDistance;
				int requested = pattern.LaneCount - pattern.GapCount;
				int allowed = host.AllowedCount(pattern.SpawnClass, requested);
				for (int lane = 0; lane < pattern.LaneCount && allowed > 0; lane++)
				{
					if (WavePatternRules.IsGapLane(lane, gapStart, pattern.GapCount))
						continue;
					int sideLane = mirrored ? pattern.LaneCount - 1 - lane : lane;
					Vector3 position = origin + side * WavePatternRules.LaneOffset(sideLane, pattern.LaneCount, pattern.LaneSpacing);
					Monster monster = pattern.Monsters[Random.Range(0, pattern.Monsters.Length)];
					host.SpawnAt(monster, position, new StraightMoveOrder(direction, pattern.Lifetime, true));
					allowed--;
				}
			}
		}
	}
}
