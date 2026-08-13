using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 들판 소식을 창마다 옳게 고르나 (TASK-WM-343).
	///
	/// ★ 이 시험이 왜 있나: 같은 것을 브라우저 관문으로 재면 한 판에 2~3분이 걸리고 <b>판마다 흔들린다</b>
	///   (같은 코드로 3/3·2/3·1/3 을 다 봤다). 그 흔들림에 두 번 속아 「고쳐졌다」·「나빠졌다」를 잘못 읽었다.
	///   여기서는 밀리초에 갈린다.
	/// </summary>
	public sealed class FieldNewsTests
	{
		private static Dictionary<int, int> Field(params int[] ids)
		{
			Dictionary<int, int> made = new Dictionary<int, int>();
			foreach (int id in ids)
				made[id] = 1;

			return made;
		}

		[Test]
		public void 처음_보는_창에는_통째로()
		{
			FieldNews news = new FieldNews();

			FieldNews.Choice choice = news.PickFor("0:0", 1, Field(10, 11, 12), 0);

			Assert.That(choice.Whole, Is.True);
			Assert.That(choice.Changed, Is.EquivalentTo(new[] { 10, 11, 12 }));
		}

		[Test]
		public void 바로_앞_판까지_받은_창에는_델타()
		{
			FieldNews news = new FieldNews();
			news.PickFor("0:0", 1, Field(10, 11, 12), 0);

			FieldNews.Choice choice = news.PickFor("0:0", 2, Field(10, 12), 1);

			Assert.That(choice.Whole, Is.False);
			Assert.That(choice.Gone, Is.EquivalentTo(new[] { 11 }), "없어진 자리를 알려 줘야 한다");
		}

		/// <summary>
		/// ★ 오늘의 결함 (TASK-WM-343): 한 판을 건너뛴 창은 그 판의 「없어졌다」를 되살릴 수 없다 —
		/// 그러니 <b>통째로</b> 줘야 한다. 예전 코드는 델타를 줬고, 그 델타는 이미 빈손이라
		/// 그 창은 없어진 자리를 <b>영영</b> 그렸다.
		/// </summary>
		[Test]
		public void 한_판_건너뛴_창에는_통째로()
		{
			FieldNews news = new FieldNews();
			news.PickFor("0:0", 1, Field(10, 11, 12), 0);   // 판 1 — 모두 받음
			news.PickFor("0:0", 2, Field(10, 12), 1);       // 판 2 — 11 이 없어졌다(이 창은 이걸 놓쳤다)

			FieldNews.Choice choice = news.PickFor("0:0", 3, Field(10, 12), 1);

			Assert.That(choice.Whole, Is.True, "놓친 판의 「없어졌다」는 되살릴 수 없다 — 통째로 줘야 한다");
			Assert.That(choice.Changed, Is.EquivalentTo(new[] { 10, 12 }));
		}

		/// <summary>
		/// ★ 오늘의 두 번째 결함: 한 창이 <b>통째로</b> 받아 갔다고 해서 같은 판의 다른 창이 빈손이 되면 안 된다.
		/// (예전에는 먼저 만들어진 판이 칸 장부를 갱신해, 뒤에 델타를 받는 창의 「없어졌다」가 사라졌다.)
		/// </summary>
		[Test]
		public void 통째로_받아간_창이_있어도_다른_창의_델타는_멀쩡하다()
		{
			FieldNews news = new FieldNews();
			news.PickFor("0:0", 1, Field(10, 11, 12), 0);

			// 밀린 창이 먼저 통째로 받아 간다(같은 판).
			FieldNews.Choice behind = news.PickFor("0:0", 2, Field(10, 12), 0);
			Assert.That(behind.Whole, Is.True);

			// 바로 뒤이어 성한 창이 델타를 받는다 — 「11 이 없어졌다」가 그대로 있어야 한다.
			FieldNews.Choice healthy = news.PickFor("0:0", 2, Field(10, 12), 1);
			Assert.That(healthy.Whole, Is.False);
			Assert.That(healthy.Gone, Is.EquivalentTo(new[] { 11 }), "남이 통째로 받아 갔다고 내 소식이 사라지면 안 된다");
		}

		[Test]
		public void 아무것도_안_바뀌면_델타는_비어_있다()
		{
			FieldNews news = new FieldNews();
			news.PickFor("0:0", 1, Field(10, 11), 0);

			FieldNews.Choice choice = news.PickFor("0:0", 2, Field(10, 11), 1);

			Assert.That(choice.Whole, Is.False);
			Assert.That(choice.Changed, Is.Empty);
			Assert.That(choice.Gone, Is.Empty);
		}
	}
}
