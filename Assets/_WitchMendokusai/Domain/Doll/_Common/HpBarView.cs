using UnityEngine;

namespace WitchMendokusai
{
	// 배경 고정, 채움만 왼쪽 끝 기준 축소. 루트 크기는 바 전체 크기 전용
	public class HpBarView : MonoBehaviour
	{
		[SerializeField] private Transform fill;
		[SerializeField] private SpriteRenderer fillRenderer;

		private Vector3 fillBaseScale;
		private Vector3 fillBasePosition;
		private bool cached;

		public void SetRatio(float ratio)
		{
			CacheBase();
			float clamped = HpBarLayout.Clamp(ratio);
			float fullWidth = fillRenderer.sprite.bounds.size.x * fillRenderer.transform.localScale.x * fillBaseScale.x;
			fill.localScale = new Vector3(fillBaseScale.x * clamped, fillBaseScale.y, fillBaseScale.z);
			fill.localPosition = new Vector3(fillBasePosition.x + HpBarLayout.FillOffset(clamped, fullWidth), fillBasePosition.y, fillBasePosition.z);
		}

		private void CacheBase()
		{
			if (cached)
				return;
			fillBaseScale = fill.localScale;
			fillBasePosition = fill.localPosition;
			cached = true;
		}
	}

	public static class HpBarLayout
	{
		public static float Clamp(float ratio) => float.IsNaN(ratio) ? 0f : Mathf.Clamp01(ratio);

		// 가운데 기준 크기 변화를 왼쪽 끝 고정으로 바꾸는 이동량
		public static float FillOffset(float ratio, float fullWidth) => -(1f - Clamp(ratio)) * fullWidth * 0.5f;
	}
}
