using UnityEngine;

namespace WitchMendokusai
{
	// 탐냄. 근처 바닥 전리품 (경험치, 아이템) 쪽으로 가서 그 위에 앉아 정신없이 머묾 (공격 안 함)
	// 주인공이 바짝 다가오거나 맞으면 화나 한동안 쫓음. 전리품이 없으면 쫓음. 줍지는 않음 (훔치기는 게임 규칙이라 사용자 결정)
	// 감각: 전리품 (Loot). 시키는 행동: 전리품으로 꾀어 모으기, 앉은 틈에 치기
	[CreateAssetMenu(fileName = "MT_Greedy", menuName = "WM/Monster/Temperament/Greedy")]
	public sealed class GreedyTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("전리품에서 이 거리 안이면 앉음 (m)"), Min(0f)] private float sitDistance = 0.5f;
		[SerializeField, Tooltip("앉아 있어도 주인공이 이만큼 다가오면 화남 (m)"), Min(0f)] private float noticeRadius = 1.8f;
		[SerializeField, Tooltip("화나서 쫓는 초"), Min(0f)] private float angerSeconds = 4f;
		[SerializeField, Tooltip("맞은 뒤 이 초 안이면 화남"), Min(0f)] private float angerOnHitSeconds = 0.3f;
		[SerializeField, Tooltip("화날 때 몸 부풀림 배율"), Min(1f)] private float angerPunch = 1.3f;

		public override string KindName => "탐냄";
		public override TemperamentSense Senses => TemperamentSense.Loot;

		public static bool IsAngry(TemperamentMemory memory, float now) => memory.IsOn && now < memory.UntilTime;

		// 기억의 IsOn 과 UntilTime 은 화남과 풀리는 시각
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			Vector3 toPlayer = TemperamentContext.Toward(context.Self, context.Player);
			bool provoked = context.SinceHit <= angerOnHitSeconds || (context.HasLoot && context.Distance <= noticeRadius);
			if (provoked && IsAngry(memory, context.Now) == false)
			{
				memory.IsOn = true;
				memory.UntilTime = context.Now + angerSeconds;
				TemperamentStep angry = TemperamentStep.Walk(toPlayer);
				angry.Punch = angerPunch;
				return angry;
			}
			if (IsAngry(memory, context.Now) || context.HasLoot == false)
				return TemperamentStep.Walk(toPlayer);
			memory.IsOn = false;
			Vector3 toLoot = TemperamentContext.Flat(context.Loot - context.Self);
			if (toLoot.magnitude <= sitDistance)
				return TemperamentStep.Idle;
			return TemperamentStep.Walk(toLoot.normalized);
		}
	}
}
