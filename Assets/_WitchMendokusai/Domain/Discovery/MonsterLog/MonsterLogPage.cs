using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 한 종의 기록 장. 왼쪽 큰 그림과 장소 고리, 이름, 기록 마름모, 처치 수 셋. 오른쪽 기록 칸 다섯 (첫 기록, 사는 곳, 몸과 버릇, 전리품, 이야기).
	/// 칸은 확률 없이 누적 처치로 차례로 열리고, 잠긴 칸은 제목과 여는 데 필요한 수를 보임. 못 만난 종은 실루엣과 ???
	/// </summary>
	public class MonsterLogPage : VisualElement
	{
		public const int SLOT_COUNT = 5;
		private const int SLOT_FIRST_RECORD = 0;
		private const int SLOT_HABITAT = 1;
		private const int SLOT_BODY = 2;
		private const int SLOT_LOOT = 3;
		private const string NO_ATTACK_KIND = "쫓아옴";
		private static readonly string[] SLOT_NAMES = { "첫 기록", "사는 곳", "몸과 버릇", "전리품", "이야기" };

		private readonly MonsterLogManager manager;
		private readonly VisualElement picture;
		private readonly Label nameLabel;
		private readonly Label typeLabel;
		private readonly VisualElement pips;
		private readonly Label killsLabel;
		private readonly Label elitesLabel;
		private readonly Label defeatsLabel;
		private readonly VisualElement slots;

		public MonsterLogPage(MonsterLogManager manager, Texture2D glowTexture)
		{
			this.manager = manager;
			AddToClassList("wm-discovery-page");

			VisualElement left = AchievementElements.Part(this, "wm-discovery-page__left");
			VisualElement glow = AchievementElements.Part(left, "wm-discovery-page__glow");
			glow.style.backgroundImage = new StyleBackground(glowTexture);
			picture = AchievementElements.Part(left, "wm-discovery-page__picture");
			left.Add(MenuStage.CreateWhereRings());
			nameLabel = AchievementElements.Text(left, string.Empty, "wm-discovery-page__name");
			VisualElement sub = AchievementElements.Part(left, "wm-discovery-page__sub");
			typeLabel = AchievementElements.Text(sub, string.Empty, "wm-ach-chip");
			pips = AchievementElements.Part(sub, "wm-ach-pips wm-discovery-page__pips");
			VisualElement numbers = AchievementElements.Part(left, "wm-discovery-page__numbers");
			killsLabel = AddNumber(numbers, "처치");
			elitesLabel = AddNumber(numbers, "정예 처치");
			defeatsLabel = AddNumber(numbers, "쓰러진 횟수");

			slots = AchievementElements.Part(this, "wm-discovery-page__slots");
		}

		private static Label AddNumber(VisualElement parent, string caption)
		{
			VisualElement box = AchievementElements.Part(parent, "wm-discovery-number");
			Label value = AchievementElements.Text(box, string.Empty, "wm-discovery-number__value");
			AchievementElements.Text(box, caption, "wm-discovery-number__caption");
			return value;
		}

		public void Bind(Monster monster)
		{
			bool met = manager.Book.IsMet(monster.ID);
			int stage = manager.StageOf(monster);
			IReadOnlyList<int> targets = manager.TargetsOf(monster);
			MonsterLogRecord record = manager.Book.Find(monster.ID);

			picture.style.backgroundImage = new StyleBackground(monster.Sprite);
			picture.EnableInClassList("wm-discovery-page__picture--locked", met == false);
			picture.style.unityBackgroundImageTintColor = met ? new StyleColor(monster.Tint) : new StyleColor(StyleKeyword.Null);
			nameLabel.text = met ? monster.Name : DiscoveryScreenCard.HIDDEN_NAME;
			typeLabel.text = MonsterDiscoveryCategory.TypeLabelOf(monster);
			DiscoveryScreenCard.FillPips(pips, stage, targets.Count);
			killsLabel.text = $"{record?.Kills ?? 0}";
			elitesLabel.text = $"{record?.EliteKills ?? 0}";
			defeatsLabel.text = $"{record?.Defeats ?? 0}";

			slots.Clear();
			for (int slot = 0; slot < SLOT_COUNT; slot++)
			{
				AddSlot(slot, monster, record, stage, targets, met);
			}
		}

		private void AddSlot(int slot, Monster monster, MonsterLogRecord record, int stage, IReadOnlyList<int> targets, bool met)
		{
			bool isOpen = slot < stage;
			VisualElement row = AchievementElements.Part(slots, "wm-discovery-slot");
			row.EnableInClassList("wm-discovery-slot--open", isOpen);
			row.EnableInClassList("wm-discovery-slot--complete", stage >= targets.Count);
			row.EnableInClassList("wm-discovery-slot--next", met && slot == stage);
			AchievementElements.Part(row, "wm-discovery-slot__mark");
			AchievementElements.Text(row, SLOT_NAMES[slot], "wm-discovery-slot__name");
			VisualElement body = AchievementElements.Part(row, "wm-discovery-slot__body");

			if (isOpen == false)
			{
				FillLocked(body, record?.Kills ?? 0, targets[slot], met);
				return;
			}
			switch (slot)
			{
				case SLOT_FIRST_RECORD:
					AddField(body, "처음 잡은 날", record.FirstKilledOn);
					AddField(body, "처음 잡은 곳", SOHelper.TryGet(record.FirstDungeonId, out Dungeon dungeon) ? dungeon.Name : "알 수 없음");
					break;
				case SLOT_HABITAT:
					FillHabitat(body, monster);
					break;
				case SLOT_BODY:
					FillBody(body, monster);
					break;
				case SLOT_LOOT:
					FillLoot(body, monster);
					break;
				default:
					FillStory(body, monster);
					break;
			}
		}

		private static void FillLocked(VisualElement body, int kills, int target, bool met)
		{
			if (met == false)
			{
				AchievementElements.Text(body, $"처치 {target}", "wm-discovery-slot__need");
				return;
			}
			VisualElement fill = AchievementElements.Bar(body, "wm-discovery-slot__bar");
			fill.style.width = Length.Percent(100f * Mathf.Min(kills, target) / target);
			AchievementElements.Text(body, $"{kills} / {target}", "wm-discovery-slot__need");
		}

		private static void AddField(VisualElement body, string caption, string value)
		{
			VisualElement field = AchievementElements.Part(body, "wm-discovery-field");
			AchievementElements.Text(field, caption, "wm-discovery-field__caption");
			AchievementElements.Text(field, value, "wm-discovery-field__value");
		}

		// 던전마다 한 줄. 막대는 던전 시간 안의 나오는 구간
		private void FillHabitat(VisualElement body, Monster monster)
		{
			VisualElement list = AchievementElements.Part(body, "wm-discovery-habitats");
			Dictionary<Dungeon, VisualElement> tracks = new();
			foreach (MonsterHabitat habitat in manager.HabitatsOf(monster))
			{
				if (tracks.TryGetValue(habitat.Dungeon, out VisualElement track) == false)
				{
					VisualElement line = AchievementElements.Part(list, "wm-discovery-habitat");
					AchievementElements.Text(line, habitat.Dungeon.Name, "wm-discovery-habitat__name");
					track = AchievementElements.Part(line, "wm-discovery-habitat__track");
					AchievementElements.Text(line, habitat.HasTime ? TimeText(habitat) : "사건", "wm-discovery-habitat__time");
					tracks[habitat.Dungeon] = track;
				}
				if (habitat.HasTime)
				{
					AddSpan(track, habitat);
				}
			}
		}

		private static void AddSpan(VisualElement track, MonsterHabitat habitat)
		{
			float total = Mathf.Max(1f, habitat.Dungeon.TimeBySecond);
			float start = Mathf.Clamp01(habitat.StartSeconds / total);
			float end = Mathf.Clamp01(habitat.EndSeconds / total);
			VisualElement span = AchievementElements.Part(track, "wm-discovery-habitat__span");
			// 한 번 나오는 종은 폭이 0 이라 USS 최소 폭으로 점처럼 보임. 끝 시각의 점은 트랙 밖으로 안 나가게 오른쪽에서 잡음
			if (start < end)
			{
				span.style.left = Length.Percent(100f * start);
				span.style.width = Length.Percent(100f * (end - start));
			}
			else if (start < 0.5f)
			{
				span.style.left = Length.Percent(100f * start);
			}
			else
			{
				span.style.right = Length.Percent(100f * (1f - start));
			}
		}

		private static string TimeText(MonsterHabitat habitat)
		{
			string start = Clock(habitat.StartSeconds);
			if (habitat.EndSeconds <= habitat.StartSeconds)
			{
				return start;
			}
			return habitat.EndSeconds >= habitat.Dungeon.TimeBySecond ? $"{start} 부터" : $"{start} ~ {Clock(habitat.EndSeconds)}";
		}

		private static string Clock(float seconds)
		{
			TimeSpan time = TimeSpan.FromSeconds(seconds);
			return $"{(int)time.TotalMinutes}:{time.Seconds:00}";
		}

		private void FillBody(VisualElement body, Monster monster)
		{
			UnitStat unitStat = manager.BaseStatOf(monster);
			AddField(body, "체력", $"{unitStat[UnitStatType.HP_MAX_STAT]}");
			AddField(body, "빠르기", $"{unitStat[UnitStatType.MOVEMENT_SPEED]}");
			// 보스의 공격은 Attack 자산이 아니라 자기 상태기계에 있어 비어 있어도 "쫓아옴" 이 아님
			if (monster.Attack != null)
			{
				AddField(body, "버릇", monster.Attack.KindName);
			}
			else if (monster.Type != MonsterType.Boss)
			{
				AddField(body, "버릇", NO_ATTACK_KIND);
			}
		}

		private static void FillLoot(VisualElement body, Monster monster)
		{
			if (monster.Loots == null || monster.Loots.Count == 0)
			{
				AchievementElements.Text(body, "떨어뜨리는 것 없음", "wm-discovery-slot__need");
				return;
			}
			foreach (DataSOWithPercentage loot in monster.Loots)
			{
				VisualElement cell = AchievementElements.Part(body, "wm-discovery-loot");
				VisualElement icon = AchievementElements.Part(cell, "wm-discovery-loot__icon");
				icon.style.backgroundImage = new StyleBackground(loot.DataSO.Sprite);
				VisualElement texts = AchievementElements.Part(cell, "wm-discovery-loot__texts");
				AchievementElements.Text(texts, loot.DataSO.Name, "wm-discovery-field__value");
				AchievementElements.Text(texts, $"{loot.Percentage:0.##}%", "wm-discovery-field__caption");
			}
		}

		// 글은 자산의 Description. 비어 있으면 빈 자리임을 그대로 보임 (글은 사람 몫)
		private static void FillStory(VisualElement body, Monster monster)
		{
			bool isWritten = string.IsNullOrWhiteSpace(monster.Description) == false;
			AchievementElements.Text(body, isWritten ? monster.Description : "아직 적히지 않은 장", isWritten ? "wm-discovery-slot__story" : "wm-discovery-slot__need");
		}
	}
}
