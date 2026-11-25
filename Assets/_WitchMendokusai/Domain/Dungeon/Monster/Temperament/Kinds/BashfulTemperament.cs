using UnityEngine;

namespace WitchMendokusai
{
	// 부끄럼. 주인공이 쳐다보면 그 자리에 얼어붙고, 등을 돌리면 빠르게 다가옴
	// 시키는 행동: 바라보는 쪽 (걷는 쪽) 으로 묶어 두기. 여럿이면 다 볼 수 없음
	[CreateAssetMenu(fileName = "MT_Bashful", menuName = "WM/Monster/Temperament/Bashful")]
	public sealed class BashfulTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("주인공 시선에서 이 각 안이면 보이는 것 (도, 한쪽)"), Range(0f, 180f)] private float watchedHalfAngle = 60f;
		[SerializeField, Tooltip("안 볼 때 다가오는 세기 0~1"), Range(0f, 1f)] private float sneakSpeed = 1f;

		public override string KindName => "부끄럼";

		public static bool IsWatched(in TemperamentContext context, float halfAngleDegrees)
		{
			Vector3 look = TemperamentContext.Flat(context.PlayerLook);
			Vector3 toSelf = TemperamentContext.Toward(context.Player, context.Self);
			if (look.sqrMagnitude <= 0f || toSelf == Vector3.zero)
				return false;
			return Vector3.Angle(look, toSelf) <= halfAngleDegrees;
		}

		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			if (IsWatched(context, watchedHalfAngle))
				return TemperamentStep.Stay;
			return TemperamentStep.Walk(TemperamentContext.Toward(context.Self, context.Player), sneakSpeed);
		}
	}
}
