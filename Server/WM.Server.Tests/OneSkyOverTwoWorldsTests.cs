using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>두 세계 위의 하늘은 하나여야 한다</b> (TASK-WM-266).
	///
	/// ★ 왜: 땅은 나눠도(WM-252~265) 하늘은 하나다. 그런데 세계마다 <b>제 시계</b>를 굴리면
	///   국경을 넘는 순간 밤이 낮이 된다 — 사람 눈에 그건 「다른 게임으로 넘어갔다」다.
	///   지금까지 이 자리는 한 번도 안 봤다(세계를 하나만 띄우고 쟀으니까).
	///
	/// ★ 시계는 <b>맞춰 주는 것</b>이 아니라 <b>같이 유도하는 것</b>이어야 한다 —
	///   서로 맞추면 둘 다 틀린 채로 흔들리고, 한쪽이 기준이면 그 한쪽이 죽을 때 하늘이 멎는다.
	/// </summary>
	public sealed class OneSkyOverTwoWorldsTests
	{
		private const int EAST_PORT = 5461;
		private const int WEST_PORT = 5462;

		private WebApplication east;
		private WebApplication west;
		private WorldHost eastHost;
		private WorldHost westHost;
		private string eastFile;
		private string westFile;

		[TearDown]
		public async Task TearDown()
		{
			Environment.SetEnvironmentVariable("WM_ZONE", null);
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", null);
			Environment.SetEnvironmentVariable("WM_ZONE_SECRET", null);

			foreach (WebApplication one in new[] { east, west })
			{
				if (one == null)
					continue;

				await one.StopAsync();
				await one.DisposeAsync();
			}

			east = null;
			west = null;

			foreach (string path in new[] { eastFile, westFile })
			{
				if (path == null)
					continue;

				foreach (string one in new[] { path, path + ".bak", path + ".tmp" })
				{
					if (File.Exists(one))
						File.Delete(one);
				}
			}
		}

		[Test]
		public async Task 나중에_뜬_세계도_같은_시각을_본다()
		{
			eastFile = Path.Combine(Path.GetTempPath(), "wm-sky-e-" + Path.GetRandomFileName() + ".json");
			westFile = Path.Combine(Path.GetTempPath(), "wm-sky-w-" + Path.GetRandomFileName() + ".json");

			Environment.SetEnvironmentVariable("WM_ZONE_SECRET", "두 세계만 아는 말");
			Environment.SetEnvironmentVariable("WM_ZONE", "동:0,-40,40,40");
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", $"서:-40,-40,0,40=ws://127.0.0.1:{WEST_PORT}/ws");
			eastHost = new WorldHost(new WorldStore(eastFile));
			east = eastHost.Build(Array.Empty<string>(), $"http://127.0.0.1:{EAST_PORT}");
			await east.StartAsync();

			// 동쪽 세계가 <b>먼저</b> 한참 돌고, 서쪽은 나중에 뜬다 — 배포·재시작이 늘 이렇다.
			await Task.Delay(3000);

			Environment.SetEnvironmentVariable("WM_ZONE", "서:-40,-40,0,40");
			Environment.SetEnvironmentVariable("WM_ZONE_NEIGHBOURS", $"동:0,-40,40,40=ws://127.0.0.1:{EAST_PORT}/ws");
			westHost = new WorldHost(new WorldStore(westFile));
			west = westHost.Build(Array.Empty<string>(), $"http://127.0.0.1:{WEST_PORT}");
			await west.StartAsync();
			await Task.Delay(500);

			int eastMinutes = eastHost.World.Calendar.TotalMinutes();
			int westMinutes = westHost.World.Calendar.TotalMinutes();

			// 세계의 1분은 실제 몇 초쯤이다 — 몇 분 어긋나는 것은 「다른 시각」이다.
			Assert.LessOrEqual(Math.Abs(eastMinutes - westMinutes), 1,
				$"두 세계의 시각이 다르다 (동 {eastMinutes}분 · 서 {westMinutes}분) — "
				+ "국경을 넘는 순간 밤이 낮이 된다");
		}

		[Test]
		public async Task 껐다_켜도_하늘은_계속_흘렀다()
		{
			// ★ 세계의 시간은 사람이 있든 없든 흐른다 — 그러면 <b>서버가 꺼져 있던 동안</b>도
			//   흘렀어야 한다. 안 그러면 배포할 때마다 세계의 하루가 멎는다(그리고 두 세계는
			//   각자 다른 만큼 멎어서 영영 어긋난다).
			eastFile = Path.Combine(Path.GetTempPath(), "wm-sky-r-" + Path.GetRandomFileName() + ".json");

			eastHost = new WorldHost(new WorldStore(eastFile));
			east = eastHost.Build(Array.Empty<string>(), $"http://127.0.0.1:{EAST_PORT}");
			await east.StartAsync();
			await Task.Delay(1500);

			int before = eastHost.World.Calendar.TotalMinutes();
			await east.StopAsync();
			await east.DisposeAsync();
			east = null;

			// 「꺼져 있는」 시간
			await Task.Delay(3000);

			eastHost = new WorldHost(new WorldStore(eastFile));
			east = eastHost.Build(Array.Empty<string>(), $"http://127.0.0.1:{EAST_PORT}");
			await east.StartAsync();
			await Task.Delay(300);

			int after = eastHost.World.Calendar.TotalMinutes();
			Assert.Greater(after - before, 2,
				$"꺼져 있던 3초 동안 세계가 멎어 있었다 (전 {before}분 · 후 {after}분) — "
				+ "배포할 때마다 하늘이 멎으면 두 세계는 영영 어긋난다");
		}
	}
}
