using System.Text;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.Tests
{
	// IdleLongHaulTests.cs 의 Prints 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 수치를 찍어 보는 판 (판정 아님).
	public sealed partial class IdleLongHaulTests
	{
		/// <summary>
		/// 손잡이 하나(<see cref="IdleTuning.PrestigeMultiplierPerPoint"/>)를 여러 값으로 돌려 보고
		/// <b>판마다 어디까지 가는지</b>를 표로 찍는다. 실패하지 않는다 — 이건 자(尺)이지 관문이 아니다.
		///
		/// ★ 왜 필요한가 — 이 값 하나로 게임이 <b>정체</b>도 되고 <b>인플레</b>도 된다.
		///   추측으로 돌리면 며칠 뒤에야 틀린 걸 안다. 여기서는 한 판에 다 본다.
		/// </summary>
		[Test]
		public void PrintDepthPerRun_AcrossMultiplierKnob()
		{
			// ★ 후보는 계산에서 나온다. 배수 1.55^p × 업그레이드 1.235^d 가 체력 1.55^d 와 같아야 하므로
			//   p = 0.518d — 즉 배수는 1.55^0.518 ≈ <b>1.246</b>. 그 언저리를 훑는다.
			double[] knobs = { 1.20d, 1.246d, 1.30d, 1.55d };

			StringBuilder table = new StringBuilder();
			// ★ <b>시간을 같이 찍는다</b> — 「판마다 몇 단계」만 보면 놓친다.
			//   지금 값(1.55)은 판마다 +21 이지만 그 2시간이 대부분 <b>막힘 대기</b>이고
			//   실제 진행은 몇 초다. 그건 게임이 아니다.
			table.AppendLine("[IdleKnob] 점수배수 | 판1 | 판2 | 판3 | 판4 | 판5  (걸린시간→환생단계)");

			foreach (double knob in knobs)
			{
				IdleTuning tuning = new IdleTuning();
				tuning.PrestigeMultiplierPerPoint = knob;

				IdleState state = new IdleState();
				StringBuilder row = new StringBuilder();
				row.AppendFormat("[IdleKnob] {0,8:N2} |", knob);

				double elapsed = 0d;
				double runStarted = 0d;
				double lastProgressAt = 0d;
				int lastStage = 1;
				int runs = 0;

				while (elapsed < 14d * DAY && runs < 5)
				{
					IdleModel.Step(state, tuning, TICK);
					elapsed += TICK;
					IdlePlay.BuyEverything(state, tuning);

					if (state.Stage > lastStage)
					{
						lastStage = state.Stage;
						lastProgressAt = elapsed;
					}

					if (elapsed - lastProgressAt < STALL_HOURS * 3600d)
					{
						continue;
					}

					if (IdleModel.CanPrestige(state, tuning) == false)
					{
						break;
					}

					row.AppendFormat(" {0,5:N1}h→{1,4} |", (elapsed - runStarted) / 3600d, state.Stage);
					IdleModel.TryPrestige(state, tuning, out long _);
					runs++;

					runStarted = elapsed;
					lastProgressAt = elapsed;
					lastStage = state.Stage;
				}

				table.AppendLine(row.ToString());
			}

			TestContext.WriteLine(table.ToString());
		}

		/// <summary>
		/// 후반 정체를 어느 손잡이가 메우나 — 여러 값으로 돌려 <b>판마다 환생한 단계</b>를 찍는다.
		/// 실패하지 않는다(자이지 관문이 아니다).
		///
		/// ★ 왜 이 손잡이들인가 — 계산이 먼저다.
		///   자원은 단계마다 `보상배수`, 업그레이드는 비용 1.22·효과 1.15 라
		///   <b>공격력이 자원의 0.70 제곱</b>으로 큰다. 즉 공격력 ∝ 보상배수^0.7·단계.
		///   체력은 1.55^단계다. 지금 값(보상 1.35)이면 1.235 &lt; 1.55 — <b>구조적으로 못 따라간다.</b>
		///   그래서 후보는 셋뿐이다: 보상을 올리거나 · 체력을 낮추거나 · 업그레이드 효과를 키우거나.
		/// </summary>
		[Test]
		public void PrintDepthPerRun_AcrossLateGameKnobs()
		{
			StringBuilder table = new StringBuilder();
			table.AppendLine("[IdleLate] 손잡이 | 판1 | 판2 | 판3 | 판4 | 판5 | 판6  (숫자 = 환생한 단계)");

			Report(table, "지금 그대로", tuning => { });
			Report(table, "보상 1.35→1.9", tuning =>
				tuning.RewardByStage = new GeometricScale(1d, 1.9d));
			Report(table, "체력 1.55→1.30", tuning =>
				tuning.TargetHealthByStage = new GeometricScale(10d, 1.30d));
			Report(table, "공격효과 1.15→1.30", tuning =>
				tuning.DamageCurve = new GeometricUpgradeCurve
				{
					BaseCost = 10d, CostRatio = 1.22d, BaseValue = 1d, ValueRatio = 1.30d,
				});

			TestContext.WriteLine(table.ToString());
		}

		private static void Report(StringBuilder table, string name, System.Action<IdleTuning> tweak)
		{
			IdleTuning tuning = new IdleTuning();
			tweak(tuning);

			IdleState state = new IdleState();
			StringBuilder row = new StringBuilder();
			row.AppendFormat("[IdleLate] {0,-18} |", name);

			double elapsed = 0d;
			double lastProgressAt = 0d;
			int lastStage = 1;
			int runs = 0;

			while (elapsed < 14d * DAY && runs < 6)
			{
				IdleModel.Step(state, tuning, TICK);
				elapsed += TICK;
				IdlePlay.BuyEverything(state, tuning);

				if (state.Stage > lastStage)
				{
					lastStage = state.Stage;
					lastProgressAt = elapsed;
				}

				if (elapsed - lastProgressAt < STALL_HOURS * 3600d)
				{
					continue;
				}

				if (IdleModel.CanPrestige(state, tuning) == false)
				{
					break;
				}

				row.AppendFormat(" {0,5} |", state.Stage);
				IdleModel.TryPrestige(state, tuning, out long _);
				runs++;
				lastProgressAt = elapsed;
				lastStage = state.Stage;
			}

			table.AppendLine(row.ToString());
		}

		/// <summary>
		/// 사람이 <b>어떻게 환생하든</b> 게임이 서는가 — 두 습관을 나란히 돌린다. 실패하지 않는다(자).
		///
		/// ★ 왜 — 지금 표는 「막힐 때까지 버틴다」 한 가지만 잰다. 그 판은 첫 환생까지 <b>24.5시간</b>이다.
		///   그런데 화면은 천장에 닿는 순간(0.7시간) 「더 내려가도 안 열린다」고 말한다.
		///   사람은 그걸 보면 환생한다 — <b>재는 습관이 사람과 다르면 게임을 잘못 재는 것이다</b>
		///   (한 번 그렇게 틀렸다: 「천장에서 환생」로 쟀다가 판이 0.0h 로 찍혔고,
		///   그때 원인은 정책이 아니라 모델의 고장이었다).
		/// </summary>
		[Test]
		public void PrintTwoHabits_StallVersusCeiling()
		{
			StringBuilder table = new StringBuilder();
			table.AppendLine("[IdleHabit] 습관 | 판1 | 판2 | 판3 | 판4 | 판5  (걸린시간 → 환생단계)");

			table.AppendLine(Habit("막힐 때까지 버틴다", false));
			table.AppendLine(Habit("천장 보면 환생한다", true));

			TestContext.WriteLine(table.ToString());
		}

		private static string Habit(string name, bool foldAtCeiling)
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			StringBuilder row = new StringBuilder();
			row.AppendFormat("[IdleHabit] {0,-18} |", name);

			double elapsed = 0d;
			double runStarted = 0d;
			double lastProgressAt = 0d;
			int lastStage = 1;
			int runs = 0;

			while (elapsed < 14d * DAY && runs < 5)
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

				bool ready = foldAtCeiling
					? IdleDrops.MaxTierAt(state.Stage, state.Ascensions, tuning)
						>= IdleDrops.CeilingFor(state.Ascensions, tuning)
					: elapsed - lastProgressAt >= STALL_HOURS * 3600d;

				if (ready == false || IdleModel.CanPrestige(state, tuning) == false)
				{
					continue;
				}

				row.AppendFormat(" {0:N1}h→{1} |", (elapsed - runStarted) / 3600d, state.Stage);
				IdleModel.TryPrestige(state, tuning, out long _);
				runs++;

				runStarted = elapsed;
				lastProgressAt = elapsed;
				lastStage = state.Stage;
			}

			return row.ToString();
		}

		/// <summary>
		/// <b>단계 난이도 자체</b>를 훑는다 — 손잡이(점수 배수)로는 안 고쳐졌기 때문이다.
		///
		/// ★ 진단: 체력이 단계마다 1.55배면 「한 방에 죽임 → 못 죽임」 구간이 21단계뿐이고,
		///   초당 처치가 크면 그건 <b>몇 초</b>다. 그래서 판마다 2.0시간이 통째로 <b>막힘 대기</b>가 된다.
		///   실제 방치형(깊이 밀기 계열)은 몬스터 체력을 <b>단계마다 1.15배</b> 로 두고 단계를 많이 둔다.
		///   난이도를 완만하게 하면 놀 수 있는 띠가 넓어진다.
		///
		/// 벽은 <c>보상 &lt; 체력</c> 이라야 서므로 보상도 같이 낮춘다.
		/// 점수 배수는 체력과 맞춘다(점수 하나 = 단계 하나만큼의 어려움).
		/// </summary>
		[Test]
		public void PrintDepthPerRun_AcrossStageSteepness()
		{
			StringBuilder table = new StringBuilder();
			table.AppendLine("[IdleSteep] 체력/보상 | 판1 | 판2 | 판3 | 판4 | 판5  (걸린시간→환생단계)");

			Steepness(table, 1.55d, 1.35d);
			Steepness(table, 1.30d, 1.20d);
			Steepness(table, 1.15d, 1.10d);
			Steepness(table, 1.08d, 1.05d);

			TestContext.WriteLine(table.ToString());
		}

		private static void Steepness(StringBuilder table, double health, double reward)
		{
			IdleTuning tuning = new IdleTuning();
			tuning.TargetHealthByStage = new GeometricScale(10d, health);
			tuning.RewardByStage = new GeometricScale(1d, reward);
			tuning.PrestigeMultiplierPerPoint = health;

			IdleState state = new IdleState();
			StringBuilder row = new StringBuilder();
			row.AppendFormat("[IdleSteep] {0,4:N2}/{1,4:N2} |", health, reward);

			double elapsed = 0d;
			double runStarted = 0d;
			double lastProgressAt = 0d;
			int lastStage = 1;
			int runs = 0;

			while (elapsed < 14d * DAY && runs < 5)
			{
				IdleModel.Step(state, tuning, TICK);
				elapsed += TICK;
				IdlePlay.BuyEverything(state, tuning);

				if (state.Stage > lastStage)
				{
					lastStage = state.Stage;
					lastProgressAt = elapsed;
				}

				if (elapsed - lastProgressAt < STALL_HOURS * 3600d)
				{
					continue;
				}

				if (IdleModel.CanPrestige(state, tuning) == false)
				{
					break;
				}

				row.AppendFormat(" {0,5:N1}h→{1,5} |", (elapsed - runStarted) / 3600d, state.Stage);
				IdleModel.TryPrestige(state, tuning, out long _);
				runs++;
				runStarted = elapsed;
				lastProgressAt = elapsed;
				lastStage = state.Stage;
			}

			table.AppendLine(row.ToString());
		}

		/// <summary>
		/// ★ <b>물러날 줄 아는 사람</b>으로 다시 잰다 — 이게 이 설계가 실제로 도는지의 증거다.
		///
		/// 앞선 표들은 전부 「앞으로만 가는 사람」이었다. 그 사람은 벽에서 <b>완전히 멎는다</b>:
		/// 못 잡으니 자원이 0 이고, 자원이 0 이니 올릴 수도 없다. 난이도를 완만하게 해도 같았다.
		/// 손잡이 문제가 아니라 <b>없는 기능</b>이었다(물러나기).
		///
		/// 습관: 막히면 <b>잘 잡히는 자리까지 물러나</b> 벌고, 세지면 다시 민다.
		/// </summary>
		[Test]
		public void PrintCurve_ForSomeoneWhoRetreats()
		{
			IdleTuning tuning = new IdleTuning();

			StringBuilder table = new StringBuilder();
			table.AppendLine("[IdleRetreat] 시각 | 앞으로만 가는 사람 | 물러날 줄 아는 사람 (가장 깊이 간 단계)");

			IdleState forward = new IdleState();
			IdleState clever = new IdleState();
			forward.EnsureProducerRoom(tuning.ProducerCount);
			clever.EnsureProducerRoom(tuning.ProducerCount);
			forward.Owned[0] = 1L;
			clever.Owned[0] = 1L;

			double elapsed = 0d;
			double lastProgressAt = 0d;
			int lastStage = 1;
			int retreats = 0;

			for (int hour = 1; hour <= 48; hour++)
			{
				double until = hour * 3600d;
				while (elapsed < until)
				{
					IdleModel.Step(forward, tuning, TICK);
					IdleModel.Step(clever, tuning, TICK);
					elapsed += TICK;

					IdlePlay.BuyEverything(forward, tuning);
					IdlePlay.BuyEverything(clever, tuning);

					if (clever.Stage > lastStage)
					{
						lastStage = clever.Stage;
						lastProgressAt = elapsed;
						continue;
					}

					if (elapsed - lastProgressAt < 900d)
					{
						continue;
					}

					// ★ 막혔다. 벌 수 있는 자리로 물러나거나, 벌었으면 다시 민다.
					if (clever.HoldingStage)
					{
						clever.HoldingStage = false;
						IdleModel.TryGoToStage(clever, clever.BestStage);
					}
					else
					{
						int farmable = FarmableStage(clever, tuning);
						if (farmable < clever.Stage && IdleModel.TryGoToStage(clever, farmable))
						{
							clever.HoldingStage = true;
							retreats++;
						}
					}

					lastProgressAt = elapsed;
					lastStage = clever.Stage;
				}

				if (hour == 1 || hour == 6 || hour == 12 || hour == 24 || hour == 48)
				{
					table.AppendLine(string.Format("[IdleRetreat] {0,3}시간 | {1,6} | {2,6}  (물러남 {3}회)",
						hour, forward.BestStage, clever.BestStage, retreats));
				}
			}

			TestContext.WriteLine(table.ToString());

			// ★ <b>층을 가른 뒤로 물러나기의 뜻이 바뀌었다</b> (실측 2026-08-16).
			//   전에는 「물러나 <b>자원</b>을 번다」였다. 이제 자원은 기지가 내므로 그 이유가 없어졌다.
			//   지금 물러나기가 주는 것은 <b>많이 떨구는 것</b>이다 —
			//   얕은 자리는 빨리 잡히고, 장비 수가 곧 합치기·감정의 재료다.
			//   깊이는 오히려 앞으로만 가는 쪽이 낫다. 그 <b>맞바꿈</b>이 성립하는지를 본다.
			// ⚠ 가방 칸 수로 재면 안 된다 — 둘 다 40칸이 꽉 차 차이가 가려진다(실측).
			//   재야 할 것은 <b>여태 얻은 총량</b>이다. 그게 합치기·감정의 재료다.
			long forwardGot = TotalDropped(forward);
			long cleverGot = TotalDropped(clever);

			TestContext.WriteLine("[IdleRetreat] 48시간 — 앞으로만: " + forward.BestStage + "단계 · 얻은 장비 " + forwardGot
				+ "  ||  물러남: " + clever.BestStage + "단계 · 얻은 장비 " + cleverGot);

			Assert.Greater(cleverGot, forwardGot,
				"물러나도 장비가 더 안 모인다 — 그러면 물러나기가 있을 이유가 없다");
		}

/// <summary>가진 것 중 <b>가장 높은 등급부터</b> 감정한다 — 사람이 하는 짓과 가장 가깝다.</summary>
		private static void AppraiseWhatWeCan(IdleState state, IdleTuning tuning)
		{
			for (int tier = state.DroppedByTier.Length; tier >= 2; tier--)
			{
				while (IdlePotentials.TryAppraise(state, tuning, tier, out PotentialRoll _))
				{
				}
			}
		}

		/// <summary>여태 얻은 장비 총량 — 가방 상한에 안 가린다.</summary>
		private static long TotalDropped(IdleState state)
		{
			long total = 0L;
			for (int tier = 0; tier < state.DroppedByTier.Length; tier++)
			{
				total += state.DroppedByTier[tier];
			}

			return total;
		}

		/// <summary>한 방에 잡히는 가장 깊은 자리 — 코어가 아는 규칙을 그대로 쓴다.</summary>
		private static int FarmableStage(IdleState state, IdleTuning tuning)
		{
			return IdleModel.BestFarmingStage(state, tuning);
		}
	}
}

