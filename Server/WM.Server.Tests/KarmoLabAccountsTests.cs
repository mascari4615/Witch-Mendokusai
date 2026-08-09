using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// KarmoLab 계정에 물어보는 자리 (TASK-WM-218) — 남의 서버가 죽어도 우리 세계는 열려야 한다.
	/// </summary>
	public sealed class KarmoLabAccountsTests
	{
		private sealed class FakeServer : HttpMessageHandler
		{
			private readonly Func<HttpRequestMessage, HttpResponseMessage> reply;

			public FakeServer(Func<HttpRequestMessage, HttpResponseMessage> reply)
			{
				this.reply = reply;
			}

			public HttpRequestMessage LastRequest { get; private set; }

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				LastRequest = request;
				return Task.FromResult(reply(request));
			}
		}

		private static HttpResponseMessage Json(string body)
		{
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
		}

		[Test]
		public async Task 로그인한_사람은_계정_이름표로_온다()
		{
			FakeServer fake = new FakeServer(_ => Json("{\"account\":{\"handle\":\"mascari\",\"displayName\":\"윤\"}}"));
			KarmoLabAccounts accounts = new KarmoLabAccounts("http://kl.test", fake);

			string id = await accounts.TryResolveAsync("세션값");

			Assert.AreEqual("karmolab:mascari", id);
			StringAssert.Contains("kl_session=세션값", string.Join(";", fake.LastRequest.Headers.GetValues("Cookie")));
		}

		[Test]
		public async Task 로그인_안_한_사람은_손님이다()
		{
			KarmoLabAccounts accounts = new KarmoLabAccounts("http://kl.test", new FakeServer(_ => Json("{\"account\":null}")));

			Assert.IsNull(await accounts.TryResolveAsync("세션값"));
		}

		[Test]
		public async Task 세션이_없으면_묻지도_않는다()
		{
			FakeServer fake = new FakeServer(_ => Json("{\"account\":{\"handle\":\"mascari\"}}"));
			KarmoLabAccounts accounts = new KarmoLabAccounts("http://kl.test", fake);

			Assert.IsNull(await accounts.TryResolveAsync(null));
			Assert.IsNull(await accounts.TryResolveAsync("   "));
			Assert.IsNull(fake.LastRequest, "쓸데없이 남의 서버를 두드리지 않는다.");
		}

		[Test]
		public async Task 남의_서버가_죽어도_우리_세계는_열린다()
		{
			KarmoLabAccounts accounts = new KarmoLabAccounts(
				"http://kl.test",
				new FakeServer(_ => throw new HttpRequestException("연결 안 됨")));

			// fail-open — 계정은 「더 좋은 경우」이지 「필요 조건」이 아니다.
			Assert.IsNull(await accounts.TryResolveAsync("세션값"));
		}

		[Test]
		public async Task 이상한_답도_그냥_손님으로()
		{
			KarmoLabAccounts accounts = new KarmoLabAccounts("http://kl.test", new FakeServer(_ => Json("이건 json 이 아니다")));

			Assert.IsNull(await accounts.TryResolveAsync("세션값"));
		}
	}
}
