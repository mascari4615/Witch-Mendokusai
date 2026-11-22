using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 달성 띠. 화면 아래 가운데의 작은 띠 하나, 조작을 막지 않음 (pickingMode Ignore).
	/// 보이고 숨기는 시간은 띄우는 쪽 (AchievementView) 이 정하고, 움직임은 wm-ach-toast--shown USS transition
	/// </summary>
	public class AchievementToast : VisualElement
	{
		public const string USS_CLASS = "wm-ach-toast";
		public const string USS_SHOWN = "wm-ach-toast--shown";
		public const string USS_TEXT = "wm-ach-toast__text";
		public const string USS_KIND = "wm-ach-toast__kind";
		public const string USS_NAME = "wm-ach-toast__name";

		private readonly AchievementMedal medal;
		private readonly Label kindLabel;
		private readonly Label nameLabel;

		public AchievementToast()
		{
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Ignore;

			medal = new AchievementMedal("wm-ach-medal--toast");
			Add(medal);

			VisualElement text = new();
			text.AddToClassList(USS_TEXT);
			text.pickingMode = PickingMode.Ignore;
			Add(text);

			kindLabel = new Label();
			kindLabel.AddToClassList(USS_KIND);
			kindLabel.pickingMode = PickingMode.Ignore;
			text.Add(kindLabel);

			nameLabel = new Label();
			nameLabel.AddToClassList(USS_NAME);
			nameLabel.pickingMode = PickingMode.Ignore;
			text.Add(nameLabel);
		}

		public void Show(AchievementSO achievement, int tier)
		{
			medal.Show(achievement, tier);
			string tierName = AchievementTiers.NameOf(tier, achievement.MaxTier);
			kindLabel.text = "메달 하나 더";
			nameLabel.text = string.IsNullOrEmpty(tierName) ? achievement.Name : $"{achievement.Name} {tierName}";
			AddToClassList(USS_SHOWN);
		}

		public void Hide() => RemoveFromClassList(USS_SHOWN);
	}
}
