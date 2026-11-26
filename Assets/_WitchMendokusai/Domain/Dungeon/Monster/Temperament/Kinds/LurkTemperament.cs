using UnityEngine;

namespace WitchMendokusai
{
	// 숨바꼭질. 가장 가까운 다른 몬스터 뒤 (주인공 반대쪽) 에 숨어 공격 안 함. 주인공이 가까이 오면 튀어나와 덮치고 쫓다가, 멀어지면 다시 숨음
	// 숨을 몬스터가 없으면 들켜서 쫓음. 장애물 대신 몬스터를 가림막으로 씀 (지형 질의 비용 없음). 감각: 다른 몬스터 (Ally)
	// 시키는 행동: 무리 뒤쪽을 살피기, 가림막 몬스터를 먼저 치워 끌어내기
	[CreateAssetMenu(fileName = "MT_Lurk", menuName = "WM/Monster/Temperament/Lurk")]
	public sealed class LurkTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("가림막 몬스터 뒤 이만큼에 숨음 (m)"), Min(0f)] private float hideDistance = 1.5f;
		[SerializeField, Tooltip("주인공이 이만큼 다가오면 튀어나옴 (m)"), Min(0f)] private float popRadius = 3f;
		[SerializeField, Tooltip("튀어나온 뒤 주인공이 이보다 멀어지면 다시 숨음 (m)"), Min(0f)] private float calmRadius = 7f;
		[SerializeField, Tooltip("튀어나오는 속도 (m/s)"), Min(0f)] private float popSpeed = 9f;
		[SerializeField, Tooltip("튀어나오는 초"), Min(0f)] private float popSeconds = 0.25f;
		[SerializeField, Tooltip("튀어나올 때 몸 부풀림 배율"), Min(1f)] private float popPunch = 1.3f;
		[SerializeField, Tooltip("숨는 자리에서 이 거리 안이면 멈춤 (m)"), Min(0f)] private float arriveDistance = 0.4f;

		public override string KindName => "숨바꼭질";
		public override TemperamentSense Senses => TemperamentSense.Ally;

		public static bool IsOut(TemperamentMemory memory) => memory.IsOn;

		private void OnValidate() => calmRadius = Mathf.Max(calmRadius, popRadius + 0.5f);

		// 기억의 IsOn 은 튀어나온 상태
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			Vector3 toPlayer = TemperamentContext.Toward(context.Self, context.Player);
			if (memory.IsOn)
			{
				if (context.Distance <= calmRadius || context.HasAlly == false)
					return TemperamentStep.Walk(toPlayer);
				memory.IsOn = false;
			}
			if (context.HasAlly == false)
				return TemperamentStep.Walk(toPlayer);
			if (context.Distance <= popRadius)
			{
				memory.IsOn = true;
				return TemperamentStep.Burst(toPlayer * popSpeed, popSeconds, popPunch);
			}
			Vector3 hide = TemperamentContext.Flat(context.Ally) + TemperamentContext.Toward(context.Player, context.Ally) * hideDistance;
			Vector3 toHide = TemperamentContext.Flat(hide - context.Self);
			TemperamentStep step = toHide.magnitude <= arriveDistance ? TemperamentStep.Stay : TemperamentStep.Walk(toHide.normalized);
			step.HoldsAttack = true;
			return step;
		}
	}
}
