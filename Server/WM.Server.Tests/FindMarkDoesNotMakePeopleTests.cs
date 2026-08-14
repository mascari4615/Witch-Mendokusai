using NUnit.Framework;
using WitchMendokusai.Identity;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>내보내려고 찾는</b> 자리는 사람을 만들지 않는다 (TASK-WM-377).
	///
	/// ★ 왜: 맞이하는 자리(<c>RecognizeMark</c>)는 없으면 만드는 것이 맞다. 그런데 「옆 세계가 받았다니
	///   여기서는 놓는다」처럼 <b>지우려고</b> 찾을 때 그걸 쓰면, 모르는 이름표가 올 때마다
	///   빈 신원이 하나 생겼다 곧 지워진다 — 장부가 이유 없이 자란다(그 장부는 저장된다).
	/// </summary>
	public sealed class FindMarkDoesNotMakePeopleTests
	{
		[Test]
		public void 모르는_이름표를_찾아도_장부가_안_자란다()
		{
			WorldIdentityRegistry book = new WorldIdentityRegistry();
			book.Recognize("열쇠-하나", out bool _);
			int before = book.Count;

			Assert.That(book.FindMark("아무도-모르는-이름표"), Is.Zero, "모르는 이름표는 0 이어야 한다");
			Assert.That(book.Count, Is.EqualTo(before), "찾기만 했는데 사람이 생겼다");
		}

		[Test]
		public void 아는_이름표는_그_사람을_준다()
		{
			WorldIdentityRegistry book = new WorldIdentityRegistry();
			WorldIdentityRecord person = book.Recognize("열쇠-둘", out bool _);

			Assert.That(book.FindMark(WorldIdentityRegistry.MarkOf(person)), Is.EqualTo(person.id),
				"이 이름표를 못 찾으면 국경 너머 도착 소식이 아무도 안 놓는다");
		}
	}
}
