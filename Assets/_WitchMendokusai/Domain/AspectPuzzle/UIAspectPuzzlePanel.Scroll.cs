using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 봉인된 두루마리 풀기. 위 줄의 두루마리 버튼을 누르면 그 종류의 문제 하나가 비워진 채로 열리고, 그 문제를 다 풀면 두루마리 하나를 쓰고 든 것을 얻음.
	/// 풀다 닫거나 다른 문제를 풀면 두루마리는 그대로. 규칙은 ScrollRules, 가방과 세이브는 ScrollInterpreter
	/// </summary>
	public sealed partial class UIAspectPuzzlePanel
	{
		private const string SCROLL_FORMAT = "{0} 두루마리 {1}";
		private const string SCROLL_NONE = "가진 두루마리가 없습니다.";
		private const string SCROLL_NOTHING_LEFT = "모두 열었습니다. 남은 두루마리는 팔 수 있습니다.";
		private const string SCROLL_STARTED = "봉인된 두루마리: {0} 번 문제를 풀면 열립니다.";
		private const string SCROLL_LEARNED = "배움: {0}";
		private const string SCROLL_NEW_FORM = "{0} 의 다른 모습 ({1}/{2})";

		[Inject] internal SOManager soManager;
		[Inject] internal ResourceManager resourceManager;
		[Inject] internal DataManager dataManager;

		private ScrollInterpreter interpreter;
		private Button paperScrollButton;
		private Button turnScrollButton;
		private Label scrollNotice;
		private ScrollPuzzleKind? pendingKind;
		private int pendingIndex;

		private ScrollInterpreter Interpreter => interpreter ??= new ScrollInterpreter(resourceManager.Scrolls, () => soManager.ItemInventory, dataManager, count => UnityEngine.Random.Range(0, count));

		private void BuildScrollButtons(VisualElement top)
		{
			paperScrollButton = AddScrollButton(top, ScrollPuzzleKind.Paper);
			turnScrollButton = AddScrollButton(top, ScrollPuzzleKind.Turn);
			scrollNotice = new Label { pickingMode = PickingMode.Ignore };
			scrollNotice.AddToClassList("wm-aspect-scroll-notice");
			top.Add(scrollNotice);
		}

		private Button AddScrollButton(VisualElement top, ScrollPuzzleKind kind)
		{
			Button button = new(() => StartScroll(kind));
			button.AddToClassList("wm-mb-tab");
			button.AddToClassList("wm-aspect-scroll");
			focus.Register(button);
			top.Add(button);
			return button;
		}

		private void ListenScrolls()
		{
			paperView.Solved += index => OnPuzzleSolved(ScrollPuzzleKind.Paper, index);
			turnView.Solved += index => OnPuzzleSolved(ScrollPuzzleKind.Turn, index);
		}

		private void RefreshScrolls()
		{
			paperScrollButton.text = string.Format(SCROLL_FORMAT, PAPER_TAB, Interpreter.Count(ScrollPuzzleKind.Paper));
			turnScrollButton.text = string.Format(SCROLL_FORMAT, TURN_TAB, Interpreter.Count(ScrollPuzzleKind.Turn));
		}

		private void StartScroll(ScrollPuzzleKind kind)
		{
			if (Interpreter.Count(kind) <= 0)
			{
				scrollNotice.text = SCROLL_NONE;
				return;
			}
			if (Interpreter.HasAnythingLeft == false)
			{
				scrollNotice.text = SCROLL_NOTHING_LEFT;
				return;
			}
			AspectPuzzleView view = ViewOf(kind);
			int index = PickPuzzle(view);
			// 앞서 풀던 두루마리는 쓰지 않은 채 접음 (두루마리는 가방에 그대로). 새 안내가 덮음
			pendingKind = null;
			Select(view);
			// StartFresh 가 비우기 전에 한 번 그리므로, 이미 풀린 문제가 두루마리를 쓰지 않게 기다림은 그 뒤에 걺
			view.StartFresh(index);
			pendingKind = kind;
			pendingIndex = index;
			scrollNotice.text = string.Format(SCROLL_STARTED, index);
		}

		private void OnPuzzleSolved(ScrollPuzzleKind kind, int index)
		{
			if (pendingKind != kind || pendingIndex != index)
			{
				return;
			}
			pendingKind = null;
			ScrollOutcome outcome = Interpreter.Complete(kind);
			scrollNotice.text = outcome.Draw.IsNew
				? string.Format(SCROLL_LEARNED, resourceManager.Scrolls.DisplayName(outcome.Row))
				: string.Format(SCROLL_NEW_FORM, resourceManager.Scrolls.DisplayName(outcome.Row), outcome.Draw.Form, outcome.Draw.Entry.Forms);
			RefreshScrolls();
		}

		private void CancelScroll()
		{
			pendingKind = null;
			if (scrollNotice != null)
			{
				scrollNotice.text = string.Empty;
			}
		}

		private AspectPuzzleView ViewOf(ScrollPuzzleKind kind) => kind == ScrollPuzzleKind.Paper ? paperView : turnView;

		// 문제는 아무것이나 (기획 정본 11-25 21:43 의 "두루마리 하나에 문제 하나" 를 AI 가 무작위로 읽음). 연습은 뺌
		private static int PickPuzzle(AspectPuzzleView view)
		{
			int candidates = 0;
			for (int i = 0; i < view.PuzzleCount; i++)
			{
				if (view.IsPractice(i) == false)
				{
					candidates++;
				}
			}
			int chosen = UnityEngine.Random.Range(0, candidates);
			for (int i = 0; i < view.PuzzleCount; i++)
			{
				if (view.IsPractice(i))
				{
					continue;
				}
				if (chosen == 0)
				{
					return i;
				}
				chosen--;
			}
			throw new System.InvalidOperationException($"{view.GetType().Name}: 연습 말고 문제가 없음");
		}
	}
}
