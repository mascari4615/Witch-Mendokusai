using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 세계가 <b>스스로 제 상태를 적어 둔다</b> (TASK-WM-297).
	///
	/// ★ 왜: 「며칠 돌면 어떻게 되나」는 prod(노트북 24시간)에서만 답이 나온다. 그런데 그 답을 볼
	///   기록이 없으면, 서버가 죽는 순간 그때까지의 상태도 같이 사라진다 — 영영 모른다.
	/// </summary>
	public sealed class HealthJournalTests
	{
		private const int PORT = 5471;

		private WebApplication app;
		private string worldFile;

		[TearDown]
		public async Task TearDown()
		{
			Environment.SetEnvironmentVariable("WM_HEALTH_EVERY_MS", null);

			if (app != null)
			{
				await app.StopAsync();
				await app.DisposeAsync();
				app = null;
			}

			foreach (string one in new[] { worldFile, worldFile + ".bak", worldFile + ".tmp", worldFile + ".health.jsonl" })
			{
				if (one != null && File.Exists(one))
					File.Delete(one);
			}
		}

		[Test]
		public async Task 세계가_제_상태를_적어_둔다()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-journal-" + Path.GetRandomFileName() + ".json");
			Environment.SetEnvironmentVariable("WM_HEALTH_EVERY_MS", "200");

			WorldHost host = new WorldHost(new WorldStore(worldFile));
			app = host.Build(Array.Empty<string>(), $"http://127.0.0.1:{PORT}");
			await app.StartAsync();

			string journal = worldFile + ".health.jsonl";
			string[] lines = Array.Empty<string>();
			for (int look = 0; look < 40 && lines.Length < 2; look++)
			{
				await Task.Delay(150);
				if (File.Exists(journal) == false)
					continue;

				// ⚠ 세계가 쓰는 중일 수 있다 — 읽기가 막히면 다음 판에 다시 본다.
				try { lines = File.ReadAllLines(journal); }
				catch (IOException) { /* 다음 판 */ }
			}

			Assert.GreaterOrEqual(lines.Length, 2,
				"세계가 제 상태를 안 적으면 「며칠 돌면 어떻게 되나」는 영영 못 본다");

			StringAssert.Contains("\"people\"", lines[0]);
			StringAssert.Contains("\"heldMegabytes\"", lines[0]);
			StringAssert.Contains("\"worldMinutes\"", lines[0]);
		}

		[Test]
		public void 기록이_무한히_자라지_않는다()
		{
			// 기록이 끝없이 자라면 그것도 새는 것이다 — 오래된 것부터 버린다.
			worldFile = Path.Combine(Path.GetTempPath(), "wm-journal-" + Path.GetRandomFileName() + ".json");
			HealthJournal journal = new HealthJournal(worldFile);

			for (int i = 0; i < HealthJournal.MOST_LINES + 50; i++)
				journal.Write("{\"n\":" + i + "}");

			string[] lines = File.ReadAllLines(journal.Path);
			Assert.LessOrEqual(lines.Length, HealthJournal.MOST_LINES);
			StringAssert.Contains((HealthJournal.MOST_LINES + 49).ToString(), lines[lines.Length - 1],
				"버릴 때는 <b>오래된 것</b>부터다 — 최근 것이 사라지면 기록이 뜻을 잃는다");
		}
	}
}
