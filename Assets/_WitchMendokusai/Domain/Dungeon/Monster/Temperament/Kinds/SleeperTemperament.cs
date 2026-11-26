using UnityEngine;

namespace WitchMendokusai
{
	// 잠꾸러기. 제자리에서 잠. 가까이 오거나 맞으면 깜짝 놀라 잠깐 굳었다가 덮치고 쫓아옴. 오래 멀어지면 다시 잠
	// 시키는 행동: 몰래 지나가기, 깨우기 전에 먼저 치기
	[CreateAssetMenu(fileName = "MT_Sleeper", menuName = "WM/Monster/Temperament/Sleeper")]
	public sealed class SleeperTemperament : MonsterTemperament
	{
		private const int ASLEEP = 0;
		private const int STARTLED = 1;
		private const int AWAKE = 2;

		[SerializeField, Tooltip("이 거리 안에 들면 깸 (m)"), Min(0f)] private float wakeRadius = 3f;
		[SerializeField, Tooltip("맞은 뒤 이 초 안이면 깸"), Min(0f)] private float wakeOnHitSeconds = 0.3f;
		[SerializeField, Tooltip("깨서 굳어 있는 초 (놀람)"), Min(0f)] private float startleSeconds = 0.5f;
		[SerializeField, Tooltip("덮치는 속도 (m/s)"), Min(0f)] private float pounceSpeed = 11f;
		[SerializeField, Tooltip("덮치는 초"), Min(0f)] private float pounceSeconds = 0.3f;
		[SerializeField, Tooltip("이 거리 밖에 오래 있으면 다시 잠 (m)"), Min(0f)] private float sleepRadius = 10f;
		[SerializeField, Tooltip("다시 잠들기까지 멀리 있어야 하는 초"), Min(0f)] private float sleepSeconds = 4f;
		[SerializeField, Tooltip("깰 때 몸 부풀림 배율"), Min(1f)] private float startlePunch = 1.4f;

		public override string KindName => "잠꾸러기";

		public static bool IsAsleep(TemperamentMemory memory) => memory.Phase == ASLEEP;

		// 기억의 Phase 는 잠, 놀람, 깸. NextTime 은 놀람이 끝나는 시각, UntilTime 은 멀어지기 시작한 시각 (0 이면 가까움)
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			Vector3 toPlayer = TemperamentContext.Toward(context.Self, context.Player);
			switch (memory.Phase)
			{
				case ASLEEP:
					if (context.Distance > wakeRadius && context.SinceHit > wakeOnHitSeconds)
						return TemperamentStep.Idle;
					memory.Phase = STARTLED;
					memory.NextTime = context.Now + startleSeconds;
					return new TemperamentStep { Direction = Vector3.zero, Speed = 0f, Punch = startlePunch, FacesPlayer = true, HoldsAttack = true };
				case STARTLED:
					if (context.Now < memory.NextTime)
					{
						TemperamentStep frozen = TemperamentStep.Stay;
						frozen.HoldsAttack = true;
						return frozen;
					}
					memory.Phase = AWAKE;
					memory.UntilTime = 0f;
					return TemperamentStep.Burst(toPlayer * pounceSpeed, pounceSeconds);
				default:
					if (context.Distance < sleepRadius)
						memory.UntilTime = 0f;
					else if (memory.UntilTime <= 0f)
						memory.UntilTime = context.Now;
					else if (context.Now - memory.UntilTime >= sleepSeconds)
					{
						memory.Phase = ASLEEP;
						return TemperamentStep.Idle;
					}
					return TemperamentStep.Walk(toPlayer);
			}
		}
	}
}
