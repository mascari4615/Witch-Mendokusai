using System.Text;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 며칠을 돌려도 <b>안 무너지는가</b> (TASK-WM-406).
	///
	/// ★ 방치형이 죽는 자리는 대개 첫 30분이 아니라 <b>사흘째</b>다 — 정체하거나 폭주한다.
	///   정체하면 켤 이유가 없어지고, 폭주하면 숫자가 뜻을 잃는다. 둘 다 사람이 손으로는 못 찾는다
	///   (그러자면 진짜로 사흘을 켜 놔야 한다).
	///
	/// ★ 그래서 <b>사람 대신 정책이 논다</b> — 살 수 있으면 싼 쪽부터 사고, 천장에 닿으면 환생한다.
	///   그리고 판이 어떻게 흘렀는지를 표로 찍는다. 이 표가 이 게임의 「며칠치 곡선」이다.
	///
	/// ★ 코어가 스텝 불변이라 <b>10초씩 밟아도 1초씩 밟은 것과 같다</b> — 그래서 며칠을 몇 초에 잰다.
	///   (사는 시점만 성기다 — 그건 사람이 계속 안 보고 있는 것과 오히려 비슷하다.)
	/// </summary>
	public sealed partial class IdleLongHaulTests
	{
		private const double TICK = 10d;
		private const double DAY = 24d * 3600d;

		/// <summary>이만큼 한 단계도 못 나가면 「막혔다」로 본다 — 사람이 환생로 마음먹는 지점.</summary>
		private const double STALL_HOURS = 2d;

		/// <summary>
		/// ★ 이레를 논다. 판마다 「몇 시간 · 어디까지 · 몇 점 · 천장」을 찍는다.
		///
		/// 지키는 것 둘:
		/// ① <b>안 멈춘다</b> — 판이 갈수록 더 깊이 간다(같은 자리에 갇히면 켤 이유가 없다)
		/// ② <b>안 터진다</b> — 숫자가 NaN·무한이 안 된다(방치형은 큰 수를 오래 곱한다)
		/// </summary>
		[Test]
		public void SevenDays_NeitherStallsNorExplodes()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			StringBuilder table = new StringBuilder();
			table.AppendLine("[IdleLongHaul] 판 | 걸린시간 | 환생단계 | 얻은점수 | 누적점수 | 다음천장 | 최고잠재");

			double elapsed = 0d;
			double runStarted = 0d;
			double lastProgressAt = 0d;
			int lastStage = 1;
			int runs = 0;
			int deepestLastRun = 0;

			while (elapsed < 7d * DAY)
			{
				IdleModel.Step(state, tuning, TICK);
				elapsed += TICK;

				IdlePlay.BuyEverything(state, tuning);
				AppraiseWhatWeCan(state, tuning);

				if (state.Stage > lastStage)
				{
					lastStage = state.Stage;
					lastProgressAt = elapsed;
				}

				// ★ <b>막혔을 때 환생한다</b> — 사람이 하는 짓이 그렇다.
				//   처음엔 「천장에 닿으면 환생한다」로 쟀는데, 그건 사람과 다르다:
				//   천장은 벽보다 훨씬 먼저 오고, 그때 환생하면 판이 0.0h 로 찍혀
				//   <b>자가 게임을 잘못 재고 있었다</b>(설계가 아니라 자가 틀린 것이었다).
				bool stalled = elapsed - lastProgressAt >= STALL_HOURS * 3600d;

				if (stalled == false || IdleModel.CanPrestige(state, tuning) == false)
				{
					continue;
				}

				int foldedAt = state.Stage;
				double hitsAtFold = IdleModel.HitsToFell(state, tuning);
				double damageAtFold = IdleModel.DamageOf(state, tuning);
				double healthAtFold = IdleModel.TargetHealthOf(state, tuning);
				double killsPerSecondAtFold = IdleModel.KillsPerSecond(state, tuning);
				Assert.Greater(foldedAt, deepestLastRun,
					"판 " + (runs + 1) + " 이 지난 판보다 얕은 데서 끝났다 — 앞으로 안 나간다");
				deepestLastRun = foldedAt;

				IdleModel.TryPrestige(state, tuning, out long awarded);
				runs++;

				// ★ 멈추는 순간의 속을 같이 찍는다 — 「왜 멎었나」를 표에서 바로 읽으려고.
				table.AppendLine(string.Format(
					"[IdleLongHaul] {0,2} | {1,7:N1}h | {2,6} | {3,7} | {4,7} | {5,6} | {6,6:P1} | 타격/마리 {7:0.###e+0} | 공격력 {8:0.###e+0} | 체력 {9:0.###e+0} | 초당 {10:0.###e+0}",
					runs, (elapsed - runStarted) / 3600d, foldedAt, awarded, state.PrestigePoints,
					IdleDrops.CeilingFor(state.Ascensions, tuning), state.BestPotentialValue,
					hitsAtFold, damageAtFold, healthAtFold, killsPerSecondAtFold));

				runStarted = elapsed;
				lastProgressAt = elapsed;
				lastStage = state.Stage;

				Assert.IsFalse(double.IsNaN(state.Resource) || double.IsInfinity(state.Resource),
					"자원이 터졌다 (판 " + runs + ")");
				Assert.IsFalse(double.IsNaN(IdleModel.DamageOf(state, tuning))
					|| double.IsInfinity(IdleModel.DamageOf(state, tuning)),
					"공격력이 터졌다 (판 " + runs + ")");
			}

			TestContext.WriteLine(table.ToString());

			Assert.Greater(runs, 1, "이레 동안 판을 두 번도 못 환생했다 — 환생 고리가 너무 멀다");
			Assert.Greater(state.PrestigePoints, 0L);
		}

		/// <summary>
		/// 환생할 때가 <b>실제로 온다</b> — 천장에 닿는 데 걸리는 시간이 사람이 기다릴 만한가.
		/// 첫 판은 특히 중요하다: 여기가 길면 아무도 두 번째 판을 못 본다.
		/// </summary>
		[Test]
		public void FirstRun_ReachesTheCeilingWithinADay()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			// 기지가 없으면 자원이 0 이라 아무것도 못 산다 — 첫 생산자 하나로 시작한다(게임도 그렇게 준다).
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 1L;

			double elapsed = 0d;
			while (elapsed < DAY)
			{
				IdleModel.Step(state, tuning, TICK);
				elapsed += TICK;
				IdlePlay.BuyEverything(state, tuning);

				if (IdleDrops.MaxTierAt(state.Stage, state.Ascensions, tuning)
					>= IdleDrops.CeilingFor(state.Ascensions, tuning))
				{
					break;
				}
			}

			TestContext.WriteLine("[IdleLongHaul] 첫 천장까지 " + (elapsed / 3600d).ToString("N1") + "시간 · "
				+ state.Stage + "단계");

			Assert.Less(elapsed, DAY, "하루를 켜 둬도 첫 천장에 못 닿는다 — 두 번째 판을 아무도 못 본다");
		}
}
}

