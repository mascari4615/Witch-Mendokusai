using UnityEngine;

namespace WitchMendokusai.Presentation
{
	/// <summary>
	/// uGUI 요소를 안전 영역 안으로. 처음 앵커를 안전 영역 비율 위에 다시 펼침.
	/// 화면 전체로 늘인 판은 안전 영역 전체로, 모서리에 붙인 칸은 안전 영역 모서리로 감.
	/// 부모가 화면 전체를 덮는 캔버스 루트일 때 맞음. 화면 전체를 덮어야 하는 막 (페이드, 피격, 비네트) 에는 붙이지 않음
	/// </summary>
	[RequireComponent(typeof(RectTransform))]
	public class SafeAreaFitter : MonoBehaviour
	{
		private RectTransform rect;
		private Vector2 baseAnchorMin;
		private Vector2 baseAnchorMax;
		private Rect applied = SafeArea.UNSET;

		private void Awake()
		{
			rect = (RectTransform)transform;
			baseAnchorMin = rect.anchorMin;
			baseAnchorMax = rect.anchorMax;
		}

		private void OnEnable()
		{
			applied = SafeArea.UNSET;
			Apply();
		}

		private void Update() => Apply();

		private void Apply()
		{
			if (SafeArea.Refresh(ref applied) == false)
				return;
			rect.anchorMin = SafeArea.RemapAnchor(applied, baseAnchorMin);
			rect.anchorMax = SafeArea.RemapAnchor(applied, baseAnchorMax);
		}
	}
}
