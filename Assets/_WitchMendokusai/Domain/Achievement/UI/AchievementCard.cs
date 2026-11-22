using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 격자의 카드 하나. 흰 테두리 칸에 메달, 이름, 단계 마름모 (동 은 금), 아래 한 줄 (날짜나 다음 단계 진행).
	/// 숨김 업적은 달성 전 빈 칸 메달과 ??? 만, 조건과 진행 없음
	/// </summary>
	public class AchievementCard : Button
	{
		public const string USS_CLASS = "wm-ach-card";
		public const string USS_HIDDEN = "wm-ach-card--hidden";
		public const string USS_NAME = "wm-ach-card__name";
		public const string USS_LINE = "wm-ach-card__line";
		public const string USS_PIPS = "wm-ach-pips";
		public const string USS_PIP = "wm-ach-pip";
		public const string USS_PIP_TIER_PREFIX = "wm-ach-pip--";
		public const string HIDDEN_NAME = "???";
		public const string HIDDEN_LINE = "숨겨진 업적";

		private readonly AchievementMedal medal;
		private readonly Label nameLabel;
		private readonly VisualElement pips;
		private readonly Label line;

		public AchievementSO Achievement { get; private set; }

		public AchievementCard()
		{
			AddToClassList(USS_CLASS);

			medal = new AchievementMedal("wm-ach-medal--card");
			Add(medal);

			nameLabel = new Label { pickingMode = PickingMode.Ignore };
			nameLabel.AddToClassList(USS_NAME);
			Add(nameLabel);

			pips = new VisualElement { pickingMode = PickingMode.Ignore };
			pips.AddToClassList(USS_PIPS);
			Add(pips);

			line = new Label { pickingMode = PickingMode.Ignore };
			line.AddToClassList(USS_LINE);
			Add(line);
		}

		public void Bind(AchievementSO achievement, AchievementBook book, int currentValue)
		{
			Achievement = achievement;
			int tier = book.TierOf(achievement.ID);
			bool concealed = achievement.Hidden && tier == 0;

			EnableInClassList(USS_HIDDEN, concealed);
			medal.Show(achievement, tier);
			nameLabel.text = concealed ? HIDDEN_NAME : achievement.Name;
			FillPips(pips, achievement.MaxTier, tier);

			if (concealed)
			{
				line.text = HIDDEN_LINE;
			}
			else if (tier >= achievement.MaxTier)
			{
				line.text = book.LatestDateOf(achievement.ID) ?? string.Empty;
			}
			else
			{
				line.text = $"{currentValue} / {achievement.TierTargets[tier]}";
			}
		}

		/// <summary>단계 마름모 줄. 닿은 단계는 그 단계 색 (wm-ach-pip--1..3), 나머지는 빈 테두리. 상세 창도 같이 씀</summary>
		public static void FillPips(VisualElement container, int maxTier, int tier)
		{
			container.Clear();
			for (int i = 1; i <= maxTier; i++)
			{
				VisualElement pip = new() { pickingMode = PickingMode.Ignore };
				pip.AddToClassList(USS_PIP);
				if (i <= tier)
				{
					pip.AddToClassList(USS_PIP_TIER_PREFIX + i);
				}
				container.Add(pip);
			}
		}
	}
}
