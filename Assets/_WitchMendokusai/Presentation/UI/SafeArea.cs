using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Presentation
{
	/// <summary>
	/// 노치, 둥근 모서리, 제스처 바를 피하는 안전 영역의 단일 출처. 모든 UI 가 이 값만 씀.
	/// 화면 대비 비율 (0~1, 왼쪽 아래 기준) 로 다뤄서 패널 배율이나 캔버스 배율과 무관.
	/// 데스크톱은 안전 영역이 화면 전체라 여백 0.
	/// uGUI 는 <see cref="SafeAreaFitter"/>, UI Toolkit 은 UIRoot 가 층 단위로 적용.
	/// </summary>
	public static class SafeArea
	{
		public static Rect Normalized => Normalize(Screen.safeArea, Screen.width, Screen.height);

		// 픽셀 안전 영역을 화면 대비 비율로. 순수 계산이라 기기 없이 시험 가능
		public static Rect Normalize(Rect safePixels, float screenWidth, float screenHeight)
		{
			float width = Mathf.Max(1f, screenWidth);
			float height = Mathf.Max(1f, screenHeight);
			return Rect.MinMaxRect(safePixels.xMin / width, safePixels.yMin / height, safePixels.xMax / width, safePixels.yMax / height);
		}

		// uGUI 앵커 하나를 안전 영역 위로 다시 펼침. 0 은 안전 영역 시작, 1 은 끝
		public static Vector2 RemapAnchor(Rect normalized, Vector2 anchor) => new(
			Mathf.Lerp(normalized.xMin, normalized.xMax, anchor.x),
			Mathf.Lerp(normalized.yMin, normalized.yMax, anchor.y));

		// 처음 값으로 쓰면 첫 Refresh 가 반드시 적용됨
		public static readonly Rect UNSET = new(-1f, -1f, 0f, 0f);

		// 안전 영역이 last 와 달라졌으면 last 를 갱신하고 true. 회전과 창 크기 변화를 매 프레임 싸게 확인
		public static bool Refresh(ref Rect last)
		{
			Rect now = Normalized;
			if (now == last)
				return false;
			last = now;
			return true;
		}

		/// <summary>
		/// 화면 전체를 덮는 절대 위치 요소를 안전 영역 크기로 줄임. 퍼센트 기준이 부모 (화면 전체) 라 배율 계산 없음.
		/// 화면 좌표는 아래가 0, UI 는 위가 0 이라 위아래를 여기서 한 번 뒤집음.
		/// </summary>
		public static void ApplyInsets(VisualElement element, Rect normalized)
		{
			element.style.left = Length.Percent(normalized.xMin * 100f);
			element.style.right = Length.Percent((1f - normalized.xMax) * 100f);
			element.style.top = Length.Percent((1f - normalized.yMax) * 100f);
			element.style.bottom = Length.Percent(normalized.yMin * 100f);
		}

		/// <summary>
		/// 흐름 배치 루트용. 자기 크기는 그대로 두고 안쪽 여백으로 비킴.
		/// UI Toolkit 의 퍼센트 여백은 위아래도 폭 기준이라 픽셀로 바꿔 넣음. 크기가 정해진 뒤 (GeometryChanged) 호출
		/// </summary>
		public static void ApplyPadding(VisualElement element, Rect normalized)
		{
			float width = element.resolvedStyle.width;
			float height = element.resolvedStyle.height;
			if (float.IsNaN(width) || float.IsNaN(height) || width <= 0f || height <= 0f)
				return;
			element.style.paddingLeft = normalized.xMin * width;
			element.style.paddingRight = (1f - normalized.xMax) * width;
			element.style.paddingTop = (1f - normalized.yMax) * height;
			element.style.paddingBottom = normalized.yMin * height;
		}
	}
}
