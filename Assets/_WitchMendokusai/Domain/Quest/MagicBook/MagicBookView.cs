using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 풀스크린 view (UI Toolkit). <see cref="UIRoot"/>.ScreenLayer 통합 — <see cref="SettingView"/> 패턴.
	/// 챕터 리스트 (좌측 사이드바) + 선택 챕터의 노드 그래프 (우측, <see cref="ChapterView"/>).
	///
	/// TASK-WM-059 C (2026-05-09) — uGUI UIMagicBookPanel 의 UI Toolkit 대체.
	/// 챕터 데이터 = <see cref="SOManager"/>.DataSOs 자동 수집 (ChapterSO : DataSO 정합).
	///
	/// 단축키 'm' (Open/Toggle) 은 단계 E (별 commit), 노드 클릭 → QuestDetail 은 단계 D (Provider OnClicked 갱신) 후속.
	/// </summary>
	public class MagicBookView : MonoBehaviour
	{
		public const string USS_CLASS = "wm-magic-book-view";
		public const string ACTIVE_CLASS = "wm-magic-book-view--active";

		private VisualElement container;
		private VisualElement chapterListContainer;
		private VisualElement chapterContentContainer;

		private readonly List<ChapterSO> chapters = new();
		private readonly Dictionary<ChapterSO, ChapterView> chapterViewsByChapter = new();
		private ChapterSO currentChapter;

		public bool IsOpen { get; private set; }

		private void Start()
		{
			container = new VisualElement();
			container.AddToClassList(USS_CLASS);
			UIRoot.Instance.ScreenLayer.Add(container);

			BuildUI();
			IsOpen = false;
		}

		private void OnDestroy()
		{
			container?.RemoveFromHierarchy();
		}

		private void BuildUI()
		{
			chapterListContainer = new VisualElement();
			chapterListContainer.AddToClassList("wm-magic-book-list");
			container.Add(chapterListContainer);

			chapterContentContainer = new VisualElement();
			chapterContentContainer.AddToClassList("wm-magic-book-content");
			container.Add(chapterContentContainer);

			Button btnClose = new Button(Close) { text = "닫기 (M)" };
			btnClose.AddToClassList("wm-magic-book-close");
			container.Add(btnClose);

			CollectChapters();
			BuildChapterButtons();

			if (chapters.Count > 0)
				OpenChapter(chapters[0]);
		}

		private void CollectChapters()
		{
			chapters.Clear();

			if (SOManager.Instance.DataSOs.TryGetValue(typeof(ChapterSO), out Dictionary<int, DataSO> chapterDict) == false)
				return;

			foreach (DataSO dataSO in chapterDict.Values)
			{
				if (dataSO is ChapterSO chapterSO == false)
					continue;
				chapters.Add(chapterSO);
			}
		}

		private void BuildChapterButtons()
		{
			chapterListContainer.Clear();

			foreach (ChapterSO chapter in chapters)
			{
				ChapterSO captured = chapter;
				Button button = new Button(() => OpenChapter(captured)) { text = string.IsNullOrEmpty(chapter.Name) ? chapter.name : chapter.Name };
				button.AddToClassList("wm-magic-book-chapter-btn");
				chapterListContainer.Add(button);
			}
		}

		private void OpenChapter(ChapterSO chapter)
		{
			if (currentChapter == chapter)
				return;

			currentChapter = chapter;
			chapterContentContainer.Clear();

			if (chapterViewsByChapter.TryGetValue(chapter, out ChapterView view) == false)
			{
				view = new ChapterView();
				view.Bind(chapter);
				chapterViewsByChapter[chapter] = view;
			}

			chapterContentContainer.Add(view);
		}

		public void Open()
		{
			if (IsOpen)
				return;
			IsOpen = true;

			// 매번 최신 ChapterSO 목록 (SOManager 가 늦게 초기화 됐을 가능성 + 동적 자산 추가 대응 — SettingView ShaderPack 패턴)
			CollectChapters();
			BuildChapterButtons();
			if (currentChapter == null && chapters.Count > 0)
				OpenChapter(chapters[0]);

			container.AddToClassList(ACTIVE_CLASS);
			TimeManager.Instance.Pause(gameObject);
		}

		public void Close()
		{
			if (IsOpen == false)
				return;
			IsOpen = false;
			container.RemoveFromClassList(ACTIVE_CLASS);
			TimeManager.Instance.Resume(gameObject);
		}

		public void Toggle()
		{
			if (IsOpen)
				Close();
			else
				Open();
		}

		[ContextMenu("Open MagicBook")]
		private void DebugOpen() => Open();

		[ContextMenu("Close MagicBook")]
		private void DebugClose() => Close();
	}
}
