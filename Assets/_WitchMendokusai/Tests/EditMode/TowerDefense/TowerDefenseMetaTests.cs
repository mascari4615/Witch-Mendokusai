using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 판 밖 메타 회귀 — 유물 적립과 뽑기. 「판이 끝나도 남는 게 없다」를 고치는 고리라
	/// 여기가 새면 다음 판이 지난 판과 똑같아진다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseMetaTests
	{
		private const int TOWER_COUNT = 4;
		private const int DEFAULT_UNLOCKED = 2;

		[Test]
		public void 유물은_버틴_웨이브에_비례하고_참가보상이_있다()
		{
			Assert.AreEqual(2, TowerDefenseMeta.RelicsFor(0, 3, 2), "0파에 져도 빈손은 아니어야 한다.");
			Assert.AreEqual(11, TowerDefenseMeta.RelicsFor(3, 3, 2));
		}

		[Test]
		public void 기본해금_포탑은_뽑지_않아도_쓴다()
		{
			List<int> unlocked = new();

			Assert.IsTrue(TowerDefenseMeta.IsUnlocked(0, DEFAULT_UNLOCKED, unlocked));
			Assert.IsTrue(TowerDefenseMeta.IsUnlocked(1, DEFAULT_UNLOCKED, unlocked));
			Assert.IsFalse(TowerDefenseMeta.IsUnlocked(2, DEFAULT_UNLOCKED, unlocked));
		}

		[Test]
		public void 유물이_모자라면_안_뽑힌다()
		{
			List<int> unlocked = new();
			int relics = 5;

			bool pulled = TowerDefenseMeta.TryPull(TOWER_COUNT, DEFAULT_UNLOCKED, unlocked, ref relics, 12, 0.5f, out _);

			Assert.IsFalse(pulled);
			Assert.AreEqual(5, relics, "실패했는데 유물이 줄면 안 된다.");
			Assert.AreEqual(0, unlocked.Count);
		}

		[Test]
		public void 뽑으면_유물이_줄고_잠긴_것_중_하나가_열린다()
		{
			List<int> unlocked = new();
			int relics = 20;

			bool pulled = TowerDefenseMeta.TryPull(TOWER_COUNT, DEFAULT_UNLOCKED, unlocked, ref relics, 12, 0.5f, out int index);

			Assert.IsTrue(pulled);
			Assert.AreEqual(8, relics);
			Assert.GreaterOrEqual(index, DEFAULT_UNLOCKED, "이미 가진 걸 뽑아주면 선택지가 안 늘어난다.");
			Assert.IsTrue(TowerDefenseMeta.IsUnlocked(index, DEFAULT_UNLOCKED, unlocked));
		}

		[Test]
		public void 중복은_나오지_않는다()
		{
			List<int> unlocked = new();
			int relics = 100;

			TowerDefenseMeta.TryPull(TOWER_COUNT, DEFAULT_UNLOCKED, unlocked, ref relics, 10, 0f, out int first);
			TowerDefenseMeta.TryPull(TOWER_COUNT, DEFAULT_UNLOCKED, unlocked, ref relics, 10, 0f, out int second);

			Assert.AreNotEqual(first, second);
			Assert.AreEqual(2, unlocked.Count);
		}

		[Test]
		public void 다_뽑으면_더_안_뽑힌다()
		{
			List<int> unlocked = new() { 2, 3 };
			int relics = 100;

			Assert.IsFalse(TowerDefenseMeta.HasLockedTower(TOWER_COUNT, DEFAULT_UNLOCKED, unlocked));
			Assert.IsFalse(TowerDefenseMeta.TryPull(TOWER_COUNT, DEFAULT_UNLOCKED, unlocked, ref relics, 10, 0.9f, out _));
			Assert.AreEqual(100, relics);
		}
	}
}
