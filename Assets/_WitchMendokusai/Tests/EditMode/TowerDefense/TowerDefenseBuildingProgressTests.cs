using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 건물 성장 회귀 — 실시간이 되면서 사라진 「웨이브를 넘겼다」는 매듭을 대신하는 축이다.
	/// 고를 것이 *쌓였다가* 건물을 고를 때 나온다(레벨업 알림이 화면을 덮지 않게). TASK-WM-194.
	/// </summary>
	public class TowerDefenseBuildingProgressTests
	{
		[Test]
		public void 시작은_1레벨_선택지_없음()
		{
			TowerDefenseBuildingProgress progress = new();

			Assert.AreEqual(1, progress.Level);
			Assert.AreEqual(0, progress.PendingChoices);
		}

		[Test]
		public void 경험치가_차면_레벨과_선택지가_는다()
		{
			TowerDefenseBuildingProgress progress = new(baseCost: 10);

			progress.AddExperience(10);

			Assert.AreEqual(2, progress.Level);
			Assert.AreEqual(1, progress.PendingChoices);
		}

		[Test]
		public void 큰_보상은_여러_레벨을_한_번에_올린다()
		{
			// 잘게 쪼개 넣으면 「방금 뭘 했는지」와 화면이 어긋난다.
			TowerDefenseBuildingProgress progress = new(baseCost: 10, growth: 1f);

			progress.AddExperience(35);

			Assert.AreEqual(4, progress.Level);
			Assert.AreEqual(3, progress.PendingChoices);
			Assert.AreEqual(5, progress.Experience, "남은 경험치는 다음 구간으로 넘어간다.");
		}

		[Test]
		public void 레벨이_오를수록_비싸진다()
		{
			TowerDefenseBuildingProgress progress = new(baseCost: 10, growth: 2f);
			int first = progress.NextLevelCost;

			progress.AddExperience(first);

			Assert.AreEqual(first * 2, progress.NextLevelCost);
		}

		[Test]
		public void 음수_경험치는_무시된다()
		{
			TowerDefenseBuildingProgress progress = new();

			progress.AddExperience(-50);

			Assert.AreEqual(1, progress.Level);
			Assert.AreEqual(0, progress.Experience);
		}

		[Test]
		public void 고를_것이_없으면_못_고른다()
		{
			TowerDefenseBuildingProgress progress = new();

			Assert.IsFalse(progress.Choose(TowerDefenseBuildingPerk.Damage));
			Assert.AreEqual(0, progress.Taken.Count);
		}

		[Test]
		public void 고르면_쌓이고_대기가_준다()
		{
			TowerDefenseBuildingProgress progress = new(baseCost: 5);
			progress.AddExperience(5);

			Assert.IsTrue(progress.Choose(TowerDefenseBuildingPerk.Damage));

			Assert.AreEqual(0, progress.PendingChoices);
			Assert.AreEqual(1, progress.CountOf(TowerDefenseBuildingPerk.Damage));
		}

		[Test]
		public void 같은_것을_여러_번_고를_수_있다()
		{
			// 한 축을 밀어붙이는 것도 선택이다 — 막으면 모든 건물이 같은 모양으로 수렴한다.
			TowerDefenseBuildingProgress progress = new(baseCost: 5, growth: 1f);
			progress.AddExperience(15);

			progress.Choose(TowerDefenseBuildingPerk.Damage);
			progress.Choose(TowerDefenseBuildingPerk.Damage);

			Assert.AreEqual(2, progress.CountOf(TowerDefenseBuildingPerk.Damage));
		}

		[Test]
		public void 같은_건물_같은_레벨이면_같은_세_장()
		{
			// 다시 열어 굴리는 짓을 막는다.
			List<TowerDefenseBuildingPerk> first = new();
			List<TowerDefenseBuildingPerk> second = new();

			TowerDefenseBuildingProgress.Offer(7, 3, isHarvester: false, first);
			TowerDefenseBuildingProgress.Offer(7, 3, isHarvester: false, second);

			CollectionAssert.AreEqual(first, second);
		}

		[Test]
		public void 채집과_포탑은_다른_것을_고른다()
		{
			List<TowerDefenseBuildingPerk> tower = new();
			List<TowerDefenseBuildingPerk> harvester = new();

			TowerDefenseBuildingProgress.Offer(1, 2, isHarvester: false, tower);
			TowerDefenseBuildingProgress.Offer(1, 2, isHarvester: true, harvester);

			Assert.IsFalse(harvester.Contains(TowerDefenseBuildingPerk.Damage), "채집이 피해를 고르는 건 말이 안 된다.");
			Assert.IsFalse(tower.Contains(TowerDefenseBuildingPerk.Yield), "포탑이 산출을 고르는 건 말이 안 된다.");
		}

		[Test]
		public void 선택지는_세_장이고_서로_다르다()
		{
			List<TowerDefenseBuildingPerk> offers = new();
			TowerDefenseBuildingProgress.Offer(42, 5, isHarvester: false, offers);

			Assert.AreEqual(3, offers.Count);
			CollectionAssert.AllItemsAreUnique(offers);
		}

		[Test]
		public void 이름이_비어있지_않다()
		{
			foreach (TowerDefenseBuildingPerk perk in System.Enum.GetValues(typeof(TowerDefenseBuildingPerk)))
				Assert.IsNotEmpty(TowerDefenseBuildingProgress.NameOf(perk));
		}

		[Test]
		public void 되살린_성장은_선택지를_다시_쌓지_않는다()
		{
			// ★ 경험치로 되감으면 레벨이 오르는 도중에 선택지가 다시 쌓여, 같은 판을 이어했을 뿐인데
			//   고를 것이 없던 자리에 갑자기 고를 것이 생긴다. 되살리기는 *기록을 그대로 얹는 일*이다.
			TowerDefenseBuildingProgress progress = new(baseCost: 10, growth: 1.6f);

			progress.Restore(4, 7, 0, new[] { TowerDefenseBuildingPerk.Damage, TowerDefenseBuildingPerk.Range });

			Assert.AreEqual(4, progress.Level);
			Assert.AreEqual(7, progress.Experience);
			Assert.AreEqual(0, progress.PendingChoices);
			Assert.AreEqual(2, progress.Taken.Count);
		}

		[Test]
		public void 되살리기는_옛_선택을_남겨두지_않는다()
		{
			// 되살린 뒤에 옛 목록이 섞여 있으면 「같은 판」이 아니라 두 판이 겹친 것이 된다.
			TowerDefenseBuildingProgress progress = new();
			progress.AddExperience(1000);
			progress.Choose(TowerDefenseBuildingPerk.Damage);

			progress.Restore(1, 0, 0, null);

			Assert.AreEqual(1, progress.Level);
			Assert.AreEqual(0, progress.Taken.Count);
		}
	}
}
