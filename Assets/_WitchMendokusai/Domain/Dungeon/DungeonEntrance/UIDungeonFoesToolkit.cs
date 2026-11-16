using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 입장 창 "출현 적" 줄. 웨이브와 사건표의 몬스터를 처음 나오는 시각 순으로, 보스와 정예는 표시
	/// 칸에 마우스를 올리면 몬스터 툴팁
	/// </summary>
	public class UIDungeonFoesToolkit : VisualElement
	{
		public const string USS_CLASS = "wm-dungeon-foes";
		private const string USS_FOE = USS_CLASS + "__foe";
		private const string USS_PIC = USS_CLASS + "__pic";
		private const string USS_IMAGE = USS_CLASS + "__image";
		private const string USS_TAG = USS_CLASS + "__tag";
		private const string USS_NAME = USS_CLASS + "__name";
		private const string USS_TIME = USS_CLASS + "__time";
		private const string USS_BOSS = USS_FOE + "--boss";
		private const int MAX_FOES = 4;
		private const float SECONDS_PER_MINUTE = 60f;

		private readonly struct FoeEntry
		{
			public readonly Monster Monster;
			public readonly float FirstSeconds;
			public readonly bool IsElite;

			public FoeEntry(Monster monster, float firstSeconds, bool isElite)
			{
				Monster = monster;
				FirstSeconds = firstSeconds;
				IsElite = isElite;
			}
		}

		private readonly List<VisualElement> foeViews = new();
		private readonly List<Image> foeImages = new();
		private readonly List<Label> foeTags = new();
		private readonly List<Label> foeNames = new();
		private readonly List<Label> foeTimes = new();
		private readonly List<FoeEntry> entries = new();
		private readonly SlotData tooltipData = new();

		public UIDungeonFoesToolkit()
		{
			AddToClassList(USS_CLASS);
		}

		public void UpdateUI(Dungeon dungeon)
		{
			CollectEntries(dungeon);
			EnsureViewCount(entries.Count);

			for (int i = 0; i < foeViews.Count; i++)
			{
				bool isUsed = i < entries.Count;
				foeViews[i].style.display = isUsed ? DisplayStyle.Flex : DisplayStyle.None;
				if (isUsed == false)
					continue;

				FoeEntry entry = entries[i];
				bool isBoss = entry.Monster.Type == MonsterType.Boss;
				foeImages[i].sprite = entry.Monster.Sprite;
				foeNames[i].text = entry.Monster.Name;
				foeTimes[i].text = entry.FirstSeconds <= 0f ? "처음부터" : Mathf.RoundToInt(entry.FirstSeconds / SECONDS_PER_MINUTE) + "분";
				foeTags[i].text = isBoss ? "보스" : entry.IsElite ? "정예" : string.Empty;
				foeTags[i].style.display = isBoss || entry.IsElite ? DisplayStyle.Flex : DisplayStyle.None;
				foeViews[i].EnableInClassList(USS_BOSS, isBoss);
			}
		}

		private void CollectEntries(Dungeon dungeon)
		{
			entries.Clear();
			Dictionary<Monster, FoeEntry> firstSeen = new();

			if (dungeon.MonsterWaves != null)
			{
				foreach (MonsterWave wave in dungeon.MonsterWaves)
				{
					if (wave.Monsters == null)
						continue;
					foreach (Monster monster in wave.Monsters)
						Remember(firstSeen, monster, wave.StartTime, false);
				}
			}

			if (dungeon.EventTable != null)
			{
				foreach (SurvivalEventEntry survivalEvent in dungeon.EventTable.Events)
					Remember(firstSeen, survivalEvent.Monster, survivalEvent.Time, survivalEvent.Type == SurvivalEventType.Elite);
			}

			entries.AddRange(firstSeen.Values);
			// 보스는 맨 뒤, 나머지는 처음 나오는 시각 순
			entries.Sort((left, right) =>
			{
				int bossOrder = (left.Monster.Type == MonsterType.Boss).CompareTo(right.Monster.Type == MonsterType.Boss);
				return bossOrder != 0 ? bossOrder : left.FirstSeconds.CompareTo(right.FirstSeconds);
			});
			if (entries.Count > MAX_FOES)
			{
				int bossIndex = entries.FindIndex(entry => entry.Monster.Type == MonsterType.Boss);
				FoeEntry? boss = bossIndex >= 0 ? entries[bossIndex] : null;
				entries.RemoveRange(MAX_FOES, entries.Count - MAX_FOES);
				if (boss.HasValue && entries.Contains(boss.Value) == false)
					entries[MAX_FOES - 1] = boss.Value;
			}
		}

		private static void Remember(Dictionary<Monster, FoeEntry> firstSeen, Monster monster, float seconds, bool isElite)
		{
			if (monster == null)
				return;
			if (firstSeen.TryGetValue(monster, out FoeEntry known) && known.FirstSeconds <= seconds)
				return;
			firstSeen[monster] = new FoeEntry(monster, seconds, isElite);
		}

		private void EnsureViewCount(int count)
		{
			while (foeViews.Count < count)
			{
				int index = foeViews.Count;
				VisualElement foe = new VisualElement { pickingMode = PickingMode.Position };
				foe.AddToClassList(USS_FOE);

				VisualElement pic = new VisualElement { pickingMode = PickingMode.Ignore };
				pic.AddToClassList(USS_PIC);
				foe.Add(pic);

				Image image = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
				image.AddToClassList(USS_IMAGE);
				pic.Add(image);

				Label tag = new Label { pickingMode = PickingMode.Ignore };
				tag.AddToClassList(USS_TAG);
				pic.Add(tag);

				Label name = new Label { pickingMode = PickingMode.Ignore };
				name.AddToClassList(USS_NAME);
				foe.Add(name);

				Label time = new Label { pickingMode = PickingMode.Ignore };
				time.AddToClassList(USS_TIME);
				foe.Add(time);

				foe.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(index, foe));
				foe.RegisterCallback<PointerLeaveEvent>(_ => foe.GetUIServices()?.Tooltip?.Hide());

				Add(foe);
				foeViews.Add(foe);
				foeImages.Add(image);
				foeTags.Add(tag);
				foeNames.Add(name);
				foeTimes.Add(time);
			}
		}

		private void ShowTooltip(int index, VisualElement owner)
		{
			if (index >= entries.Count)
				return;
			tooltipData.SetData(entries[index].Monster);
			owner.GetUIServices()?.Tooltip?.Show(tooltipData, owner: owner);
		}
	}
}
