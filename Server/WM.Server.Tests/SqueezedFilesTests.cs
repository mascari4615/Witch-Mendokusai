using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 창을 이루는 파일이 <b>미리 최고로 눌려</b> 나가나 (TASK-WM-226).
	///
	/// ★ 왜 시험이 필요한가: 이건 눈으로 안 보인다. 안 눌려도 창은 똑같이 뜬다 —
	///   회선이 좋은 자리에서는 아무 차이가 없기 때문이다. 차이가 나는 곳(모바일)에서는
	///   개발자가 안 본다. 그래서 기계가 <b>바이트 수</b>로 지킨다.
	/// </summary>
	public sealed class SqueezedFilesTests
	{
		private const int PORT = 5412;

		private WebApplication app;
		private string worldFile;

		[SetUp]
		public async Task SetUp()
		{
			worldFile = Path.Combine(Path.GetTempPath(), "wm-squeeze-" + Path.GetRandomFileName() + ".json");
			WorldHost host = new WorldHost(new WorldStore(worldFile));
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
		public void 이미_눌린_것은_또_안_누른다()
		{
			Assert.IsTrue(StaticSqueeze.WorthSqueezing("a/b/three.module.min.js"));
			Assert.IsTrue(StaticSqueeze.WorthSqueezing("index.html"));
			Assert.IsFalse(StaticSqueeze.WorthSqueezing("art/face.png"), "그림은 눌러도 안 줄고 CPU 만 쓴다");
			Assert.IsFalse(StaticSqueeze.WorthSqueezing("font/body.woff2"));
		}

		[Test]
		public void 문은_index_다()
		{
			Assert.AreEqual("/index.html", StaticSqueeze.Normalize("/"), "「/」 를 놓치면 제일 큰 파일이 샌다");
			Assert.AreEqual("/vendor/x.js", StaticSqueeze.Normalize("/vendor/x.js"));
			Assert.AreEqual("text/javascript; charset=utf-8", StaticSqueeze.KindOf("/vendor/x.js"));
			Assert.AreEqual("text/html; charset=utf-8", StaticSqueeze.KindOf("/"));
		}

		[Test]
		public async Task 창이_받는_양이_원본보다_훨씬_작다()
		{
			using HttpClient window = new HttpClient();
			window.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");
			// 창이 스스로 풀어 버리면 「얼마나 왔나」를 못 재므로, 푼 뒤 크기가 아니라 <b>온 바이트</b>를 본다.

			// 뒤에서 누르는 중이다 — 다 눌릴 때까지 기다린다(안 눌린 동안은 원본이 나가는 것이 정상이다).
			using CancellationTokenSource timeout = TestTimeout.After(30);
			byte[] came = Array.Empty<byte>();
			string encoding = null;
			while (timeout.IsCancellationRequested == false)
			{
				using HttpResponseMessage answer = await window.GetAsync($"http://127.0.0.1:{PORT}/vendor/three.module.min.js");
				came = await answer.Content.ReadAsByteArrayAsync();
				encoding = answer.Content.Headers.ContentEncoding.ToString();
				if (encoding == "br")
					break;

				await Task.Delay(300);
			}

			long raw = new FileInfo(Path.Combine(AppContext.BaseDirectory, "wwwroot", "vendor", "three.module.min.js")).Length;

			Assert.AreEqual("br", encoding, "눌러 보내지 않았다 — 좁은 회선에서 창이 백지가 된다");
			Assert.Less(came.Length, raw / 3,
				$"눌린 것이 {came.Length}B 밖에 안 줄었다 (원본 {raw}B) — 최고로 누른 게 아니다");
		}

		[Test]
		public async Task 첫_문도_눌려_나간다()
		{
			using HttpClient window = new HttpClient();
			window.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");

			using CancellationTokenSource timeout = TestTimeout.After(30);
			string encoding = null;
			string kind = null;
			while (timeout.IsCancellationRequested == false)
			{
				using HttpResponseMessage answer = await window.GetAsync($"http://127.0.0.1:{PORT}/");
				encoding = answer.Content.Headers.ContentEncoding.ToString();
				kind = answer.Content.Headers.ContentType?.MediaType;
				if (encoding == "br")
					break;

				await Task.Delay(300);
			}

			Assert.AreEqual("br", encoding, "「/」 가 안 눌렸다 — 창이 처음 여는 문이 제일 크다");
			Assert.AreEqual("text/html", kind, "글 종류를 안 붙이면 창이 화면 대신 글자를 본다");
		}

		[Test]
		public async Task 두_번째_오는_창은_다시_안_받는다()
		{
			using HttpClient window = new HttpClient();
			window.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");

			using CancellationTokenSource timeout = TestTimeout.After(30);
			string tag = null;
			while (timeout.IsCancellationRequested == false)
			{
				using HttpResponseMessage first = await window.GetAsync($"http://127.0.0.1:{PORT}/vendor/three.module.min.js");
				tag = first.Headers.ETag?.ToString();
				if (tag != null)
					break;

				await Task.Delay(300);
			}

			Assert.IsNotNull(tag, "이름표가 없으면 창은 매번 처음부터 받는다");

			using HttpRequestMessage again = new HttpRequestMessage(HttpMethod.Get,
				$"http://127.0.0.1:{PORT}/vendor/three.module.min.js");
			again.Headers.AcceptEncoding.ParseAdd("br");
			again.Headers.TryAddWithoutValidation("If-None-Match", tag);
			using HttpResponseMessage second = await window.SendAsync(again);
			byte[] came = await second.Content.ReadAsByteArrayAsync();

			Assert.AreEqual(System.Net.HttpStatusCode.NotModified, second.StatusCode,
				"다시 온 창에게 138KB 를 또 보내면 누르기로 번 것을 도로 빼앗는 것이다");
			Assert.AreEqual(0, came.Length, "안 바뀌었다고 해 놓고 몸통을 또 보냈다");
		}

		[Test]
		public async Task br_을_모르는_창에도_그대로_준다()
		{
			using HttpClient old = new HttpClient();
			using HttpResponseMessage answer = await old.GetAsync($"http://127.0.0.1:{PORT}/");

			Assert.IsTrue(answer.IsSuccessStatusCode, "옛 창이 문 앞에서 막히면 안 된다");
			string text = await answer.Content.ReadAsStringAsync();
			Assert.IsTrue(text.Contains("<html", StringComparison.OrdinalIgnoreCase) || text.Contains("<!doctype", StringComparison.OrdinalIgnoreCase),
				"눌리지 않은 창에 화면이 아닌 것이 갔다");
		}
	}
}
