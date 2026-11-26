using UnityEngine;

namespace WitchMendokusai
{
	// 낮잠과 순찰. 태어난 자리를 가운데 두고 두 점을 천천히 오감. 끝에 닿으면 낮잠 (그동안 못 봄, 공격 안 함)
	// 걷는 중에 주인공을 보면 놀라 쫓고, 멀어지면 가까운 끝으로 돌아가 다시 순찰. 맞으면 자다가도 쫓음
	// 시키는 행동: 길을 읽고 낮잠 틈에 지나가기
	[CreateAssetMenu(fileName = "MT_Patrol", menuName = "WM/Monster/Temperament/Patrol")]
	public sealed class PatrolTemperament : MonsterTemperament
	{
		private const int WALK = 0;
		private const int NAP = 1;
		private const int CHASE = 2;

		[SerializeField, Tooltip("가운데에서 끝까지 거리 (m). 두 끝 사이는 이 두 배"), Min(0.5f)] private float halfLength = 4f;
		[SerializeField, Tooltip("순찰 걷는 세기 (1 이 제 속도)"), Range(0f, 1f)] private float patrolSpeed = 0.5f;
		[SerializeField, Tooltip("끝에서 이 거리 안이면 닿음 (m)"), Min(0f)] private float arriveDistance = 0.4f;
		[SerializeField, Tooltip("끝에서 자는 초"), Min(0f)] private float napSeconds = 2f;
		[SerializeField, Tooltip("걷는 중 이 거리 안이면 알아챔 (m)"), Min(0f)] private float noticeRadius = 4f;
		[SerializeField, Tooltip("쫓다가 이보다 멀어지면 순찰로 돌아감 (m)"), Min(0f)] private float loseRadius = 9f;
		[SerializeField, Tooltip("맞은 뒤 이 초 안이면 쫓음"), Min(0f)] private float chaseOnHitSeconds = 0.3f;
		[SerializeField, Tooltip("알아챌 때 몸 부풀림 배율"), Min(1f)] private float noticePunch = 1.3f;

		public override string KindName => "낮잠 순찰";

		public static bool IsNapping(TemperamentMemory memory) => memory.Phase == NAP;

		private void OnValidate() => loseRadius = Mathf.Max(loseRadius, noticeRadius + 0.5f);

		/// <summary>순찰 길의 한 끝. side 는 +1, -1. 길의 방향은 유닛의 Seed 로 정함 (무리가 같은 쪽으로 안 걷게)</summary>
		public Vector3 End(TemperamentMemory memory, float side)
		{
			float radians = memory.Seed * Mathf.PI;
			Vector3 axis = new(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
			return memory.Anchor + axis * (halfLength * side);
		}

		// 기억의 Phase 는 걸음, 낮잠, 쫓음. Side 는 지금 향하는 끝, NextTime 은 낮잠이 끝나는 시각
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			Vector3 toPlayer = TemperamentContext.Toward(context.Self, context.Player);
			bool isHit = context.SinceHit <= chaseOnHitSeconds;
			switch (memory.Phase)
			{
				case CHASE:
					if (context.Distance <= loseRadius || isHit)
						return TemperamentStep.Walk(toPlayer);
					memory.Phase = WALK;
					memory.Side = NearerSide(context, memory);
					break;
				case NAP:
					if (isHit)
						return Notice(memory, toPlayer);
					if (context.Now < memory.NextTime)
						return TemperamentStep.Idle;
					memory.Phase = WALK;
					memory.Side = -memory.Side;
					break;
			}
			if (isHit || context.Distance <= noticeRadius)
				return Notice(memory, toPlayer);
			Vector3 toEnd = TemperamentContext.Flat(End(memory, memory.Side) - context.Self);
			if (toEnd.magnitude <= arriveDistance)
			{
				memory.Phase = NAP;
				memory.NextTime = context.Now + napSeconds;
				return TemperamentStep.Idle;
			}
			return TemperamentStep.Walk(toEnd.normalized, patrolSpeed);
		}

		private TemperamentStep Notice(TemperamentMemory memory, Vector3 toPlayer)
		{
			memory.Phase = CHASE;
			TemperamentStep chase = TemperamentStep.Walk(toPlayer);
			chase.Punch = noticePunch;
			return chase;
		}

		private float NearerSide(in TemperamentContext context, TemperamentMemory memory)
		{
			float plus = TemperamentContext.Flat(End(memory, 1f) - context.Self).sqrMagnitude;
			float minus = TemperamentContext.Flat(End(memory, -1f) - context.Self).sqrMagnitude;
			return plus <= minus ? 1f : -1f;
		}
	}
}
