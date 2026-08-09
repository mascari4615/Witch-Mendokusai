using NUnit.Framework;
using WitchMendokusai.Identity;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 사람이 <b>스스로 이름을 정한다</b> — 그리고 세계가 검사한다 (TASK-WM-218).
	///
	/// ★ 왜 세계가 검사하나: 이름은 남에게 보이는 것이다. 창이 정하게 두면 빈 이름·공백만·
	///   끝없이 긴 이름·남과 똑같은 이름이 그대로 박힌다 — 「누가 누군지」가 무너진다.
	/// </summary>
	public sealed class WorldRenameTests
	{
		private static WorldIdentityRegistry World(out int me, out int other)
		{
			WorldIdentityRegistry registry = new WorldIdentityRegistry();
			me = registry.Recognize("기기-나", out bool _, 0).id;
			other = registry.Recognize("기기-남", out bool _, 0).id;
			return registry;
		}

		[Test]
		public void 정한_이름으로_불린다()
		{
			WorldIdentityRegistry registry = World(out int me, out int _);

			Assert.IsTrue(registry.TryRename(me, "욘", out string _));
			Assert.AreEqual("욘", registry.NameOf(me));
		}

		[Test]
		public void 이름을_안_정하면_손님으로_불린다()
		{
			WorldIdentityRegistry registry = World(out int me, out int _);

			Assert.AreEqual("손님 " + me, registry.NameOf(me), "빈칸으로 두면 창마다 다르게 부르게 된다");
		}

		[Test]
		public void 앞뒤_공백은_떼고_받는다()
		{
			WorldIdentityRegistry registry = World(out int me, out int _);

			Assert.IsTrue(registry.TryRename(me, "  링  ", out string _));
			Assert.AreEqual("링", registry.NameOf(me));
		}

		[Test]
		public void 공백만_적으면_거절한다()
		{
			WorldIdentityRegistry registry = World(out int me, out int _);

			Assert.IsFalse(registry.TryRename(me, "   ", out string denied));
			Assert.AreEqual("이름이 너무 짧다", denied, "왜 안 되는지 안 알려주면 사람은 「고장」으로 읽는다");
			Assert.AreEqual("손님 " + me, registry.NameOf(me), "거절했는데 이름이 바뀌면 거절이 아니다");
		}

		[Test]
		public void 너무_긴_이름은_거절한다()
		{
			WorldIdentityRegistry registry = World(out int me, out int _);

			Assert.IsFalse(registry.TryRename(me, new string('가', WorldIdentityRegistry.MAX_NAME + 1), out string denied));
			Assert.AreEqual("이름이 너무 길다", denied, "긴 이름은 남의 화면을 덮는다");
		}

		[Test]
		public void 남과_똑같은_이름은_거절한다()
		{
			WorldIdentityRegistry registry = World(out int me, out int other);
			registry.TryRename(other, "알리사", out string _);

			Assert.IsFalse(registry.TryRename(me, "알리사", out string denied));
			Assert.AreEqual("이미 그렇게 불리는 사람이 있다", denied);
		}

		[Test]
		public void 대소문자만_다른_이름도_같은_이름이다()
		{
			WorldIdentityRegistry registry = World(out int me, out int other);
			registry.TryRename(other, "Ring", out string _);

			Assert.IsFalse(registry.TryRename(me, "ring", out string _),
				"눈으로 못 가리는 차이는 「누가 누군지」를 지킨 것이 아니다");
		}

		[Test]
		public void 내_이름을_그대로_다시_적는_것은_된다()
		{
			WorldIdentityRegistry registry = World(out int me, out int _);
			registry.TryRename(me, "욘", out string _);

			Assert.IsTrue(registry.TryRename(me, "욘", out string _), "내 이름이 나와 겹친다고 막으면 못 고친다");
		}

		[Test]
		public void 세계가_모르는_사람은_이름을_못_정한다()
		{
			WorldIdentityRegistry registry = World(out int _, out int _);

			Assert.IsFalse(registry.TryRename(9999, "누구", out string denied));
			Assert.AreEqual("세계가 모르는 사람이다", denied);
		}

		[Test]
		public void 정한_이름은_껐다_켜도_남는다()
		{
			WorldIdentityRegistry registry = World(out int me, out int _);
			registry.TryRename(me, "욘", out string _);

			WorldIdentityRegistry reborn = new WorldIdentityRegistry();
			reborn.Load(registry.Save());

			Assert.AreEqual("욘", reborn.NameOf(me), "이름이 안 남으면 다시 들어올 때마다 손님이 된다");
		}
	}
}
