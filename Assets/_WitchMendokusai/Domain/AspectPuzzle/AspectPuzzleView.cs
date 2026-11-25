using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>
	/// 육각 종이와 번갈아가 같이 쓰는 틀. 왼쪽 문제 목록, 가운데 판, 오른쪽 상태와 고를 성질과 버튼.
	/// 규칙은 DomainSDK/Aspect 의 한 판 (HexPaperGame, TurnGame) 이 갖고 여기는 보이기만
	/// </summary>
	public abstract class AspectPuzzleView : VisualElement
	{
		private const string USS_CLASS = "wm-aspect-view";
		private const string TAB_CURRENT = "wm-aspect-tab--current";
		private const string TAB_SOLVED = "wm-aspect-tab--solved";
		private const string HIDDEN = "wm-aspect--hidden";
		private const string PRACTICE_ID = "practice";
		private const string PRACTICE_NAME = "연습";
		private const string PARTNERS_FORMAT = "{0} 옆에 붙는 것: {1}";

		private readonly List<Button> puzzleButtons = new();
		private readonly VisualElement board;
		private readonly VisualElement puzzleList;
		private readonly AspectLinkLines lines;
		private readonly Label status;
		private readonly Label help;
		private readonly Label notice;

		protected AspectPuzzleView(AspectPuzzleSettings settings, AspectLinks links, MenuFocus focus)
		{
			Settings = settings;
			Links = links;
			Focus = focus;
			AddToClassList(USS_CLASS);

			puzzleList = Part(this, "wm-aspect-list");
			board = Part(this, "wm-aspect-board");
			// 평상시 빛은 사각형이라 육각과 마름모 칸에서 이웃까지 덮음
			board.AddToClassList(UIIdleFx.SKIP_CLASS);
			Side = Part(this, "wm-aspect-side");
			lines = new AspectLinkLines(settings);
			board.RegisterCallback<GeometryChangedEvent>(_ => Layout());

			status = new Label { pickingMode = PickingMode.Ignore };
			status.AddToClassList("wm-aspect-status");
			help = new Label { pickingMode = PickingMode.Ignore };
			help.AddToClassList("wm-aspect-help");
			notice = new Label { pickingMode = PickingMode.Ignore };
			notice.AddToClassList("wm-aspect-notice");
			Palette = new AspectPalette(focus);
			Actions = new VisualElement();
			Actions.AddToClassList("wm-aspect-actions");
		}

		protected AspectPuzzleSettings Settings { get; }
		protected AspectLinks Links { get; }
		protected MenuFocus Focus { get; }
		protected VisualElement Side { get; }
		protected VisualElement Actions { get; }
		protected AspectPalette Palette { get; }
		protected Rect BoardRect => board.contentRect;

		public static void SetShown(VisualElement element, bool isShown) => element.EnableInClassList(HIDDEN, isShown == false);

		/// <summary>오른쪽 칸을 위에서부터 쌓음. 놀이마다 다른 칸은 부르기 전에 Side 에</summary>
		protected void BuildSide()
		{
			Side.Add(status);
			Side.Add(help);
			Side.Add(notice);
			Side.Add(Palette);
			Side.Add(Actions);
		}

		protected void BuildPuzzleList(IReadOnlyList<string> ids, Action<int> selected)
		{
			for (int i = 0; i < ids.Count; i++)
			{
				int index = i;
				// 문제 이름은 미정이라 번호로만 (사용자 몫)
				Button button = new(() => selected(index)) { text = ids[i] == PRACTICE_ID ? PRACTICE_NAME : i.ToString() };
				button.AddToClassList("wm-mb-tab");
				button.AddToClassList("wm-aspect-tab");
				Focus.Register(button);
				puzzleButtons.Add(button);
				puzzleList.Add(button);
			}
		}

		protected void ShowSolved(int index, bool isSolved) => puzzleButtons[index].EnableInClassList(TAB_SOLVED, isSolved);

		protected void ShowCurrentPuzzle(int index)
		{
			for (int i = 0; i < puzzleButtons.Count; i++)
			{
				puzzleButtons[i].EnableInClassList(TAB_CURRENT, i == index);
			}
		}

		protected Button AddAction(string text, Action clicked)
		{
			Button button = new(clicked) { text = text };
			button.AddToClassList("wm-mb-arena");
			button.AddToClassList("wm-aspect-action");
			Focus.Register(button);
			Actions.Add(button);
			return button;
		}

		protected void ClearBoard()
		{
			board.Clear();
		}

		protected void AddCell(AspectCell cell)
		{
			Focus.Register(cell);
			board.Add(cell);
		}

		/// <summary>칸을 다 넣은 뒤. 선이 칸 위에 오게</summary>
		protected void FinishBoard()
		{
			board.Add(lines);
			Layout();
		}

		protected void ShowText(string statusText, string helpText)
		{
			status.text = statusText;
			help.text = helpText;
		}

		/// <summary>방금 한 일의 답 (안 되는 까닭, 붙는 것). 다음 조작에서 지움</summary>
		protected void ShowNotice(string text) => notice.text = text;

		/// <summary>칸의 성질과 붙는 것들</summary>
		protected void ShowPartners(int aspect, IEnumerable<int> candidates)
		{
			List<string> names = new();
			foreach (int candidate in candidates)
			{
				if (Links.AreLinked(aspect, candidate))
				{
					names.Add(NameOf(candidate));
				}
			}
			ShowNotice(string.Format(PARTNERS_FORMAT, NameOf(aspect), string.Join(", ", names)));
		}

		protected void ShowLinks(IEnumerable<(Vector2 From, Vector2 To)> pairs) => lines.Show(pairs);

		/// <summary>처음 포커스. 지금 문제 버튼. 배치 전이면 false</summary>
		public bool TryFocusCurrentPuzzle()
		{
			Button button = puzzleButtons.Find(candidate => candidate.ClassListContains(TAB_CURRENT));
			if ((button.worldBound.width > 0f) == false)
			{
				return false;
			}
			button.Focus();
			return true;
		}

		/// <summary>판 안의 방향 키. 판의 칸이 아니거나 그쪽에 칸이 없으면 null</summary>
		public VisualElement StepFrom(VisualElement from, NavigationMoveEvent.Direction direction)
		{
			if (from is not AspectCell cell)
			{
				return null;
			}
			return direction switch
			{
				NavigationMoveEvent.Direction.Left => NextCell(cell, BoardArrow.Left),
				NavigationMoveEvent.Direction.Right => NextCell(cell, BoardArrow.Right),
				NavigationMoveEvent.Direction.Up => NextCell(cell, BoardArrow.Up),
				NavigationMoveEvent.Direction.Down => NextCell(cell, BoardArrow.Down),
				_ => null,
			};
		}

		protected abstract AspectCell NextCell(AspectCell from, BoardArrow arrow);

		public abstract void Refresh();

		/// <summary>판 크기가 바뀌거나 문제가 바뀌었을 때 칸 자리</summary>
		protected abstract void PlaceCells();

		protected static string NameOf(int aspect) => SOHelper.GetAspectData(aspect).Name;

		protected static Sprite SpriteOf(int aspect) => SOHelper.GetAspectData(aspect).Sprite;

		protected static VisualElement Part(VisualElement parent, string className)
		{
			VisualElement element = new();
			element.AddToClassList(className);
			parent.Add(element);
			return element;
		}

		private void Layout()
		{
			if (float.IsNaN(BoardRect.width) || BoardRect.width <= 0f || BoardRect.height <= 0f)
			{
				return;
			}
			PlaceCells();
			Refresh();
		}
	}
}
