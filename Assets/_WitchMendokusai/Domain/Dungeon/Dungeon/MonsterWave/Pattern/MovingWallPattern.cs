using UnityEngine;

namespace WitchMendokusai
{
	public enum WallDirectionMode
	{
		Random = 0,
		// DirectionDegrees 고정
		Fixed = 1,
		// 주인공 이동 방향에 수직 (가로지름). 멈춰 있으면 무작위
		AcrossPlayerMove = 2,
	}

	// 움직이는 벽. 여러 줄이 한 방향으로 직진, 빈 줄 묶음이 파마다 옆으로 움직여 빠져나갈 틈
	// 한쪽만, 빈 줄 0, 가로지름, 예고를 켜면 횡단 띠 (th8, 정본 memo wm/design/crossing-threat-20261119)
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
		[field: SerializeField, Tooltip("진행 방향. 무작위, 고정 각도, 주인공 이동에 수직 (가로지름)")] public WallDirectionMode DirectionMode { get; private set; } = WallDirectionMode.Random;
		[field: SerializeField, Tooltip("Fixed 일 때 진행 방향 각도. 0 은 +x, 반시계"), Range(0f, 360f)] public float DirectionDegrees { get; private set; } = 0f;
		[field: SerializeField, Tooltip("벽 중심을 주인공 이동 예측 자리로 (초 뒤). 0 이면 시작 자리"), Min(0f)] public float LeadSeconds { get; private set; } = 0f;
		[field: SerializeField, Tooltip("반대편에서도 마주 오는 벽")] public bool BothSides { get; private set; } = true;
		[field: SerializeField, Tooltip("한 개체 직진 수명 초. 끝나면 드롭 없이 사라짐"), Min(0.1f)] public float Lifetime { get; private set; } = 12f;
		[field: SerializeField, Tooltip("절대 상한에 걸리면 남은 줄은 생략")] public SpawnClass SpawnClass { get; private set; } = SpawnClass.Event;

		[field: Header("예고")]
		[field: SerializeField, Tooltip("첫 파 전에 지나갈 길을 바닥에 띄우는 초. 0 이면 예고 없음, 바로 출발"), Min(0f)] public float WarnSeconds { get; private set; } = 0f;
		[field: SerializeField, Tooltip("지나갈 길 바닥 예고 (띠 예고 부품)")] public CombatFx WarnTelegraph { get; private set; }

		public override WavePatternRun CreateRun() => new Run(this);

		public override void Validate()
		{
			RequireMonsters(Monsters, nameof(Monsters));
			if (WarnSeconds > 0f && WarnTelegraph == null)
				throw new System.InvalidOperationException($"{name}: {nameof(WarnSeconds)} {WarnSeconds} 인데 {nameof(WarnTelegraph)} 비어 있음");
		}

		// 예고 띠 길이. 출발선에서 반대편 출발선까지. 개체는 Lifetime 동안 직진하니 자산에서 속도 x 수명을 이 길이에 맞춤
		public float TravelLength => StartDistance * 2f;

		// 예고 띠 폭. 양끝 줄 중심 사이 + 줄 간격 한 칸 (몸 폭 여유)
		public float WallWidth => LaneCount * LaneSpacing;

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
					// 벽 중심은 시작 순간 주인공 위치 (예측이 있으면 그만큼 앞) 에 고정. 그 자리를 쓸고 지나감
					started = true;
					Vector3 velocity = host.PlayerMoveVelocity;
					center = host.PlayerPosition + velocity * pattern.LeadSeconds;
					forward = PickForward(velocity);
					gapStart = WavePatternRules.InitialGapStart(pattern.LaneCount, pattern.GapCount);
					if (pattern.WarnSeconds > 0f)
					{
						Warn(forward);
						if (pattern.BothSides)
							Warn(-forward);
					}
				}

				while (nextWave < pattern.WaveCount && localSeconds >= pattern.WarnSeconds + nextWave * pattern.WaveInterval)
				{
					SpawnWave(host, forward, false);
					if (pattern.BothSides)
						SpawnWave(host, -forward, true);
					gapStart = WavePatternRules.StepGapStart(gapStart, pattern.LaneCount, pattern.GapCount, pattern.GapStepLanes, (min, max) => Random.Range(min, max + 1));
					nextWave++;
				}
				return nextWave >= pattern.WaveCount;
			}

			private Vector3 PickForward(Vector3 playerVelocity)
			{
				float degrees;
				switch (pattern.DirectionMode)
				{
					case WallDirectionMode.Fixed:
						degrees = pattern.DirectionDegrees;
						break;
					case WallDirectionMode.AcrossPlayerMove when playerVelocity.sqrMagnitude > SurvivalThreatRules.STILL_SQUARED:
						// 이동 방향의 왼쪽이나 오른쪽에서 옴
						Vector3 move = playerVelocity.normalized;
						float side = Random.value < 0.5f ? 1f : -1f;
						return new Vector3(-move.z, 0f, move.x) * side;
					default:
						degrees = Random.Range(0f, 360f);
						break;
				}
				return new Vector3(Mathf.Cos(degrees * Mathf.Deg2Rad), 0f, Mathf.Sin(degrees * Mathf.Deg2Rad));
			}

			// 출발선 가운데에서 진행 방향으로 지나갈 길 전체
			private void Warn(Vector3 direction)
			{
				pattern.WarnTelegraph.Play(new CombatFxContext
				{
					Position = center - direction * pattern.StartDistance,
					Direction = direction,
					Length = pattern.TravelLength,
					Width = pattern.WallWidth,
					Seconds = pattern.WarnSeconds,
				});
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
