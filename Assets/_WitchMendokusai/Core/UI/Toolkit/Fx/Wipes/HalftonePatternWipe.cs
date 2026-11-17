using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>망점이 대각선을 따라 자라며 이어져 덮음 (인쇄 망점 결)</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Halftone", menuName = "WM/UI/Wipe/Pattern/Halftone")]
	public sealed class HalftonePatternWipe : PatternUIWipe
	{
		// 단위 칸 가운데에서 모서리까지 거리
		private const float HALF_DIAGONAL = 0.7071f;

		[Tooltip("가로 망점 수")]
		public int cells = 16;
		[Tooltip("망점 결 세기. 0 이면 매끈한 대각선")]
		[Range(0f, 1f)] public float dotWeight = 0.4f;

		public override float Order(float x, float y, float aspect)
		{
			float cellCount = Mathf.Max(2, cells);
			float column = x * cellCount;
			float row = y * cellCount / aspect;
			float localX = column - Mathf.Floor(column) - 0.5f;
			float localY = row - Mathf.Floor(row) - 0.5f;
			// 칸 가운데 0, 모서리 1
			float dot = Mathf.Sqrt(localX * localX + localY * localY) / HALF_DIAGONAL;
			float sweep = (x * aspect + y) / (aspect + 1f);
			return sweep * (1f - dotWeight) + dot * dotWeight;
		}
	}
}
