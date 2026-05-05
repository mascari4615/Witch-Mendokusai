using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 윈도우 본체 — 엔드필드식 3 모드 전환.
	/// - Root: 큰 주제 버튼 (블록/아이템/주민)
	/// - Category: 뒤로 + DataExplorerView (좌 사이드바 + 우 카드 그리드)
	/// - Detail: 뒤로 + CodexDetailPanel
	///
	/// 사이드바/그리드 베이스는 DataExplorerView (TASK-WM-038 단계 A 추출).
	/// CodexView 는 *Codex 도메인 흐름* (Root/Detail 모드 + 모드 전환) 만 담당.
	/// </summary>
	public class CodexView : VisualElement
	{
		public const string USS_CLASS = "wm-codex";
		public const string USS_ROOT = "wm-codex__root";
		public const string USS_ROOT_BUTTON = "wm-codex__root-button";
		public const string USS_CATEGORY = "wm-codex__category";
		public const string USS_BACK_BUTTON = "wm-codex__back-button";
		public const string USS_DETAIL = "wm-codex__detail";
		public const string USS_DETAIL_CONTENT = "wm-codex__detail-content";

		public event Action<IEntryProvider> OnCategorySelected = delegate { };
		public event Action<EntryDescriptor> OnEntrySelected = delegate { };

		private enum CodexMode
		{
			Root,
			Category,
			Detail,
		}

		private readonly VisualElement rootArea;
		private readonly VisualElement categoryArea;
		private readonly DataExplorerView dataExplorerView;
		private readonly VisualElement detailArea;
		private readonly VisualElement detailContent;

		private IEntryProvider activeCategory;
		private EntryDescriptor activeEntry;
		private CodexMode currentMode;

		public IEntryProvider ActiveCategory => activeCategory;
		public EntryDescriptor ActiveEntry => activeEntry;

		public CodexView()
		{
			AddToClassList(USS_CLASS);
			style.flexDirection = FlexDirection.Column;
			style.flexGrow = 1;

			rootArea = new VisualElement();
			rootArea.AddToClassList(USS_ROOT);
			rootArea.style.flexGrow = 1;
			rootArea.style.flexDirection = FlexDirection.Row;
			rootArea.style.justifyContent = Justify.Center;
			rootArea.style.alignItems = Align.Center;

			categoryArea = new VisualElement();
			categoryArea.AddToClassList(USS_CATEGORY);
			categoryArea.style.flexGrow = 1;
			categoryArea.style.flexDirection = FlexDirection.Column;

			Button categoryBackButton = new(BackToRoot)
			{
				text = "← 뒤로",
			};
			categoryBackButton.AddToClassList(USS_BACK_BUTTON);
			categoryArea.Add(categoryBackButton);

			dataExplorerView = new DataExplorerView();
			dataExplorerView.OnEntrySelected += entry => OnEntrySelected.Invoke(entry);
			categoryArea.Add(dataExplorerView);

			detailArea = new VisualElement();
			detailArea.AddToClassList(USS_DETAIL);
			detailArea.style.flexGrow = 1;
			detailArea.style.flexDirection = FlexDirection.Column;

			Button detailBackButton = new(BackToCategory)
			{
				text = "← 뒤로",
			};
			detailBackButton.AddToClassList(USS_BACK_BUTTON);
			detailArea.Add(detailBackButton);

			detailContent = new VisualElement();
			detailContent.AddToClassList(USS_DETAIL_CONTENT);
			detailContent.style.flexGrow = 1;
			detailArea.Add(detailContent);

			SetMode(CodexMode.Root);
		}

		private void SetMode(CodexMode mode)
		{
			currentMode = mode;

			Clear();
			if (mode == CodexMode.Root)
				Add(rootArea);
			else if (mode == CodexMode.Category)
				Add(categoryArea);
			else
				Add(detailArea);
		}

		public void RebuildRoot(IReadOnlyList<IEntryProvider> categories)
		{
			rootArea.Clear();
			for (int i = 0; i < categories.Count; i++)
			{
				IEntryProvider category = categories[i];
				IEntryProvider captured = category;
				Button button = new(() => OnCategorySelected.Invoke(captured))
				{
					text = category.DisplayName,
				};
				button.AddToClassList(USS_ROOT_BUTTON);
				rootArea.Add(button);
			}
		}

		public void SetActiveCategory(IEntryProvider category)
		{
			detailContent.Clear();
			activeEntry = null;
			activeCategory = category;

			dataExplorerView.SetActiveProvider(category);

			if (category == null)
			{
				SetMode(CodexMode.Root);
				return;
			}

			SetMode(CodexMode.Category);
		}

		public void SetActiveEntry(EntryDescriptor entry)
		{
			if (activeEntry != null)
				dataExplorerView.SetEntryActive(activeEntry, false);

			detailContent.Clear();
			activeEntry = entry;
			if (entry == null)
			{
				SetMode(CodexMode.Category);
				return;
			}

			dataExplorerView.SetEntryActive(entry, true);

			if (entry.IsUnlocked == false)
			{
				detailContent.Add(new Label("???"));
				SetMode(CodexMode.Detail);
				return;
			}

			if (activeCategory == null)
				return;

			CodexDetailPanel panel = new(entry, activeCategory);
			detailContent.Add(panel);

			SetMode(CodexMode.Detail);
		}

		private void BackToRoot()
		{
			dataExplorerView.SetActiveProvider(null);
			activeCategory = null;
			activeEntry = null;
			detailContent.Clear();
			SetMode(CodexMode.Root);
		}

		private void BackToCategory()
		{
			if (activeEntry != null)
				dataExplorerView.SetEntryActive(activeEntry, false);
			activeEntry = null;
			detailContent.Clear();
			SetMode(CodexMode.Category);
		}
	}
}
