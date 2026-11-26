using UnityEngine;

namespace WitchMendokusai
{
	// 호위. 가장 가까운 다른 몬스터와 주인공 사이에 서서 지킴. 주인공이 그 몬스터에 다가오면 달려들어 막아섬
	// 지킬 몬스터가 없으면 곧장 쫓음. 감각: 다른 몬스터 (Ally)
	// 시키는 행동: 지키는 쪽을 돌아 뒤의 몬스터를 먼저 치기, 호위를 끌어내기
	[CreateAssetMenu(fileName = "MT_Guard", menuName = "WM/Monster/Temperament/Guard")]
	public sealed class GuardTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("지키는 몬스터에서 주인공 쪽으로 이만큼 나와 섬 (m)"), Min(0f)] private float guardDistance = 1.8f;
		[SerializeField, Tooltip("주인공이 지키는 몬스터에 이만큼 다가오면 막아섬 (m)"), Min(0f)] private float interceptRadius = 4f;
		[SerializeField, Tooltip("지킬 자리에서 이 거리 안이면 멈춤 (m)"), Min(0f)] private float arriveDistance = 0.4f;
		[SerializeField, Tooltip("막아서는 순간 몸 부풀림 배율"), Min(1f)] private float interceptPunch = 1.25f;

		public override string KindName => "호위";
		public override TemperamentSense Senses => TemperamentSense.Ally;

		// 기억의 IsOn 은 막아서는 중 (막아서는 순간 부풀림)
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			Vector3 toPlayer = TemperamentContext.Toward(context.Self, context.Player);
			if (context.HasAlly == false)
			{
				memory.IsOn = false;
				return TemperamentStep.Walk(toPlayer);
			}
			float playerToAlly = TemperamentContext.Flat(context.Ally - context.Player).magnitude;
			if (playerToAlly <= interceptRadius)
			{
				TemperamentStep intercept = TemperamentStep.Walk(toPlayer);
				intercept.Punch = memory.IsOn ? 0f : interceptPunch;
				memory.IsOn = true;
				return intercept;
			}
			memory.IsOn = false;
			Vector3 post = TemperamentContext.Flat(context.Ally) + TemperamentContext.Toward(context.Ally, context.Player) * guardDistance;
			Vector3 toPost = TemperamentContext.Flat(post - context.Self);
			if (toPost.magnitude <= arriveDistance)
				return TemperamentStep.Stay;
			TemperamentStep walk = TemperamentStep.Walk(toPost.normalized);
			walk.FacesPlayer = true;
			return walk;
		}
	}
}
