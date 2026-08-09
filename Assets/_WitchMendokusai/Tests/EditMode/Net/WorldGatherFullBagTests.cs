using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 가방이 꽉 찼을 때 주우면 어떻게 되나 (TASK-WM-217).
	///
	/// ★ 실측 2026-08-10: 뽑힌 자리는 비는데 가방엔 안 들어가서 <b>그냥 사라졌다</b>.
	///   사람 눈에는 「주웠는데 없어졌다」 — 사라지는 물건은 세계의 규칙이 아니다.
	/// </summary>
	public sealed class WorldGatherFullBagTests
	{
		private const int WOOD = 0;

		private static WorldGatherables Field()
		{
			return new WorldGatherables(new[]
			{
				new GatherableKind { itemId = WOOD, amount = 2, respawnMinutes = 60 },
			});
		}

		[Test]
		public void 못_받으면_도로_선다()
		{
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];

			field.TryTake(node.Id, node.X, node.Z, 0, out _, out _);
			Assert.IsFalse(field.Alive(0).Exists(one => one.Id == node.Id), "뽑은 직후엔 비어 있다");

			field.Restore(node.Id);

			Assert.IsTrue(field.Alive(0).Exists(one => one.Id == node.Id), "못 받았으면 세계로 돌아와야 한다");
			Assert.IsTrue(field.TryTake(node.Id, node.X, node.Z, 0, out int itemId, out int amount));
			Assert.AreEqual(WOOD, itemId);
			Assert.AreEqual(2, amount);
		}

		[Test]
		public void 도로_세우면_들판_수가_돌아온다()
		{
			WorldGatherables field = Field();
			int before = field.Alive(0).Count;
			GatherableNode node = field.Alive(0)[0];

			field.TryTake(node.Id, node.X, node.Z, 0, out _, out _);
			Assert.AreEqual(before - 1, field.Alive(0).Count);

			field.Restore(node.Id);
			Assert.AreEqual(before, field.Alive(0).Count);
		}

		[Test]
		public void 안_뽑은_자리를_되돌려도_탈이_없다()
		{
			WorldGatherables field = Field();
			int before = field.Alive(0).Count;

			field.Restore(field.Alive(0)[0].Id);

			Assert.AreEqual(before, field.Alive(0).Count, "두 번 세워지지 않는다");
		}
	}
}
