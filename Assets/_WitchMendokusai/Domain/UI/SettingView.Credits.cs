using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 크레딧 탭: 글꼴 크레딧과 라이선스 원문
	public partial class SettingView
	{
		private const string TAB_KEY_CREDITS = "credits";

		private VisualElement BuildCreditsContent()
		{
			VisualElement content = new VisualElement();

			Label fontHeader = new Label("글꼴");
			fontHeader.AddToClassList("wm-setting-header");
			content.Add(fontHeader);

			ScrollView licenseText = new ScrollView(ScrollViewMode.Vertical);
			licenseText.AddToClassList("wm-setting-license");
			licenseText.style.display = DisplayStyle.None;

			foreach (CreditEntry entry in uiRoot.Credits.Fonts)
			{
				VisualElement row = new VisualElement();
				row.AddToClassList("wm-setting-credit");
				Label nameLabel = new Label(entry.Name);
				nameLabel.AddToClassList("wm-setting-credit__name");
				row.Add(nameLabel);
				Label noteLabel = new Label($"{entry.LicenseName}\n{entry.CopyrightLine}");
				noteLabel.AddToClassList("wm-setting-credit__note");
				row.Add(noteLabel);
				content.Add(row);

				Label fullText = new Label($"[{entry.Name}]\n{entry.License.text}");
				fullText.AddToClassList("wm-setting-license__text");
				licenseText.Add(fullText);
			}

			VisualElement buttonGroup = new VisualElement();
			buttonGroup.AddToClassList("wm-setting-credit-buttons");
			Button licenseButton = null;
			licenseButton = new Button(() =>
			{
				bool show = licenseText.style.display == DisplayStyle.None;
				licenseText.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
				licenseButton.text = show ? "라이선스 닫기" : "라이선스 전문";
				UiSfx.Play(UiSfxEvent.Confirm);
			}) { text = "라이선스 전문" };
			licenseButton.AddToClassList("wm-setting-btn");
			licenseButton.AddToClassList(NAV_CLASS);
			buttonGroup.Add(licenseButton);
			content.Add(buttonGroup);
			content.Add(licenseText);

			return content;
		}
	}
}
