using NUnit.Framework;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 국경 너머를 보는 규칙 (TASK-WM-263) — 국경에 서면 1m 옆 사람이 보여야 한다.
	/// </summary>
	public class BorderBandTests
	{
		private static readonly ZonePatch West = new ZonePatch("서", -40f, -40f, 0f, 40f);

		[Test]
		public void 저_땅에서_먼_사람은_안_보낸다()
		{
			// 띠 밖은 안 보낸다 — 전부 보내면 세계를 나눈 뜻이 없다(나눈 이유가 회선이다).
			Assert.IsFalse(BorderBand.WorthTelling(West, new Vector3(BorderBand.BAND + 1f, 0f, 0f)));
		}

		[Test]
		public void 국경_바로_너머의_사람은_보낸다()
		{
			Assert.IsTrue(BorderBand.WorthTelling(West, new Vector3(1f, 0f, 0f)),
				"1m 옆 사람이 안 보이면 그건 한 세계가 아니라 벽이다");
			Assert.IsTrue(BorderBand.WorthTelling(West, new Vector3(BorderBand.BAND, 0f, 0f)));
		}

		[Test]
		public void 저_땅_안의_자리는_거리가_0_이다()
		{
			Assert.AreEqual(0f, BorderBand.AwayFrom(West, new Vector3(-10f, 0f, 5f)), 0.001f);
		}

		[Test]
		public void 모서리로도_잰다()
		{
			// 땅은 네모다 — 모서리 너머는 대각선 거리로 재야 띠가 둥글다.
			float away = BorderBand.AwayFrom(West, new Vector3(3f, 0f, 44f));
			Assert.AreEqual(5f, away, 0.01f, "가로 3 · 세로 4 면 5 다");
		}

		[Test]
		public void 경계_없는_세계에는_띠가_없다()
		{
			Assert.IsFalse(BorderBand.WorthTelling(ZonePatch.Everywhere, new Vector3(1f, 0f, 0f)));
		}

		[Test]
		public void 그림자_번호는_늘_음수고_안_겹친다()
		{
			int fromEast = BorderBand.ShadowId("동", 3);
			int fromWest = BorderBand.ShadowId("서", 3);

			Assert.Less(fromEast, 0, "양수면 이 세계의 인형과 겹쳐 남을 나로 그린다");
			Assert.AreNotEqual(fromEast, fromWest, "세계가 달라도 같은 번호면 두 사람이 한 사람이 된다");
			Assert.AreEqual(fromEast, BorderBand.ShadowId("동", 3), "같은 사람은 늘 같은 번호여야 안 깜빡인다");
			Assert.IsTrue(BorderBand.IsShadow(fromEast));
			Assert.IsFalse(BorderBand.IsShadow(3));
		}

		[Test]
		public void 이름이_같으면_두_세계가_같은_번호를_센다()
		{
			// 세계끼리 사전을 안 나눠 가진다 — 이름만으로 같은 값이 나와야 한다.
			Assert.AreEqual(BorderBand.MarkOfZone("동"), BorderBand.MarkOfZone("동"));
			Assert.AreNotEqual(BorderBand.MarkOfZone("동"), BorderBand.MarkOfZone("서"));
			Assert.GreaterOrEqual(BorderBand.MarkOfZone(null), 1);
		}

		[Test]
		public void 번호가_없는_사람은_그림자도_없다()
		{
			Assert.AreEqual(0, BorderBand.ShadowId("동", 0));
		}

		[Test]
		public void 이름이_같은_번호로_뭉개지면_찾아낸다()
		{
			// 이름 → 작은 번호는 굴린 값이라 언젠가 겹친다. 겹치면 국경에서 한 사람이
			// 다른 한 사람을 <b>조용히</b> 지운다 — 그래서 띄울 때 미리 찾는다.
			Assert.IsNull(BorderBand.FirstClash(new[] { "동", "서", "북", "남" }));
			Assert.IsNull(BorderBand.FirstClash(null));
			Assert.IsNull(BorderBand.FirstClash(new[] { "동", "동" }), "같은 세계를 두 번 적은 것은 겹침이 아니다");

			// 진짜로 겹치는 두 이름을 찾아서 넣는다 — 손으로 지어내면 오늘의 셈에만 맞는다.
			string first = "가";
			string clashing = null;
			for (int i = 0; i < 5000 && clashing == null; i++)
			{
				string candidate = "터" + i;
				if (BorderBand.MarkOfZone(candidate) == BorderBand.MarkOfZone(first))
					clashing = candidate;
			}

			Assert.IsNotNull(clashing, "겹치는 이름을 못 찾았다 — 이 시험이 뜻을 잃었다");
			Assert.IsNotNull(BorderBand.FirstClash(new[] { first, clashing }));
		}
	}
}
