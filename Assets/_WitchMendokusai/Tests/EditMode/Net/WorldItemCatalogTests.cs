using NUnit.Framework;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 세계가 아는 아이템 목록 (TASK-WM-217) — 손으로 적은 씨앗을 대신할 그릇.
	/// </summary>
	public sealed class WorldItemCatalogTests
	{
		private static ItemCatalogData Data(params ItemCatalogEntry[] entries) => new ItemCatalogData { items = entries };

		[Test]
		public void 목록에서_꺼내_쓴다()
		{
			WorldItemCatalog catalog = new WorldItemCatalog(Data(new ItemCatalogEntry { id = 7, maxAmount = 42 }));

			IItemData item = catalog.Find(7);

			Assert.That(item, Is.Not.Null);
			Assert.That(item.MaxAmount, Is.EqualTo(42));
			Assert.That(item.IsCountable, Is.True);
		}

		[Test]
		public void 모르는_번호는_없다고_한다()
		{
			WorldItemCatalog catalog = new WorldItemCatalog(Data(new ItemCatalogEntry { id = 1 }));

			// 세계는 모르는 것을 가방에 넣지 않는다 — null 이 그 신호다.
			Assert.That(catalog.Find(999), Is.Null);
		}

		[Test]
		public void 쌓을_수_없는_것은_한_칸에_하나()
		{
			WorldItemCatalog catalog = new WorldItemCatalog(Data(new ItemCatalogEntry { id = 3, maxAmount = 1 }));

			Assert.That(catalog.Find(3).IsCountable, Is.False);
		}

		[Test]
		public void 최대치가_0_이하면_한_개로_고친다()
		{
			WorldItemCatalog catalog = new WorldItemCatalog(Data(new ItemCatalogEntry { id = 4, maxAmount = 0 }));

			// 0 이면 가방에 영영 못 들어간다 — 망가진 자료가 게임을 멈추게 두지 않는다.
			Assert.That(catalog.Find(4).MaxAmount, Is.EqualTo(1));
		}

		[Test]
		public void 같은_번호가_두_번이면_먼저_것이_남는다()
		{
			WorldItemCatalog catalog = new WorldItemCatalog(Data(
				new ItemCatalogEntry { id = 5, maxAmount = 10 },
				new ItemCatalogEntry { id = 5, maxAmount = 99 }));

			Assert.That(catalog.Find(5).MaxAmount, Is.EqualTo(10));
			Assert.That(catalog.Count, Is.EqualTo(1));
		}

		[Test]
		public void 목록이_없어도_터지지_않는다()
		{
			WorldItemCatalog catalog = new WorldItemCatalog(null);

			Assert.That(catalog.Count, Is.EqualTo(0));
			Assert.That(catalog.Find(1), Is.Null);
		}
	}
}
