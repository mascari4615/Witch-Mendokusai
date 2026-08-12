using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 세계가 <b>자기 땅만</b> 굴린다 (TASK-WM-252).
	///
	/// ★ 왜 여기부터인가 (실측 2026-08-12): 사람이 늘 때 먼저 막히는 것은 CPU 가 아니라 회선이었다
	///   (800명에 CPU 6%, 대역 65Mbps). 한 기계의 회선은 못 늘리니, 넘으려면 세계를 나눠
	///   각자 다른 기계가 맡아야 한다. 그 첫걸음이 「여기까지가 내 땅이다」이다.
	///
	/// ★ 넘겨주기(다음 자리)가 서기 전까지는 <b>경계에 세우는 것</b>이 정직하다 —
	///   남의 땅을 내가 굴리면 두 세계가 갈라진다.
	/// </summary>
	public sealed class ZoneBorderTests
	{
		private const int PORT = 5420;

		private WebApplication app;
		private WorldHost host;
		private string worldFile;

		[TearDown]
		public async Task TearDown()
		{
			Environment.SetEnvironmentVariable("WM_ZONE", null);

			if (app != null)
			{
				await app.StopAsync();
				await app.DisposeAsync();
				app = null;
			}

			foreach (string path in new[] { worldFile, worldFile + ".bak", worldFile + ".tmp" })
			{
				if (path != null && File.Exists(path))
					File.Delete(path);
			}
		}

		private async Task StartAsync(string zone)
		{
			Environment.SetEnvironmentVariable("WM_ZONE", zone);
			worldFile = Path.Combine(Path.GetTempPath(), "wm-zone-" + Path.GetRandomFileName() + ".json");
			host = new WorldHost(new WorldStore(worldFile));
			app = host.Build(Array.Empty<string>(), $"http://127.0.0.1:{PORT}");
			await app.StartAsync();
		}

		[Test]
		public async Task 자기_땅_밖으로는_못_나간다()
		{
			await StartAsync("동:-10,-10,10,10");

			WorldDoll doll = host.World.Join();
			for (int step = 0; step < 60; step++)
				host.World.TryMove(doll.Id, new Vector3(1f, 0f, 1f));

			Vector3 stood = host.World.PositionOf(doll.Id);

			Assert.AreEqual(10f, stood.x, 0.001f, "남의 땅까지 걸어갔다 — 그 자리는 다른 세계가 굴린다");
			Assert.AreEqual(10f, stood.z, 0.001f);
		}

		[Test]
		public async Task 안_나눈_세계는_그대로_돈다()
		{
			await StartAsync(null);

			WorldDoll doll = host.World.Join();
			for (int step = 0; step < 60; step++)
				host.World.TryMove(doll.Id, new Vector3(1f, 0f, 0f));

			Assert.Greater(host.World.PositionOf(doll.Id).x, 50f,
				"안 나눈 세계까지 막으면 지금 도는 세계가 멈춘다");
		}

		[Test]
		public async Task 세계가_자기_땅을_말해_준다()
		{
			await StartAsync("동:-10,-10,10,10");

			using HttpClient asking = new HttpClient();
			string health = await asking.GetStringAsync($"http://127.0.0.1:{PORT}/health");

			StringAssert.Contains("동", health, "어느 땅을 맡았는지 밖에서 못 보면 여러 세계를 못 굴린다");
		}
	}
}
