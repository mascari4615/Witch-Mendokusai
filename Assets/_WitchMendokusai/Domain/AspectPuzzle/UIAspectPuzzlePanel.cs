using System;
using UnityEngine.UIElements;
using VContainer;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>
	/// 성질 놀이 (육각 종이, 번갈아) 창. 솥 NPC 메뉴에서 열고 릴리스에서는 안 보임 (UINPCMenu.devBuildOnlyPanels).
	/// 모습은 임시. 밤하늘과 위 줄은 마도서, 마름모는 스킬 트리. 푼 것은 저장하지 않음 (자리 나눔이 미정)
	/// 자료와 규칙의 출처: memo wm/design/aspect-notes-20261125
	/// </summary>
	public sealed class UIAspectPuzzlePanel : UIToolkitPanel
	{
		private const string NAV_CLASS = "wm-aspect-nav";
		private const string TAB_CURRENT = "wm-aspect-tab--current";
		private const string PAPER_TAB = "종이";
		private const string TURN_TAB = "번갈아";
		private const string CLOSE = "닫기";
		private const string CLOSE_KEY = "ESC";

		[Inject] internal InputManager inputManager;

		private AspectPuzzleSettings settings;
		private MenuFocus focus;
		private VisualElement stage;
		private HexPaperView paperView;
		private TurnBoardView turnView;
		private AspectPuzzleView current;
		private Button paperTab;
		private Button turnTab;
		private bool isOpen;

		public override string Name => settings.PanelName;

		/// <summary>UINPC 가 만든 직후 넣음</summary>
		public void SetSettings(AspectPuzzleSettings newSettings)
		{
			if (newSettings == null)
			{
				throw new InvalidOperationException($"{nameof(UIAspectPuzzlePanel)}: NPC 패널 프리팹의 AspectPuzzleSettings 가 비어 있음. 값이 있는데 비면 새 필드보다 프리팹이 먼저 임포트된 것 ([Panel] NPC.prefab Reimport)");
			}
			settings = newSettings;
		}

		protected override void BuildUI(VisualElement content)
		{
			FullMenuSettings menu = settings.MagicBook.Menu;
			Root.AddToClassList("wm-fullmenu-palette");
			Root.AddToClassList("wm-mb");
			Root.AddToClassList("wm-aspect");
			Root.styleSheets.Add(menu.Style);
			Root.styleSheets.Add(settings.MagicBook.Style);
			Root.styleSheets.Add(settings.SkillTreeStyle);
			Root.styleSheets.Add(settings.Style);

			stage = new VisualElement();
			stage.AddToClassList("wm-fullmenu__stage");
			content.Add(stage);
			content.RegisterCallback<GeometryChangedEvent>(_ => MenuStage.Fit(content, stage, menu.ReferenceHeight, menu.MinimumWidth));

			focus = new MenuFocus(Root, NAV_CLASS, () => isOpen, () => menu.NavOverlapTolerance, () => menu.NavAcrossWeight)
			{
				FocusFirst = FocusFirst,
				Step = (from, direction) => current.StepFrom(from, direction),
			};
			BuildTop();
		}

		protected override void OnOpen()
		{
			EnsureViews();
			isOpen = true;
			current.Refresh();
			// 손가락이면 고른 칸 강조가 남으니 포커스를 주지 않음
			if (inputManager.IsTouchMode == false)
			{
				focus.FocusWhenReady();
			}
		}

		protected override void OnClose()
		{
			isOpen = false;
			Root?.panel?.focusController?.focusedElement?.Blur();
		}

		public override void UpdateUI() => current.Refresh();

		private void BuildTop()
		{
			VisualElement top = new();
			top.AddToClassList("wm-fullmenu__top");
			stage.Add(top);
			VisualElement tabs = new();
			tabs.AddToClassList("wm-mb-tabs");
			top.Add(tabs);
			paperTab = AddTab(tabs, PAPER_TAB, () => Select(paperView));
			turnTab = AddTab(tabs, TURN_TAB, () => Select(turnView));

			VisualElement spacer = new();
			spacer.AddToClassList("wm-fullmenu__spacer");
			top.Add(spacer);
			Button closeButton = KeyHint.KeyButton(CLOSE, CLOSE_KEY, () => PanelGroup.ClosePanel());
			closeButton.AddToClassList("wm-fullmenu-close");
			focus.Register(closeButton);
			top.Add(closeButton);
		}

		// 성질과 인형 자료 (SOManager) 는 창을 만드는 때 아직 안 들어왔을 수 있어 처음 열 때 짬
		private void EnsureViews()
		{
			if (current != null)
			{
				return;
			}
			AspectPuzzleBook book = AspectPuzzleLoader.Load(settings.Book);
			AspectLinks links = new(book.Recipes);
			paperView = new HexPaperView(settings, book, links, focus);
			turnView = new TurnBoardView(settings, book, links, focus);
			stage.Add(paperView);
			stage.Add(turnView);
			Select(paperView);
		}

		private Button AddTab(VisualElement tabs, string text, Action clicked)
		{
			Button tab = new(clicked) { text = text };
			tab.AddToClassList("wm-mb-tab");
			focus.Register(tab);
			tabs.Add(tab);
			return tab;
		}

		private void Select(AspectPuzzleView next)
		{
			current = next;
			AspectPuzzleView.SetShown(paperView, next == paperView);
			AspectPuzzleView.SetShown(turnView, next == turnView);
			paperTab.EnableInClassList(TAB_CURRENT, next == paperView);
			turnTab.EnableInClassList(TAB_CURRENT, next == turnView);
			next.Refresh();
		}

		private bool FocusFirst() => current.TryFocusCurrentPuzzle();
	}
}
