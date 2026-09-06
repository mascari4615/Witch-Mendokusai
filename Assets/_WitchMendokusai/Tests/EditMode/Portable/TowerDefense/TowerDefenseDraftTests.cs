using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 파도 사이 드래프트 회귀 — 「매 파도 강제 선택」이 성립하려면 ① 같은 판이면 같은 카드 ② 세 장이 서로 다름
	/// ③ 고른 것이 실제로 쌓임. 하나라도 깨지면 선택이 무게를 잃는다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseDraftTests
	{
		private static TowerDefenseDraftRules Rules(int offerCount = 3)
		{
			return new TowerDefenseDraftRules
			{
				OfferCount = offerCount,
				FirepowerBonus = 0.2f,
				IncomeBonus = 0.25f,
				BountyBonus = 0.3f,
				LivesBonus = 1f,
				EssenceBonus = 5f,
				WindfallResource = 60f,
				RateBonus = 0.2f,
				DiscountBonus = 0.15f,
				ReachBonus = 0.2f,
				PowerBonus = 3f,
				SlowBonus = 0.12f,
				RepairRatio = 0.35f,
			};
		}

		private static List<TowerDefenseBoon> Offer(int waveIndex, int seed, int offerCount = 3)
		{
			List<TowerDefenseBoon> result = new();
			TowerDefenseDraft.Offer(waveIndex, seed, Rules(offerCount), result);
			return result;
		}

		[Test]
		public void 요청한_장수만큼_나온다()
		{
			Assert.AreEqual(3, Offer(0, 1234).Count);
		}

		[Test]
		public void 같은_판_같은_파도면_같은_카드가_나온다()
		{
			// 「다시 뽑기」로 흔들 수 있으면 선택의 무게가 사라진다.
			List<TowerDefenseBoon> first = Offer(2, 77);
			List<TowerDefenseBoon> second = Offer(2, 77);

			for (int index = 0; index < first.Count; index++)
				Assert.AreEqual(first[index].Kind, second[index].Kind);
		}

		[Test]
		public void 파도가_다르면_구성이_달라진다()
		{
			// 매 파도 같은 세 장이면 두 번째부터는 선택이 아니라 반복이다.
			bool anyDifference = false;
			for (int wave = 0; wave < 8 && anyDifference == false; wave++)
			{
				List<TowerDefenseBoon> a = Offer(wave, 42);
				List<TowerDefenseBoon> b = Offer(wave + 1, 42);
				for (int index = 0; index < a.Count; index++)
				{
					if (a[index].Kind != b[index].Kind)
						anyDifference = true;
				}
			}

			Assert.IsTrue(anyDifference, "어떤 파도에서도 구성이 안 바뀌면 드래프트가 고정 목록이다.");
		}

		[Test]
		public void 같은_종류가_두_장_나오지_않는다()
		{
			for (int wave = 0; wave < 30; wave++)
			{
				HashSet<TowerDefenseBoonKind> seen = new();
				foreach (TowerDefenseBoon boon in Offer(wave, 9001))
					Assert.IsTrue(seen.Add(boon.Kind), $"{wave}파에 같은 종류가 두 장 — 그건 선택이 아니다.");
			}
		}

		[Test]
		public void 장수가_0이면_드래프트가_없다()
		{
			Assert.AreEqual(0, Offer(0, 1, offerCount: 0).Count);
			Assert.IsFalse(Rules(0).IsEnabled);
		}

		[Test]
		public void 종류_수보다_많이_요청해도_종류_수까지만()
		{
			List<TowerDefenseBoon> offers = Offer(0, 5, offerCount: 99);

			HashSet<TowerDefenseBoonKind> kinds = new();
			foreach (TowerDefenseBoon boon in offers)
				kinds.Add(boon.Kind);

			Assert.AreEqual(offers.Count, kinds.Count, "중복 없이 채워야 한다.");
			Assert.Greater(offers.Count, 0);
		}

		[Test]
		public void 모든_카드는_이름과_설명을_갖는다()
		{
			// 이름 없는 카드는 화면에서 빈 상자로 보인다.
			foreach (TowerDefenseBoon boon in Offer(3, 314))
			{
				Assert.IsTrue(boon.IsValid);
				Assert.IsNotEmpty(boon.Note);
			}
		}

		[Test]
		public void 지속효과는_고를수록_쌓인다()
		{
			TowerDefenseBoonState state = new();
			TowerDefenseBoon firepower = TowerDefenseDraft.Make(TowerDefenseBoonKind.Firepower, Rules());

			state.Take(firepower);
			state.Take(firepower);

			Assert.AreEqual(2, state.TakenCount);
			Assert.AreEqual(1.4f, state.DamageMultiplier, 0.0001f);
		}

		[Test]
		public void 즉시효과는_배수에_안_쌓인다()
		{
			// 목숨·정수·자원은 받은 순간 끝 — 배수에 섞이면 화면 숫자가 거짓말을 한다.
			TowerDefenseBoonState state = new();

			state.Take(TowerDefenseDraft.Make(TowerDefenseBoonKind.Life, Rules()));

			Assert.AreEqual(1f, state.DamageMultiplier);
			Assert.AreEqual(1f, state.IncomeMultiplier);
			Assert.AreEqual(1f, state.BountyMultiplier);
			Assert.AreEqual(1, state.TakenCount);
		}

		[Test]
		public void 리셋하면_새_판이_된다()
		{
			TowerDefenseBoonState state = new();
			state.Take(TowerDefenseDraft.Make(TowerDefenseBoonKind.Income, Rules()));

			state.Reset();

			Assert.AreEqual(0, state.TakenCount);
			Assert.AreEqual(1f, state.IncomeMultiplier);
			Assert.IsEmpty(state.Describe());
		}

		[Test]
		public void 모든_종류가_뽑기_풀에_들어있다()
		{
			// 카드를 늘려놓고 풀에 안 넣으면 *영원히 안 나오는 카드*가 생긴다 — 조용히 죽은 콘텐츠.
			HashSet<TowerDefenseBoonKind> seen = new();
			for (int wave = 0; wave < 400; wave++)
			{
				foreach (TowerDefenseBoon boon in Offer(wave, wave * 13 + 1, offerCount: 99))
					seen.Add(boon.Kind);
			}

			foreach (TowerDefenseBoonKind kind in System.Enum.GetValues(typeof(TowerDefenseBoonKind)))
				Assert.IsTrue(seen.Contains(kind), $"{kind} 카드가 한 번도 안 나온다 — 풀에 빠졌다.");
		}

		[Test]
		public void 스무_종류_이상이다()
		{
			Assert.GreaterOrEqual(System.Enum.GetValues(typeof(TowerDefenseBoonKind)).Length, 20);
		}

		[Test]
		public void 할인은_공짜가_되지_않는다()
		{
			// 공짜가 되면 선택이 아니라 스위치다.
			TowerDefenseBoonState state = new();
			for (int repeat = 0; repeat < 30; repeat++)
			{
				state.Take(TowerDefenseDraft.Make(TowerDefenseBoonKind.BuildDiscount, Rules()));
				state.Take(TowerDefenseDraft.Make(TowerDefenseBoonKind.ResearchDiscount, Rules()));
			}

			Assert.GreaterOrEqual(state.CostMultiplier, 0.34f);
			Assert.GreaterOrEqual(state.ResearchCostMultiplier, 0.34f);
		}

		[Test]
		public void 마수는_절반_아래로_안_느려진다()
		{
			// 멈춘 적은 적이 아니다.
			TowerDefenseBoonState state = new();
			for (int repeat = 0; repeat < 30; repeat++)
				state.Take(TowerDefenseDraft.Make(TowerDefenseBoonKind.EnemySlow, Rules()));

			Assert.GreaterOrEqual(state.EnemySpeedMultiplier, 0.49f);
		}

		[Test]
		public void 새_배수들도_쌓인다()
		{
			TowerDefenseBoonState state = new();
			state.Take(TowerDefenseDraft.Make(TowerDefenseBoonKind.HarvestYield, Rules()));
			state.Take(TowerDefenseDraft.Make(TowerDefenseBoonKind.Vision, Rules()));
			state.Take(TowerDefenseDraft.Make(TowerDefenseBoonKind.SupplyReach, Rules()));

			Assert.Greater(state.HarvestYieldMultiplier, 1f);
			Assert.Greater(state.VisionMultiplier, 1f);
			Assert.Greater(state.SupplyReachMultiplier, 1f);
		}

		[Test]
		public void 아무것도_안_골랐으면_요약이_비어있다()
		{
			Assert.IsEmpty(new TowerDefenseBoonState().Describe());
		}
	}
}
