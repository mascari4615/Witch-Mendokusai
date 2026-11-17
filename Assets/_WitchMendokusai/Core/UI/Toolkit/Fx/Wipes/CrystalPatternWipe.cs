using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>깨진 유리 조각이 하나씩 덮임</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Crystal", menuName = "WM/UI/Wipe/Pattern/Crystal")]
	public sealed class CrystalPatternWipe : PatternUIWipe
	{
		[Tooltip("조각 수")]
		public int cells = 28;
		[Tooltip("조각 안에서 가운데부터 차오르는 정도. 0 이면 조각이 한 번에")]
		[Range(0f, 1f)] public float innerGrow = 0.15f;

		public override float Order(float x, float y, float aspect)
		{
			int cellCount = Mathf.Max(2, cells);
			Vector2 point = new(x * aspect, y);
			float nearest = float.MaxValue;
			int nearestIndex = 0;
			for (int i = 0; i < cellCount; i++)
			{
				Vector2 seed = new(WipePatterns.Hash01(i, 11) * aspect, WipePatterns.Hash01(i, 23));
				float distance = (seed - point).sqrMagnitude;
				if (distance < nearest)
				{
					nearest = distance;
					nearestIndex = i;
				}
			}
			float cellSize = Mathf.Sqrt(aspect / cellCount);
			return WipePatterns.Hash01(nearestIndex, 37) * (1f - innerGrow) + Mathf.Sqrt(nearest) / cellSize * innerGrow;
		}
	}
}
