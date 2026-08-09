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
		public void 초대_열쇠로_다른_기기도_같은_사람이_된다()
		{
			WorldIdentityRegistry registry = Fresh();
			WorldIdentityRecord phone = registry.Recognize(null, out bool _);

			string invite = registry.IssueInvite(phone.id);
			WorldIdentityRecord laptopSeen = registry.RedeemInvite(invite, "노트북-열쇠");

			Assert.That(laptopSeen, Is.Not.Null);
			Assert.That(laptopSeen.id, Is.EqualTo(phone.id));

			// 이제 그 기기의 열쇠로 들어와도 같은 사람이다.
			Assert.That(registry.Recognize("노트북-열쇠", out bool created).id, Is.EqualTo(phone.id));
			Assert.That(created, Is.False);
			Assert.That(registry.Count, Is.EqualTo(1), "사람이 늘어나면 안 된다 — 기기가 는 것이다.");
		}

		[Test]
		public void 초대_열쇠는_한_번만_쓴다()
		{
			WorldIdentityRegistry registry = Fresh();
			WorldIdentityRecord person = registry.Recognize(null, out bool _);
			string invite = registry.IssueInvite(person.id);

			registry.RedeemInvite(invite, "첫-기기");

			// 남이 주워도 이미 쓴 것은 소용없다.
			Assert.That(registry.RedeemInvite(invite, "주운-기기"), Is.Null);
			Assert.That(registry.PendingInvites, Is.EqualTo(0));
			Assert.That(registry.Recognize("주운-기기", out bool created).id, Is.Not.EqualTo(person.id));
			Assert.That(created, Is.True);
		}

		[Test]
		public void 지난_초대_열쇠는_안_통한다()
		{
			WorldIdentityRegistry registry = Fresh();
			WorldIdentityRecord person = registry.Recognize(null, out bool _);
			string invite = registry.IssueInvite(person.id, today: 10);

			// 주운 종이 한 장이 영원히 유효하면 안 된다.
			Assert.That(registry.RedeemInvite(invite, "기기", today: 10 + WorldIdentityRegistry.INVITE_DAYS + 1), Is.Null);
			Assert.That(registry.PendingInvites, Is.EqualTo(0), "지난 열쇠는 그 자리에서 버린다.");
		}

		[Test]
		public void 기한_안이면_통한다()
		{
			WorldIdentityRegistry registry = Fresh();
			WorldIdentityRecord person = registry.Recognize(null, out bool _);
			string invite = registry.IssueInvite(person.id, today: 10);

			Assert.That(registry.RedeemInvite(invite, "기기", today: 10 + WorldIdentityRegistry.INVITE_DAYS)?.id,
				Is.EqualTo(person.id));
		}

		[Test]
		public void 새로_만들면_옛_열쇠는_죽는다()
		{
			WorldIdentityRegistry registry = Fresh();
			WorldIdentityRecord person = registry.Recognize(null, out bool _);

			string first = registry.IssueInvite(person.id);
			string second = registry.IssueInvite(person.id);

			Assert.That(registry.PendingInvites, Is.EqualTo(1), "한 사람에게 살아 있는 열쇠는 하나뿐.");
			Assert.That(registry.RedeemInvite(first, "기기A"), Is.Null);
			Assert.That(registry.RedeemInvite(second, "기기B")?.id, Is.EqualTo(person.id));
		}

		[Test]
		public void 모르는_초대_열쇠는_아무_일도_없다()
		{
			WorldIdentityRegistry registry = Fresh();
			registry.Recognize(null, out bool _);

			Assert.That(registry.RedeemInvite("없는코드", "기기"), Is.Null);
			Assert.That(registry.RedeemInvite(null, "기기"), Is.Null);
		}

		[Test]
		public void 초대_열쇠는_서버가_꺼졌다_켜져도_살아_있다()
		{
			WorldIdentityRegistry before = Fresh();
			WorldIdentityRecord person = before.Recognize(null, out bool _);
			string invite = before.IssueInvite(person.id);

			WorldIdentityRegistry after = new WorldIdentityRegistry(new Random(3));
			after.Load(before.Save());

			Assert.That(after.RedeemInvite(invite, "다른-기기")?.id, Is.EqualTo(person.id));
		}

		[Test]
		public void 없는_사람의_초대_열쇠는_안_낸다()
		{
			WorldIdentityRegistry registry = Fresh();

			Assert.That(registry.IssueInvite(999), Is.Null);
			Assert.That(registry.PendingInvites, Is.EqualTo(0));
		}

		[Test]
		public void 빈손이고_오래_안_온_손님만_지운다()
		{
			WorldIdentityRegistry registry = Fresh();
			WorldIdentityRecord guest = registry.Recognize(null, out bool _, today: 0);
			WorldIdentityRecord owner = registry.Recognize(null, out bool _, today: 0);

			// owner 는 세계에 뭔가 남겼다 — 오래 안 왔어도 지우지 않는다.
			int pruned = registry.PruneGuests(today: 100, notSeenForDays: 30, ownsSomething: id => id == owner.id);

			Assert.That(pruned, Is.EqualTo(1));
			Assert.That(registry.Find(guest.id), Is.Null);
			Assert.That(registry.Find(owner.id), Is.Not.Null);
		}

		[Test]
		public void 최근에_온_사람은_안_지운다()
		{
			WorldIdentityRegistry registry = Fresh();
			WorldIdentityRecord person = registry.Recognize(null, out bool _, today: 0);

			// 다시 왔으면 그 날짜가 새로 찍힌다.
			registry.Recognize(person.secret, out bool _, today: 95);

			Assert.That(registry.PruneGuests(today: 100, notSeenForDays: 30, ownsSomething: id => false), Is.EqualTo(0));
		}

		[Test]
		public void 지운_사람의_초대_열쇠도_같이_버린다()
		{
			WorldIdentityRegistry registry = Fresh();
			WorldIdentityRecord guest = registry.Recognize(null, out bool _, today: 0);
			string invite = registry.IssueInvite(guest.id, today: 0);

			registry.PruneGuests(today: 100, notSeenForDays: 30, ownsSomething: id => false);

			Assert.That(registry.PendingInvites, Is.EqualTo(0));
			Assert.That(registry.RedeemInvite(invite, "기기", today: 100), Is.Null);
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
