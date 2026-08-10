using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>세계의 시계가 실제 시간과 같이 가는가</b> (TASK-WM-217).
	///
	/// ★ 왜: 시계는 「20Hz 로 도는 방송 루프가 한 번 돌 때마다 0.05분」씩 흐른다. 그런데 그 루프는
	///   <c>Task.Delay(50)</c> 로 쉬고, 그건 <b>정확히 50ms 가 아니다</b>(항상 조금 더 걸린다).
	///   차이는 한 번엔 눈에 안 띄지만 서버는 며칠씩 돈다 — 하루가 스무 시간처럼 흐르면
	///   밤낮·재생 시각이 전부 밀리고, 사람은 「이 세계는 시간이 이상하다」고 느낀다.
	/// </summary>
	public sealed class ClockDriftTests
	{
		private const int PORT = 5423;

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-clock-" + Path.GetRandomFileName() + ".json");
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
		public async Task 실제로_흐른_만큼_세계도_흐른다()
		{
			int before = host.World.Calendar.TotalMinutes();
			Stopwatch clock = Stopwatch.StartNew();

			// ★ 10초를 잰다 (실측 2026-08-10): 5초로 재면 「분」이 정수라 늘 최대 1분이 잘려
			//   4 vs 5 경계에 걸린다 — 고쳐 놓고도 시험이 오락가락했다. 길게 재면 그 절삭이 묻힌다.
			await Task.Delay(10000);

			clock.Stop();
			int after = host.World.Calendar.TotalMinutes();

			// 규약: 실제 1초 = 세계 1분.
			double expected = clock.Elapsed.TotalSeconds;
			int moved = after - before;

			// 10분 흐를 자리에서 1.5분 이상 어긋나면(15%) 하루에 몇 시간씩 밀린다.
			Assert.That(moved, Is.EqualTo(expected).Within(1.5),
				$"실제 {expected:0.0}초 동안 세계는 {moved}분 흘렀다 — 오래 켜 두면 밤낮이 밀린다");
		}
	}
}
