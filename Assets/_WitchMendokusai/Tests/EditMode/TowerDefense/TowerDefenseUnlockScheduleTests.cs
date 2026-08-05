using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 연구가 약속한 것과 실제로 열리는 것이 같은가 (TASK-WM-194 / WM-200).
	///
	/// ★ 왜 못 박나: 연구 창은 「다음 단계에 이게 열립니다」라고 말하고, 규칙층은 「지금 쓸 수 있는
	///   칸」을 따로 만든다. 둘이 어긋나면 플레이어는 열리지도 않을 것을 보고 자원을 쓴다 —
	///   화면이 거짓말한 것이라 그 뒤로 아무 표시도 못 믿게 된다. 같은 표에서 나오는지 여기서 묻는다.
	/// </summary>
	public class TowerDefenseUnlockScheduleTests
	{
		private static TowerDefenseUnlockLevels Levels() =>
			new TowerDefenseUnlockLevels(tower: 1, wall: 2, trap: 3, generator: 4, outpost: 5, towerVariantStep: 2);

		private static List<TowerDefenseUnlockEntry> BuildAll(int archetypes = 3)
		{
			List<TowerDefenseUnlockEntry> entries = new();
			TowerDefenseUnlockSchedule.Build(Levels(), archetypes, entries);
			return entries;
		}

		private static List<TowerDefenseSlot> AvailableAt(int researchLevel, int archetypes = 3)
		{
			List<TowerDefenseSlot> slots = new();
			TowerDefenseUnlockSchedule.Available(Levels(), archetypes, researchLevel, new List<TowerDefenseUnlockEntry>(), slots);
			return slots;
		}

		[Test]
		public void 처음엔_채집만_열려_있다()
		{
			List<TowerDefenseSlot> slots = AvailableAt(0);

			Assert.AreEqual(1, slots.Count, "첫 판에 고를 수 있는 것이 하나가 아니다.");
			Assert.AreEqual(TowerDefensePlaceableKind.Harvester, slots[0].Kind);
		}

		[Test]
		public void 표가_약속한_단계에_실제로_열린다()
		{
			foreach (TowerDefenseUnlockEntry entry in BuildAll())
			{
				List<TowerDefenseSlot> before = AvailableAt(entry.Level - 1);
				List<TowerDefenseSlot> after = AvailableAt(entry.Level);

				bool inBefore = before.Exists(s => s.Kind == entry.Kind && s.TowerIndex == entry.TowerIndex);
				bool inAfter = after.Exists(s => s.Kind == entry.Kind && s.TowerIndex == entry.TowerIndex);

				Assert.IsTrue(inAfter, $"{entry.Kind}{entry.TowerIndex} 이 {entry.Level}단계에 열린다고 했는데 안 열렸다.");
				if (entry.Level > 0)
					Assert.IsFalse(inBefore, $"{entry.Kind}{entry.TowerIndex} 이 약속보다 일찍 열려 있다.");
			}
		}

		[Test]
		public void 포탑_종류는_간격만큼_띄워_열린다()
		{
			List<TowerDefenseUnlockEntry> towers = BuildAll().FindAll(e => e.Kind == TowerDefensePlaceableKind.Tower);

			Assert.AreEqual(3, towers.Count);
			Assert.AreEqual(1, towers[0].Level);
			Assert.AreEqual(3, towers[1].Level, "둘째 포탑이 간격(2)만큼 안 띄워졌다.");
			Assert.AreEqual(5, towers[2].Level);
		}

		[Test]
		public void 단계가_오르면_열린_것이_줄지_않는다()
		{
			int previous = 0;
			for (int level = 0; level <= 8; level++)
			{
				int count = AvailableAt(level).Count;
				Assert.GreaterOrEqual(count, previous, $"{level}단계에서 쓸 수 있는 것이 오히려 줄었다.");
				previous = count;
			}
		}

		[Test]
		public void 표는_단계_순서로_정렬된다()
		{
			List<TowerDefenseUnlockEntry> entries = BuildAll();

			for (int i = 1; i < entries.Count; i++)
				Assert.GreaterOrEqual(entries[i].Level, entries[i - 1].Level, "연구 길이 뒤죽박죽이면 읽을 수 없다.");
		}

		[Test]
		public void 포탑_종류가_없다고_해도_포탑_한_칸은_있다()
		{
			List<TowerDefenseSlot> slots = AvailableAt(9, archetypes: 0);

			Assert.IsTrue(slots.Exists(s => s.Kind == TowerDefensePlaceableKind.Tower),
				"포탑 종류 데이터가 비었다고 포탑 자체가 사라지면 판이 성립하지 않는다.");
		}
	}
}
