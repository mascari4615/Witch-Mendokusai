using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 연성 가마솥 NPC 패널 — UI Toolkit (TASK-WM-113 S2 substrate first-use).
	/// 구 uGUI UIPot 은 빈 스텁(UpdateUI = NotImplementedException 잠복 크래시)이었음 →
	/// Toolkit 으로 대체하며 크래시 제거. 실제 연성/조합 UI 내용은 게임 비전 결정
	/// (WM-113 § 후속 — NPC 제작대 UI). 본 클래스는 substrate 입증 + 크래시 해소가 목적.
	/// </summary>
	public class UIPotToolkit : UIToolkitPanel
	{
		public override string Name => "연성 가마솥";

		private Label statusLabel;

		protected override void BuildUI(VisualElement root)
		{
			root.style.flexGrow = 1;
			root.style.alignItems = Align.Center;
			root.style.justifyContent = Justify.Center;

			Label title = new("연성 가마솥");
			title.style.fontSize = 28;
			root.Add(title);

			statusLabel = new Label("연성 시스템 준비 중");
			statusLabel.style.marginTop = 8;
			root.Add(statusLabel);
		}

		public override void UpdateUI()
		{
			// 구 UIPot.UpdateUI 의 NotImplementedException 제거 — 내용은 WM-113 후속(비전).
		}
	}
}
