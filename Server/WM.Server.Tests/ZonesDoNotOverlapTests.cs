using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 땅이 <b>겹치는지</b> 알아보나 (TASK-WM-368).
	///
	/// ★ 왜: 겹친 자리는 두 세계가 다 자기 것이라 우기는 자리다. 그 자리에 선 사람은
	///   넘겨주기가 오락가락하며 두 세계를 왕복하거나, 최악에는 <b>둘로 늘어난다</b>(가방까지).
	///   땅은 손으로 적는 글자(WM_ZONE)라 오타 한 번이면 그렇게 된다.
	///   ⚠ <b>맞닿은 것</b>은 겹친 것이 아니다 — 국경선은 서로 나눠 갖는다(그래야 걸어서 넘어간다).
	/// </summary>
	public sealed class ZonesDoNotOverlapTests
	{
		private static ZonePatch Land(string name, float fromX, float toX)
		{
			return new ZonePatch(name, fromX, -100f, toX, 100f);
		}

		[Test]
		public void 맞닿은_땅은_겹친_것이_아니다()
		{
			ZonePatch west = Land("서", -100f, 0f);
			ZonePatch east = Land("동", 0f, 100f);

			Assert.That(west.Overlaps(east), Is.False, "국경선은 서로 나눠 갖는다 — 그래야 걸어서 넘어간다");
			Assert.That(east.Overlaps(west), Is.False);
		}

		[Test]
		public void 겹친_땅은_알아본다()
		{
			ZonePatch west = Land("서", -100f, 10f);
			ZonePatch east = Land("동", 0f, 100f);

			Assert.That(west.Overlaps(east), Is.True, "0~10 은 둘 다 자기 땅이라고 우기는 자리다");
			Assert.That(east.Overlaps(west), Is.True, "겹침은 어느 쪽에서 봐도 겹침이다");
		}

		[Test]
		public void 떨어진_땅은_안_겹친다()
		{
			Assert.That(Land("서", -100f, -10f).Overlaps(Land("동", 10f, 100f)), Is.False);
		}

		/// <summary>온 세상을 맡은 세계(안 나눈 세계)는 견줄 것이 없다 — 그때는 이웃도 없다.</summary>
		[Test]
		public void 안_나눈_세계는_겹침을_안_따진다()
		{
			Assert.That(ZonePatch.Everywhere.Overlaps(Land("동", 0f, 100f)), Is.False);
			Assert.That(Land("동", 0f, 100f).Overlaps(ZonePatch.Everywhere), Is.False);
		}

		/// <summary>세로(z)로만 겹쳐도 겹친 것이다 — 한 축만 보면 놓친다.</summary>
		[Test]
		public void 세로로_겹쳐도_겹친_것이다()
		{
			ZonePatch north = new ZonePatch("북", -100f, 0f, 100f, 100f);
			ZonePatch south = new ZonePatch("남", -100f, -100f, 100f, 10f);

			Assert.That(north.Overlaps(south), Is.True, "0~10 (z) 이 겹친다");
		}
	}
}
