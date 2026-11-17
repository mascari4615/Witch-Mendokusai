using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>하트 모양으로 닫힘</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Heart", menuName = "WM/UI/Wipe/Pattern/Heart")]
	public sealed class HeartPatternWipe : PatternUIWipe
	{
		// 하트 곡선 좌표로 바꾸는 배율. 클수록 같은 진행에서 하트가 작음 (모양 정의, 조절은 진행 시간으로)
		private const float HEART_FIT = 2.6f;
		// 하트 가운데를 화면 가운데에 맞추는 위쪽 이동 (모양 정의)
		private const float HEART_LIFT = 0.1f;
		// 배율 탐색 상한과 이분 탐색 횟수. 14 회면 1/16000 정밀도
		private const float HEART_SEARCH_MAX = 8f;
		private const int HEART_SEARCH_STEPS = 14;

		public override float Order(float x, float y, float aspect)
		{
			// 하트 곡선 (x^2 + y^2 - 1)^3 - x^2 y^3 이 0 이하인 영역을 몇 배로 키워야 이 점이 경계에 닿는지. 클수록 바깥
			float offsetX = (x - 0.5f) * aspect * HEART_FIT;
			float offsetY = (0.5f - y) * HEART_FIT + HEART_LIFT;
			float low = 0f;
			float high = HEART_SEARCH_MAX;
			for (int step = 0; step < HEART_SEARCH_STEPS; step++)
			{
				float middle = (low + high) * 0.5f;
				if (InsideHeart(offsetX / middle, offsetY / middle))
					high = middle;
				else
					low = middle;
			}
			return -high;
		}

		private static bool InsideHeart(float x, float y)
		{
			float squared = x * x + y * y - 1f;
			return squared * squared * squared - x * x * y * y * y <= 0f;
		}
	}
}
