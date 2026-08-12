using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>창이 온 곳을 어떻게 아나 — 터널 뒤에서도 (TASK-WM-220).</summary>
	public class ClientOriginTests
	{
		[Test]
		public void 터널이_붙인_이름표를_먼저_본다()
		{
			DefaultHttpContext context = new DefaultHttpContext();
			context.Request.Headers["CF-Connecting-IP"] = "203.0.113.7";
			context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

			Assert.That(ClientOrigin.Of(context), Is.EqualTo("203.0.113.7"));
		}

		[Test]
		public void 이어_붙은_이름표에서는_맨_앞만_쓴다()
		{
			// ⚠ 맨 뒤를 쓰면 <b>터널</b> 주소가 되어 모든 사람이 한 사람으로 뭉친다.
			DefaultHttpContext context = new DefaultHttpContext();
			context.Request.Headers["X-Forwarded-For"] = "203.0.113.7, 10.0.0.1, 127.0.0.1";

			Assert.That(ClientOrigin.Of(context), Is.EqualTo("203.0.113.7"));
		}

		[Test]
		public void 이름표가_없으면_소켓이_말하는_주소를_쓴다()
		{
			DefaultHttpContext context = new DefaultHttpContext();
			context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.5");

			Assert.That(ClientOrigin.Of(context), Is.EqualTo("192.0.2.5"));
		}

		[Test]
		public void 아무것도_모르면_한_곳으로_센다()
		{
			Assert.That(ClientOrigin.Of(new DefaultHttpContext()), Is.EqualTo("?"));
		}
	}
}
