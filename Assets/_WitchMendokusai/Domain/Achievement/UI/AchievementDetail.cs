using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 고른 업적의 상세. 큰 칸, 이름, 한 줄 사연, 조건, 단계 칸 (목표값), 다음 단계 진행, 달성 날짜.
	/// 숨김 업적은 달성 전 이름과 사연과 조건을 감춤
	/// </summary>
	public class AchievementDetail : VisualElement
	{
		public const string USS_CLASS = "wm-ach-detail";
		public const string USS_NAME = "wm-ach-detail__name";
		public const string USS_STORY = "wm-ach-detail__story";
		public const string USS_CONDITION = "wm-ach-detail__condition";
		public const string USS_TIERS = "wm-ach-detail__tiers";
		public const string USS_TIER = "wm-ach-detail__tier";
		public const string USS_TIER_DONE = "wm-ach-detail__tier--done";
		public const string USS_PROGRESS = "wm-ach-detail__progress";
		public const string USS_PROGRESS_FILL = "wm-ach-detail__progress-fill";
		public const string USS_NOTE = "wm-ach-detail__note";

		private const string HIDDEN_STORY = "아직 모르는 기록";
		private const string HIDDEN_CONDITION = "어떻게 얻는지 아직 모름";
		public const string EMPTY_TEXT = "업적을 고르면 여기에 보임";

		private readonly AchievementSlot slot;
		private readonly Label nameLabel;
		private readonly Label storyLabel;
		private readonly Label conditionLabel;
		private readonly VisualElement tiers;
		private readonly VisualElement progressFill;
		private readonly Label noteLabel;

		public AchievementDetail()
		{
			AddToClassList(USS_CLASS);

			slot = new AchievementSlot(withCaption: false);
			slot.pickingMode = PickingMode.Ignore;
			slot.focusable = false;
			Add(slot);

			nameLabel = AddLabel(USS_NAME);
			storyLabel = AddLabel(USS_STORY);
			conditionLabel = AddLabel(USS_CONDITION);

			tiers = new VisualElement();
			tiers.AddToClassList(USS_TIERS);
			Add(tiers);

			VisualElement progress = new();
			progress.AddToClassList(USS_PROGRESS);
			Add(progress);
			progressFill = new VisualElement();
			progressFill.AddToClassList(USS_PROGRESS_FILL);
			progress.Add(progressFill);

			noteLabel = AddLabel(USS_NOTE);
			Clear(EMPTY_TEXT);
		}

		public void Bind(AchievementSO achievement, AchievementBook book, int currentValue)
		{
			int tier = book.TierOf(achievement.ID);
			bool concealed = achievement.Hidden && tier == 0;

			slot.style.display = DisplayStyle.Flex;
			slot.Bind(achievement, tier);
			nameLabel.text = concealed ? AchievementSlot.HIDDEN_NAME : achievement.Name;
			storyLabel.text = concealed ? HIDDEN_STORY : achievement.Description;
			conditionLabel.text = concealed ? HIDDEN_CONDITION : achievement.Condition;

			tiers.Clear();
			tiers.style.display = concealed ? DisplayStyle.None : DisplayStyle.Flex;
			for (int i = 0; i < achievement.MaxTier; i++)
			{
				Label tierLabel = new(achievement.TierTargets[i].ToString());
				tierLabel.AddToClassList(USS_TIER);
				tierLabel.EnableInClassList(USS_TIER_DONE, i < tier);
				tiers.Add(tierLabel);
			}

			bool maxed = tier >= achievement.MaxTier;
			int nextTarget = maxed ? achievement.TierTargets[achievement.MaxTier - 1] : achievement.TierTargets[tier];
			float ratio = maxed ? 1f : (float)currentValue / nextTarget;
			progressFill.style.width = Length.Percent(concealed ? 0f : UnityEngine.Mathf.Clamp01(ratio) * 100f);

			string date = book.UnlockedOnOf(achievement.ID);
			string progressText = maxed ? "모든 단계 달성" : $"다음 단계 {currentValue} / {nextTarget}";
			noteLabel.text = concealed ? string.Empty : (date == null ? progressText : $"{progressText}\n{tier}단계에 닿은 날 {date}");
		}

		public void Clear(string message)
		{
			slot.style.display = DisplayStyle.None;
			nameLabel.text = string.Empty;
			storyLabel.text = message;
			conditionLabel.text = string.Empty;
			tiers.Clear();
			progressFill.style.width = Length.Percent(0f);
			noteLabel.text = string.Empty;
		}

		private Label AddLabel(string ussClass)
		{
			Label label = new();
			label.AddToClassList(ussClass);
			Add(label);
			return label;
		}
	}
}
