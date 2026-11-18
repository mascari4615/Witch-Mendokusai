using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum HordeFormation
	{
		// 플레이어 둘레 원
		Ring = 0,
		// 한 방향 한 덩어리
		Cluster = 1,
	}

	// 끼어드는 무리 사건. 시각 목록마다 무리 하나
	[CreateAssetMenu(fileName = nameof(HordeBurstPattern), menuName = "WM/Wave Pattern/" + nameof(HordeBurstPattern))]
	public class HordeBurstPattern : WavePattern
	{
		[field: SerializeField, Tooltip("패턴 시작부터 초")] public List<float> BurstTimes { get; private set; } = new() { 0f };
		[field: SerializeField] public Monster[] Monsters { get; private set; }
		[field: SerializeField, Tooltip("무리 하나 마릿수. 위협 단계 물량 배율 적용"), Min(1)] public int Count { get; private set; } = 18;
		[field: SerializeField] public HordeFormation Formation { get; private set; } = HordeFormation.Cluster;
		[field: SerializeField, Tooltip("플레이어에서 떨어진 거리"), Min(1f)] public float Distance { get; private set; } = 12f;
		[field: SerializeField, Tooltip("덩어리 대형의 흩어짐 반경"), Min(0f)] public float ClusterSpread { get; private set; } = 1.5f;
		[field: SerializeField, Tooltip("생성 순간 플레이어 방향으로 직진 고정. 끄면 평소처럼 추적")] public bool ChargeStraight { get; private set; } = true;
		[field: SerializeField, Tooltip("직진 수명 초. 끝나면 드롭 없이 사라짐"), Min(0.1f)] public float ChargeLifetime { get; private set; } = 10f;
		[field: SerializeField] public SpawnClass SpawnClass { get; private set; } = SpawnClass.Event;

		public override WavePatternRun CreateRun() => new Run(this);

		public override void Validate() => RequireMonsters(Monsters, nameof(Monsters));

		private class Run : WavePatternRun
		{
			private readonly HordeBurstPattern pattern;
			private readonly List<float> burstTimes;
			private int nextBurst;

			public Run(HordeBurstPattern pattern)
			{
				this.pattern = pattern;
				burstTimes = new List<float>(pattern.BurstTimes);
				burstTimes.Sort();
			}

			public override bool Tick(IWavePatternHost host, float localSeconds, float deltaSeconds)
			{
				while (nextBurst < burstTimes.Count && localSeconds >= burstTimes[nextBurst])
				{
					SpawnBurst(host);
					nextBurst++;
				}
				return nextBurst >= burstTimes.Count;
			}

			private void SpawnBurst(IWavePatternHost host)
			{
				int count = host.AllowedCount(pattern.SpawnClass, host.ScaleSwarmCount(pattern.Count));
				Vector3 center = host.PlayerPosition;
				float clusterAngle = Random.Range(0f, Mathf.PI * 2f);
				Vector3 clusterCenter = center + new Vector3(Mathf.Cos(clusterAngle), 0f, Mathf.Sin(clusterAngle)) * pattern.Distance;
				for (int i = 0; i < count; i++)
				{
					Vector3 position;
					if (pattern.Formation == HordeFormation.Ring)
					{
						float angle = Mathf.PI * 2f * i / count + clusterAngle;
						position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * pattern.Distance;
					}
					else
					{
						Vector2 spread = Random.insideUnitCircle * pattern.ClusterSpread;
						position = clusterCenter + new Vector3(spread.x, 0f, spread.y);
					}

					// 덩어리는 한 방향으로 나란히, 원은 각자 가운데로
					Vector3 chargeDirection = pattern.Formation == HordeFormation.Ring ? center - position : center - clusterCenter;
					WaveMoveOrder moveOrder = pattern.ChargeStraight ? new StraightMoveOrder(chargeDirection, pattern.ChargeLifetime, true) : null;
					Monster monster = pattern.Monsters[Random.Range(0, pattern.Monsters.Length)];
					host.SpawnAt(monster, position, moveOrder);
				}
			}
		}
	}
}
