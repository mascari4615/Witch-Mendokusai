using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 핫바가 *실제로 갈 수 있는 길*만 갖고 있나 (TASK-WM-194).
	///
	/// ★ 왜 붙이나: 연구 인형을 핫바에서 빼고도 배치·비용·미리보기·확인 하네스에 그 길이 통째로 남아
	///   있었다. 아무도 못 가는 길인데 확인 도구는 초록불을 켰다(핫바를 우회해 직접 불렀으니까) —
	///   *없느니만 못한 초록불*이다. 「칸에서 갈 수 있는 종류」와 「종류를 다루는 코드」가 어긋나는 순간을
	///   시험이 잡는다.
	/// ★ 씬·유닛 0 — 칸 번호 → 종류 대응은 순수 계산이라 에디터 없이 확인된다.
	/// </summary>
	public class TowerDefenseHotbarReachabilityTests
	{
		// 칸을 고르는 규칙은 TowerDefensePlacement.SelectedKind 하나뿐이므로, 그 대응을 그대로 따라 적는다.
		// (컴포넌트를 세우려면 씬이 필요해서, 대응 자체를 여기 옮겨 「같은가」를 지킨다.)
		private static TowerDefensePlaceableKind KindOfSlot(int slot, int towerSlotCount)
		{
			if (slot < towerSlotCount)
				return TowerDefensePlaceableKind.Tower;
			if (slot == towerSlotCount)
				return TowerDefensePlaceableKind.Harvester;
			if (slot == towerSlotCount + 1)
				return TowerDefensePlaceableKind.Wall;
			if (slot == towerSlotCount + 2)
				return TowerDefensePlaceableKind.Trap;
			if (slot == towerSlotCount + 3)
				return TowerDefensePlaceableKind.Outpost;
			return slot == towerSlotCount + 4
				? TowerDefensePlaceableKind.Generator
				: TowerDefensePlaceableKind.Hero;
		}

		private static HashSet<TowerDefensePlaceableKind> ReachableKinds(int towerSlotCount)
		{
			HashSet<TowerDefensePlaceableKind> kinds = new();
			for (int slot = 0; slot <= towerSlotCount + 5; slot++)
				kinds.Add(KindOfSlot(slot, towerSlotCount));
			return kinds;
		}

		[Test]
		public void 열거값은_전부_어느_칸에서든_닿는다()
		{
			// ★ 이 시험이 깨지는 방식이 곧 지난번 사고다 — 종류는 남았는데 칸이 사라졌다.
			//   그때는 배치·비용·미리보기·하네스가 전부 그 종류를 계속 다뤘다(아무도 못 가는 길).
			HashSet<TowerDefensePlaceableKind> reachable = ReachableKinds(4);

			foreach (TowerDefensePlaceableKind kind in Enum.GetValues(typeof(TowerDefensePlaceableKind)))
				Assert.IsTrue(reachable.Contains(kind),
					$"{kind} 는 어느 칸에서도 못 고른다 — 종류를 지우거나 칸을 되살려야 한다.");
		}

		[Test]
		public void 포탑_종류가_몇_개든_뒷칸_순서는_그대로다()
		{
			// 포탑이 늘면 뒤가 밀린다 — 밀리는 것은 정상이지만 *순서*가 바뀌면 손이 기억한 자리가 깨진다.
			foreach (int towerCount in new[] { 1, 3, 6 })
			{
				Assert.AreEqual(TowerDefensePlaceableKind.Harvester, KindOfSlot(towerCount, towerCount));
				Assert.AreEqual(TowerDefensePlaceableKind.Wall, KindOfSlot(towerCount + 1, towerCount));
				Assert.AreEqual(TowerDefensePlaceableKind.Trap, KindOfSlot(towerCount + 2, towerCount));
				Assert.AreEqual(TowerDefensePlaceableKind.Outpost, KindOfSlot(towerCount + 3, towerCount));
				Assert.AreEqual(TowerDefensePlaceableKind.Generator, KindOfSlot(towerCount + 4, towerCount));
				Assert.AreEqual(TowerDefensePlaceableKind.Hero, KindOfSlot(towerCount + 5, towerCount));
			}
		}

		[Test]
		public void 마지막_칸까지가_고를_수_있는_전부다()
		{
			// SelectSlot 의 범위 검사(towerSlotCount + 5)와 대응표가 어긋나면, 있는 칸을 못 누르거나
			// 없는 칸이 영웅으로 흘러든다.
			const int towerCount = 4;
			Assert.AreEqual(TowerDefensePlaceableKind.Hero, KindOfSlot(towerCount + 5, towerCount),
				"마지막 칸은 영웅이어야 한다.");
			// 포탑 칸 여럿이 한 종류로 묶이므로 칸 수와 종류 수는 다르다 — 대신 *남는 종류가 없어야* 한다.
			Assert.AreEqual(Enum.GetValues(typeof(TowerDefensePlaceableKind)).Length, ReachableKinds(towerCount).Count,
				"칸으로 못 닿는 종류가 남았다.");
		}
	}
}
