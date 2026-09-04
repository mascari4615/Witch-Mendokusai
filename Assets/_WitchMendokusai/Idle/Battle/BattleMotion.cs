using UnityEngine;

namespace WitchMendokusai.Idle
{
	/// <summary>전투 개체의 시간 기반 모션 계산. 게임 상태나 씬 생성에 의존 없음. 박자 수치는 부르는 쪽 자산</summary>
	internal static class BattleMotion
	{
		public static float CatchUp(float speed, float delta)
		{
			return 1f - Mathf.Exp(-speed * delta);
		}

		/// <summary>걷기 위아래. 절댓값이라 바닥에서 튀는 꼴. index 위상으로 자리마다 어긋남</summary>
		public static float WalkBob(float clock, int index, float amplitude, float frequency, float phaseStep)
		{
			return Mathf.Abs(Mathf.Sin(clock * frequency + index * phaseStep)) * amplitude;
		}

		/// <summary>적 떠다님. 부드러운 사인</summary>
		public static float FoeBob(float clock, int index, float amplitude, float frequency, float phaseStep)
		{
			return Mathf.Sin(clock * frequency + index * phaseStep) * amplitude;
		}

		/// <summary>화면 흔들림. 가로가 주, 세로는 몫만큼</summary>
		public static Vector3 Shake(
			float clock, float distance, float envelope, float frequencyX, float frequencyY, float shareY)
		{
			return new Vector3(
				Mathf.Sin(clock * frequencyX) * distance * envelope,
				Mathf.Sin(clock * frequencyY) * distance * shareY * envelope,
				0f);
		}
	}
}
