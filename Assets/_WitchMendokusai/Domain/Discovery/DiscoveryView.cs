using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 윈도우 본체. 3 모드 전환.
	/// - Root: 큰 주제 버튼 (블록/아이템/주민)
	/// - Category: 뒤로 + DataExplorerView (좌 사이드바 + 우 카드 그리드)
	/// - Detail: 뒤로 + DiscoveryDetailPanel
	///
	/// 사이드바/그리드 베이스는 DataExplorerView (TASK-WM-038 단계 A 추출).
	/// DiscoveryView 는 *Discovery 도메인 흐름* (Root/Detail 모드 + 모드 전환) 만 담당.
	/// </summary>
	public class DiscoveryView : VisualElement
	{
		public const string USS_CLASS = "wm-discovery";
		public const string USS_ROOT = "wm-discovery__root";
		public const string USS_ROOT_BUTTON = "wm-discovery__root-button";
		public const string USS_CATEGORY = "wm-discovery__category";
		public const string USS_BACK_BUTTON = "wm-discovery__back-button";
		public const string USS_DETAIL = "wm-discovery__detail";
		public const string USS_DETAIL_CONTENT = "wm-discovery__detail-content";
		public const string USS_PROGRESS = "wm-discovery__progress";

		public event Action<IEntryProvider> OnCategorySelected = delegate { };
		public event Action<EntryDescriptor> OnEntrySelected = delegate { };

		private enum DiscoveryMode
		{
			Root,
			Category,
			Detail,
		}

		private readonly VisualElement rootArea;
		private readonly VisualElement categoryArea;
		private readonly DataExplorerView dataExplorerView;
		private readonly Label progressLabel;
		private readonly VisualElement detailArea;
		private readonly VisualElement detailContent;

		private IEntryProvider activeCategory;
		private EntryDescriptor activeEntry;
		private DiscoveryMode currentMode;

		public IEntryProvider ActiveCategory => activeCategory;
		public EntryDescriptor ActiveEntry => activeEntry;

		public DiscoveryView()
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

			progressLabel = new Label();
			progressLabel.AddToClassList(USS_PROGRESS);
			categoryArea.Add(progressLabel);

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

			SetMode(DiscoveryMode.Root);
		}

		private void SetMode(DiscoveryMode mode)
		{
			currentMode = mode;

			Clear();
			if (mode == DiscoveryMode.Root)
				Add(rootArea);
			else if (mode == DiscoveryMode.Category)
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
				SetMode(DiscoveryMode.Root);
				return;
			}

			RefreshProgress(category);
			SetMode(DiscoveryMode.Category);
		}

		/// <summary>
		/// 이 갈래를 얼마나 채웠나. 항목이 이미 들고 있는 답을 세므로 등록소에 다시 묻지 않는다
		/// (카드에 보이는 잠금과 언제나 같은 수).
		/// </summary>
		private void RefreshProgress(IEntryProvider category)
		{
			IReadOnlyList<EntryDescriptor> entries = category.GetEntries();
			int unlocked = 0;

			for (int index = 0; index < entries.Count; index++)
			{
				if (entries[index].IsUnlocked)
				{
					unlocked++;
				}
			}

			DiscoveryProgress progress = new(entries.Count, unlocked);
			progressLabel.text = $"{progress.Unlocked} / {progress.Total} 발견";
		}

		public void SetActiveEntry(EntryDescriptor entry)
		{
			if (activeEntry != null)
				dataExplorerView.SetEntryActive(activeEntry, false);

			detailContent.Clear();
			activeEntry = entry;
			if (entry == null)
			{
				SetMode(DiscoveryMode.Category);
				return;
			}

			dataExplorerView.SetEntryActive(entry, true);

			if (entry.IsUnlocked == false)
			{
				detailContent.Add(new Label("???"));
				SetMode(DiscoveryMode.Detail);
				return;
			}

			if (activeCategory == null)
				return;

			DiscoveryDetailPanel panel = new(entry, activeCategory);
			detailContent.Add(panel);

			SetMode(DiscoveryMode.Detail);
		}

		private void BackToRoot()
		{
			dataExplorerView.SetActiveProvider(null);
			activeCategory = null;
			activeEntry = null;
			detailContent.Clear();
			SetMode(DiscoveryMode.Root);
		}

		private void BackToCategory()
		{
			if (activeEntry != null)
				dataExplorerView.SetEntryActive(activeEntry, false);
			activeEntry = null;
			detailContent.Clear();
			SetMode(DiscoveryMode.Category);
		}
	}
}
