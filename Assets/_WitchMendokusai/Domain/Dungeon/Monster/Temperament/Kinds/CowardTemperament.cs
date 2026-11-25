using UnityEngine;

namespace WitchMendokusai
{
	// 겁쟁이. 맞으면 움찔해 잠깐 달아나고, 체력이 얼마 안 남으면 계속 달아남
	// 시키는 행동: 끝까지 쫓아가 마무리하기, 아니면 내버려 두기
	[CreateAssetMenu(fileName = "MT_Coward", menuName = "WM/Monster/Temperament/Coward")]
	public sealed class CowardTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("맞은 뒤 달아나는 초"), Min(0f)] private float flinchSeconds = 1.2f;
		[SerializeField, Tooltip("남은 체력이 이 비율 아래면 계속 달아남"), Range(0f, 1f)] private float panicBelow = 0.35f;
		[SerializeField, Tooltip("달아나는 세기 0~1"), Range(0f, 1f)] private float fleeSpeed = 1f;

		public override string KindName => "겁쟁이";

		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			bool isScared = context.SinceHit < flinchSeconds || context.HealthRatio < panicBelow;
			if (isScared == false)
				return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player));
			TemperamentStep flee = TemperamentStep.Walk(TemperamentContext.Toward(context.Player, context.Self), fleeSpeed);
			flee.HoldsAttack = true;
			return flee;
		}
	}
}
