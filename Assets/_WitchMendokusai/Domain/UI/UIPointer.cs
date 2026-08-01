using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 「지금 포인터가 UI 위인가」 단일 판정 — 월드 클릭(배치·조준 등)이 UI 버튼을 뚫고 나가는 것을 막는다.
	///
	/// ★ 근본: 버튼마다 "다음 클릭 한 번 삼켜줘" 를 호출하는 방식은 버튼을 하나 새로 만들 때마다 잊으면
	///   조용히 깨진다(사용자 실증: HUD 를 눌렀는데 그 아래 지면에 건물이 서고 자원이 빠짐).
	///   판정은 UI 쪽에 한 곳으로 있어야 하고, 월드 입력은 그걸 물어보기만 하면 된다.
	///
	/// UI Toolkit 은 panel.Pick 으로 포인터 아래 요소를 준다. pickingMode=Ignore 인 요소는 건너뛰므로
	/// 결과가 「진짜 눌리는 것」(버튼 등)일 때만 UI 위로 본다 — 레이어 컨테이너 자체는 UI 로 치지 않는다
	/// (안 그러면 화면 전체가 UI 라 아무것도 설치할 수 없다).
	/// </summary>
	public static class UIPointer
	{
		/// <summary> 스크린 좌표(좌하단 원점, Input 계열과 동일)가 눌리는 UI 요소 위인지. </summary>
		public static bool IsOverInteractive(Vector2 screenPosition)
		{
			if (UIRoot.TryGetExistingInstance(out UIRoot uiRoot) == false)
				return false;

			VisualElement root = uiRoot.Root;
			if (root == null || root.panel == null)
				return false;

			// UI Toolkit 패널 좌표계는 좌상단 원점 — y 를 뒤집지 않으면 화면 위/아래가 반대로 판정된다.
			Vector2 screenPointTopLeft = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
			Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, screenPointTopLeft);

			VisualElement picked = root.panel.Pick(panelPosition);
			if (picked == null)
				return false;

			return IsStructuralLayer(picked, uiRoot) == false;
		}

		// 레이어/루트 = 화면 전체를 덮는 구조용 컨테이너. 이걸 UI 로 치면 월드 클릭이 전부 막힌다.
		private static bool IsStructuralLayer(VisualElement element, UIRoot uiRoot)
		{
			return element == uiRoot.Root
				|| element == uiRoot.WindowsLayer
				|| element == uiRoot.ScreenLayer
				|| element == uiRoot.HudLayer
				|| element == uiRoot.OverlayLayer;
		}
	}
}
