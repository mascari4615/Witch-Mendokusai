using UnityEngine;

namespace WitchMendokusai
{
	// 지그재그. 주인공 쪽으로 가되 좌우로 물결치며 감. 겨누기 어렵게
	// 시키는 행동: 넓게 맞히는 공격을 고르기, 다가올 때까지 기다리기
	[CreateAssetMenu(fileName = "MT_Zigzag", menuName = "WM/Monster/Temperament/Zigzag")]
	public sealed class ZigzagTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("좌우로 꺾는 최대 각 (도)"), Range(0f, 89f)] private float swingDegrees = 60f;
		[SerializeField, Tooltip("한 번 왕복하는 초"), Min(0.1f)] private float periodSeconds = 1.4f;

		public override string KindName => "지그재그";

		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			Vector3 toPlayer = TemperamentContext.Toward(context.Self, context.Player);
			// 유닛마다 물결의 시작점을 달리 해 무리가 같이 출렁이지 않게 (Seed)
			float phase = (context.Now / periodSeconds + memory.Seed) * Mathf.PI * 2f;
			float angle = Mathf.Sin(phase) * swingDegrees;
			return TemperamentStep.Walk(Quaternion.AngleAxis(angle, Vector3.up) * toPlayer);
		}
	}
}
