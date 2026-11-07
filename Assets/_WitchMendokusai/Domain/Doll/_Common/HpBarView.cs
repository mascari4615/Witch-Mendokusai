using UnityEngine;

namespace WitchMendokusai
{
	// 배경 고정, 채움만 화면 왼쪽 끝 기준 축소. 루트 크기는 바 전체 크기 전용
	// - 몬스터 바 판은 Y 180도 돌아 있어 로컬 왼쪽이 화면 오른쪽 (2026-11-07 MOIS2 Play 실측). 카메라 기준으로 끝 선택
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
			float offset = HpBarLayout.FillOffset(clamped, fullWidth) * HpBarLayout.ScreenLeftSign(fill.parent.right, CameraRight());
			fill.localPosition = new Vector3(fillBasePosition.x + offset, fillBasePosition.y, fillBasePosition.z);
		}

		// 카메라 없는 순간 (씬 전환 중 생성) 은 로컬 왼쪽 기준. 다음 피격 때 다시 계산
		private static Vector3 CameraRight()
		{
			Camera camera = Camera.main;
			return camera != null ? camera.transform.right : Vector3.right;
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

		// 바의 로컬 +x 가 화면 오른쪽이면 1, 반대면 -1. FillOffset 에 곱해 화면 왼쪽 끝 고정
		public static float ScreenLeftSign(Vector3 barRight, Vector3 cameraRight) => Vector3.Dot(barRight, cameraRight) < 0f ? -1f : 1f;
	}
}
