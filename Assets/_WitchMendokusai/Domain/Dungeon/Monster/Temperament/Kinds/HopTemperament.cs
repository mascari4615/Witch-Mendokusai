using UnityEngine;

namespace WitchMendokusai
{
	// 깡충. 멈춰 쉬다가, 웅크려 부풀고, 주인공 쪽으로 한 번에 튀어 나감. 쉼, 웅크림, 뜀이 돌아감
	// 시키는 행동: 멈춘 틈에 치고, 웅크림을 보면 옆으로
	[CreateAssetMenu(fileName = "MT_Hop", menuName = "WM/Monster/Temperament/Hop")]
	public sealed class HopTemperament : MonsterTemperament
	{
		private const int REST = 0;
		private const int HOP = 1;
		private const int CROUCH = 2;

		[SerializeField, Tooltip("뛴 뒤 멈춰 쉬는 초"), Min(0f)] private float restSeconds = 0.6f;
		[SerializeField, Tooltip("쉼 길이를 유닛마다 이 비율만큼 달리 함. 무리가 한꺼번에 안 뛰게"), Range(0f, 1f)] private float restJitter = 0.4f;
		[SerializeField, Tooltip("뛰기 직전 웅크려 부푼 채 기다리는 초. 피할 틈"), Min(0f)] private float crouchSeconds = 0.3f;
		[SerializeField, Tooltip("한 번 튀는 초"), Min(0.05f)] private float hopSeconds = 0.35f;
		[SerializeField, Tooltip("튀는 속도 (m/s)"), Min(0f)] private float hopSpeed = 9f;
		[SerializeField, Tooltip("웅크릴 때 몸 부풀림 배율. 1 이면 없음"), Min(1f)] private float crouchPunch = 1.25f;

		public override string KindName => "깡충";

		public static bool IsCrouching(TemperamentMemory memory) => memory.Phase == CROUCH;

		// 기억의 Phase 는 쉼, 웅크림, 뜀. NextTime 은 다음 단계 시각 (0 이면 막 태어남)
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			if (memory.Phase == REST && memory.NextTime <= 0f)
			{
				// 막 태어남. 첫 쉼도 유닛마다 달리 해 같은 물결이 한꺼번에 뛰지 않게
				memory.NextTime = context.Now + RestFor(memory);
				return TemperamentStep.Stay;
			}
			if (context.Now < memory.NextTime)
				return memory.Phase == HOP ? TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player)) : HoldStill();
			switch (memory.Phase)
			{
				case REST:
					memory.Phase = CROUCH;
					memory.NextTime = context.Now + crouchSeconds;
					TemperamentStep crouch = HoldStill();
					crouch.Punch = crouchPunch;
					return crouch;
				case CROUCH:
					memory.Phase = HOP;
					memory.NextTime = context.Now + hopSeconds;
					Vector3 direction = TemperamentContext.Toward(context.Self, context.Player);
					return TemperamentStep.Burst(direction * hopSpeed, hopSeconds);
				default:
					memory.Phase = REST;
					memory.NextTime = context.Now + RestFor(memory);
					return TemperamentStep.Stay;
			}
		}

		private float RestFor(TemperamentMemory memory) => restSeconds * (1f - restJitter * 0.5f + memory.Seed * restJitter);

		// 웅크린 동안은 공격 준비를 시작하지 않음 (뜀이 곧 공격)
		private static TemperamentStep HoldStill()
		{
			TemperamentStep still = TemperamentStep.Stay;
			still.HoldsAttack = true;
			return still;
		}
	}
}
