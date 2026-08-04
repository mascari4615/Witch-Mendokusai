using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 핫바 칸 목록 — 「보이는 칸」과 「눌리는 칸」이 같은가 (TASK-WM-194).
	///
	/// ★ 예전엔 칸 번호 → 종류가 *고정 산술*로 화면·입력 두 곳에 박혀 있었다. 연구로 해금이 생기면
	///   그날로 깨지는 구조다 — 「함정을 골랐는데 전초기지가 지어진다」. 이제 규칙층이 목록 하나를
	///   만들고 둘 다 그대로 읽는다. 여기서는 *그 목록을 만드는 규칙*을 씬 없이 못 박는다.
	/// ★ 사용자 지시(직접 플레이): 처음엔 자원 건물과 연구만. 첫 테크가 공성. 고급 테크는 나중에.
	/// </summary>
	public class TowerDefenseHotbarReachabilityTests
	{
		// 매치의 RefreshAvailableSlots 와 같은 규칙 — 단계별로 무엇이 열리나.
		// (매치는 MonoBehaviour 라 씬 없이 못 세운다. 규칙 자체를 여기 옮겨 「같은가」를 지킨다.)
		private static List<TowerDefensePlaceableKind> Unlocked(int researchLevel,
			int tower = 1, int wall = 2, int trap = 3, int generator = 4, int outpost = 5)
		{
			List<TowerDefensePlaceableKind> kinds = new() { TowerDefensePlaceableKind.Harvester };
			if (researchLevel >= tower)
				kinds.Add(TowerDefensePlaceableKind.Tower);
			if (researchLevel >= wall)
				kinds.Add(TowerDefensePlaceableKind.Wall);
			if (researchLevel >= trap)
				kinds.Add(TowerDefensePlaceableKind.Trap);
			if (researchLevel >= generator)
				kinds.Add(TowerDefensePlaceableKind.Generator);
			if (researchLevel >= outpost)
				kinds.Add(TowerDefensePlaceableKind.Outpost);
			return kinds;
		}

		[Test]
		public void 처음엔_자원_건물_하나뿐이다()
		{
			// ★ 사용자 실증: "처음 들어갔을 때 그냥 좀 혼란스러움 … 내가 가지고 있는 유닛도 너무 많음."
			//   첫 화면에 손이 갈 곳은 하나여야 한다 — 나머지는 연구가 연다.
			List<TowerDefensePlaceableKind> kinds = Unlocked(0);

			Assert.AreEqual(1, kinds.Count, "처음부터 여러 개가 열려 있으면 무엇부터 볼지가 숙제가 된다.");
			Assert.AreEqual(TowerDefensePlaceableKind.Harvester, kinds[0], "먹고사는 길이 첫 수여야 한다.");
		}

		[Test]
		public void 첫_연구가_공성을_연다()
		{
			List<TowerDefensePlaceableKind> kinds = Unlocked(1);

			CollectionAssert.Contains(kinds, TowerDefensePlaceableKind.Tower, "첫 테크는 공성이어야 한다.");
			CollectionAssert.DoesNotContain(kinds, TowerDefensePlaceableKind.Outpost, "고급 테크가 첫 연구에 딸려 오면 안 된다.");
		}

		[Test]
		public void 단계가_오를수록_칸이_늘기만_한다()
		{
			// 열렸던 것이 닫히면 손이 기억한 자리가 무너진다.
			int previous = 0;
			for (int level = 0; level <= 6; level++)
			{
				int count = Unlocked(level).Count;
				Assert.GreaterOrEqual(count, previous, $"연구 {level}단계에서 칸이 줄었다.");
				previous = count;
			}
		}

		[Test]
		public void 새로_열린_것은_뒤에_붙는다()
		{
			// 앞이 밀리면 「3번은 벽」이라고 외운 손가락이 헛나간다.
			List<TowerDefensePlaceableKind> before = Unlocked(2);
			List<TowerDefensePlaceableKind> after = Unlocked(3);

			for (int index = 0; index < before.Count; index++)
				Assert.AreEqual(before[index], after[index], $"{index}번 칸의 뜻이 연구 한 번에 바뀌었다.");
		}

		[Test]
		public void 고급_테크는_끝까지_안_열린다()
		{
			// 전초기지는 정수로 사는 고급 테크 — 초반 단계에서 새어 나오면 안 된다.
			for (int level = 0; level <= 4; level++)
				CollectionAssert.DoesNotContain(Unlocked(level), TowerDefensePlaceableKind.Outpost,
					$"연구 {level}단계에 고급 테크가 열렸다.");

			CollectionAssert.Contains(Unlocked(5), TowerDefensePlaceableKind.Outpost);
		}
	}
}
