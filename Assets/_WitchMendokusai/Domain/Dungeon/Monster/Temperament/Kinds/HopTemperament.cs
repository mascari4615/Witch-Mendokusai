using UnityEngine;

namespace WitchMendokusai
{
	// 깡충. 멈춰 웅크렸다가 주인공 쪽으로 한 번에 튀어 나감. 쉼과 뜀이 번갈아
	// 시키는 행동: 멈춘 틈에 때리고, 뜀 직전에 옆으로
	[CreateAssetMenu(fileName = "MT_Hop", menuName = "WM/Monster/Temperament/Hop")]
	public sealed class HopTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("뛰기 전 멈춰 있는 초"), Min(0f)] private float restSeconds = 0.9f;
		[SerializeField, Tooltip("쉼 길이를 유닛마다 이 비율만큼 달리 함. 무리가 한꺼번에 안 뛰게"), Range(0f, 1f)] private float restJitter = 0.4f;
		[SerializeField, Tooltip("한 번 튀는 초"), Min(0.05f)] private float hopSeconds = 0.35f;
		[SerializeField, Tooltip("튀는 속도 (m/s)"), Min(0f)] private float hopSpeed = 9f;
		[SerializeField, Tooltip("뛰기 직전 몸 부풀림 배율. 1 이면 없음"), Min(1f)] private float crouchPunch = 1.25f;

		public override string KindName => "깡충";

		// 기억의 Phase 는 쉼 (0) 과 뜀 (1), NextTime 은 다음 단계 시각
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			if (context.Now < memory.NextTime)
				return memory.Phase == 1 ? TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player)) : TemperamentStep.Stay;
			if (memory.Phase == 0)
			{
				memory.Phase = 1;
				memory.NextTime = context.Now + hopSeconds;
				Vector3 direction = TemperamentContext.Toward(context.Self, context.Player);
				return TemperamentStep.Burst(direction * hopSpeed, hopSeconds, crouchPunch);
			}
			memory.Phase = 0;
			memory.NextTime = context.Now + restSeconds * (1f - restJitter * 0.5f + memory.Seed * restJitter);
			return TemperamentStep.Stay;
		}
	}
}
