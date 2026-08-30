using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>전투 개체의 시간 기반 모션 계산. 게임 상태나 씬 생성에 의존하지 않는다.</summary>
	internal static class IdleBattleMotion
	{
		public static float CatchUp(float speed, float delta)
		{
			return 1f - Mathf.Exp(-speed * delta);
		}

		public static float WalkBob(float clock, int index, float amplitude)
		{
			return Mathf.Abs(Mathf.Sin(clock * 7f + index * 1.3f)) * amplitude;
		}

		public static float FoeBob(float clock, int index, float amplitude)
		{
			return Mathf.Sin(clock * 2.4f + index * 1.7f) * amplitude;
		}

		public static Vector3 Shake(float clock, float distance, float envelope)
		{
			return new Vector3(
				Mathf.Sin(clock * 83f) * distance * envelope,
				Mathf.Sin(clock * 117f) * distance * 0.35f * envelope,
				0f);
		}
	}
}
