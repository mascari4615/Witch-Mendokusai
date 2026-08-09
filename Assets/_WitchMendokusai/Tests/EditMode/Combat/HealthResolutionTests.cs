using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 체력 판정이 <b>엔진 없이도</b> 같은 답을 낸다 (TASK-WM-215).
	/// 「맞으면 얼마 남고 죽었나」는 규칙이지 화면의 일이 아니다 — 서버가 같은 답을 내야 한다.
	/// </summary>
	public sealed class HealthResolutionTests
	{
		[Test]
		public void 피해는_남은_체력에서_깎인다()
		{
			HealthChange change = HealthResolution.Apply(100, 100, -30);

			Assert.AreEqual(70, change.NewHp);
			Assert.AreEqual(30, change.AppliedDamage);
			Assert.AreEqual(0, change.AppliedHeal);
			Assert.IsFalse(change.Died);
		}

		[Test]
		public void 과잉_피해는_0_에서_멈추고_남은만큼만_센다()
		{
			HealthChange change = HealthResolution.Apply(20, 100, -500);

			Assert.AreEqual(0, change.NewHp, "체력은 음수가 되지 않는다");
			Assert.AreEqual(20, change.AppliedDamage, "실제로 깎인 건 남아 있던 20 뿐");
			Assert.IsTrue(change.Died);
		}

		[Test]
		public void 회복은_최대치를_넘지_않는다()
		{
			HealthChange change = HealthResolution.Apply(90, 100, 50);

			Assert.AreEqual(100, change.NewHp);
			Assert.AreEqual(10, change.AppliedHeal, "실제로 찬 건 10 뿐");
			Assert.IsFalse(change.Died);
		}

		[Test]
		public void 딱_0_이_되면_죽는다()
		{
			HealthChange change = HealthResolution.Apply(15, 100, -15);

			Assert.AreEqual(0, change.NewHp);
			Assert.IsTrue(change.Died, "0 은 살아 있는 상태가 아니다");
		}

		[Test]
		public void 피해정보를_그대로_먹인_결과도_같다()
		{
			DamageInfo damageInfo = new DamageInfo { damage = 25 };

			HealthChange change = HealthResolution.ApplyDamage(60, 100, damageInfo);

			Assert.AreEqual(35, change.NewHp);
			Assert.AreEqual(25, change.AppliedDamage);
		}

		[Test]
		public void 이미_죽은_대상에_회복을_넣어도_최대치_안에_머문다()
		{
			HealthChange change = HealthResolution.ApplyHeal(0, 50, 999);

			Assert.AreEqual(50, change.NewHp);
			Assert.AreEqual(50, change.AppliedHeal);
			Assert.IsFalse(change.Died);
		}
	}
}
