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

		[Test]
		public void 덜_가져가면_남은_만큼_그_자리에_있다()
		{
			// ★ 실측 2026-08-10: 3개짜리를 한 칸 남은 가방으로 주우면 1개만 들어가고 2개가 증발했다.
			//   자리가 「있다/없다」 둘뿐이라 「2개만 남았다」를 적을 데가 없었다.
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];

			field.TryTake(node.Id, node.X, node.Z, 0, out _, out int amount);
			Assert.AreEqual(2, amount);

			field.RestorePartial(node.Id, 1); // 하나만 못 들었다

			GatherableNode again = field.Alive(0).Find(one => one.Id == node.Id);
			Assert.AreEqual(node.Id, again.Id, "덜 가져간 자리는 비지 않는다");
			Assert.AreEqual(1, again.Amount, "남긴 개수가 그 자리에 보여야 사람이 다시 주우러 온다");

			Assert.IsTrue(field.TryTake(again.Id, again.X, again.Z, 0, out _, out int second));
			Assert.AreEqual(1, second, "남은 것보다 많이 주워지면 그건 공짜다");
		}

		[Test]
		public void 남겨_둔_것은_세계가_잠들었다_깨어도_그대로다()
		{
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];

			field.TryTake(node.Id, node.X, node.Z, 0, out _, out _);
			field.RestorePartial(node.Id, 1);

			WorldGatherables woken = Field();
			woken.Load(field.Save());

			GatherableNode again = woken.Alive(0).Find(one => one.Id == node.Id);
			Assert.AreEqual(1, again.Amount, "깨어나며 개수가 도로 늘면 그 자리는 무한 밭이 된다");
		}

		[Test]
		public void 남긴_것을_다_가져가면_다시_온전하게_자란다()
		{
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];

			field.TryTake(node.Id, node.X, node.Z, 0, out _, out _);
			field.RestorePartial(node.Id, 1);
			Assert.IsTrue(field.TryTake(node.Id, node.X, node.Z, 0, out _, out _));

			GatherableNode grown = field.Alive(60).Find(one => one.Id == node.Id);
			Assert.AreEqual(2, grown.Amount, "다시 자란 자리가 1개짜리로 남으면 들판이 야위어 간다");
		}
	}
}
