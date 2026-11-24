using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 크레딧 탭: CreditsInfo 의 분류별 접이식 목록, 항목을 누르면 라이선스와 원본 주소
	public partial class SettingView
	{
		private const string TAB_KEY_CREDITS = "credits";
		private const string CREDIT_OPEN_CLASS = "wm-setting-credit--open";
		private const string CREDIT_GROUP_OPEN_CLASS = "wm-setting-credit-group--open";

		private ScrollView creditScroll;
		private Button firstCreditGroup;

		/// <summary> 설정창을 크레딧 탭으로 바로 엶. 개발 창 credits 명령 </summary>
		public void OpenCredits()
		{
			Open();
			SwitchTab(TAB_KEY_CREDITS);
			firstCreditGroup.schedule.Execute(() => firstCreditGroup.Focus());
		}

		private VisualElement BuildCreditsContent()
		{
			VisualElement content = new VisualElement();

			Label hint = new Label("항목을 고르면 제작자, 원본 주소, 라이선스 원문 표시");
			hint.AddToClassList("wm-setting-credit-hint");
			content.Add(hint);

			creditScroll = new ScrollView(ScrollViewMode.Vertical);
			creditScroll.AddToClassList("wm-setting-credit-scroll");
			creditScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
			FollowFocus(creditScroll);
			creditScroll.verticalScroller.valueChanged += _ => RefreshCreditFrame();
			// 펼침과 접힘으로 줄 위치가 바뀐 뒤에도 선택 틀 따라감
			creditScroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => RefreshCreditFrame());
			content.Add(creditScroll);

			foreach (CreditCategory category in CreditCategoryText.Order)
			{
				List<CreditEntry> entries = uiRoot.Credits.InCategory(category);
				if (entries.Count == 0)
					continue;
				Button groupButton = BuildCreditGroup(category, entries);
				firstCreditGroup ??= groupButton;
			}

			// 첫 분류는 펼친 채로 시작
			if (firstCreditGroup != null)
				ToggleCreditGroup(firstCreditGroup, false);

			return content;
		}

		private Button BuildCreditGroup(CreditCategory category, List<CreditEntry> entries)
		{
			VisualElement body = new VisualElement();
			body.AddToClassList("wm-setting-credit-body");
			body.style.display = DisplayStyle.None;

			Button groupButton = null;
			groupButton = new Button(() => ToggleCreditGroup(groupButton, true));
			groupButton.AddToClassList("wm-setting-credit-group");
			groupButton.AddToClassList(NAV_CLASS);
			groupButton.userData = body;

			Label groupTitle = new Label(CreditCategoryText.Label(category));
			groupTitle.AddToClassList("wm-setting-credit-group__title");
			groupButton.Add(groupTitle);
			Label groupCount = new Label($"{entries.Count}");
			groupCount.AddToClassList("wm-setting-credit-group__count");
			groupButton.Add(groupCount);

			creditScroll.Add(groupButton);
			creditScroll.Add(body);

			foreach (CreditEntry entry in entries)
				BuildCreditRow(body, entry);
			return groupButton;
		}

		private void BuildCreditRow(VisualElement body, CreditEntry entry)
		{
			VisualElement detail = BuildCreditDetail(entry);

			Button row = null;
			row = new Button(() => ToggleCreditDetail(row, detail));
			row.AddToClassList("wm-setting-credit");
			row.AddToClassList(NAV_CLASS);

			Label nameLabel = new Label(entry.Name);
			nameLabel.AddToClassList("wm-setting-credit__name");
			row.Add(nameLabel);
			Label authorLabel = new Label(entry.Author);
			authorLabel.AddToClassList("wm-setting-credit__author");
			row.Add(authorLabel);
			Label licenseLabel = new Label(entry.LicenseName);
			licenseLabel.AddToClassList("wm-setting-credit__license");
			row.Add(licenseLabel);

			body.Add(row);
			body.Add(detail);
		}

		// 주소: 고를 수 있는 글자, 누르는 링크 아님 (게임 안 브라우저 열기는 범위 밖)
		private VisualElement BuildCreditDetail(CreditEntry entry)
		{
			VisualElement detail = new VisualElement();
			detail.AddToClassList("wm-setting-credit-detail");
			detail.style.display = DisplayStyle.None;

			AddCreditLine(detail, "제작자", entry.Author);
			AddCreditLine(detail, "저작권", entry.CopyrightLine);
			AddCreditLine(detail, "원본", entry.SourceUrl);
			AddCreditLine(detail, "라이선스", entry.LicenseName);
			AddCreditLine(detail, "라이선스 주소", entry.LicenseUrl);
			AddCreditLine(detail, "수정", entry.Modified ? $"있음, {entry.ModificationNote}" : "없음");
			AddCreditLine(detail, "쓰는 곳", entry.UsedIn);

			if (entry.License != null)
			{
				Label fullText = new Label(entry.License.text);
				fullText.AddToClassList("wm-setting-license__text");
				fullText.selection.isSelectable = true;
				detail.Add(fullText);
			}
			return detail;
		}

		private static void AddCreditLine(VisualElement detail, string title, string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return;
			VisualElement line = new VisualElement();
			line.AddToClassList("wm-setting-credit-detail__line");
			Label titleLabel = new Label(title);
			titleLabel.AddToClassList("wm-setting-credit-detail__title");
			line.Add(titleLabel);
			Label valueLabel = new Label(value);
			valueLabel.AddToClassList("wm-setting-credit-detail__value");
			valueLabel.selection.isSelectable = true;
			line.Add(valueLabel);
			detail.Add(line);
		}

		private void ToggleCreditGroup(Button groupButton, bool withSound)
		{
			VisualElement body = (VisualElement)groupButton.userData;
			bool open = body.style.display == DisplayStyle.None;
			body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
			groupButton.EnableInClassList(CREDIT_GROUP_OPEN_CLASS, open);
			if (withSound)
				UiSfx.Play(open ? UiSfxEvent.Confirm : UiSfxEvent.Cancel);
		}

		private void ToggleCreditDetail(Button row, VisualElement detail)
		{
			bool open = detail.style.display == DisplayStyle.None;
			detail.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
			row.EnableInClassList(CREDIT_OPEN_CLASS, open);
			UiSfx.Play(open ? UiSfxEvent.Confirm : UiSfxEvent.Cancel);
		}

		// 키보드 이동: 목록 밖 항목으로 스크롤, 선택 틀은 스크롤 반영 뒤 갱신. 크레딧과 키 탭이 같이 씀
		private void FollowFocus(ScrollView scroll) => scroll.RegisterCallback<FocusInEvent>(evt =>
		{
			if (evt.target is VisualElement element && element.ClassListContains(NAV_CLASS))
			{
				scroll.ScrollTo(element);
				element.schedule.Execute(() => MoveFrameTo(element));
			}
		});

		private void RefreshCreditFrame()
		{
			// 다른 탭에서는 선택 틀 유지
			if (currentTabKey != TAB_KEY_CREDITS)
				return;
			if (container.focusController?.focusedElement is VisualElement focused && creditScroll.Contains(focused))
				MoveFrameTo(focused);
		}
	}
}
