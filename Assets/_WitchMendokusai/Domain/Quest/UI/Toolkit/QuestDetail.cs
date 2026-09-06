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

		private readonly Label nameLabel;
		private readonly Label descLabel;
		private readonly Label stateLabel;
		private readonly Label progressLabel;
		private readonly VisualElement criteriaContainer;
		private readonly Button workButton;
		private readonly Button completeButton;

		private RuntimeQuest quest;
		private QuestSO questSOFallback;

		private static readonly Color NAME_COLOR = new(0.95f, 0.95f, 1f, 1f);
		private static readonly Color DESC_COLOR = new(0.78f, 0.78f, 0.88f, 1f);
		private static readonly Color STATE_COLOR = new(0.85f, 0.95f, 1f, 1f);
		private static readonly Color PROGRESS_COLOR = new(0.95f, 0.85f, 0.5f, 1f);

		private readonly QuestManager questManager;

		public QuestDetail(QuestManager questManager)
		{
			this.questManager = questManager;
			AddToClassList(USS_CLASS);

			nameLabel = new Label();
			nameLabel.AddToClassList(USS_NAME);
			nameLabel.style.fontSize = 18;
			nameLabel.style.color = NAME_COLOR;
			nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			nameLabel.style.marginBottom = 8;
			nameLabel.style.whiteSpace = WhiteSpace.Normal;
			Add(nameLabel);

			descLabel = new Label();
			descLabel.AddToClassList(USS_DESC);
			descLabel.style.whiteSpace = WhiteSpace.Normal;
			descLabel.style.fontSize = 12;
			descLabel.style.color = DESC_COLOR;
			descLabel.style.marginBottom = 12;
			Add(descLabel);

			stateLabel = new Label();
			stateLabel.AddToClassList(USS_STATE);
			stateLabel.style.fontSize = 13;
			stateLabel.style.color = STATE_COLOR;
			stateLabel.style.marginBottom = 4;
			Add(stateLabel);

			progressLabel = new Label();
			progressLabel.AddToClassList(USS_PROGRESS);
			progressLabel.style.fontSize = 13;
			progressLabel.style.color = PROGRESS_COLOR;
			progressLabel.style.marginBottom = 8;
			Add(progressLabel);

			criteriaContainer = new VisualElement();
			criteriaContainer.AddToClassList(USS_CRITERIA);
			criteriaContainer.style.marginBottom = 12;
			Add(criteriaContainer);

			workButton = new Button(OnWorkClicked) { text = "작업 시작" };
			workButton.style.marginTop = 4;
			Add(workButton);

			completeButton = new Button(OnCompleteClicked) { text = "보상 받기" };
			completeButton.style.marginTop = 4;
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
			stateLabel.text = quest != null ? quest.State.ToString() : "잠김";
			progressLabel.text = quest != null ? questManager.ProgressTextOf(quest) : string.Empty;

			criteriaContainer.Clear();
			if (quest != null)
			{
				foreach (RuntimeCriteria criteria in quest.Criteria)
				{
					Label label = new(BuildCriteriaText(criteria));
					label.style.color = criteria.IsCompleted
						? new Color(0.3f, 0.9f, 0.3f)
						: Color.white;
					criteriaContainer.Add(label);
				}
			}

			workButton.style.display = quest != null && quest.State == RuntimeQuestState.CanWork
				? DisplayStyle.Flex : DisplayStyle.None;
			completeButton.style.display = quest != null && quest.State == RuntimeQuestState.CanComplete
				? DisplayStyle.Flex : DisplayStyle.None;
		}

		private string BuildCriteriaText(RuntimeCriteria criteria)
		{
			string name = criteria.Criteria is ItemCountCriteria itemCount
				? SOHelper.GetItemData(itemCount.ItemID)?.Name ?? "?"
				: criteria.Criteria.GetType().Name;

			int target = criteria.GetTargetValue();
			if (target > 0)
				return $"{name}  {criteria.GetCurValue()}/{target}";

			return name;
		}

		private void OnWorkClicked()
		{
			if (quest == null || quest.State != RuntimeQuestState.CanWork)
				return;
			quest.StartWork(0);
			Refresh();
		}

		private void OnCompleteClicked()
		{
			if (quest == null || quest.State != RuntimeQuestState.CanComplete)
				return;
			quest.Complete();
		}
	}
}
