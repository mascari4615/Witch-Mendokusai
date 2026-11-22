using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 첫 화면, 메달 벽. 왼쪽 모은 수와 단계별 메달 수, 가운데 걸어 둔 메달 (마름모가 두 줄로 엇갈려 맞물림) 과
	/// 고른 메달의 이름 (ESC 메뉴 코어 아래 장소 표시와 같은 고리), 오른쪽 도감 칸과 최근 달성.
	/// 벽의 메달을 고르면 단계 상세, 도감 칸을 고르면 도감
	/// </summary>
	public class AchievementWallScreen : VisualElement
	{
		private const string USS_CLASS = "wm-ach-wall-screen";

		private readonly AchievementScreenSettings settings;
		private readonly MenuFocus focus;
		private readonly Action<AchievementSO> openDetail;
		private readonly Label countLabel;
		private readonly Label totalLabel;
		private readonly VisualElement countFill;
		private readonly List<(AchievementMedal Medal, Label Count)> tierRows = new();
		private readonly Label wallCaption;
		private readonly VisualElement wall;
		private readonly Label placeLabel;
		private readonly Label placeSubLabel;
		private readonly Label albumSubLabel;
		private readonly VisualElement recentList;
		private readonly Texture2D glowTexture;

		private AchievementManager manager;

		public Button AlbumButton { get; }

		public AchievementWallScreen(AchievementScreenSettings settings, MenuFocus focus, Action<AchievementSO> openDetail, Action openAlbum)
		{
			this.settings = settings;
			this.focus = focus;
			this.openDetail = openDetail;
			AddToClassList(USS_CLASS);

			// 왼쪽: 모은 수
			VisualElement left = AchievementElements.Part(this, "wm-ach-wall-screen__left");
			AchievementElements.Text(left, "모은 메달", "wm-ach-sum__label");
			VisualElement number = AchievementElements.Part(left, "wm-ach-sum__number");
			countLabel = AchievementElements.Text(number, string.Empty, "wm-ach-sum__count");
			totalLabel = AchievementElements.Text(number, string.Empty, "wm-ach-sum__total");
			countFill = AchievementElements.Bar(left, "wm-ach-sum__bar");
			AchievementElements.Text(left, "단계별 메달 수", "wm-ach-sum__label wm-ach-sum__label--tiers");
			for (int tier = AchievementTiers.MAX_TIER_COUNT; tier >= 1; tier--)
			{
				VisualElement row = AchievementElements.Part(left, "wm-ach-tier-row");
				AchievementMedal medal = new("wm-ach-medal--tier-row");
				row.Add(medal);
				VisualElement texts = AchievementElements.Part(row, "wm-ach-tier-row__texts");
				AchievementElements.Text(texts, AchievementTiers.NameOf(tier, AchievementTiers.MAX_TIER_COUNT), "wm-ach-tier-row__name wm-ach-tier--" + tier);
				AchievementElements.Text(texts, tier == AchievementTiers.MAX_TIER_COUNT ? "끝까지 올린 메달" : $"{AchievementTiers.NameOf(tier, AchievementTiers.MAX_TIER_COUNT)} 이상 올린 메달", "wm-ach-tier-row__desc");
				Label count = AchievementElements.Text(row, string.Empty, "wm-ach-tier-row__count");
				tierRows.Add((medal, count));
			}

			// 가운데: 벽
			VisualElement center = AchievementElements.Part(this, "wm-ach-wall-screen__center");
			wallCaption = AchievementElements.Text(center, string.Empty, "wm-ach-wall__caption");
			VisualElement glow = AchievementElements.Part(center, "wm-ach-wall__glow");
			glowTexture = MenuStage.CreateGlowTexture(settings.GlowTextureSize, "AchievementWallGlow");
			glow.style.backgroundImage = new StyleBackground(glowTexture);
			wall = AchievementElements.Part(center, "wm-ach-wall");
			VisualElement where = AchievementElements.Part(center, "wm-fullmenu-where wm-ach-wall__where");
			for (int i = 1; i <= 3; i++)
			{
				AchievementElements.Part(where, "wm-fullmenu-where__ring wm-fullmenu-where__ring--" + i);
			}
			placeLabel = AchievementElements.Text(center, string.Empty, "wm-fullmenu-place");
			placeSubLabel = AchievementElements.Text(center, string.Empty, "wm-fullmenu-place-sub");

			// 오른쪽: 도감 칸, 최근 달성
			VisualElement right = AchievementElements.Part(this, "wm-ach-wall-screen__right");
			AlbumButton = new Button(openAlbum);
			AlbumButton.AddToClassList("wm-ach-album-tile");
			VisualElement albumIcon = AchievementElements.Part(AlbumButton, "wm-ach-album-tile__icon");
			albumIcon.style.backgroundImage = new StyleBackground(settings.AlbumIcon);
			VisualElement albumTexts = AchievementElements.Part(AlbumButton, "wm-ach-album-tile__texts");
			AchievementElements.Text(albumTexts, "업적 도감", "wm-ach-album-tile__label");
			albumSubLabel = AchievementElements.Text(albumTexts, string.Empty, "wm-ach-album-tile__desc");
			focus.Register(AlbumButton);
			right.Add(AlbumButton);
			VisualElement recent = AchievementElements.Part(right, "wm-ach-recent");
			AchievementElements.Text(recent, "최근 달성", "wm-ach-recent__title");
			recentList = AchievementElements.Part(recent, "wm-ach-recent__list");
		}

		/// <summary>처음 고를 칸. 걸린 메달이 있으면 첫 메달, 없으면 도감 칸</summary>
		public VisualElement FirstFocus => wall.Q<Button>() ?? AlbumButton;

		public void Bind(AchievementManager achievementManager)
		{
			manager = achievementManager;
			AchievementBook book = manager.Book;
			IReadOnlyList<AchievementSO> achievements = manager.Achievements;

			countLabel.text = book.UnlockedCount.ToString();
			totalLabel.text = "/ " + achievements.Count;
			countFill.style.width = Length.Percent(achievements.Count == 0 ? 0f : 100f * book.UnlockedCount / achievements.Count);

			// 단계별 줄의 그림은 단계가 셋인 첫 업적의 그 단계 메달
			AchievementSO sample = null;
			foreach (AchievementSO achievement in achievements)
			{
				if (achievement.MaxTier == AchievementTiers.MAX_TIER_COUNT)
				{
					sample = achievement;
					break;
				}
			}
			for (int i = 0; i < tierRows.Count; i++)
			{
				int tier = AchievementTiers.MAX_TIER_COUNT - i;
				tierRows[i].Count.text = book.CountAtLeast(tier).ToString();
				tierRows[i].Medal.style.display = sample != null ? DisplayStyle.Flex : DisplayStyle.None;
				if (sample != null)
				{
					tierRows[i].Medal.Show(sample, tier);
				}
			}

			BuildWall(achievements, book);
			albumSubLabel.text = $"알려진 {achievements.Count}개 중 {book.UnlockedCount}개";
			BuildRecent(achievements, book);
		}

		private void BuildWall(IReadOnlyList<AchievementSO> achievements, AchievementBook book)
		{
			int slotCount = settings.WallSlotCount;
			List<int> ordered = new();
			foreach (AchievementSO achievement in achievements)
			{
				ordered.Add(achievement.ID);
			}
			manager.Wall.Refresh(ordered, book, slotCount);
			IReadOnlyList<int> pins = manager.Wall.Pins;
			wallCaption.text = $"걸어 둔 메달 {pins.Count} / {slotCount}칸";

			wall.Clear();
			int topRow = (slotCount + 1) / 2;
			wall.style.width = (topRow - 1) * settings.WallSlotStep;
			wall.style.height = settings.WallRowStep;
			for (int index = 0; index < slotCount; index++)
			{
				bool top = index < topRow;
				int column = top ? index : index - topRow;
				float left = column * settings.WallSlotStep + (top ? 0f : settings.WallSlotStep * 0.5f);
				float topOffset = top ? 0f : settings.WallRowStep;
				VisualElement slot = index < pins.Count ? MakePinnedSlot(SOHelper.Get<AchievementSO>(pins[index]), book) : MakeSocket(achievements);
				slot.style.left = left;
				slot.style.top = topOffset;
				wall.Add(slot);
			}
			ShowPlace(pins.Count > 0 ? SOHelper.Get<AchievementSO>(pins[0]) : null, book);
		}

		private VisualElement MakePinnedSlot(AchievementSO achievement, AchievementBook book)
		{
			Button slot = new(() => openDetail(achievement));
			slot.AddToClassList("wm-ach-wall__slot");
			AchievementMedal medal = new("wm-ach-medal--wall");
			medal.Show(achievement, book.TierOf(achievement.ID));
			slot.Add(medal);
			slot.RegisterCallback<FocusInEvent>(_ => ShowPlace(achievement, manager.Book));
			focus.Register(slot);
			return slot;
		}

		private static VisualElement MakeSocket(IReadOnlyList<AchievementSO> achievements)
		{
			VisualElement socket = new() { pickingMode = PickingMode.Ignore };
			socket.AddToClassList("wm-ach-wall__slot");
			socket.AddToClassList("wm-ach-wall__slot--socket");
			AchievementMedal medal = new("wm-ach-medal--wall");
			medal.ShowSocket(achievements[0].Category.EmptySlot);
			socket.Add(medal);
			return socket;
		}

		// 고른 메달의 이름과 다음 단계. 걸린 메달이 없으면 안내 한 줄
		private void ShowPlace(AchievementSO achievement, AchievementBook book)
		{
			if (achievement == null)
			{
				placeLabel.text = "걸어 둔 메달이 없음";
				placeSubLabel.text = "도감에서 메달을 골라 걸 수 있음";
				return;
			}
			int tier = book.TierOf(achievement.ID);
			string tierName = AchievementTiers.NameOf(tier, achievement.MaxTier);
			placeLabel.text = string.IsNullOrEmpty(tierName) ? achievement.Name : $"{achievement.Name} {tierName}";
			string date = book.LatestDateOf(achievement.ID);
			string next = tier >= achievement.MaxTier ? "모든 단계 달성" : $"다음 단계 {manager.CurrentValue(achievement)} / {achievement.TierTargets[tier]}";
			placeSubLabel.text = date == null ? next : $"{date} 달성, {next}";
		}

		private void BuildRecent(IReadOnlyList<AchievementSO> achievements, AchievementBook book)
		{
			List<AchievementSO> unlocked = new();
			foreach (AchievementSO achievement in achievements)
			{
				if (book.TierOf(achievement.ID) > 0)
				{
					unlocked.Add(achievement);
				}
			}
			// yyyy-MM-dd 는 글자 순서가 곧 날짜 순서. 날짜를 모르는 것은 뒤로
			unlocked.Sort((left, right) => string.CompareOrdinal(book.LatestDateOf(right.ID) ?? string.Empty, book.LatestDateOf(left.ID) ?? string.Empty));

			recentList.Clear();
			for (int i = 0; i < unlocked.Count && i < settings.RecentCount; i++)
			{
				AchievementSO achievement = unlocked[i];
				int tier = book.TierOf(achievement.ID);
				VisualElement row = AchievementElements.Part(recentList, "wm-ach-recent__row");
				AchievementMedal medal = new("wm-ach-medal--recent");
				medal.Show(achievement, tier);
				row.Add(medal);
				VisualElement texts = AchievementElements.Part(row, "wm-ach-recent__texts");
				VisualElement nameLine = AchievementElements.Part(texts, "wm-ach-recent__name-line");
				AchievementElements.Text(nameLine, achievement.Name, "wm-ach-recent__name");
				AchievementElements.Text(nameLine, AchievementTiers.NameOf(tier, achievement.MaxTier), "wm-ach-recent__tier wm-ach-tier--" + tier);
				AchievementElements.Text(texts, book.LatestDateOf(achievement.ID) ?? string.Empty, "wm-ach-recent__date");
			}
			if (unlocked.Count == 0)
			{
				AchievementElements.Text(recentList, "아직 없음", "wm-ach-recent__date");
			}
		}

		public void Dispose() => UnityEngine.Object.Destroy(glowTexture);
	}
}
