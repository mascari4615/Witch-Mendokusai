using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 격자의 카드 하나. 업적 카드와 같은 칸 (wm-ach-card) 에 그림, 이름, 기록 마름모, 아래 한 줄.
	/// 못 만난 것은 검은 실루엣과 ???, 새 기록은 왼쪽 위 NEW, 다 채우면 금테와 오른쪽 위 도장
	/// </summary>
	public class DiscoveryScreenCard : Button
	{
		public const string HIDDEN_NAME = "???";
		private const string USS_LOCKED = "wm-discovery-tile--locked";
		private const string USS_COMPLETE = "wm-discovery-tile--complete";
		private const string USS_STAMP = "wm-discovery-stamp wm-discovery-stamp--card";

		public EntryDescriptor Entry { get; }

		private readonly VisualElement pips;
		private readonly Label line;
		private readonly Label news;

		public DiscoveryScreenCard(EntryDescriptor entry)
		{
			Entry = entry;
			AchievementElements.AddClasses(this, "wm-ach-card wm-discovery-tile");
			EnableInClassList(USS_LOCKED, entry.IsUnlocked == false);

			VisualElement picture = AchievementElements.Part(this, "wm-discovery-tile__picture");
			if (entry.Icon != null)
			{
				picture.style.backgroundImage = new StyleBackground(entry.Icon);
			}
			// 색 곱이 있는 변종 (박치기, 씨앗). 못 만난 것의 실루엣은 USS 의 검정
			if (entry.IsUnlocked && entry.Source is Monster monster)
			{
				picture.style.unityBackgroundImageTintColor = monster.Tint;
			}
			AchievementElements.Text(this, entry.IsUnlocked ? entry.DisplayName : HIDDEN_NAME, "wm-ach-card__name");
			pips = AchievementElements.Part(this, "wm-ach-pips");
			line = AchievementElements.Text(this, string.Empty, "wm-ach-card__line");
			news = AchievementElements.Text(this, "NEW", "wm-discovery-news");
			news.style.display = DisplayStyle.None;
		}

		public void Bind(DiscoveryRecordInfo record)
		{
			FillPips(pips, record.Stage, record.MaxStage);
			line.text = record.Line;
			news.style.display = record.HasNews ? DisplayStyle.Flex : DisplayStyle.None;
			EnableInClassList(USS_COMPLETE, record.IsComplete);
		}

		/// <summary>다 채운 칸에만 찍음</summary>
		public void AddStamp(RenderTexture texture) => ShowStamp(AchievementElements.Part(this, USS_STAMP), texture);

		/// <summary>도장 하나를 보이거나, null 이면 숨김. 한 종의 장도 같이 씀</summary>
		public static void ShowStamp(VisualElement stamp, RenderTexture texture)
		{
			stamp.style.backgroundImage = texture != null ? new StyleBackground(Background.FromRenderTexture(texture)) : new StyleBackground(StyleKeyword.None);
		}

		/// <summary>기록 칸 마름모. 채운 칸은 보라, 다 채우면 모두 금색. 한 종의 장과 띠도 같이 씀</summary>
		public static void FillPips(VisualElement container, int stage, int maxStage)
		{
			container.Clear();
			for (int i = 0; i < maxStage; i++)
			{
				VisualElement pip = AchievementElements.Part(container, "wm-ach-pip");
				if (i < stage)
				{
					pip.AddToClassList(stage >= maxStage ? "wm-discovery-pip--complete" : "wm-discovery-pip--on");
				}
			}
		}
	}
}
