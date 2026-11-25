using UnityEngine;

namespace WitchMendokusai
{
	// 수줍음. 멀면 다가오고, 가까워지면 쉬는 구석으로 물러남. 물러날 때와 다시 올 때 거리를 달리 둬 떨지 않게
	// 시키는 행동: 쫓아가 잡기. 무리 사이 숨 쉴 틈
	[CreateAssetMenu(fileName = "MT_Shy", menuName = "WM/Monster/Temperament/Shy")]
	public sealed class ShyTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("이 거리 안에 들면 물러남 (m)"), Min(0f)] private float shyRadius = 4f;
		[SerializeField, Tooltip("이 거리 밖으로 나가면 다시 다가옴 (m). shyRadius 보다 커야 함"), Min(0f)] private float returnRadius = 7f;
		[SerializeField, Tooltip("물러날 때 걷는 세기 0~1"), Range(0f, 1f)] private float retreatSpeed = 0.8f;

		public override string KindName => "수줍음";

		// 기억의 IsOn 은 물러나는 중
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			float distance = context.Distance;
			if (memory.IsOn == false && distance < shyRadius)
				memory.IsOn = true;
			else if (memory.IsOn && distance > returnRadius)
				memory.IsOn = false;
			if (memory.IsOn == false)
				return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player));
			Vector3 away = TemperamentContext.Toward(context.Player, context.Self);
			return new TemperamentStep { Direction = away, Speed = retreatSpeed, FacesPlayer = true };
		}
	}
}
