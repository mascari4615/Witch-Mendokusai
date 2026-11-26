using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum ArenaShiftMode
	{
		// 둘레를 CellCount 칸으로 나눠 LockedCells 칸을 잠금 (무대 파괴, 영혼의 벽)
		Cells = 0,
		// 바깥 고리부터 안으로 Steps 겹이 차례로 잠김 (조여 오는 벽)
		Closing = 1,
	}

	public enum ArenaShiftCenter
	{
		// 실행 순간 시전자 자리
		Caster = 0,
		// 던전 경기장 중심. 경기장이 없으면 시전자 자리
		Arena = 1,
	}

	// 무대 변화. 예고 뒤 무대의 칸 (중심이 같은 고리 부채) 을 잠금. 잠긴 칸 안의 주인공은 느려지고 간격마다 맞음. 낙사가 없어 막지는 않음
	// 잠긴 칸은 ArenaShiftField 가 들고 감 (공격 자산은 상태 없음). 몬스터 생성 자리는 잠긴 칸을 피함 (MonsterSpawner.ClampToArena). 정본 memo wm/design/boss-arena-20261126
	[CreateAssetMenu(fileName = "MA_ArenaShift", menuName = "WM/Variable/MonsterAttack/" + nameof(ArenaShiftMonsterAttack))]
	public class ArenaShiftMonsterAttack : WindupMonsterAttack
	{
		public override string KindName => "무대 변화";

		[field: Header("무대")]
		[field: SerializeField] public ArenaShiftMode Mode { get; private set; } = ArenaShiftMode.Cells;
		[field: SerializeField] public ArenaShiftCenter CenterAt { get; private set; } = ArenaShiftCenter.Caster;
		[field: SerializeField, Tooltip("칸의 안 반지름 (m). 이 안은 늘 안전"), Min(0f)] public float InnerRadius { get; private set; } = 2.5f;
		[field: SerializeField, Tooltip("칸의 바깥 반지름 (m)"), Min(1f)] public float OuterRadius { get; private set; } = 9f;
		[field: SerializeField, Tooltip("칸 꼴: 둘레를 나누는 수"), Min(2)] public int CellCount { get; private set; } = 6;
		[field: SerializeField, Tooltip("칸 꼴: 잠그는 칸 수. 고르게 띄워 고름"), Min(1)] public int LockedCells { get; private set; } = 3;
		[field: SerializeField, Tooltip("조여 옴 꼴: 겹 수"), Min(1)] public int Steps { get; private set; } = 3;
		[field: SerializeField, Tooltip("차례마다 늦어지는 초 (칸 꼴은 칸마다, 조여 옴은 겹마다)"), Min(0f)] public float StepSeconds { get; private set; } = 1.5f;
		[field: SerializeField, Tooltip("잠긴 채 남는 초. 0 이면 시전자가 살아 있는 동안"), Min(0f)] public float LockSeconds { get; private set; } = 8f;

		[field: Header("잠긴 칸 안")]
		[field: SerializeField, Tooltip("이동 속도를 줄이는 비율"), Range(0f, 0.9f)] public float SlowRatio { get; private set; } = 0.45f;
		[field: SerializeField, Tooltip("둔화 한 번의 초. 간격보다 길어야 칸 안에서 안 끊김"), Min(0.1f)] public float SlowSeconds { get; private set; } = 1.2f;
		[field: SerializeField, Tooltip("피해 간격 (초)"), Min(0.2f)] public float TickInterval { get; private set; } = 0.8f;
		[field: SerializeField, Tooltip("간격마다 기본 피해. 0 이면 느려지기만"), Min(0)] public int TickDamage { get; private set; } = 1;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 거는 것 (맞은 그림)")] public MonsterHitEffect HitEffect { get; private set; } = new();
		[field: SerializeField, Tooltip("잠긴 동안 바닥에 깔리는 부품. 고리 부채 (InnerRadius, ArcDegrees) 를 그림")] public CombatFx LockFx { get; private set; }
		[field: SerializeField, Tooltip("끝없는 칸 (LockSeconds 0) 의 그림 초. 시전자가 이보다 오래 살면 그림만 먼저 사라짐"), Min(1f)] public float EndlessFxSeconds { get; private set; } = 600f;

		protected override bool TargetsArePositions => false;

		// 잠기는 칸 수
		public int LockCount => Mode == ArenaShiftMode.Cells ? Mathf.Min(LockedCells, CellCount - 1) : Steps;

		// 칸마다 방향 (가운데). 조여 옴은 온 둘레라 앞만
		public override void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results)
		{
			if (Mode == ArenaShiftMode.Closing)
			{
				for (int i = 0; i < Steps; i++)
					results.Add(Vector3.forward);
				return;
			}
			CellDirections(CellCount, LockCount, Random.value * 360f, results);
		}

		// 고르게 띄운 칸 count 개의 가운데 방향. rotation 은 첫 칸의 각 (도, +z 에서 시계 방향)
		public static void CellDirections(int cellCount, int count, float rotation, List<Vector3> results)
		{
			for (int i = 0; i < count; i++)
			{
				int cell = i * cellCount / count;
				float angle = (rotation + (cell + 0.5f) * 360f / cellCount) * Mathf.Deg2Rad;
				results.Add(new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)));
			}
		}

		public override float StrikeDelay(int index) => index * StepSeconds;

		public float ArcDegrees => Mode == ArenaShiftMode.Cells ? 360f / CellCount : 360f;

		// index 겹의 안팎 반지름. 조여 옴은 바깥 겹이 0
		public void RingOf(int index, out float inner, out float outer)
		{
			if (Mode == ArenaShiftMode.Cells)
			{
				inner = InnerRadius;
				outer = OuterRadius;
				return;
			}
			float width = (OuterRadius - InnerRadius) / Steps;
			outer = OuterRadius - width * index;
			inner = outer - width;
		}

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target, int index)
		{
			RingOf(index, out float inner, out float outer);
			return new CombatFxContext
			{
				Position = CenterOf(unitObject),
				Follow = FollowsCaster ? unitObject.transform : null,
				Direction = target,
				Radius = outer,
				Length = outer,
				InnerRadius = inner,
				ArcDegrees = ArcDegrees,
				Seconds = WindupSeconds,
			};
		}

		public override bool Execute(UnitObject unitObject, PlayerObject player, Vector3 target, int index)
		{
			if (unitObject.IsAlive == false)
				return false;
			RingOf(index, out float inner, out float outer);
			// 첫 칸이 새 시전. 같은 시전자의 앞 시전 칸은 풂 (끝없는 칸이 쌓여 둘레가 다 잠기지 않게)
			if (index == 0)
				ArenaShiftField.Release(unitObject, this);
			ArenaShiftField.Instance.Add(unitObject, this, new ArenaCell(CenterOf(unitObject), target, ArcDegrees, inner, outer));
			return false;
		}

		private bool FollowsCaster => CenterAt == ArenaShiftCenter.Caster || HasArena == false;

		private static bool HasArena => DungeonManagerBridge.IsDungeon && DungeonManager.Instance.MonsterSpawner.HasArena;

		private Vector3 CenterOf(UnitObject unitObject)
		{
			Vector3 center = FollowsCaster ? unitObject.transform.position : DungeonManager.Instance.MonsterSpawner.ArenaCenter;
			center.y = unitObject.transform.position.y;
			return center;
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 무대 변화는 잠길 칸을 예고해야 함 ({nameof(WindupSeconds)} 0)");
			if (InnerRadius >= OuterRadius)
				errors.Add($"{name}: 안 반지름 {InnerRadius} 이 바깥 {OuterRadius} 이상");
			if (Mode == ArenaShiftMode.Cells && LockedCells >= CellCount)
				errors.Add($"{name}: 칸을 다 잠그면 설 곳이 없음 ({LockedCells}/{CellCount})");
			if (Mode == ArenaShiftMode.Closing && InnerRadius <= 0f)
				errors.Add($"{name}: 조여 오는 벽은 가운데가 남아야 함 ({nameof(InnerRadius)} 0)");
			if (LockFx == null)
				errors.Add($"{name}: 잠긴 칸을 그릴 {nameof(LockFx)} 이 비어 있음");
			if (SlowRatio > 0f && SlowSeconds <= TickInterval)
				errors.Add($"{name}: 둔화 {SlowSeconds}초가 간격 {TickInterval}초보다 짧아 칸 안에서 끊김");
			if (TickDamage <= 0 && SlowRatio <= 0f)
				errors.Add($"{name}: 잠긴 칸이 아무 일도 안 함 (피해 0, 둔화 0)");
			if (Linger.Seconds > 0f)
				errors.Add($"{name}: 무대 변화는 남는 물체를 쓰지 않음 (잠긴 칸이 남음)");
			HitEffect.Validate(name, errors);
		}
	}

	// 잠긴 칸 하나. 중심이 같은 고리 부채. 높이는 무시
	public readonly struct ArenaCell
	{
		public readonly Vector3 Center;
		public readonly Vector3 Direction;
		public readonly float ArcDegrees;
		public readonly float Inner;
		public readonly float Outer;

		public ArenaCell(Vector3 center, Vector3 direction, float arcDegrees, float inner, float outer)
		{
			Center = center;
			direction.y = 0f;
			Direction = direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
			ArcDegrees = arcDegrees;
			Inner = inner;
			Outer = outer;
		}

		// 점이 칸 안인가. margin 만큼 칸을 넓혀 봄 (생성 자리는 몸 반지름)
		public bool Contains(Vector3 point, float margin)
		{
			Vector3 offset = point - Center;
			offset.y = 0f;
			float distance = offset.magnitude;
			if (distance > Outer + margin || distance < Inner - margin)
				return false;
			if (ArcDegrees >= 360f || distance < 0.0001f)
				return true;
			float beyond = Vector3.Angle(Direction, offset) - ArcDegrees * 0.5f;
			return beyond <= 0f || (beyond < 90f && distance * Mathf.Sin(beyond * Mathf.Deg2Rad) <= margin);
		}

		// 칸 바깥 테두리 밖으로. 안쪽 빈 곳은 주인공이 숨는 자리라 생성 자리를 거기로 몰지 않음. 칸 밖이면 그대로
		public Vector3 PushOut(Vector3 point, float margin)
		{
			if (Contains(point, margin) == false)
				return point;
			Vector3 offset = point - Center;
			offset.y = 0f;
			Vector3 direction = offset.sqrMagnitude < 0.0001f ? Direction : offset.normalized;
			Vector3 pushed = Center + direction * (Outer + margin * 2f);
			pushed.y = point.y;
			return pushed;
		}
	}
}
