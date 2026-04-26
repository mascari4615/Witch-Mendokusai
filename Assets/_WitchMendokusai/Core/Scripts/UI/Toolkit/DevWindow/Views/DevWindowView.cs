using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발자 윈도우 레이아웃. 사이드바(좌) + 컨텐츠(우 상) + 콘솔 출력(하 중) + 명령행(하 끝).
	/// WMWindow 를 직접 상속 X — 컴포지션으로 가짐 (DevWindowController 가 WMWindow 보유).
	/// </summary>
	public class DevWindowView : VisualElement
	{
		public const string USS_CLASS = "wm-dev-window";
		public const string USS_BODY = "wm-dev-window__body";
		public const string USS_SIDEBAR = "wm-dev-window__sidebar";
		public const string USS_SIDEBAR_BUTTON = "wm-dev-window__sidebar-button";
		public const string USS_SIDEBAR_BUTTON_ACTIVE = "wm-dev-window__sidebar-button--active";
		public const string USS_CONTENT = "wm-dev-window__content";

		public event Action<IDevMode> OnModeSelected = delegate { };

		public ConsoleView Console { get; }
		public CommandLineField CommandLine { get; }

		private readonly VisualElement sidebar;
		private readonly VisualElement contentArea;
		private readonly Dictionary<string, Button> sidebarButtons = new();
		private IDevMode activeMode;

		public DevWindowView()
		{
			AddToClassList(USS_CLASS);

			VisualElement body = new();
			body.AddToClassList(USS_BODY);
			Add(body);

			sidebar = new VisualElement();
			sidebar.AddToClassList(USS_SIDEBAR);
			body.Add(sidebar);

			contentArea = new VisualElement();
			contentArea.AddToClassList(USS_CONTENT);
			body.Add(contentArea);

			Console = new ConsoleView();
			Add(Console);

			CommandLine = new CommandLineField();
			Add(CommandLine);
			// dropdown 은 normal flow 밖 — DevWindowController 가 UIRoot.OverlayLayer 에 attach.
		}

		public void RebuildSidebar(IReadOnlyList<IDevMode> modes)
		{
			sidebar.Clear();
			sidebarButtons.Clear();

			for (int i = 0; i < modes.Count; i++)
			{
				IDevMode mode = modes[i];
				Button button = new(() => OnModeSelected.Invoke(mode))
				{
					text = mode.DisplayName
				};
				button.AddToClassList(USS_SIDEBAR_BUTTON);
				sidebarButtons[mode.Id] = button;
				sidebar.Add(button);
			}
		}

		public void SetActiveMode(IDevMode mode)
		{
			if (activeMode != null)
			{
				activeMode.OnDeactivate();
				if (sidebarButtons.TryGetValue(activeMode.Id, out Button previous))
					previous.RemoveFromClassList(USS_SIDEBAR_BUTTON_ACTIVE);
			}

			contentArea.Clear();

			activeMode = mode;
			if (mode == null)
				return;

			contentArea.Add(mode.Root);
			mode.OnActivate();

			if (sidebarButtons.TryGetValue(mode.Id, out Button current))
				current.AddToClassList(USS_SIDEBAR_BUTTON_ACTIVE);
		}

		public IDevMode ActiveMode => activeMode;
	}
}
