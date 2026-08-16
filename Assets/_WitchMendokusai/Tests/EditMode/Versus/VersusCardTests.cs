using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 카드 8종이 스탯을 정말 바꾸나, 그리고 아무리 쌓아도 판이 안 깨지나 (TASK-WM-411).
	/// 후자가 더 중요하다 — v0 의 목적은 「친구랑 한 판 더」이고, 조합 하나가 판을 못 돌게 만들면 그 판정 자체를 못 한다.
	/// </summary>
	public sealed class VersusCardTests
	{
		[Test]
		public void 카드_8종이_전부_등록돼_있다()
		{
			Assert.AreEqual(8, VersusCards.All.Length);
			CollectionAssert.AllItemsAreUnique(VersusCards.All);
		}

		[Test]
		public void 카드마다_설명이_있다()
		{
			foreach (VersusCardKind card in VersusCards.All)
				Assert.IsNotEmpty(VersusCards.Describe(card), card.ToString());
		}

		[Test]
		public void 모든_카드가_스탯을_실제로_바꾼다()
		{
			VersusFighterStats baseline = VersusFighterStats.Default();

			foreach (VersusCardKind card in VersusCards.All)
			{
				VersusFighterStats applied = VersusCards.Apply(baseline, card);
				Assert.AreNotEqual(baseline, applied, card.ToString() + " 가 아무것도 안 바꿨다");
			}
		}

		[Test]
		public void 큰탄은_몸도_같이_커진다()
		{
			VersusFighterStats applied = VersusCards.Apply(VersusFighterStats.Default(), VersusCardKind.Huge);

			Assert.Greater(applied.ProjectileScale, 1f);
			Assert.Greater(applied.BodyScale, 1f);
		}

		[Test]
		public void 연사는_간격을_줄인다()
		{
			VersusFighterStats baseline = VersusFighterStats.Default();
			VersusFighterStats applied = VersusCards.Apply(baseline, VersusCardKind.RapidFire);

			Assert.Less(applied.FireInterval, baseline.FireInterval);
		}

		[Test]
		public void 같은_카드를_스무장_쌓아도_판이_돈다()
		{
			foreach (VersusCardKind card in VersusCards.All)
			{
				VersusFighterStats stats = VersusFighterStats.Default();
				for (int stack = 0; stack < 20; stack++)
					stats = VersusCards.Apply(stats, card);

				Assert.Greater(stats.FireInterval, 0f, card.ToString());
				Assert.Greater(stats.MoveSpeed, 0f, card.ToString());
				Assert.Greater(stats.ProjectileSpeed, 0f, card.ToString());
				Assert.GreaterOrEqual(stats.ProjectileCount, 1, card.ToString());
				Assert.LessOrEqual(stats.ProjectileCount, 24, card.ToString());
				Assert.LessOrEqual(stats.BodyScale, 3f, card.ToString());
			}
		}

		[Test]
		public void 카드를_섞어_쌓아도_상한을_안_넘는다()
		{
			VersusRandom random = new VersusRandom(4615);
			VersusFighterStats stats = VersusFighterStats.Default();

			for (int step = 0; step < 200; step++)
				stats = VersusCards.Apply(stats, VersusCards.All[random.NextInt(VersusCards.All.Length)]);

			Assert.GreaterOrEqual(stats.FireInterval, 0.06f);
			Assert.LessOrEqual(stats.MoveSpeed, 30f);
			Assert.LessOrEqual(stats.ProjectileSpeed, 80f);
			Assert.LessOrEqual(stats.BounceCount, 12);
			Assert.LessOrEqual(stats.ShieldCharges, 8);
		}
	}
}
