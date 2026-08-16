using System.Text;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 방치 곡선을 <b>눈으로 보기 전에 숫자로</b> 본다 (TASK-WM-406).
	///
	/// 방치형의 재미는 곡선에 다 들어 있어서, UI 를 만들고 나서 조율하면 늦다.
	/// 여기서 시간대별 표를 뽑아 「업그레이드 간격이 계속 벌어지기만 하는지(지루)」
	/// 「좁혀지기만 하는지(무의미)」를 먼저 판정한다.
	///
	/// 판정 자체는 사람이 한다 — 이 시험이 지키는 것은 <b>표가 믿을 만한가</b>다:
	/// 스텝을 쪼개도 같은 값이 나오고(①), 두 번 돌려도 같은 값이 나온다(②).
	/// 그 둘이 깨지면 표를 보고 내린 판단이 전부 헛것이 된다.
	/// </summary>
	public sealed class IdleCurveTests
	{
		private const double TOLERANCE = 1e-6d;

		private static IdleTuning DefaultTuning()
		{
			return new IdleTuning();
		}

		/// <summary>① 스텝 불변 — 60초를 한 번에 밟든 0.1초씩 600번 밟든 같아야 한다.</summary>
		[Test]
		public void Step_SameElapsed_SameResult_RegardlessOfStepSize()
		{
			IdleTuning tuning = DefaultTuning();

			IdleState coarse = new IdleState();
			IdleModel.Step(coarse, tuning, 60d);

			IdleState fine = new IdleState();
			for (int tick = 0; tick < 600; tick++)
			{
				IdleModel.Step(fine, tuning, 0.1d);
			}

			Assert.AreEqual(coarse.Kills, fine.Kills, "쪼갠 스텝이 처치 수를 흘렸다");
			Assert.AreEqual(coarse.Resource, fine.Resource, TOLERANCE, "쪼갠 스텝이 자원을 흘렸다");
		}

		/// <summary>② 결정적 — 무작위가 없으니 같은 입력이면 언제나 같은 표.</summary>
		[Test]
		public void Simulation_IsDeterministic()
		{
			string first = Simulate(DefaultTuning(), 3600d, 1d, out _);
			string second = Simulate(DefaultTuning(), 3600d, 1d, out _);

			Assert.AreEqual(first, second, "같은 입력인데 표가 달라졌다 — 곡선 판단의 전제가 깨진다");
		}

		/// <summary>③ 올리기는 자원을 정확히 그만큼만 쓴다.</summary>
		[Test]
		public void TryRaise_SpendsExactlyTheCost()
		{
			IdleTuning tuning = DefaultTuning();
			IdleState state = new IdleState();

			Assert.IsTrue(IdleModel.TryGetNextCost(state, tuning, IdleUpgradeKind.Damage, out double cost));

			state.Resource = cost;
			Assert.IsTrue(IdleModel.TryRaise(state, tuning, IdleUpgradeKind.Damage, out UpgradeRaiseFailure failure), failure.ToString());
			Assert.AreEqual(0d, state.Resource, TOLERANCE, "값을 더 쓰거나 덜 썼다");
			Assert.AreEqual(1, state.Damage.Level);
		}

		/// <summary>④ 자원이 모자라면 레벨도 자원도 그대로다.</summary>
		[Test]
		public void TryRaise_WithoutFunds_ChangesNothing()
		{
			IdleTuning tuning = DefaultTuning();
			IdleState state = new IdleState();
			state.Resource = 0d;

			Assert.IsFalse(IdleModel.TryRaise(state, tuning, IdleUpgradeKind.Damage, out UpgradeRaiseFailure failure));
			Assert.AreEqual(UpgradeRaiseFailure.NotEnoughFunds, failure);
			Assert.AreEqual(0, state.Damage.Level);
			Assert.AreEqual(0d, state.Resource, TOLERANCE);
		}

		/// <summary>
		/// ⑤ 8시간치 표를 찍는다 — <b>사람이 보는 산출물</b>. 실패하지 않는다(판정은 사람 몫).
		/// 콘솔에서 「5분 / 30분 / 2시간 / 8시간」에 레벨과 초당 산출이 어떻게 벌어지는지 본다.
		/// </summary>
		[Test]
		public void PrintCurveTable()
		{
			IdleTuning tuning = DefaultTuning();
			string table = Simulate(tuning, 8d * 3600d, 1d, out IdleState final);

			TestContext.WriteLine(table);
			Assert.Greater(final.Kills, 0L, "8시간을 돌렸는데 하나도 못 잡았다 — 시작 손잡이가 잘못됐다");
		}

		/// <summary>
		/// 자동으로 굴려 본다 — 살 수 있으면 <b>둘 중 싼 쪽</b>을 산다(사람의 기본 습관에 가장 가까운 정책).
		/// 정책이 바뀌면 표도 바뀌지만, 곡선의 모양을 보는 데는 이 하나로 충분하다.
		/// </summary>
		private static string Simulate(IdleTuning tuning, double totalSeconds, double stepSeconds, out IdleState state)
		{
			state = new IdleState();

			StringBuilder report = new StringBuilder();
			report.AppendLine("[IdleCurve] 경과 | 공격력 | 공격속도 | 자원 | 초당산출 | 처치");

			double elapsed = 0d;
			int nextMarkIndex = 0;
			double[] marks = { 60d, 300d, 900d, 1800d, 3600d, 7200d, 14400d, 28800d };

			while (elapsed < totalSeconds)
			{
				IdleModel.Step(state, tuning, stepSeconds);
				elapsed += stepSeconds;

				IdlePlay.BuyEverything(state, tuning);

				if (nextMarkIndex < marks.Length && elapsed >= marks[nextMarkIndex])
				{
					report.AppendLine(Row(state, tuning, marks[nextMarkIndex]));
					nextMarkIndex++;
				}
			}

			return report.ToString();
		}

		private static string Row(IdleState state, IdleTuning tuning, double atSeconds)
		{
			return string.Format(
				"[IdleCurve] {0,6} | {1,4} | {2,6} | {3,12:N0} | {4,10:N2} | {5,12:N0}",
				Elapsed(atSeconds),
				state.Damage.Level,
				state.AttackSpeed.Level,
				state.Resource,
				IdleModel.IncomePerSecond(state, tuning),
				state.Kills);
		}

		private static string Elapsed(double seconds)
		{
			if (seconds < 3600d)
			{
				return string.Format("{0}분", (int)(seconds / 60d));
			}

			return string.Format("{0}시간", (int)(seconds / 3600d));
		}
	}
}
