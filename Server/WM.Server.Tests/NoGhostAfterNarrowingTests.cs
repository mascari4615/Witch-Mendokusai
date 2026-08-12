using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 좁힘에서 돌아온 창에 <b>유령이 안 남는다</b> (TASK-WM-246).
	///
	/// ★ 무엇이었나: 회선이 못 따라오는 창에는 작은 한 장을 준다(TASK-WM-228). 그런데 그 작은 장에는
	///   <b>「그 사람 나갔다」가 없다</b> — 칸 장부를 안 쓰기 때문이다. 그래서 좁힘 동안 떠난 사람이
	///   그 창에 <b>영영</b> 남았다. 화면에는 서 있는 사람으로 보이고, 말을 걸어도 대답이 없다.
	///   CI 의 느린 러너가 이 자리를 잡았다(빠른 기계에서는 좁힘 자체가 잘 안 일어난다).
	///
	/// ★ 고침: 좁힘에서 돌아올 때 <b>전체 한 장</b>을 준다. 그 한 장이 유령을 지운다.
	/// </summary>
	public sealed class NoGhostAfterNarrowingTests
	{
		private const int PORT = 5417;

		private static readonly Uri address = new Uri($"ws://127.0.0.1:{PORT}/ws");

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-ghost-" + Path.GetRandomFileName() + ".json");
			host = new WorldHost(new WorldStore(worldFile));
			app = host.Build(Array.Empty<string>(), $"http://127.0.0.1:{PORT}");
			await app.StartAsync();
		}

		[TearDown]
		public async Task TearDown()
		{
			if (app != null)
			{
				await app.StopAsync();
				await app.DisposeAsync();
				app = null;
			}

			foreach (string path in new[] { worldFile, worldFile + ".bak", worldFile + ".tmp" })
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		[Test]
		public async Task 좁힘_동안_떠난_사람이_돌아온_뒤_지워진다()
		{
			using ClientWebSocket viewer = await JoinAsync();
			using ClientWebSocket traveler = await JoinAsync();

			WorldDoll[] people = host.World.Snapshot();
			Assert.AreEqual(2, people.Length, "이 시험은 둘만 있는 세계를 본다");
			int travelerId = people[1].Id;

			// 둘 다 보이는 것부터 확인한다(여기서 못 보면 아래가 뜻이 없다).
			await ReadUntilAsync(viewer, text => text.Contains("\"id\":" + travelerId), 15);

			// ① 회선이 못 따라오는 상태로 만든다 — 이제 이 창은 작은 한 장만 받는다.
			host.MarkBehindForTest(InterestCrowd.MISSES_BEFORE_NARROWING + 2);
			await Task.Delay(300);

			// ② 그 사이에 그 사람이 멀리 떠난다(반경 32 + 칸 16 밖으로).
			for (int step = 0; step < 70; step++)
				host.World.TryMove(travelerId, new Vector3(1f, 0f, 0f));

			await Task.Delay(300);

			// ⚠ 줄에는 좁힘 <b>중에</b> 나간 옛 판들이 쌓여 있다. 그것들을 「풀린 뒤의 판」으로 오해하면
			//   이 시험은 <b>거짓 초록</b>이 된다(실제로 그랬다 — 고침을 되돌려도 통과했다).
			//   그래서 판 번호를 적어 두고, 그보다 <b>뒤에 나온 판</b>만 본다.
			//   ⚠ 번호는 소켓이 아니라 <b>세계에</b> 묻는다 — 시간 재며 읽다가 취소하면 그 소켓이 끊긴다.
			long lastBefore = host.LastSnapshotSequence;

			// ③ 회선이 풀린다 — 이때 <b>전체 한 장</b>이 와야 유령이 지워진다.
			//   전체가 아니면(바뀐 것만 오면) 떠난 사람을 지울 말이 <b>영영</b> 안 온다.
			host.MarkBehindForTest(0);

			string repair = await ReadUntilAsync(viewer,
				text => text.Contains("\"type\":\"world\"") && SequenceOf(text) > lastBefore, 15);

			Assert.IsFalse(repair.Contains("\"changed\""),
				"좁힘에서 풀린 뒤 <b>바뀐 것만</b> 왔다 — 좁힘 동안 떠난 사람을 지울 말이 영영 안 온다: " + repair);
			Assert.IsFalse(repair.Contains("\"id\":" + travelerId), "떠난 사람이 아직 실려 온다: " + repair);
		}

		/// <summary>그 판의 번호 — 없으면 -1.</summary>
		private static long SequenceOf(string text)
		{
			const string mark = "\"sequence\":";
			int at = text.IndexOf(mark, StringComparison.Ordinal);
			if (at < 0)
				return -1;

			int from = at + mark.Length;
			int to = from;
			while (to < text.Length && char.IsDigit(text[to]))
				to += 1;

			return to > from && long.TryParse(text.Substring(from, to - from), out long said) ? said : -1;
		}

		private static async Task<ClientWebSocket> JoinAsync()
		{
			ClientWebSocket window = new ClientWebSocket();
			await window.ConnectAsync(address, CancellationToken.None);
			byte[] hello = Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"secret\":\"\"}");
			await window.SendAsync(new ArraySegment<byte>(hello), WebSocketMessageType.Text, true, CancellationToken.None);
			return window;
		}

		private static async Task<string> ReadUntilAsync(ClientWebSocket window, Func<string, bool> matches, int seconds)
		{
			using CancellationTokenSource timeout = TestTimeout.After(seconds);
			byte[] bin = new byte[65536];

			while (timeout.IsCancellationRequested == false)
			{
				WebSocketReceiveResult came;
				try
				{
					came = await window.ReceiveAsync(new ArraySegment<byte>(bin), timeout.Token);
				}
				catch (OperationCanceledException)
				{
					break;
				}

				if (came.MessageType == WebSocketMessageType.Close)
					break;

				string text = Encoding.UTF8.GetString(bin, 0, came.Count);
				if (matches(text))
					return text;
			}

			Assert.Fail("기다린 말이 안 왔다");
			return null;
		}
	}
}
