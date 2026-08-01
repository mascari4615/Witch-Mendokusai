using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// **지금 화면에 실제로 보이는** 카메라를 돌려준다 — 월드 좌표를 화면 좌표로 옮기는 모든 곳의 단일 정본.
	///
	/// ★ 왜 `Camera.main` 이면 안 되나 (TASK-WM-194 실측): `Camera.main` 은 "MainCamera 태그가 붙은 카메라"일
	///   뿐 "지금 보이는 카메라"가 아니다. 개척(특수시공)처럼 **모드 전용 카메라를 본편 카메라 위에 겹쳐**
	///   렌더하는 화면에서는 둘이 갈라진다 — 그러면 데미지 숫자가 *화면 뒤에 숨은 본편 카메라* 기준으로
	///   투영돼 엉뚱한 자리(또는 화면 밖)에 뜬다. "데미지 텍스트가 제대로 안 나온다"의 실제 원인.
	///   화면에 최종적으로 그려지는 것은 depth 가 가장 큰 카메라이므로 그것이 곧 "보이는 카메라"다.
	/// ★ 프레임 단위 캐시 — 한 프레임에 수십 개 텍스트가 물어봐도 카메라 스캔은 한 번.
	/// </summary>
	public static class ViewCameraResolver
	{
		private static Camera cached;
		private static int cachedFrame = -1;

		/// <summary> 화면에 보이는 카메라(최상위 depth). 하나도 없으면 null. </summary>
		public static Camera Current
		{
			get
			{
				if (cachedFrame == Time.frameCount && cached != null)
					return cached;

				cached = ResolveTopmost();
				cachedFrame = Time.frameCount;
				return cached;
			}
		}

		private static Camera ResolveTopmost()
		{
			Camera topmost = null;
			foreach (Camera candidate in Camera.allCameras) // allCameras = 활성 카메라만.
			{
				if (candidate.targetTexture != null)
					continue; // 렌더 텍스처 전용(미니맵 등)은 화면이 아니다.
				if (topmost == null || candidate.depth > topmost.depth)
					topmost = candidate;
			}

			return topmost != null ? topmost : Camera.main;
		}
	}
}
