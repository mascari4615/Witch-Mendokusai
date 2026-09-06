using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 다시 시작하는 것이 <b>보상</b>인가 (TASK-WM-406).
	///
	/// ★ 방치형에서 리셋은 그 자체로는 벌이다 — 모은 걸 다 뺏기니까.
	///   보상이 되는 조건은 딱 하나: <b>다시 내려가는 게 지난번보다 빨라야 한다.</b>
	///   그게 이 파일의 핵심 판이고, 나머지는 그 판이 성립하는 조건들이다.
	///
	/// 근거(레퍼런스): 생산자 클리커 계열은 구운 쿠키의 세제곱근, 깊이 밀기 계열 2층은 로그로 점수를 준다.
	///   둘 다 「지수로 커지는 노력 → 선형으로 커지는 보상」이다. 우리는 단계 난이도가 지수라
	///   단계 번호가 이미 그 로그다 — 그래서 단계에 선형으로 준다.
	/// </summary>
	public sealed class IdlePrestigeTests
	{
		private const double TOLERANCE = 1e-9d;

		/// <summary>벽을 느끼기 전에는 아직 환생 못 한다 — 그 전의 리셋은 보상이 아니라 벌이다.</summary>
		[Test]
		public void CannotPrestige_BeforeFeelingTheWall()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState { Stage = tuning.PrestigeMinStage - 1 };

			Assert.IsFalse(IdleModel.CanPrestige(state, tuning));
			Assert.AreEqual(0L, IdleModel.PrestigeAwardFor(state, tuning));
			Assert.IsFalse(IdleModel.TryPrestige(state, tuning, out long _), "못 환생해야 하는데 환생됐다");
			Assert.AreEqual(tuning.PrestigeMinStage - 1, state.Stage, "실패했는데 판이 건드려졌다");
		}

		/// <summary>깊이 갈수록 환생했을 때 더 받는다.</summary>
		[Test]
		public void DeeperRun_PaysMore()
		{
			IdleTuning tuning = new IdleTuning();

			long shallow = IdleModel.PrestigeAwardFor(new IdleState { Stage = tuning.PrestigeMinStage }, tuning);
			long deep = IdleModel.PrestigeAwardFor(new IdleState { Stage = tuning.PrestigeMinStage + 20 }, tuning);

			Assert.AreEqual(1L, shallow, "닿자마자 환생하면 1점이어야 한다");
			Assert.Greater(deep, shallow);
		}

		/// <summary>무엇이 남고 무엇이 지워지나 — 이 게임의 성격을 정하는 자리.</summary>
		[Test]
		public void Prestige_KeepsProofOfPastRun_ClearsTheRunItself()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState
			{
				Stage = 15,
				BestStage = 15,
				KillsInStage = 4,
				Kills = 500L,
				Resource = 9999d,
				HitsOnTarget = 3L,
			};
			state.Damage.Level = 20;
			state.AttackSpeed.Level = 12;

			Assert.IsTrue(IdleModel.TryPrestige(state, tuning, out long awarded));

			// 남는 것 = 「지난 판이 헛되지 않았다」의 증거
			Assert.AreEqual(awarded, state.PrestigePoints);
			Assert.AreEqual(1, state.Ascensions);
			Assert.AreEqual(15, state.BestStage, "가장 깊이 닿은 기록이 지워졌다");
			Assert.AreEqual(500L, state.Kills, "총 처치는 살아 있어야 한다");

			// 지워지는 것 = 「다시 빠르게 내려가는 재미」의 재료
			Assert.AreEqual(0d, state.Resource, TOLERANCE);
			Assert.AreEqual(1, state.Stage);
			Assert.AreEqual(0, state.KillsInStage);
			Assert.AreEqual(0L, state.HitsOnTarget);
			Assert.AreEqual(0, state.Damage.Level);
			Assert.AreEqual(0, state.AttackSpeed.Level);
		}

		/// <summary>
		/// ★ 핵심 — <b>리셋한 뒤가 더 빠르다.</b> 이게 아니면 리셋은 그냥 벌이다.
		///
		/// 같은 정책(살 수 있으면 싼 쪽부터)으로 두 판을 돌려 「10단계에 닿는 데 걸린 시간」을 잰다.
		/// </summary>
		[Test]
		public void AfterPrestige_ReachingSameDepthIsFaster()
		{
			IdleTuning tuning = new IdleTuning();
			int goal = tuning.PrestigeMinStage;

			IdleState first = new IdleState();
			double before = SecondsToReach(first, tuning, goal);

			Assert.IsTrue(IdleModel.TryPrestige(first, tuning, out long awarded), "10단계에 닿았는데 아직 환생 못 한다");
			Assert.Greater(awarded, 0L);

			double after = SecondsToReach(first, tuning, goal);

			TestContext.WriteLine("[IdlePrestige] 처음 " + before.ToString("N0") + "초 → 환생한 뒤 " + after.ToString("N0")
				+ "초 (점수 " + awarded + " · 배수 " + IdleModel.PrestigeMultiplier(first, tuning).ToString("N2") + "배)");

			Assert.Less(after, before, "환생하고 다시 내려가는 게 더 느리다 — 리셋이 벌이 됐다");
		}

		/// <summary>
		/// ★ <b>깊이 갔다 환생해야 값어치가 난다</b> — 이 게임이 「한 번 더」를 파는 방식.
		///
		/// 실측(2026-08-16): 관문(10단계)에 닿자마자 환생하면 1점 = +10% 라 710초 → 646초,
		/// <b>9% 밖에 안 빨라진다.</b> 그건 사람이 「환생할 이유」로 못 느낀다.
		/// 반대로 8시간 방치하면 57단계까지 가고 그건 48점 = +480% 다.
		///
		/// 그래서 여기서 재는 것은 「빨라지나」가 아니라 <b>얼마나</b> 빨라지나다.
		/// 숫자를 손보다 이 성질을 깨면 이 판이 잡는다.
		/// </summary>
		[Test]
		public void PrestigingDeep_IsWorthMuchMoreThanPrestigingAtTheGate()
		{
			IdleTuning tuning = new IdleTuning();
			int goal = 20;

			IdleState plain = new IdleState();
			double before = SecondsToReach(plain, tuning, goal);

			// 깊이 갔다 환생한 사람 — 8시간 방치가 닿는 언저리(실측 57단계)보다 보수적으로 잡는다.
			IdleState veteran = new IdleState { Stage = 40 };
			Assert.IsTrue(IdleModel.TryPrestige(veteran, tuning, out long deepAward));

			double after = SecondsToReach(veteran, tuning, goal);

			TestContext.WriteLine("[IdlePrestige-깊이] " + goal + "단계까지 처음 " + before.ToString("N0")
				+ "초 → 40단계에서 환생한 뒤 " + after.ToString("N0") + "초 (점수 " + deepAward
				+ " · 배수 " + IdleModel.PrestigeMultiplier(veteran, tuning).ToString("N1") + "배)");

			Assert.Greater(deepAward, 20L, "40단계에서 환생했는데 점수가 20 이하다");
			Assert.Less(after, before * 0.7d,
				"깊이 갔다 환생했는데 30%도 안 빨라진다 — 「한 번 더」를 팔 수 없다");
		}

		/// <summary>
		/// ★ <b>점수 하나 = 단계 하나만큼의 어려움.</b>
		///
		/// 점수는 대략 단계 수만큼 쌓이므로, 점수 하나가 단계 하나의 어려움을 갚으면
		/// 환생할 때마다 그 판이 늘린 어려움이 정확히 상쇄된다.
		/// 실측(2026-08-16, 넘치는 피해를 버린 뒤): 1.55 는 판마다 +21 로 고르고,
		/// 1.10 은 +64 → +68 → +28 로 감속하다 멎는다.
		///
		/// ⚠ 한때 이 등식을 「틀렸다」며 물렀다. 그때 폭주한 진짜 원인은 이 값이 아니라
		///   <b>모델의 고장</b>이었다(한 번 때려 여러 마리가 죽었다). 모델을 고치니 등식이 맞았다.
		/// </summary>
		[Test]
		public void PointValue_MatchesStageDifficulty()
		{
			IdleTuning tuning = new IdleTuning();

			Assert.AreEqual(tuning.TargetHealthByStage.Ratio, tuning.PrestigeMultiplierPerPoint, 1e-9d,
				"점수 배수와 단계 난이도 배수가 어긋났다 — 며칠 뒤에 정체하거나 폭주한다");
		}

		/// <summary>
		/// ★ <b>버티는 쪽이 낫다</b> — 천장에 닿자마자 환생를 되풀이하면 손해다.
		///
		/// 실측(2026-08-16, 이레): 「천장 보면 환생한다」는 판5에 102단계,
		/// 「막힐 때까지 버틴다」는 <b>363단계</b>. 3.5배 차이다.
		/// 이 부등식이 뒤집히면 게임이 <b>환생 남발</b>로 무너진다 —
		/// 그때는 환생에 비용을 붙여야 한다.
		/// </summary>
		[Test]
		public void GrindingDeeper_BeatsFoldingEarly()
		{
			IdleTuning tuning = new IdleTuning();

			int shallowFold = 26;
			int deepFold = 70;

			long shallow = IdleModel.PrestigeStandingFor(new IdleState { Stage = shallowFold }, tuning);
			long deep = IdleModel.PrestigeStandingFor(new IdleState { Stage = deepFold }, tuning);

			Assert.Greater(deep, shallow * 2L,
				"더 내려가도 점수가 별로 안 는다 — 그러면 천장에서 바로 환생하는 게 이득이 되어 게임이 무너진다");
		}

		/// <summary>점수가 공격력에 실제로 실린다 — 배수가 이름뿐이면 위 판이 우연히 통과할 수 있다.</summary>
		[Test]
		public void Points_MultiplyDamage()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState plain = new IdleState();
			IdleState blessed = new IdleState { PrestigePoints = 10L };

			double expected = System.Math.Pow(tuning.PrestigeMultiplierPerPoint, 10d);

			Assert.AreEqual(1d, IdleModel.PrestigeMultiplier(plain, tuning), TOLERANCE);
			Assert.AreEqual(expected, IdleModel.PrestigeMultiplier(blessed, tuning), 1e-6d, "점수마다 곱해야 한다");
			Assert.AreEqual(IdleModel.DamageOf(plain, tuning) * expected, IdleModel.DamageOf(blessed, tuning), 1e-3d);
		}

		/// <summary>점수는 저장을 건너 살아남는다. 옛 저장에는 없으므로 0 으로 온다.</summary>
		[Test]
		public void Points_SurviveSaveLoad()
		{
			IdleState state = new IdleState { PrestigePoints = 37L, Ascensions = 4 };

			IdleState restored = new IdleState();
			restored.Load(state.Save());

			Assert.AreEqual(37L, restored.PrestigePoints);
			Assert.AreEqual(4, restored.Ascensions);

			IdleState fromOld = new IdleState();
			fromOld.Load(new IdleSaveData { Resource = 1d });
			Assert.AreEqual(0L, fromOld.PrestigePoints);
		}

		/// <summary>사진에 실린다 — 표현이 규칙을 다시 짜지 않게.</summary>
		[Test]
		public void Snapshot_CarriesPrestige()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState { Stage = 12, PrestigePoints = 5L };
			IdleSession session = new IdleSession(tuning, state);

			IdleSnapshot snapshot = session.Capture();

			Assert.AreEqual(5L, snapshot.PrestigePoints);
			Assert.AreEqual(IdleModel.PrestigeAwardFor(state, tuning), snapshot.PrestigeAward);
			Assert.AreEqual(System.Math.Pow(tuning.PrestigeMultiplierPerPoint, 5d), snapshot.PrestigeMultiplier, 1e-9d);
		}

		/// <summary>의도로도 접힌다 — 표현이 쓰는 길이 진짜 도는지.</summary>
		[Test]
		public void Intent_Prestiges()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession tooEarly = new IdleSession(tuning, new IdleState { Stage = 2 });
			Assert.IsFalse(tooEarly.Send(new IdlePrestigeIntent()), "아직인데 환생됐다");

			IdleSession ready = new IdleSession(tuning, new IdleState { Stage = 14 });
			Assert.IsTrue(ready.Send(new IdlePrestigeIntent()));
			Assert.AreEqual(1, ready.State.Stage);
			Assert.Greater(ready.State.PrestigePoints, 0L);
		}


		/// <summary>목표 단계에 닿을 때까지 걸린 시간 — 두 층을 다 사면서 돌린다.</summary>
		private static double SecondsToReach(IdleState state, IdleTuning tuning, int goalStage)
		{
			const double TICK = 10d;
			const double LIMIT = 60d * 60d * 24d * 30d;

			double elapsed = 0d;
			while (state.Stage < goalStage && elapsed < LIMIT)
			{
				IdleModel.Step(state, tuning, TICK);
				elapsed += TICK;
				IdlePlay.BuyEverything(state, tuning);
			}

			Assert.Less(elapsed, LIMIT, "한 달을 돌려도 " + goalStage + "단계에 못 닿는다 — 곡선이 막혔다");
			return elapsed;
		}

		/// <summary>
		/// ★ 환생 점수는 <b>가장 깊이 간 곳</b>으로 센다 — 물러나 판다고 벌 받지 않는다 (회귀).
		///
		/// 이 게임은 물러나서 파는 것을 <b>권한다</b>(TryGoToStage 주석: 물러나는 데 벌을 주면
		/// 아무도 안 물러나고 그러면 벽에서 게임이 멎는다). 그런데 환생 점수는 「지금 서 있는
		/// 곳」으로 세고 있었다 — 500까지 갔다가 300으로 물러나 파는 순간 점수가 0이 됐다.
		/// 권장한 행동이 조용히 벌을 받고 있었다.
		/// </summary>
		[Test]
		public void RetreatingToFarm_DoesNotEatThePrestigeAward()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState deep = new IdleState();
			deep.EnsureProducerRoom(tuning.ProducerCount);
			deep.Stage = tuning.PrestigeMinStage + 200;
			deep.BestStage = deep.Stage;

			long standing = IdleModel.PrestigeAwardFor(deep, tuning);
			Assert.Greater(standing, 0L, "잴 것이 없다 — 깊이 갔는데도 점수가 0 이다");

			// 그 자리에서 <b>물러나 판다</b>. 가장 깊이 간 곳은 그대로다.
			Assert.IsTrue(IdleModel.TryGoToStage(deep, tuning.PrestigeMinStage + 50));

			Assert.AreEqual(standing, IdleModel.PrestigeAwardFor(deep, tuning),
				"물러났다고 환생 점수가 깎였다");
		}

		/// <summary>★ 환생한 뒤에는 <b>더 깊이 가야</b> 새 점수가 붙는다 — 같은 길을 다시 팔 수는 없다.</summary>
		[Test]
		public void AfterPrestige_OnlyNewDepthPays()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Stage = tuning.PrestigeMinStage + 100;
			state.BestStage = state.Stage;

			Assert.IsTrue(IdleModel.TryPrestige(state, tuning, out long awarded));
			Assert.Greater(awarded, 0L);

			Assert.AreEqual(0L, IdleModel.PrestigeAwardFor(state, tuning),
				"같은 깊이인데 또 점수를 준다 — 환생만 반복하는 것이 최적이 된다");

			state.Stage = state.BestStage + 10;
			state.BestStage = state.Stage;

			Assert.Greater(IdleModel.PrestigeAwardFor(state, tuning), 0L,
				"더 깊이 갔는데도 새 점수가 없다");
		}

		/// <summary>
		/// ★ 환생이 <b>어느 깊이부터</b> 값어치가 생기는지 말한다 — 「더 내려가야 한다」로 끝내면
		///   그건 안내가 아니다.
		///
		/// 첫 판에서는 최소 깊이가 답이고, 환생한 뒤로는 「여태 가장 깊이 간 곳 + 1」이 답이다.
		/// </summary>
		[Test]
		public void ItSaysHowDeepYouMustGo()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState fresh = new IdleState();
			fresh.EnsureProducerRoom(tuning.ProducerCount);

			Assert.AreEqual(tuning.PrestigeMinStage, IdleModel.PrestigeNextPayingStage(fresh, tuning),
				"첫 판인데 최소 깊이를 안 짚는다");

			// 거기까지 가면 값어치가 생기고, 그때는 0 (더 갈 필요 없다).
			fresh.Stage = tuning.PrestigeMinStage;
			fresh.BestStage = fresh.Stage;
			Assert.Greater(IdleModel.PrestigeAwardFor(fresh, tuning), 0L);
			Assert.AreEqual(0, IdleModel.PrestigeNextPayingStage(fresh, tuning),
				"이미 값어치가 있는데도 더 가라고 한다");
		}

		/// <summary>★ 환생한 뒤에는 <b>여태보다 깊이</b> 가야 한다고 말한다 — 그리고 그 말이 맞다.</summary>
		[Test]
		public void AfterPrestige_ThePointedStageActuallyPays()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Stage = tuning.PrestigeMinStage + 37;
			state.BestStage = state.Stage;

			Assert.IsTrue(IdleModel.TryPrestige(state, tuning, out long _));

			int pointed = IdleModel.PrestigeNextPayingStage(state, tuning);
			Assert.Greater(pointed, state.BestStage, "여태 간 곳보다 얕은 곳을 가리킨다");

			// 한 칸 앞에서는 아직 0 이어야 하고, 짚은 자리에서는 값어치가 생겨야 한다.
			state.Stage = pointed - 1;
			state.BestStage = state.Stage > state.BestStage ? state.Stage : state.BestStage;
			Assert.AreEqual(0L, IdleModel.PrestigeAwardFor(state, tuning),
				"짚은 자리보다 얕은데도 값어치가 있다 — 너무 깊이 가라고 한 것이다");

			state.Stage = pointed;
			state.BestStage = pointed;
			Assert.Greater(IdleModel.PrestigeAwardFor(state, tuning), 0L,
				"짚은 자리까지 갔는데 값어치가 없다 — 거짓말을 한 것이다");
		}
}
}
