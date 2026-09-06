using NUnit.Framework;
using WitchMendokusai.Identity;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 신원은 <b>세계를 건넌다</b> (TASK-WM-259).
	///
	/// ★ 번호(id)는 세계마다 따로 매긴다 — 동쪽의 1번과 서쪽의 1번은 다른 사람이다.
	///   건너는 것은 <b>이름표</b>다: 계정이거나 열쇠의 지문. 지문은 세계가 달라도 같은 값이라
	///   저쪽 세계가 그 지문 그대로 적어 두면, 그 사람은 제 열쇠로 다시 인사할 때 이어서 나다.
	/// </summary>
	public class IdentityCrossesZonesTests
	{
		[Test]
		public void 지문으로_이어_주면_제_열쇠로_다시_와도_나다()
		{
			// 동쪽 세계 — 여기서 사람이 생기고 열쇠를 받는다.
			WorldIdentityRegistry east = new WorldIdentityRegistry();
			WorldIdentityRecord there = east.Recognize(string.Empty, out _, out string myKey, 1);
			string mark = WorldIdentityRegistry.MarkOf(there);

			// 서쪽 세계 — 이 사람을 처음 본다. 통행증의 이름표로 이어 준다.
			WorldIdentityRegistry west = new WorldIdentityRegistry();
			WorldIdentityRecord here = west.RecognizeMark(mark, myKey, 1);
			Assert.IsNotNull(here);

			// 그리고 <b>통행증 없이</b> 제 열쇠로 다시 인사해도 같은 사람이어야 한다.
			WorldIdentityRecord again = west.Recognize(myKey, out bool created, out _, 2);
			Assert.IsFalse(created, "제 열쇠를 내밀었는데 새 사람이 됐다 — 국경을 넘으면 모은 게 사라진다");
			Assert.AreEqual(here.id, again.id);
		}

		[Test]
		public void 세계마다_번호가_달라도_같은_사람이다()
		{
			WorldIdentityRegistry east = new WorldIdentityRegistry();
			WorldIdentityRegistry west = new WorldIdentityRegistry();

			// 서쪽에 이미 세 사람이 산다 — 그러니 번호가 겹칠 수밖에 없다.
			for (int i = 0; i < 3; i++)
				west.Recognize(string.Empty, out _, out _, 1);

			WorldIdentityRecord traveller = east.Recognize(string.Empty, out _, out string key, 1);
			WorldIdentityRecord landed = west.RecognizeMark(WorldIdentityRegistry.MarkOf(traveller), key, 1);

			Assert.AreNotEqual(0, landed.id);
			Assert.AreEqual(WorldIdentityRegistry.MarkOf(traveller), WorldIdentityRegistry.MarkOf(landed),
				"이름표가 같아야 같은 사람이다 — 번호는 세계 안에서만 뜻이 있다");
		}

		[Test]
		public void 계정_이름표는_계정으로_이어진다()
		{
			WorldIdentityRegistry west = new WorldIdentityRegistry();
			WorldIdentityRecord made = west.RecognizeMark("karmolab:mascari", "기기 열쇠", 1);

			Assert.AreEqual("karmolab:mascari", made.externalId);
			Assert.AreEqual(made.id, west.RecognizeExternal("karmolab:mascari", "기기 열쇠", 2, out bool created).id);
			Assert.IsFalse(created, "같은 계정으로 두 사람이 생기면 그건 두 사람이 된 것이다");
		}

		[Test]
		public void 이름표가_없으면_아무도_아니다()
		{
			WorldIdentityRegistry west = new WorldIdentityRegistry();

			Assert.IsNull(west.RecognizeMark(null, "열쇠", 1));
			Assert.IsNull(west.RecognizeMark(string.Empty, "열쇠", 1));
			Assert.IsNull(west.TryFind("모르는 열쇠"), "안 아는 열쇠에 사람을 만들어 주면 「있나 없나」를 못 묻는다");
		}
	}
}
