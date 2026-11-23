using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class QuestDetail : VisualElement
	{
		public const string USS_CLASS = "wm-quest-detail";
		public const string USS_NAME = "wm-quest-detail__name";
		public const string USS_DESC = "wm-quest-detail__desc";
		public const string USS_STATE = "wm-quest-detail__state";
		public const string USS_PROGRESS = "wm-quest-detail__progress";
		public const string USS_CRITERIA = "wm-quest-detail__criteria";
		public const string USS_CRITERION = "wm-quest-detail__criterion";
		public const string USS_CRITERION_DONE = "wm-quest-detail__criterion--done";
		public const string USS_BUTTON = "wm-quest-detail__button";

		private readonly Label nameLabel;
		private readonly Label descLabel;
		private readonly Label stateLabel;
		private readonly Label progressLabel;
		private readonly VisualElement criteriaContainer;
		private readonly Button workButton;
		private readonly Button completeButton;

		private RuntimeQuest quest;
		private QuestSO questSOFallback;

		private readonly QuestManager questManager;

		/// <summary>작업 시작, 보상 받기로 퀘스트가 바뀐 뒤 (클릭과 확인 키 모두)</summary>
		public event Action Changed = delegate { };

		public QuestDetail(QuestManager questManager)
		{
			this.questManager = questManager;
			AddToClassList(USS_CLASS);

			nameLabel = new Label();
			nameLabel.AddToClassList(USS_NAME);
			Add(nameLabel);

			descLabel = new Label();
			descLabel.AddToClassList(USS_DESC);
			Add(descLabel);

			stateLabel = new Label();
			stateLabel.AddToClassList(USS_STATE);
			Add(stateLabel);

			progressLabel = new Label();
			progressLabel.AddToClassList(USS_PROGRESS);
			Add(progressLabel);

			criteriaContainer = new VisualElement();
			criteriaContainer.AddToClassList(USS_CRITERIA);
			Add(criteriaContainer);

			workButton = new Button(OnWorkClicked) { text = "작업 시작" };
			workButton.AddToClassList(USS_BUTTON);
			Add(workButton);

			completeButton = new Button(OnCompleteClicked) { text = "보상 받기" };
			completeButton.AddToClassList(USS_BUTTON);
			Add(completeButton);

			style.display = DisplayStyle.None;
		}

		/// <summary>
		/// QuestSO + RuntimeQuest (nullable) 통합 Bind — TASK-WM-059 polish 후속 (2026-05-10).
		/// RuntimeQuest 미생성 (Locked / 미발견) 일 때 so fallback 으로 Name/Description 표시.
		/// 086 F7 RuntimeQuest 격상 (SO field 제거 — QuestSOID int 만) 에 따라 caller 가 둘 다 전달.
		/// </summary>
		public void Bind(QuestSO so, RuntimeQuest runtimeOrNull)
		{
			quest = runtimeOrNull;
			questSOFallback = so;
			Refresh();
		}

		public void Refresh()
		{
			bool hasContext = quest != null || questSOFallback != null;
			style.display = hasContext ? DisplayStyle.Flex : DisplayStyle.None;

			if (hasContext == false)
				return;

			nameLabel.text = quest?.Name ?? questSOFallback?.Name ?? "?";
			descLabel.text = quest?.Description ?? questSOFallback?.Description ?? string.Empty;
			stateLabel.text = quest != null ? StateText(quest.State) : "잠김";
			progressLabel.text = quest != null ? questManager.ProgressTextOf(quest) : string.Empty;
			// 조건이 하나면 아래 조건 줄이 이름과 같은 값을 보여 주므로 큰 진행 글은 숨김
			progressLabel.style.display = quest != null && quest.Criteria.Count == 1 ? DisplayStyle.None : DisplayStyle.Flex;

			criteriaContainer.Clear();
			if (quest != null)
			{
				foreach (RuntimeCriteria criteria in quest.Criteria)
				{
					Label label = new(CriteriaText.LineOf(criteria));
					label.AddToClassList(USS_CRITERION);
					label.EnableInClassList(USS_CRITERION_DONE, criteria.IsCompleted);
					criteriaContainer.Add(label);
				}
			}

			workButton.style.display = quest != null && quest.State == RuntimeQuestState.CanWork
				? DisplayStyle.Flex : DisplayStyle.None;
			completeButton.style.display = quest != null && quest.State == RuntimeQuestState.CanComplete
				? DisplayStyle.Flex : DisplayStyle.None;
		}

		/// <summary>화면에 보이는 진행 상태 이름. 버튼으로 할 수 있는 일과 같은 말로</summary>
		public static string StateText(RuntimeQuestState state) => state switch
		{
			RuntimeQuestState.InProgress => "진행 중",
			RuntimeQuestState.CanWork => "작업할 수 있음",
			RuntimeQuestState.Working => "작업 중",
			RuntimeQuestState.CanComplete => "보상 받을 수 있음",
			RuntimeQuestState.Completed => "마침",
			_ => state.ToString(),
		};

		/// <summary>화면에 보이는 퀘스트 갈래 이름. 거름 줄 (QuestFilterBar) 과 같은 말로</summary>
		public static string TypeText(QuestType type) => type switch
		{
			QuestType.Normal => "일반",
			QuestType.VillageRequest => "마을의뢰",
			QuestType.Achievement => "업적",
			QuestType.Research => "연구",
			QuestType.MagicBookPage => "마도서",
			QuestType.Dungeon => "던전",
			_ => type.ToString(),
		};

		private void OnWorkClicked()
		{
			if (quest == null || quest.State != RuntimeQuestState.CanWork)
				return;
			quest.StartWork(0);
			Refresh();
			Changed();
		}

		private void OnCompleteClicked()
		{
			if (quest == null || quest.State != RuntimeQuestState.CanComplete)
				return;
			quest.Complete();
			Refresh();
			Changed();
		}
	}
}
