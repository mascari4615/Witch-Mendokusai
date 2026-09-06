using NUnit.Framework;
using WitchMendokusai.Identity;

namespace WitchMendokusai.Tests
{
	/// <summary>세계가 사람을 <b>뭐라고 부르나</b> (TASK-WM-218).</summary>
	public sealed class WorldIdentityNameTests
	{
		[Test]
		public void 이름이_없으면_손님으로_부른다()
		{
			WorldIdentityRegistry registry = new WorldIdentityRegistry();
			WorldIdentityRecord guest = registry.Recognize(string.Empty, out _, 0);

			Assert.AreEqual("손님 " + guest.id, registry.NameOf(guest.id), "빈칸으로 두면 창마다 다르게 부른다");
		}

		[Test]
		public void 계정_이름이_있으면_그_이름이다()
		{
			WorldIdentityRegistry registry = new WorldIdentityRegistry();
			WorldIdentityRecord person = registry.Recognize(string.Empty, out _, 0);

			registry.NameIfEmpty(person.id, "mascari");

			Assert.AreEqual("mascari", registry.NameOf(person.id));
		}

		[Test]
		public void 이미_이름이_있으면_안_덮어쓴다()
		{
			WorldIdentityRegistry registry = new WorldIdentityRegistry();
			WorldIdentityRecord person = registry.Recognize(string.Empty, out _, 0);

			registry.NameIfEmpty(person.id, "먼저");
			registry.NameIfEmpty(person.id, "나중");

			Assert.AreEqual("먼저", registry.NameOf(person.id), "사람이 고친 이름을 계정이 덮으면 안 된다");
		}

		[Test]
		public void 모르는_사람은_빈_이름이다()
		{
			Assert.AreEqual(string.Empty, new WorldIdentityRegistry().NameOf(9999));
		}

		[Test]
		public void 이름은_껐다_켜도_남는다()
		{
			WorldIdentityRegistry registry = new WorldIdentityRegistry();
			WorldIdentityRecord person = registry.Recognize(string.Empty, out _, 0);
			registry.NameIfEmpty(person.id, "mascari");

			WorldIdentityRegistry reborn = new WorldIdentityRegistry();
			reborn.Load(registry.Save());

			Assert.AreEqual("mascari", reborn.NameOf(person.id), "이름이 날아가면 남이 나를 못 알아본다");
		}
	}
}
