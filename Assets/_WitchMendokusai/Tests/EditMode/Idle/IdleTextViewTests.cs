using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 진단 표현으로 <b>판을 기계가 읽는다</b> (TASK-WM-406).
	///
	/// 여기서 지키는 것은 재미가 아니라 <b>망가짐</b>이다 —
	/// 숫자가 깨지지 않았나 · 초반이 막히지 않았나 · 후반에 긴장이 남아 있나.
	/// 「재밌나」는 사람이 창을 열어 본다. 「망가졌나」는 이 시험이 본다.
	///
	/// ★ 이 시험이 도는 것 자체가 표현 계약이 진짜라는 증거다 —
	///   둘째 표현이 코어를 한 줄도 안 건드리고 붙었고, Unity 없이 그려진다.
	/// </summary>
	public sealed class IdleTextViewTests
	{
		private static IdleSession NewSession()
		{
			return new IdleSession(new IdleTuning());
		}

		/// <summary>어떤 시점에도 숫자가 깨지지 않는다 — NaN·무한은 곡선이 터졌다는 뜻이다.</summary>
		[Test]
		public void Line_NeverContainsBrokenNumbers()
		{
			IdleSession session = NewSession();
			IdleTextView view = new IdleTextView();

			for (int minute = 0; minute < 60 * 8; minute++)
			{
				session.Advance(60d);
				IdlePlay.BuyEverything(session.State, new IdleTuning());

				view.Render(session.Capture());

				Assert.IsFalse(view.Line.Contains("NaN"), "NaN 이 찍혔다 — 곡선이 터졌다: " + view.Line);
				Assert.IsFalse(view.Line.Contains("Infinity"), "무한이 찍혔다: " + view.Line);
				Assert.IsFalse(view.Line.Contains("∞"), "무한이 찍혔다: " + view.Line);
			}
		}

		/// <summary>
		/// 초반 30분이 <b>막히지 않는다</b> — 아무것도 못 사는 채로 오래 서 있으면 그건 지루함이다.
		/// 방치형은 초반에 보상을 쏟아붓는 게 관례다(쿠키 클리커).
		/// </summary>
		[Test]
		public void EarlyGame_IsNotStuck()
		{
			IdleSession session = NewSession();

			int purchases = 0;
			for (int minute = 0; minute < 30; minute++)
			{
				session.Advance(60d);
				purchases += IdlePlay.BuyEverything(session.State, new IdleTuning());
			}

			Assert.Greater(purchases, 3, "30분 동안 산 게 3개 이하다 — 초반이 막혀 있다");
		}

		/// <summary>
		/// 후반에 <b>긴장이 남아 있다</b> — 살 수 있는 만큼 다 샀는데도 또 둘 다 살 수 있으면
		/// 값이 수입을 못 따라간다는 뜻이고, 그 순간 고를 이유가 사라진다.
		/// </summary>
		[Test]
		public void LateGame_StillHasScarcity()
		{
			IdleSession session = NewSession();
			IdleTextView view = new IdleTextView();

			for (int minute = 0; minute < 60 * 4; minute++)
			{
				session.Advance(60d);
				IdlePlay.BuyEverything(session.State, new IdleTuning());
			}

			IdleSnapshot snapshot = session.Capture();
			view.Render(snapshot);

			bool bothAffordable = snapshot.Damage.CanAfford && snapshot.AttackSpeed.CanAfford;
			Assert.IsFalse(bothAffordable, "다 샀는데 둘 다 또 살 수 있다 — 긴장 0: " + view.Line);
		}

		/// <summary>형식이 고정돼 있다 — 시험·로그가 파싱해서 읽는다.</summary>
		[Test]
		public void Line_HasStableKeys()
		{
			IdleSession session = NewSession();
			session.Advance(120d);

			IdleTextView view = new IdleTextView();
			view.Render(session.Capture());

			Assert.AreEqual(PresentationKind.Text, view.Kind);
			StringAssert.Contains("res=", view.Line);
			StringAssert.Contains("ips=", view.Line);
			StringAssert.Contains("kills=", view.Line);
			StringAssert.Contains("hp=", view.Line);
			StringAssert.Contains("dmg=L", view.Line);
			StringAssert.Contains("spd=L", view.Line);
		}

		/// <summary>구간별 한 줄씩 찍는다 — 곡선을 눈으로 훑는 산출물. 실패하지 않는다.</summary>
		[Test]
		public void PrintLineAtMilestones()
		{
			IdleSession session = NewSession();
			IdleTextView view = new IdleTextView();

			double[] marks = { 300d, 1800d, 7200d, 28800d };
			string[] names = { "5분", "30분", "2시간", "8시간" };

			double elapsed = 0d;
			for (int index = 0; index < marks.Length; index++)
			{
				while (elapsed < marks[index])
				{
					session.Advance(10d);
					IdlePlay.BuyEverything(session.State, new IdleTuning());
					elapsed += 10d;
				}

				view.Render(session.Capture());
				Debug.Log("[IdleText] " + names[index] + "  " + view.Line);
			}
		}

}
}
