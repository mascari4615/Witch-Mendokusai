using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>주울 것은 세계에 <b>실제로 있고</b>, 손이 닿아야 줍힌다 (TASK-WM-217).</summary>
	public sealed class WorldGatherablesTests
	{
		private static WorldGatherables Field()
		{
			return new WorldGatherables(new[]
			{
				new GatherableKind { itemId = 1, amount = 2, respawnMinutes = 60 },
			});
		}

		[Test]
		public void 세계에_주울_것이_흩어져_있다()
		{
			List<GatherableNode> alive = Field().Alive(0);

			Assert.Greater(alive.Count, 10, "빈 들판이면 주우러 갈 곳이 없다");
		}

		[Test]
		public void 같은_세계는_늘_같은_자리다()
		{
			// 자리를 저장하지 않으므로, 계산이 흔들리면 세계가 켤 때마다 다른 모양이 된다.
			List<GatherableNode> first = Field().Alive(0);
			List<GatherableNode> second = Field().Alive(0);

			Assert.AreEqual(first.Count, second.Count);
			for (int i = 0; i < first.Count; i++)
			{
				Assert.AreEqual(first[i].Id, second[i].Id);
				Assert.AreEqual(first[i].X, second[i].X, 0.0001f);
				Assert.AreEqual(first[i].Z, second[i].Z, 0.0001f);
			}
		}

		[Test]
		public void 옆에_서면_줍힌다()
		{
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];

			Assert.IsTrue(field.TryTake(node.Id, node.X, node.Z, 0, out int itemId, out int amount));
			Assert.AreEqual(1, itemId);
			Assert.AreEqual(2, amount);
		}

		[Test]
		public void 멀리서는_못_줍는다()
		{
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];

			Assert.IsFalse(field.TryTake(node.Id, node.X + 50f, node.Z, 0, out _, out _),
				"창이 우겨도 손이 닿지 않으면 못 줍는다");
		}

		[Test]
		public void 한_번_뽑으면_사라졌다가_다시_자란다()
		{
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];

			Assert.IsTrue(field.TryTake(node.Id, node.X, node.Z, 0, out _, out _));
			Assert.IsFalse(field.TryTake(node.Id, node.X, node.Z, 10, out _, out _), "방금 뽑은 자리는 비어 있다");
			Assert.IsFalse(field.Alive(10).Exists(one => one.Id == node.Id), "빈 자리는 창에도 안 보인다");

			// 60분 뒤에는 다시 자란다.
			Assert.IsTrue(field.Alive(60).Exists(one => one.Id == node.Id));
			Assert.IsTrue(field.TryTake(node.Id, node.X, node.Z, 60, out _, out _));
		}

		[Test]
		public void 없는_자리는_못_줍는다()
		{
			Assert.IsFalse(Field().TryTake(999999, 0f, 0f, 0, out _, out _));
		}

		[Test]
		public void 뽑아_간_자리는_세계가_기억한다()
		{
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];
			field.TryTake(node.Id, node.X, node.Z, 0, out _, out _);

			WorldGatherables reborn = Field();
			reborn.Load(field.Save());

			Assert.IsFalse(reborn.TryTake(node.Id, node.X, node.Z, 5, out _, out _),
				"껐다 켜니 방금 뽑은 것이 도로 서 있으면 무한히 뽑을 수 있다");
		}

		[Test]
		public void 아무것도_안_자라는_세계는_빈_들판이다()
		{
			WorldGatherables empty = new WorldGatherables(null);

			Assert.AreEqual(0, empty.KindCount);
			Assert.AreEqual(0, empty.Alive(0).Count);
		}

		/// <summary>
		/// 다시 자란 것이 <b>창에 돌아온다</b> (TASK-WM-217).
		///
		/// ★ 실측 2026-08-10 — 자물쇠였다: 재생은 들판을 훑을 때만 일어나고, 훑는 쪽(방송)은
		///   「버전이 올랐을 때만」 훑는다. 아무도 안 훑으니 버전이 안 오르고, 버전이 안 오르니
		///   아무도 안 훑는다. 그 사이 다시 자란 자리는 화면에 영영 안 나타난다.
		/// </summary>
		[Test]
		public void 시간이_흐르면_훑지_않아도_다시_자란다()
		{
			WorldGatherables field = new WorldGatherables(new[] { new GatherableKind { itemId = 5, amount = 1, respawnMinutes = 100 } });
			GatherableNode node = field.Alive(0)[0];
			Assert.IsTrue(field.TryTake(node.Id, node.X, node.Z, 0, out int _, out int _));

			int afterTaken = field.Version;

			// 방송이 하는 일 그대로 — 시계만 흐르고, 아무도 들판을 훑지 않는다.
			field.Tick(200);

			Assert.AreNotEqual(afterTaken, field.Version,
				"버전이 안 오르면 방송은 들판을 안 보낸다 — 다시 자란 것이 화면에 영영 안 나타난다");
		}

		[Test]
		public void 아직_때가_아니면_버전이_흔들리지_않는다()
		{
			WorldGatherables field = new WorldGatherables(new[] { new GatherableKind { itemId = 5, amount = 1, respawnMinutes = 100 } });
			GatherableNode node = field.Alive(0)[0];
			field.TryTake(node.Id, node.X, node.Z, 0, out int _, out int _);

			int afterTaken = field.Version;
			field.Tick(50);

			Assert.AreEqual(afterTaken, field.Version, "안 바뀐 들판을 매 프레임 나르면 그건 세계가 아니라 소음이다");
		}


		[Test]
		public void 남이_방금_가져간_자리는_그렇게_말한다()
		{
			// 「자라는 중」과 「방금 남이」는 사람이 겪는 일이 다르다 (TASK-WM-275) —
			// 뒤엣것은 <b>겨루기에 진 것</b>이다. 같은 말로 뭉치면 왜 졌는지도 모른다.
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];

			Assert.IsTrue(field.TryTake(node.Id, node.X, node.Z, 0, out _, out _, out _));

			Assert.IsFalse(field.TryTake(node.Id, node.X, node.Z, 1, out _, out _, out GatherDenial rightAfter));
			Assert.AreEqual(GatherDenial.JUST_TAKEN, rightAfter);
		}

		[Test]
		public void 한참_지난_자리는_자라는_중이라고_말한다()
		{
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];
			field.TryTake(node.Id, node.X, node.Z, 0, out _, out _, out _);

			// 「방금」이 지난 뒤 — 아직 다 안 자랐지만 남이 가져간 그 순간은 아니다.
			Assert.IsFalse(field.TryTake(node.Id, node.X, node.Z, WorldGatherables.JUST_NOW_MINUTES + 5,
				out _, out _, out GatherDenial later));
			Assert.AreEqual(GatherDenial.STILL_REGROWING, later);
		}

		[Test]
		public void 다_자란_자리는_다시_주울_수_있다()
		{
			WorldGatherables field = Field();
			GatherableNode node = field.Alive(0)[0];
			field.TryTake(node.Id, node.X, node.Z, 0, out _, out _, out _);

			Assert.IsTrue(field.TryTake(node.Id, node.X, node.Z, 60, out _, out _, out _),
				"다 자랐는데도 못 주우면 들판이 한 번 쓰고 마는 것이 된다");
		}
	}
}
