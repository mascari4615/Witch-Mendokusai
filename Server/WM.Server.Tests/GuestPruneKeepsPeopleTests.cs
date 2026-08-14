using NUnit.Framework;
using WitchMendokusai.Identity;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 장부 청소가 <b>사람을 지우지 않나</b> (TASK-WM-363).
	///
	/// ★ 왜 여기(.NET)에 또 두나: 이 규칙의 시험은 유니티 EditMode 에 있는데 그건 <b>CI 에서 안 돈다</b>
	///   (라이선스 미해결, WM-221). 즉 지금 <b>도는</b> 검사 중에 이걸 보는 것은 하나도 없었다.
	///   신원을 지우는 것은 <b>그 사람의 세계를 지우는 것</b>이다 — 열쇠가 안 통하고, 이름도 없어지고,
	///   다음에 와서 「남」이 된다. 도는 자리에 둔다.
	/// </summary>
	public sealed class GuestPruneKeepsPeopleTests
	{
		private const int TODAY = 100;
		private const int FORGET_AFTER = 30;

		/// <summary>오래 안 온 사람 하나가 있는 장부.</summary>
		private static WorldIdentityRegistry OldTimer(out int id, string name = "")
		{
			WorldIdentityRegistry book = new WorldIdentityRegistry();
			WorldIdentityRecord person = book.Recognize(string.Empty, out bool _, TODAY - FORGET_AFTER - 5);
			if (string.IsNullOrEmpty(name) == false)
				Assert.That(book.TryRename(person.id, name, out string _), Is.True, "이름을 못 지었다");

			id = person.id;
			return book;
		}

		[Test]
		public void 빈손이고_오래_안_온_손님은_지운다()
		{
			WorldIdentityRegistry book = OldTimer(out int id);

			int pruned = book.PruneGuests(TODAY, FORGET_AFTER, (one) => false);

			Assert.That(pruned, Is.EqualTo(1), "아무것도 안 남긴 손님까지 안 지우면 장부가 영영 커진다");
			Assert.That(book.Find(id), Is.Null);
		}

		[Test]
		public void 뭔가_남긴_사람은_안_지운다()
		{
			WorldIdentityRegistry book = OldTimer(out int id);

			int pruned = book.PruneGuests(TODAY, FORGET_AFTER, (one) => one == id);

			Assert.That(pruned, Is.Zero, "세계에 뭔가 남긴 사람을 지우면 그 사람의 세계가 사라진다");
			Assert.That(book.Find(id), Is.Not.Null);
		}

		/// <summary>★ 이 판에서 고친 자리 — 이름은 「나 여기 산다」는 표시다.</summary>
		[Test]
		public void 이름을_지은_사람은_빈손이어도_안_지운다()
		{
			WorldIdentityRegistry book = OldTimer(out int id, name: "요네");

			int pruned = book.PruneGuests(TODAY, FORGET_AFTER, (one) => false);

			Assert.That(pruned, Is.Zero,
				"이름을 지운 사람은 다음에 왔을 때 남이 된다 — 열쇠도 이름도 없다");
			Assert.That(book.Find(id), Is.Not.Null);
			Assert.That(book.NameOf(id), Is.EqualTo("요네"));
		}

		[Test]
		public void 어제_온_사람은_빈손이어도_안_지운다()
		{
			WorldIdentityRegistry book = new WorldIdentityRegistry();
			WorldIdentityRecord person = book.Recognize(string.Empty, out bool _, TODAY - 1);

			int pruned = book.PruneGuests(TODAY, FORGET_AFTER, (one) => false);

			Assert.That(pruned, Is.Zero, "오래 안 온 것이 아니면 지울 이유가 없다");
			Assert.That(book.Find(person.id), Is.Not.Null);
		}
	}
}
