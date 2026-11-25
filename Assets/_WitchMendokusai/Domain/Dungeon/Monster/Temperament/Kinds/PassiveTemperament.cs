using UnityEngine;

namespace WitchMendokusai
{
	// 순둥이. 태어난 자리 둘레를 천천히 거닐며 주인공을 모른 척. 맞으면 화가 나 한동안 쫓아옴, 풀리면 다시 거닒
	// 시키는 행동: 건드릴지 말지 고르기
	[CreateAssetMenu(fileName = "MT_Passive", menuName = "WM/Monster/Temperament/Passive")]
	public sealed class PassiveTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("거니는 반지름 (m)"), Min(0f)] private float wanderRadius = 3f;
		[SerializeField, Tooltip("거니는 세기 0~1"), Range(0f, 1f)] private float wanderSpeed = 0.35f;
		[SerializeField, Tooltip("거닐 목표를 바꾸는 간격 (초)"), Min(0.1f)] private float wanderSeconds = 2.5f;
		[SerializeField, Tooltip("거닐 목표를 차례마다 이 각만큼 돌림 (도). 137.5 (황금각) 면 무작위 없이도 고르게 퍼짐"), Range(0f, 360f)] private float wanderTurnDegrees = 137.5f;
		[SerializeField, Tooltip("목표에서 이 거리 안이면 멈춰 섬 (m)"), Min(0f)] private float arriveDistance = 0.3f;
		[SerializeField, Tooltip("맞은 뒤 화가 나 있는 초. 또 맞으면 그때부터 다시 셈"), Min(0f)] private float angrySeconds = 6f;
		[SerializeField, Tooltip("화날 때 몸 부풀림 배율"), Min(1f)] private float angerPunch = 1.3f;

		public override string KindName => "순둥이";

		// 기억의 IsOn 은 화남, NextTime 은 거닐 목표를 바꿀 시각, Phase 는 거닐 목표의 차례
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			bool isAngry = context.SinceHit < angrySeconds;
			if (isAngry && memory.IsOn == false)
			{
				memory.IsOn = true;
				return new TemperamentStep { Direction = Vector3.zero, Speed = 0f, Punch = angerPunch, FacesPlayer = true, HoldsAttack = true };
			}
			memory.IsOn = isAngry;
			if (isAngry)
				return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player));
			if (context.Now >= memory.NextTime)
			{
				memory.Phase++;
				memory.NextTime = context.Now + wanderSeconds;
			}
			float angle = (memory.Phase * wanderTurnDegrees + memory.Seed * 360f) * Mathf.Deg2Rad;
			Vector3 goal = memory.Anchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * wanderRadius;
			Vector3 toGoal = TemperamentContext.Flat(goal - context.Self);
			if (toGoal.magnitude < arriveDistance)
				return TemperamentStep.Idle;
			TemperamentStep wander = TemperamentStep.Walk(toGoal.normalized, wanderSpeed);
			wander.HoldsAttack = true;
			return wander;
		}
	}
}
