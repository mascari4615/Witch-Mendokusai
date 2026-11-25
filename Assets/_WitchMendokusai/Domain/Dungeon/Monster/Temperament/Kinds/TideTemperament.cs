using UnityEngine;

namespace WitchMendokusai
{
	// 밀물 썰물. 모두 같은 시계로 몇 초 쫓고 몇 초 흩어짐. 흩어질 때는 썰물이 시작된 순간 정한 자기 구석 (주인공 반대쪽 멀리) 으로
	// 같은 성격끼리 한꺼번에 방향을 바꿔 화면에 큰 박자가 생김. 시키는 행동: 썰물 때 숨 돌리고 줍기, 밀물 전에 자리 잡기
	[CreateAssetMenu(fileName = "MT_Tide", menuName = "WM/Monster/Temperament/Tide")]
	public sealed class TideTemperament : MonsterTemperament
	{
		private const int UNKNOWN = 0;
		private const int CHASE = 1;
		private const int SCATTER = 2;

		[SerializeField, Tooltip("쫓는 초"), Min(0.1f)] private float chaseSeconds = 7f;
		[SerializeField, Tooltip("흩어지는 초"), Min(0.1f)] private float scatterSeconds = 4f;
		[SerializeField, Tooltip("흩어질 때 그때 주인공 자리에서 이만큼 떨어진 구석으로 (m)"), Min(0f)] private float scatterDistance = 9f;
		[SerializeField, Tooltip("흩어지는 쪽을 유닛마다 이 각 안에서 달리 함 (도, 한쪽)"), Range(0f, 90f)] private float scatterSpreadDegrees = 60f;
		[SerializeField, Tooltip("구석에서 이 거리 안이면 멈춰 기다림 (m)"), Min(0f)] private float arriveDistance = 0.5f;
		[SerializeField, Tooltip("물결이 바뀔 때 몸 부풀림 배율"), Min(1f)] private float turnPunch = 1.15f;

		public override string KindName => "밀물 썰물";

		// 게임 시간만으로 정하므로 같은 성격은 모두 같은 때 바뀜
		public bool IsChasing(float now) => Mathf.Repeat(now, chaseSeconds + scatterSeconds) < chaseSeconds;

		// 기억의 Phase 는 쫓음과 흩어짐 (처음은 모름), Anchor 는 이번 썰물의 구석. 바뀌는 순간 몸 부풀림
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			int phase = IsChasing(context.Now) ? CHASE : SCATTER;
			bool isTurning = memory.Phase != phase;
			float punch = isTurning && memory.Phase != UNKNOWN ? turnPunch : 0f;
			memory.Phase = phase;
			if (phase == CHASE)
			{
				TemperamentStep chase = TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player));
				chase.Punch = punch;
				return chase;
			}
			if (isTurning)
				memory.Anchor = CornerFor(context, memory.Seed);
			Vector3 toCorner = TemperamentContext.Flat(memory.Anchor - context.Self);
			TemperamentStep scatter = toCorner.magnitude < arriveDistance ? TemperamentStep.Stay : TemperamentStep.Walk(toCorner.normalized);
			scatter.Punch = punch;
			return scatter;
		}

		// 주인공에서 나를 향하는 쪽을 유닛마다 조금 돌린 곳. 주인공을 가로지르지 않음
		private Vector3 CornerFor(in TemperamentContext context, float seed)
		{
			Vector3 away = TemperamentContext.Toward(context.Player, context.Self);
			if (away == Vector3.zero)
				away = Vector3.forward;
			float spread = (seed * 2f - 1f) * scatterSpreadDegrees;
			return context.Player + Quaternion.AngleAxis(spread, Vector3.up) * away * scatterDistance;
		}
	}
}
