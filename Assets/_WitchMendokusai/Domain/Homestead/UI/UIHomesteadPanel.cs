using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 생활 화면 하나. 틀과 조작은 같고 내용 (IHomesteadPage) 만 갈림
	// 방향키로 목록과 할 일을 오가고, Z 로 고르고, X 로 한 단계 뒤로, Tab 으로 분류
	public class UIHomesteadPanel : UIToolkitPanel, IUIPanelInputOwner
	{
		[SerializeField] private int popVisibleMilliseconds = 900;
		[SerializeField] private int popFadeMilliseconds = 200;
		[SerializeField] private int focusGuardMilliseconds = 200;

		[Inject] internal InputManager inputManager;

		private IHomesteadPage page;
		private HomesteadPageView view;
		private List<HomesteadEntry> entries = new();
		private HomesteadDetail detail;
		private int tab;
		private int selected;
		private int actionIndex;
		private bool inActions;

		public override string Name => page.Title;
		public override bool IsFullscreen => true;
		public bool OwnsTabInput => page != null && page.Tabs.Count > 1;

		private HomesteadSim Sim => HomesteadManager.Instance.Sim;

		public void SetPage(IHomesteadPage newPage) => page = newPage;

		// 처음 열 탭. 문을 연 자리가 정함
		public void StartAt(int startTab)
		{
			tab = Mathf.Clamp(startTab, 0, page.Tabs.Count - 1);
			selected = 0;
			inActions = false;
		}

		protected override void BuildUI(VisualElement content)
		{
			view = new HomesteadPageView(HomesteadArt.Instance);
			content.Add(view);
			Root.styleSheets.Add(HomesteadArt.Instance.ShopStyleSheet);
			Root.AddToClassList("wm-shop-backdrop");

			view.RegisterCallback<NavigationMoveEvent>(OnMove, TrickleDown.TrickleDown);
			view.RegisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
			view.RegisterCallback<NavigationCancelEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
			view.RegisterCallback<PointerUpEvent>(_ => FocusView());
			view.schedule.Execute(RestoreLostFocus).Every(focusGuardMilliseconds);
			view.EntryHovered += OnEntryHovered;
			view.EntryClicked += OnEntryClicked;
			view.TabClicked += SetTab;
			view.ActionClicked += OnActionClicked;
		}

		protected override void OnOpen()
		{
			inputManager.RegisterInputEvent(InputEventType.CameraControlModeToggle, InputEventResponseType.Performed, OnTabInput);
			Sim.Changed += OnSimChanged;
			UiSfx.Play(UiSfxEvent.WindowOpen);
			FocusView();
		}

		protected override void OnClose()
		{
			inputManager.UnregisterInputEvent(InputEventType.CameraControlModeToggle, InputEventResponseType.Performed, OnTabInput);
			if (HomesteadManager.TryGetExistingInstance(out HomesteadManager manager) && manager.Sim != null)
				manager.Sim.Changed -= OnSimChanged;
			Root?.panel?.focusController?.focusedElement?.Blur();
		}

		private void OnDestroy()
		{
			if (inputManager != null)
				inputManager.UnregisterInputEvent(InputEventType.CameraControlModeToggle, InputEventResponseType.Performed, OnTabInput);
			if (HomesteadManager.TryGetExistingInstance(out HomesteadManager manager) && manager.Sim != null)
				manager.Sim.Changed -= OnSimChanged;
		}

		private void FocusView() => view.schedule.Execute(() => view.Focus());

		private void RestoreLostFocus()
		{
			if (view.panel != null && Root.resolvedStyle.display == DisplayStyle.Flex && view.focusController.focusedElement == null)
				view.Focus();
		}

		// 분이 흐를 때마다 규칙이 알림. 열린 화면은 다음 프레임에 한 번만 다시 그림
		private bool refreshQueued;

		private void OnSimChanged()
		{
			if (refreshQueued)
				return;
			refreshQueued = true;
			view.schedule.Execute(() =>
			{
				refreshQueued = false;
				if (Root.resolvedStyle.display == DisplayStyle.Flex)
					Refresh(keepScroll: true);
			});
		}

		public override void UpdateUI() => Refresh(keepScroll: false);

		private void Refresh(bool keepScroll)
		{
			string selectedId = selected < entries.Count ? entries[selected].Id : string.Empty;
			entries = page.Entries(tab);
			if (keepScroll && selectedId.Length > 0)
			{
				int found = entries.FindIndex(entry => entry.Id == selectedId);
				if (found >= 0)
					selected = found;
			}
			selected = Mathf.Clamp(selected, 0, Mathf.Max(0, entries.Count - 1));
			view.SetHead(page.Title, page.Icon, page.Status, Sim.Purse.Nyang);
			view.SetTabs(page.Tabs, tab);
			view.SetEntries(entries, page.IsGrid(tab), page.EmptyText(tab));
			RefreshDetail(scrollTo: true);
		}

		private void RefreshDetail(bool scrollTo)
		{
			detail = entries.Count > 0 ? entries[selected].Detail() : null;
			int count = detail == null ? 0 : detail.Actions.Count;
			actionIndex = Mathf.Clamp(actionIndex, 0, Mathf.Max(0, count - 1));
			if (count == 0)
				inActions = false;
			view.SetDetail(detail, Sim.Catalog);
			view.SelectEntry(selected, scrollTo);
			view.SelectAction(inActions ? actionIndex : -1);
		}

		private void OnMove(NavigationMoveEvent evt)
		{
			evt.StopPropagation();
			view.focusController?.IgnoreEvent(evt);
			switch (evt.direction)
			{
				case NavigationMoveEvent.Direction.Up:
					MoveVertical(-1);
					break;
				case NavigationMoveEvent.Direction.Down:
					MoveVertical(1);
					break;
				case NavigationMoveEvent.Direction.Left:
					MoveHorizontal(-1);
					break;
				case NavigationMoveEvent.Direction.Right:
					MoveHorizontal(1);
					break;
			}
		}

		private void MoveVertical(int direction)
		{
			if (inActions)
			{
				int next = Mathf.Clamp(actionIndex + direction, 0, detail.Actions.Count - 1);
				if (next == actionIndex)
					return;
				actionIndex = next;
				UiSfx.Play(UiSfxEvent.FocusMove);
				view.SelectAction(actionIndex);
				return;
			}
			int columns = page.IsGrid(tab) ? view.Columns : 1;
			int target = selected + direction * columns;
			if (target >= entries.Count && columns > 1 && selected / columns < (entries.Count - 1) / columns)
				target = entries.Count - 1;
			Select(target, scrollTo: true);
		}

		private void MoveHorizontal(int direction)
		{
			if (inActions)
			{
				if (direction < 0)
					LeaveActions();
				return;
			}
			int columns = page.IsGrid(tab) ? view.Columns : 1;
			bool atRight = columns == 1 || selected % columns == columns - 1 || selected == entries.Count - 1;
			if (direction > 0 && atRight)
			{
				EnterActions();
				return;
			}
			if (columns > 1)
				Select(selected + direction, scrollTo: true);
		}

		private void Select(int index, bool scrollTo)
		{
			if (index < 0 || index >= entries.Count || index == selected)
				return;
			selected = index;
			actionIndex = 0;
			UiSfx.Play(UiSfxEvent.FocusMove);
			RefreshDetail(scrollTo);
		}

		private void EnterActions()
		{
			if (detail == null || detail.Actions.Count == 0)
				return;
			inActions = true;
			UiSfx.Play(UiSfxEvent.FocusMove);
			view.SelectAction(actionIndex);
		}

		private void LeaveActions()
		{
			inActions = false;
			UiSfx.Play(UiSfxEvent.Cancel);
			view.SelectAction(-1);
		}

		private void OnSubmit(NavigationSubmitEvent evt)
		{
			evt.StopPropagation();
			if (detail == null || detail.Actions.Count == 0)
				return;
			// 할 일이 하나면 목록에서 바로, 여럿이면 먼저 할 일로
			if (inActions == false && detail.Actions.Count > 1)
			{
				EnterActions();
				return;
			}
			RunAction(inActions ? actionIndex : 0);
		}

		private void RunAction(int index)
		{
			HomesteadAction action = detail.Actions[index];
			HomesteadResult result = action.Can.Ok ? action.Run() : action.Can;
			if (result.Ok == false)
			{
				UiSfx.Play(UiSfxEvent.PurchaseFail);
				view.Pop(HomesteadText.Reason(result, Sim.Catalog), true, popVisibleMilliseconds, popFadeMilliseconds);
				return;
			}
			UiSfx.Play(action.Sound);
			Refresh(keepScroll: true);
		}

		private void OnEntryHovered(int index)
		{
			if (inActions == false)
				Select(index, scrollTo: false);
		}

		private void OnEntryClicked(int index)
		{
			inActions = false;
			if (index == selected)
			{
				view.SelectAction(-1);
				return;
			}
			Select(index, scrollTo: false);
		}

		private void OnActionClicked(int index)
		{
			inActions = true;
			actionIndex = index;
			view.SelectAction(index);
			RunAction(index);
		}

		private void OnTabInput() => SetTab(tab + (inputManager.IsShiftHeld ? -1 : 1));

		private void SetTab(int index)
		{
			int count = page.Tabs.Count;
			int next = (index % count + count) % count;
			if (next == tab)
				return;
			tab = next;
			selected = 0;
			actionIndex = 0;
			inActions = false;
			UiSfx.Play(UiSfxEvent.Tab);
			Refresh(keepScroll: false);
		}

		// X 와 Esc. 할 일에서는 목록으로, 목록에서는 닫기
		public bool HandleCancelInput()
		{
			if (inActions)
			{
				LeaveActions();
				return true;
			}
			UiSfx.Play(UiSfxEvent.WindowClose);
			return false;
		}
	}
}
