using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Care;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-171 — 소원을 들고 있는 쪽이 <b>언제 「채워졌다」고 말하는지</b>를 지킨다.
	///
	/// ★ 이 층의 알맹이: 재료만 모아도, 곁에만 있어줘도 안 끝난다. <b>둘 다</b> 차야 끝난다.
	///   그리고 한 번 배웅한 소원을 두 번 배웅하면 안 된다.
	/// </summary>
	public class WishKeeperTests
	{
		private const string WISH_ID = "시험-소원";
		private const string ITEM_ID = "꽃";
		private const string CHANNEL = "곁에 있어주기";

		private GameObject host;
		private WishKeeper keeper;
		private readonly List<(string Id, WishOutcome Outcome)> resolved = new List<(string, WishOutcome)>();

		[SetUp]
		public void SetUp()
		{
			host = new GameObject("WishKeeperTestHost");
			keeper = host.AddComponent<WishKeeper>();
			resolved.Clear();
			keeper.OnWishResolved += (id, outcome) => resolved.Add((id, outcome));
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(host);
		}

		private void AddWish(int materialNeeded, float satisfactionTarget, WishOutcome outcome)
		{
			keeper.AddWish(new WishSpec(
				WISH_ID,
				WishKind.Companionship,
				new List<WishMaterialReq> { new WishMaterialReq(ITEM_ID, materialNeeded) },
				new Dictionary<string, float> { { CHANNEL, satisfactionTarget } },
				outcome));
		}

		[Test]
		public void 재료만_다_모아도_안_끝난다()
		{
			AddWish(2, 1f, WishOutcome.Settle);

			keeper.Contribute(WISH_ID, ITEM_ID, 2);

			Assert.AreEqual(0, resolved.Count);
			Assert.AreEqual(1, keeper.PendingCount);
		}

		[Test]
		public void 곁에만_있어줘도_안_끝난다()
		{
			AddWish(2, 1f, WishOutcome.Settle);

			keeper.Satisfy(WISH_ID, CHANNEL, 1f);

			Assert.AreEqual(0, resolved.Count);
			Assert.AreEqual(1, keeper.PendingCount);
		}

		[Test]
		public void 둘_다_차면_그때_끝나고_결말은_데이터가_정한다()
		{
			AddWish(2, 1f, WishOutcome.Depart);

			keeper.Contribute(WISH_ID, ITEM_ID, 2);
			keeper.Satisfy(WISH_ID, CHANNEL, 1f);

			Assert.AreEqual(1, resolved.Count);
			Assert.AreEqual(WISH_ID, resolved[0].Id);
			Assert.AreEqual(WishOutcome.Depart, resolved[0].Outcome); // 코드가 아니라 소원 데이터가 정한다.
			Assert.AreEqual(0, keeper.PendingCount);
		}

		[Test]
		public void 이미_채워진_소원은_더_줘도_두_번_배웅되지_않는다()
		{
			AddWish(1, 1f, WishOutcome.Settle);

			keeper.Contribute(WISH_ID, ITEM_ID, 1);
			keeper.Satisfy(WISH_ID, CHANNEL, 1f);
			Assert.AreEqual(1, resolved.Count);

			keeper.Contribute(WISH_ID, ITEM_ID, 5);
			keeper.Satisfy(WISH_ID, CHANNEL, 1f);

			Assert.AreEqual(1, resolved.Count); // 여전히 한 번.
		}

		[Test]
		public void 없는_소원에_줘도_터지지_않는다()
		{
			keeper.Contribute("그런-소원-없다", ITEM_ID, 3);
			keeper.Satisfy("그런-소원-없다", CHANNEL, 1f);

			Assert.AreEqual(0, resolved.Count);
			Assert.AreEqual(0, keeper.PendingCount);
			Assert.IsNull(keeper.ProgressOf("그런-소원-없다"));
		}

		[Test]
		public void 같은_소원을_두_번_걸면_하나만_남는다()
		{
			AddWish(1, 1f, WishOutcome.Settle);
			AddWish(9, 1f, WishOutcome.Depart); // 같은 id — 무시돼야 한다.

			Assert.AreEqual(1, keeper.PendingCount);

			keeper.Contribute(WISH_ID, ITEM_ID, 1);
			keeper.Satisfy(WISH_ID, CHANNEL, 1f);

			Assert.AreEqual(1, resolved.Count);
			Assert.AreEqual(WishOutcome.Settle, resolved[0].Outcome); // 먼저 건 쪽이 남는다.
		}
	}
}
