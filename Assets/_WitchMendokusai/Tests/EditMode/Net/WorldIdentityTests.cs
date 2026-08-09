using System;
using NUnit.Framework;
using WitchMendokusai.Identity;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 「다시 온 그 사람인가」 (TASK-WM-218) — 여기가 틀리면 남의 가방이 열린다.
	/// 눈으로는 절대 못 보는 사고라, 시험이 그 자리를 지킨다.
	/// </summary>
	public sealed class WorldIdentityTests
	{
		private static WorldIdentityRegistry Fresh() => new WorldIdentityRegistry(new Random(1234));

		[Test]
		public void 처음_온_사람은_그냥_받아_준다()
		{
			WorldIdentityRegistry registry = Fresh();

			WorldIdentityRecord person = registry.Recognize(null, out bool created);

			// 가입 화면 없이 그냥 논다 — 열쇠는 세계가 만들어 준다.
			Assert.That(created, Is.True);
			Assert.That(person.id, Is.GreaterThan(0));
			Assert.That(person.secret.Length, Is.EqualTo(WorldIdentityRegistry.SECRET_LENGTH));
		}

		[Test]
		public void 같은_열쇠면_같은_사람이다()
		{
			WorldIdentityRegistry registry = Fresh();
			WorldIdentityRecord first = registry.Recognize(null, out bool _);

			WorldIdentityRecord again = registry.Recognize(first.secret, out bool created);

			Assert.That(created, Is.False);
			Assert.That(again.id, Is.EqualTo(first.id));
			Assert.That(registry.Count, Is.EqualTo(1));
		}

		[Test]
		public void 모르는_열쇠는_남의_것을_안_준다()
		{
			WorldIdentityRegistry registry = Fresh();
			WorldIdentityRecord owner = registry.Recognize(null, out bool _);

			WorldIdentityRecord stranger = registry.Recognize("이건-내가-지어낸-열쇠", out bool created);

			// 찍어서 남의 번호를 부를 수 없다 — 모르는 열쇠는 늘 새 사람이다.
			Assert.That(created, Is.True);
			Assert.That(stranger.id, Is.Not.EqualTo(owner.id));
		}

		[Test]
		public void 사람마다_열쇠가_다르다()
		{
			WorldIdentityRegistry registry = Fresh();

			WorldIdentityRecord first = registry.Recognize(null, out bool _);
			WorldIdentityRecord second = registry.Recognize(null, out bool _);

			Assert.That(second.secret, Is.Not.EqualTo(first.secret));
			Assert.That(second.id, Is.Not.EqualTo(first.id));
		}

		[Test]
		public void 껐다_켜도_같은_사람으로_알아본다()
		{
			WorldIdentityRegistry before = Fresh();
			WorldIdentityRecord person = before.Recognize(null, out bool _);

			WorldIdentityRegistry after = new WorldIdentityRegistry(new Random(99));
			after.Load(before.Save());

			WorldIdentityRecord again = after.Recognize(person.secret, out bool created);

			Assert.That(created, Is.False);
			Assert.That(again.id, Is.EqualTo(person.id));
		}

		[Test]
		public void 되살린_뒤_새_사람은_남의_번호를_안_뺏는다()
		{
			WorldIdentityRegistry before = Fresh();
			WorldIdentityRecord first = before.Recognize(null, out bool _);
			WorldIdentityRecord second = before.Recognize(null, out bool _);

			WorldIdentityBook book = before.Save();
			book.nextId = 1; // 망가진 기억: 다음 번호가 이미 쓰인 번호를 가리킨다

			WorldIdentityRegistry after = new WorldIdentityRegistry(new Random(7));
			after.Load(book);
			WorldIdentityRecord third = after.Recognize(null, out bool _);

			Assert.That(third.id, Is.Not.EqualTo(first.id));
			Assert.That(third.id, Is.Not.EqualTo(second.id));
		}

		[Test]
		public void 망가진_줄은_버리고_세계는_열린다()
		{
			WorldIdentityBook broken = new WorldIdentityBook
			{
				people = new[]
				{
					new WorldIdentityRecord { id = 1, secret = "" },
					null,
					new WorldIdentityRecord { id = 2, secret = "good-key" },
					new WorldIdentityRecord { id = 2, secret = "duplicate-id" },
				},
				nextId = 3,
			};

			WorldIdentityRegistry registry = Fresh();
			registry.Load(broken);

			Assert.That(registry.Count, Is.EqualTo(1));
			Assert.That(registry.Recognize("good-key", out bool created).id, Is.EqualTo(2));
			Assert.That(created, Is.False);
			Assert.That(registry.Recognize("duplicate-id", out bool _).id, Is.Not.EqualTo(2));
		}
	}
}
