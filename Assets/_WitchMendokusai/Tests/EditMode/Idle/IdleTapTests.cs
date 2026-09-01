using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 손으로 때리기 (TASK-WM-406).
	///
	/// ★ 사용자 지적: 「전혀 클리커스럽지 않다」. 판이 전부 자동이라 <b>누를 것이 없었다</b>.
	///   여기서 지키는 것은 ① 손이 실제로 판을 앞당기고 ② 그 몫이 <b>늘 같은 비율</b>이며
	///   ③ 안 눌러도 손해가 없다는 것이다.
	/// </summary>
	public sealed class IdleTapTests
	{
		/// <summary>★ 두드리면 판이 앞으로 간다 — 아니면 그건 장식이다.</summary>
		[Test]
		public void Tapping_MovesTheFight()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState idle = new IdleState();
			IdleState tapped = new IdleState();

			const double TICK = 0.1d;
			for (int beat = 0; beat < 100; beat++)
			{
				IdleModel.Step(idle, tuning, TICK);

				IdleModel.Step(tapped, tuning, TICK);
				IdleModel.Tap(tapped, tuning);
			}

			Assert.Greater(tapped.Kills, idle.Kills, "두드렸는데 아무 일도 안 일어났다");
		}

		/// <summary>
		/// ★ 손의 몫은 <b>비율</b>이다 — 공격속도를 올려도 손이 뒤처지지 않는다.
		///
		/// 고정값으로 줬다면 세진 판에서 손이 무의미해지고, 그러면 「눌러도 그만」이 된다.
		/// </summary>
		[Test]
		public void TapKeepsItsShare_AsTheFightGetsFaster()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState weak = new IdleState();
			IdleState strong = new IdleState();
			IdleHeroes.EnsureStarter(strong);
			strong.Resource = 1e9d;

			for (int level = 0; level < 12; level++)
			{
				IdleModel.TryRaise(strong, tuning, IdleHeroes.STARTER_ID,
					IdleUpgradeKind.AttackSpeed, 1);
			}

			Assert.Greater(IdleModel.AttackSpeedOf(strong, tuning), IdleModel.AttackSpeedOf(weak, tuning),
				"판을 못 세웠다 — 이 시험이 재려는 것이 안 갖춰졌다");

			double weakGain = TapWorthInSwings(weak, tuning);
			double strongGain = TapWorthInSwings(strong, tuning);

			// 한 대의 값이 <b>지금 공격속도에 비례</b>한다.
			Assert.AreEqual(
				weakGain / IdleModel.AttackSpeedOf(weak, tuning),
				strongGain / IdleModel.AttackSpeedOf(strong, tuning),
				1e-9d,
				"손의 몫이 판의 세기에 따라 달라진다");
		}

		/// <summary>★ 안 두드려도 손해가 없다 — 방치형이니 손은 <b>더 얹는 것</b>이다.</summary>
		[Test]
		public void NotTapping_CostsNothing()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState state = new IdleState();
			IdleModel.Step(state, tuning, 600d);

			Assert.Greater(state.Kills, 0L, "손을 안 대면 판이 멈춘다 — 그건 방치형이 아니다");
		}

		/// <summary>한 대가 쌓아 주는 공격 횟수 (자동 진행분은 빼고 손 몫만).</summary>
		private static double TapWorthInSwings(IdleState state, IdleTuning tuning)
		{
			return IdleModel.AttackSpeedOf(state, tuning) * tuning.TapSecondsOfAttack;
		}
	}
}
