using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 타원 포위. 플레이어 둘레 타원에 늘어선 뒤 반지름을 조임
	[CreateAssetMenu(fileName = nameof(EllipseSiegePattern), menuName = "WM/Wave Pattern/" + nameof(EllipseSiegePattern))]
	public class EllipseSiegePattern : WavePattern
	{
		[field: SerializeField] public Monster[] Monsters { get; private set; }

		public override IReadOnlyList<Monster> SpawnedMonsters => Monsters;
		[field: SerializeField, Tooltip("마릿수. 위협 단계 물량 배율 적용"), Min(1)] public int Count { get; private set; } = 30;
		[field: SerializeField, Tooltip("가로 반지름"), Min(0.5f)] public float RadiusX { get; private set; } = 7.5f;
		[field: SerializeField, Tooltip("세로 반지름"), Min(0.5f)] public float RadiusZ { get; private set; } = 5f;
		[field: SerializeField, Tooltip("초당 반지름 줄어드는 비율. 0 이면 유지"), Min(0f)] public float ShrinkPerSecond { get; private set; } = 0.03f;
		[field: SerializeField, Tooltip("반지름 최소 비율"), Range(0f, 1f)] public float MinScale { get; private set; } = 0.35f;
		[field: SerializeField, Tooltip("자리 도착으로 볼 거리"), Min(0f)] public float ArriveDistance { get; private set; } = 0.3f;
		[field: SerializeField, Tooltip("타원 중심이 플레이어를 따라감. 끄면 시작 위치 고정")] public bool FollowPlayer { get; private set; } = true;
		[field: SerializeField, Tooltip("포위 유지 초"), Min(0.1f)] public float Lifetime { get; private set; } = 30f;
		[field: SerializeField, Tooltip("끝에 드롭 없이 사라짐. 끄면 평소처럼 추적")] public bool DespawnOnEnd { get; private set; } = true;
		[field: SerializeField] public SpawnClass SpawnClass { get; private set; } = SpawnClass.Event;

		public override WavePatternRun CreateRun() => new Run(this);

		public override void Validate() => RequireMonsters(Monsters, nameof(Monsters));

		private class Run : WavePatternRun
		{
			private readonly EllipseSiegePattern pattern;

			public Run(EllipseSiegePattern pattern) => this.pattern = pattern;

			public override bool Tick(IWavePatternHost host, float localSeconds, float deltaSeconds)
			{
				int count = host.AllowedCount(pattern.SpawnClass, host.ScaleSwarmCount(pattern.Count));
				Vector3 center = host.PlayerPosition;
				float startAngle = Random.Range(0f, Mathf.PI * 2f);
				Transform follow = pattern.FollowPlayer ? host.PlayerTransform : null;
				for (int i = 0; i < count; i++)
				{
					WavePatternRules.EllipsePoint(i, count, pattern.RadiusX, pattern.RadiusZ, startAngle, out float x, out float z);
					Vector3 offset = new(x, 0f, z);
					EllipseSlotMoveOrder moveOrder = new(follow, center, offset, pattern.ShrinkPerSecond, pattern.MinScale, pattern.ArriveDistance, pattern.Lifetime, pattern.DespawnOnEnd);
					host.SpawnAt(pattern.Monsters[Random.Range(0, pattern.Monsters.Length)], center + offset, moveOrder);
				}
				return true;
			}
		}
	}
}
