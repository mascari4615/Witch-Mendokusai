using System.IO;
using NUnit.Framework;
using WitchMendokusai.Identity;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>세계 파일에 열쇠가 그대로 남지 않는다</b> (TASK-WM-220).
	///
	/// ★ 왜: 전에는 창이 갖고 다니는 열쇠를 세계가 그대로 적어 뒀다. 그 파일 한 장이 새면
	///   (백업·지원 문의·실수로 올린 로그) 모두의 신원을 그대로 가져갈 수 있다.
	///   이제 세계는 <b>지문</b>만 갖는다 — 지문에서 열쇠는 못 되돌린다.
	/// </summary>
	public class SecretIsNotStoredTests
	{
		/// <summary>세계가 파일에 적는 그 모양 그대로 뜬다(필드까지).</summary>
		private static string Dump(WorldIdentityBook book)
		{
			return System.Text.Json.JsonSerializer.Serialize(book,
				new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
		}

		[Test]
		public void 새_사람의_열쇠는_그때만_주고_장부에는_지문만_남는다()
		{
			WorldIdentityRegistry people = new WorldIdentityRegistry();

			WorldIdentityRecord person = people.Recognize(string.Empty, out bool created, out string granted);

			Assert.That(created, Is.True);
			Assert.That(granted, Is.Not.Empty, "창에 줄 열쇠가 없다 — 그러면 다음에 못 알아본다");
			Assert.That(person.secret, Is.Empty, "장부에 열쇠가 그대로 남았다");
			Assert.That(person.secretHash, Is.EqualTo(WorldIdentityRegistry.Fingerprint(granted)));
		}

		[Test]
		public void 받은_열쇠로_다시_들어오면_같은_사람이다()
		{
			WorldIdentityRegistry people = new WorldIdentityRegistry();
			WorldIdentityRecord first = people.Recognize(string.Empty, out _, out string granted);

			WorldIdentityRecord again = people.Recognize(granted, out bool createdAgain, out _);

			Assert.That(createdAgain, Is.False);
			Assert.That(again.id, Is.EqualTo(first.id));
		}

		[Test]
		public void 저장한_글에_열쇠가_안_보인다()
		{
			WorldIdentityRegistry people = new WorldIdentityRegistry();
			people.Recognize(string.Empty, out _, out string granted);

			// ⚠ 저장 모양은 <b>필드</b>다 — IncludeFields 없이 뜨면 「{}」 가 나와 시험이 거짓 초록이 된다
			//   (실제로 그렇게 났다). 세계가 쓰는 그 설정 그대로 뜬다.
			string saved = Dump(people.Save());

			StringAssert.Contains("secretHash", saved, "장부를 제대로 안 떴다 — 아래 판정이 의미가 없다");
			StringAssert.DoesNotContain(granted, saved, "세계 파일에 열쇠가 그대로 적혀 있다");
			StringAssert.Contains(WorldIdentityRegistry.Fingerprint(granted), saved);
		}

		[Test]
		public void 초대_열쇠도_장부에는_지문만_남는다()
		{
			// 초대 열쇠는 3일 살아 있는 <b>남의 사람이 되는 종이</b>다 — 파일에 그대로 적으면
			// 그 파일을 본 사람이 그 사이에 남이 될 수 있다.
			WorldIdentityRegistry people = new WorldIdentityRegistry();
			WorldIdentityRecord person = people.Recognize(string.Empty, out _, out _);

			string code = people.IssueInvite(person.id, 0);
			Assert.That(code, Is.Not.Empty);

			string saved = Dump(people.Save());
			StringAssert.DoesNotContain(code, saved, "세계 파일에 초대 열쇠가 그대로 적혀 있다");
			StringAssert.Contains(WorldIdentityRegistry.Fingerprint(code), saved);

			// 그래도 그 종이는 통해야 한다.
			WorldIdentityRecord linked = people.RedeemInvite(code, "다른-기기-열쇠", 0);
			Assert.That(linked?.id, Is.EqualTo(person.id));
		}

		[Test]
		public void 옛_파일의_평문_열쇠는_읽으면서_지문으로_옮긴다()
		{
			// 옛 세계 파일 모양 — 열쇠가 그대로 적혀 있다.
			WorldIdentityBook old = new WorldIdentityBook
			{
				nextId = 2,
				people = new[] { new WorldIdentityRecord { id = 1, secret = "옛-열쇠-1234", lastSeenDay = 3 } },
			};

			WorldIdentityRegistry people = new WorldIdentityRegistry();
			people.Load(old);

			// 쓰던 열쇠는 그대로 통해야 한다(사람은 아무것도 안 바뀐 것처럼 느낀다).
			WorldIdentityRecord same = people.Recognize("옛-열쇠-1234", out bool created, out _);
			Assert.That(created, Is.False);
			Assert.That(same.id, Is.EqualTo(1));

			// 그리고 다시 적을 때는 평문이 사라진다.
			string saved = Dump(people.Save());
			StringAssert.DoesNotContain("옛-열쇠-1234", saved);
		}
	}
}
