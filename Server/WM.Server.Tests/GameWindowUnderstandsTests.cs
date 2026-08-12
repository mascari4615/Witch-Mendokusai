using System.Text.Json;
using NUnit.Framework;
using WitchMendokusai.Net;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 게임 창의 <b>말그릇</b>이 세계의 말과 맞나 (TASK-WM-261).
	///
	/// ★ 왜 이 자리인가: 게임 창(유니티)은 JsonUtility 로 읽는다 — 칸 이름이 <b>글자 그대로</b>
	///   맞아야 담기고, 안 맞으면 <b>조용히 0/빈 글자</b>가 된다(예외도 안 난다).
	///   유니티 러너는 며칠씩 죽어 있을 수 있으므로(TASK-WM-221), 그 갈라짐을 여기서 잡는다.
	///   여기 쓰는 것은 서버가 <b>실제로 내보내는 글자</b>다 — 손으로 적은 사본이 아니다.
	/// </summary>
	public sealed class GameWindowUnderstandsTests
	{
		// 칸 이름이 다르면 안 담긴다 — 유니티의 JsonUtility 와 같은 잣대다.
		private static readonly JsonSerializerOptions Strict = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = false,
			IncludeFields = true,
		};

		[Test]
		public void 누가_말했다를_게임_창이_읽는다()
		{
			SaidMessage came = JsonSerializer.Deserialize<SaidMessage>(Protocol.Said(7, "마스카", "안녕"), Strict);

			Assert.AreEqual(NetMessageType.SAID, came.type);
			Assert.AreEqual(7, came.dollId);
			Assert.AreEqual("마스카", came.name);
			Assert.AreEqual("안녕", came.text);
		}

		[Test]
		public void 누가_맞았다를_게임_창이_읽는다()
		{
			HurtMessage came = JsonSerializer.Deserialize<HurtMessage>(Protocol.Hurt(3, 9, 40, false), Strict);

			Assert.AreEqual(3, came.dollId);
			Assert.AreEqual(9, came.by);
			Assert.AreEqual(40, came.health, "몸이 0 으로 읽히면 게임 창은 모두가 쓰러진 세계를 그린다");
			Assert.IsFalse(came.down);

			HurtMessage fell = JsonSerializer.Deserialize<HurtMessage>(Protocol.Hurt(3, 9, 0, true), Strict);
			Assert.IsTrue(fell.down);
		}

		[Test]
		public void 저_세계로_가라를_게임_창이_읽는다()
		{
			MoveOnMessage came = JsonSerializer.Deserialize<MoveOnMessage>(
				Protocol.MoveOn("서", "ws://127.0.0.1:5199/ws", -1.5f, 2.25f, "몸통|도장"), Strict);

			Assert.AreEqual("서", came.zone);
			Assert.AreEqual("ws://127.0.0.1:5199/ws", came.address);
			Assert.AreEqual(-1.5f, came.x, 0.01f);
			Assert.AreEqual(2.25f, came.z, 0.01f);
			Assert.AreEqual("몸통|도장", came.pass, "통행증이 빈 글자로 읽히면 저 세계가 안 받아 준다");
		}

		[Test]
		public void 게임_창이_보내는_말을_세계가_읽는다()
		{
			// 반대 방향 — 게임 창이 <b>제 말그릇으로 지은</b> 글자를 세계의 손이 그대로 읽어야 한다.
			using JsonDocument say = JsonDocument.Parse(
				JsonSerializer.Serialize(new SayMessage { text = "안녕" }, Strict));
			Assert.AreEqual(Protocol.SAY, say.RootElement.GetProperty("type").GetString());
			Assert.AreEqual("안녕", say.RootElement.GetProperty("text").GetString(),
				"세계는 「text」 칸만 읽는다 — 이름이 다르면 말이 조용히 사라진다");

			using JsonDocument strike = JsonDocument.Parse(
				JsonSerializer.Serialize(new StrikeMessage { targetId = 5 }, Strict));
			Assert.AreEqual(Protocol.STRIKE, strike.RootElement.GetProperty("type").GetString());
			Assert.AreEqual(5, strike.RootElement.GetProperty("targetId").GetInt32(),
				"세계는 「targetId」 칸만 읽는다 — 이름이 다르면 늘 0번을 때린다(아무 일도 안 일어난다)");
		}
	}
}
