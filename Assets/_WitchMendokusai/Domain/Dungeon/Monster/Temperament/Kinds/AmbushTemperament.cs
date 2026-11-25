using UnityEngine;

namespace WitchMendokusai
{
	// 앞질러 막기. 주인공이 몇 초 뒤 있을 자리로 감. 가까워지면 그제야 곧장
	// 시키는 행동: 가던 길을 틀어 속이기
	[CreateAssetMenu(fileName = "MT_Ambush", menuName = "WM/Monster/Temperament/Ambush")]
	public sealed class AmbushTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("주인공 속도로 이만큼 앞을 노림 (초)"), Min(0f)] private float leadSeconds = 1.2f;
		[SerializeField, Tooltip("이 거리 안이면 앞이 아니라 몸을 노림 (m)"), Min(0f)] private float closeRange = 2f;

		public override string KindName => "앞질러 막기";

		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			if (context.Distance <= closeRange)
				return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player));
			Vector3 ahead = context.Player + TemperamentContext.Flat(context.PlayerVelocity) * leadSeconds;
			return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, ahead));
		}
	}
}
