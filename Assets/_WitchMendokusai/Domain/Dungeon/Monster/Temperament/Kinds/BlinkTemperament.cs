using UnityEngine;

namespace WitchMendokusai
{
	// 점멸. 쫓다가 때가 되면 멈춰 몸을 부풀리고 (예고), 주인공 등 뒤로 순간이동한 뒤 잠깐 굳음
	// 떠난 자리와 닿은 자리에 먼지 고리 (TeleportPuff). 예고, 굳음 동안은 공격 안 함
	// 시키는 행동: 부풀면 몸을 돌려 등 뒤를 보기, 굳은 틈에 치기
	[CreateAssetMenu(fileName = "MT_Blink", menuName = "WM/Monster/Temperament/Blink")]
	public sealed class BlinkTemperament : MonsterTemperament
	{
		private const int CHASE = 0;
		private const int WINDUP = 1;
		private const int ARRIVED = 2;
		// 바라보는 쪽이 이보다 짧으면 주인공에서 나를 향하는 쪽을 앞으로 봄
		private const float NO_LOOK_SQUARED = 0.01f;

		[SerializeField, Tooltip("점멸 사이 쫓는 초 (첫 점멸은 유닛마다 이 값의 0.5~1.5배)"), Min(0.1f)] private float cooldownSeconds = 5f;
		[SerializeField, Tooltip("이 거리 안에서만 점멸 (m)"), Min(0f)] private float maxRange = 10f;
		[SerializeField, Tooltip("이 거리보다 가까우면 점멸 안 함 (m)"), Min(0f)] private float minRange = 3f;
		[SerializeField, Tooltip("사라지기 전 멈춰 부푸는 초 (예고)"), Min(0f)] private float windupSeconds = 0.6f;
		[SerializeField, Tooltip("예고 몸 부풀림 배율"), Min(1f)] private float windupPunch = 1.3f;
		[SerializeField, Tooltip("주인공 등 뒤 이만큼에 나타남 (m)"), Min(0f)] private float behindDistance = 2.5f;
		[SerializeField, Tooltip("나타난 뒤 굳어 있는 초 (빈틈)"), Min(0f)] private float arriveSeconds = 0.5f;
		[SerializeField, Tooltip("나타날 때 몸 부풀림 배율"), Min(1f)] private float arrivePunch = 1.2f;
		[SerializeField, Tooltip("떠난 자리와 닿은 자리의 먼지 고리 (등장 연출 자산)")] private MonsterSpawnStyle teleportPuff;

		public override string KindName => "점멸";
		public override MonsterSpawnStyle TeleportPuff => teleportPuff;

		public static bool IsWindingUp(TemperamentMemory memory) => memory.Phase == WINDUP;

		// 기억의 Phase 는 쫓음, 예고, 굳음. NextTime 은 다음 점멸 가능 시각 (쫓음) 이나 단계가 끝나는 시각. IsOn 은 첫 대기를 정했나
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			switch (memory.Phase)
			{
				case WINDUP:
					if (context.Now < memory.NextTime)
						return Frozen(0f);
					memory.Phase = ARRIVED;
					memory.NextTime = context.Now + arriveSeconds;
					TemperamentStep blink = Frozen(arrivePunch);
					blink.Teleport = true;
					blink.TeleportTo = BehindPlayer(context);
					return blink;
				case ARRIVED:
					if (context.Now < memory.NextTime)
						return Frozen(0f);
					memory.Phase = CHASE;
					memory.NextTime = context.Now + cooldownSeconds;
					break;
			}
			if (memory.IsOn == false)
			{
				memory.IsOn = true;
				memory.NextTime = context.Now + cooldownSeconds * (0.5f + memory.Seed);
			}
			float distance = context.Distance;
			if (context.Now >= memory.NextTime && distance <= maxRange && distance >= minRange)
			{
				memory.Phase = WINDUP;
				memory.NextTime = context.Now + windupSeconds;
				return Frozen(windupPunch);
			}
			return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player));
		}

		private static TemperamentStep Frozen(float punch)
		{
			TemperamentStep step = TemperamentStep.Stay;
			step.HoldsAttack = true;
			step.Punch = punch;
			return step;
		}

		private Vector3 BehindPlayer(in TemperamentContext context)
		{
			Vector3 look = TemperamentContext.Flat(context.PlayerLook);
			if (look.sqrMagnitude < NO_LOOK_SQUARED)
				look = TemperamentContext.Toward(context.Player, context.Self);
			if (look == Vector3.zero)
				look = Vector3.forward;
			return TemperamentContext.Flat(context.Player) - look.normalized * behindDistance;
		}
	}
}
