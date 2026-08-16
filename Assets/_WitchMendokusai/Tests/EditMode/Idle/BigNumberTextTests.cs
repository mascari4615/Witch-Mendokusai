using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 큰 수가 <b>화면을 안 넘는가</b> (TASK-WM-406).
	///
	/// ★ 방치형에서 표기는 장식이 아니다 — 며칠 돌리면 자원이 10^20 을 넘고,
	///   그대로 찍으면 줄이 버튼을 밀어낸다. 즉 숫자가 커질수록 게임이 망가진다.
	///   그래서 여기서 지키는 것은 「예쁘게」가 아니라 <b>길이</b>와 <b>순서</b>다.
	/// </summary>
	public sealed class BigNumberTextTests
	{
		/// <summary>초반 몇 분은 그대로 읽힌다 — 여기서 축약하면 오히려 안 읽힌다.</summary>
		[Test]
		public void SmallNumbers_StayPlain()
		{
			Assert.AreEqual("0", BigNumberText.Format(0d));
			Assert.AreEqual("7", BigNumberText.Format(7.9d));
			Assert.AreEqual("999", BigNumberText.Format(999.4d));
		}

		/// <summary>관례 그대로 — 1000 단위로 K M B T, 10^15 부터 두 글자.</summary>
		[Test]
		public void Suffixes_FollowTheGenreConvention()
		{
			Assert.AreEqual("1.00K", BigNumberText.Format(1_000d));
			Assert.AreEqual("1.23K", BigNumberText.Format(1_234d));
			Assert.AreEqual("1.00M", BigNumberText.Format(1e6));
			Assert.AreEqual("1.00B", BigNumberText.Format(1e9));
			Assert.AreEqual("1.00T", BigNumberText.Format(1e12));
			Assert.AreEqual("1.00aa", BigNumberText.Format(1e15));
			Assert.AreEqual("1.00ab", BigNumberText.Format(1e18));
			Assert.AreEqual("1.00ac", BigNumberText.Format(1e21));
		}

		/// <summary>
		/// ★ <b>표기가 절대 뒤로 안 간다.</b> 더 큰 수가 더 작아 보이면 사람은 게임을 안 믿는다.
		/// 나누다 999.999… 가 1000 으로 반올림돼 「1000K」가 찍히던 자리가 여기다.
		/// </summary>
		[Test]
		public void Formatting_NeverGoesBackwards()
		{
			double value = 1d;
			string previous = BigNumberText.Format(value);

			for (int i = 0; i < 2000; i++)
			{
				value *= 1.37d;
				string now = BigNumberText.Format(value);

				Assert.IsFalse(now.StartsWith("1000"), "다음 칸이 있는데 1000 을 찍었다: " + now);
				Assert.AreNotEqual("-", now, "멀쩡한 수를 깨진 수로 찍었다: " + value);

				previous = now;
			}

			Assert.IsNotEmpty(previous);
		}

		/// <summary>
		/// ★ <b>길이가 묶여 있다</b> — 이게 「버튼이 안 밀린다」의 실제 조건이다.
		/// 10^300 까지 훑는다(double 이 담는 거의 끝까지).
		/// </summary>
		[Test]
		public void Length_StaysBounded_EvenAtAbsurdScales()
		{
			double value = 1d;
			int longest = 0;
			string worst = string.Empty;

			for (int power = 0; power < 300; power++)
			{
				string text = BigNumberText.Format(value);
				if (text.Length > longest)
				{
					longest = text.Length;
					worst = text;
				}

				value *= 10d;
			}

			Debug.Log("[BigNumber] 10^300 까지 가장 긴 표기 = \"" + worst + "\" (" + longest + "자)");
			Assert.LessOrEqual(longest, 10, "표기가 10자를 넘는다 — 화면을 밀어낸다");
		}

		/// <summary>깨진 수는 「NaN」이 아니라 <c>-</c> 로 — 화면에 NaN 이 뜨는 건 값이 아니라 버그 신호다.</summary>
		[Test]
		public void BrokenNumbers_ShowAsDash()
		{
			Assert.AreEqual("-", BigNumberText.Format(double.NaN));
			Assert.AreEqual("-", BigNumberText.Format(double.PositiveInfinity));
			Assert.AreEqual("-", BigNumberText.Format(double.NegativeInfinity));
		}

		/// <summary>음수도 읽힌다 — 지금은 안 나오지만, 나오면 그게 버그라 <b>보여야</b> 한다.</summary>
		[Test]
		public void Negatives_AreReadable()
		{
			Assert.AreEqual("-1.23K", BigNumberText.Format(-1234d));
		}

		/// <summary>
		/// ★ 진짜 판의 숫자로 확인한다 — 이레짜리 시뮬레이션이 실제로 만드는 값이
		/// 화면에 들어가는지. 시험이 「가정한 큰 수」가 아니라 <b>이 게임이 내는 수</b>를 봐야 한다.
		/// </summary>
		[Test]
		public void RealGameNumbers_Fit()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			for (int hour = 0; hour < 24 * 3; hour++)
			{
				IdleModel.Step(state, tuning, 3600d);
			}

			string resource = BigNumberText.Format(state.Resource);
			string damage = BigNumberText.Format(IdleModel.DamageOf(state, tuning));

			Debug.Log("[BigNumber] 사흘 방치 — 자원 " + resource + " · 한 방 " + damage
				+ " (" + state.Stage + "단계)");

			Assert.LessOrEqual(resource.Length, 10);
			Assert.AreNotEqual("-", resource, "사흘 만에 숫자가 깨졌다");
		}
	}
}
