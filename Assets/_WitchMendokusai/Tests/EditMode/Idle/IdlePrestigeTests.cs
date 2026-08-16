using NUnit.Framework;
using UnityEngine;
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
	/// 근거(레퍼런스): 쿠키 클리커는 구운 쿠키의 세제곱근, 클리커 히어로즈 2층은 로그로 점수를 준다.
	///   둘 다 「지수로 커지는 노력 → 선형으로 커지는 보상」이다. 우리는 단계 난이도가 지수라
	///   단계 번호가 이미 그 로그다 — 그래서 단계에 선형으로 준다.
	/// </summary>
	public sealed class IdlePrestigeTests
	{
		private const double TOLERANCE = 1e-9d;

		/// <summary>벽을 느끼기 전에는 못 접는다 — 그 전의 리셋은 보상이 아니라 벌이다.</summary>
		[Test]
		public void CannotPrestige_BeforeFeelingTheWall()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState { Stage = tuning.PrestigeMinStage - 1 };

			Assert.IsFalse(IdleModel.CanPrestige(state, tuning));
			Assert.AreEqual(0L, IdleModel.PrestigeAwardFor(state, tuning));
			Assert.IsFalse(IdleModel.TryPrestige(state, tuning, out long _), "못 접어야 하는데 접혔다");
			Assert.AreEqual(tuning.PrestigeMinStage - 1, state.Stage, "실패했는데 판이 건드려졌다");
		}

		/// <summary>깊이 갈수록 접었을 때 더 받는다.</summary>
		[Test]
		public void DeeperRun_PaysMore()
		{
			IdleTuning tuning = new IdleTuning();

			long shallow = IdleModel.PrestigeAwardFor(new IdleState { Stage = tuning.PrestigeMinStage }, tuning);
			long deep = IdleModel.PrestigeAwardFor(new IdleState { Stage = tuning.PrestigeMinStage + 20 }, tuning);

			Assert.AreEqual(1L, shallow, "닿자마자 접으면 1점이어야 한다");
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

			Assert.IsTrue(IdleModel.TryPrestige(first, tuning, out long awarded), "10단계에 닿았는데 못 접는다");
			Assert.Greater(awarded, 0L);

			double after = SecondsToReach(first, tuning, goal);

			Debug.Log("[IdlePrestige] 처음 " + before.ToString("N0") + "초 → 접은 뒤 " + after.ToString("N0")
				+ "초 (점수 " + awarded + " · 배수 " + IdleModel.PrestigeMultiplier(first, tuning).ToString("N2") + "배)");

			Assert.Less(after, before, "접고 다시 내려가는 게 더 느리다 — 리셋이 벌이 됐다");
		}

		/// <summary>
		/// ★ <b>깊이 갔다 접어야 값어치가 난다</b> — 이 게임이 「한 번 더」를 파는 방식.
		///
		/// 실측(2026-08-16): 관문(10단계)에 닿자마자 접으면 1점 = +10% 라 710초 → 646초,
		/// <b>9% 밖에 안 빨라진다.</b> 그건 사람이 「접을 이유」로 못 느낀다.
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

			// 깊이 갔다 접은 사람 — 8시간 방치가 닿는 언저리(실측 57단계)보다 보수적으로 잡는다.
			IdleState veteran = new IdleState { Stage = 40 };
			Assert.IsTrue(IdleModel.TryPrestige(veteran, tuning, out long deepAward));

			double after = SecondsToReach(veteran, tuning, goal);

			Debug.Log("[IdlePrestige-깊이] " + goal + "단계까지 처음 " + before.ToString("N0")
				+ "초 → 40단계에서 접은 뒤 " + after.ToString("N0") + "초 (점수 " + deepAward
				+ " · 배수 " + IdleModel.PrestigeMultiplier(veteran, tuning).ToString("N1") + "배)");

			Assert.Greater(deepAward, 20L, "40단계에서 접었는데 점수가 20 이하다");
			Assert.Less(after, before * 0.7d,
				"깊이 갔다 접었는데 30%도 안 빨라진다 — 「한 번 더」를 팔 수 없다");
		}

		/// <summary>
		/// ★ <b>점수는 난이도보다 한참 작게 곱해야 한다.</b>
		///
		/// 실측(2026-08-16): 점수 배수를 단계 난이도(1.55)와 같게 두면 <b>인플레</b>가 난다 —
		/// 지나온 길을 공짜로 되찾고 그 위에 또 쌓여 판마다 깊이가 5배씩 뛴다(69 → 363).
		/// 점수는 <b>되돌아가는 삯</b>이지 앞으로 미는 힘이 아니다. 미는 힘은 올리기가 낸다.
		///
		/// 이건 필요조건일 뿐이다 — 실제 모양은 <c>IdleLongHaulTests</c> 의 표가 본다.
		/// </summary>
		[Test]
		public void PointMultiplier_StaysWellBelowStageDifficulty()
		{
			IdleTuning tuning = new IdleTuning();

			// 문턱은 <b>실측 경계</b>다 — 1.10 은 판마다 +70단계로 일정했고, 1.20 은 인플레였다.
			// 난이도(1.55)와의 비율로 적으면 그럴싸하지만 그건 재 본 값이 아니다.
			Assert.LessOrEqual(tuning.PrestigeMultiplierPerPoint, 1.15d,
				"점수 배수가 실측 경계를 넘었다 — 며칠이면 숫자가 인플레로 뜻을 잃는다 (1.10 안정 · 1.20 인플레)");
			Assert.Less(tuning.PrestigeMultiplierPerPoint, tuning.TargetHealthByStage.Ratio,
				"점수 배수가 단계 난이도 이상이다 — 지나온 길을 공짜로 되찾는다");
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
			Assert.IsFalse(tooEarly.Send(new IdlePrestigeIntent()), "아직인데 접혔다");

			IdleSession ready = new IdleSession(tuning, new IdleState { Stage = 14 });
			Assert.IsTrue(ready.Send(new IdlePrestigeIntent()));
			Assert.AreEqual(1, ready.State.Stage);
			Assert.Greater(ready.State.PrestigePoints, 0L);
		}

		/// <summary>목표 단계에 닿을 때까지 걸린 시간 — 살 수 있으면 싼 쪽부터 사는 정책으로.</summary>
		private static double SecondsToReach(IdleState state, IdleTuning tuning, int goalStage)
		{
			const double TICK = 1d;
			const double LIMIT = 60d * 60d * 24d * 30d;

			double elapsed = 0d;
			while (state.Stage < goalStage && elapsed < LIMIT)
			{
				IdleModel.Step(state, tuning, TICK);
				elapsed += TICK;
				BuyWhatWeCan(state, tuning);
			}

			Assert.Less(elapsed, LIMIT, "한 달을 돌려도 " + goalStage + "단계에 못 닿는다 — 곡선이 막혔다");
			return elapsed;
		}

		private static void BuyWhatWeCan(IdleState state, IdleTuning tuning)
		{
			while (true)
			{
				bool hasDamage = IdleModel.TryGetNextCost(state, tuning, IdleUpgradeKind.Damage, out double damageCost);
				bool hasSpeed = IdleModel.TryGetNextCost(state, tuning, IdleUpgradeKind.AttackSpeed, out double speedCost);

				bool canDamage = hasDamage && damageCost <= state.Resource;
				bool canSpeed = hasSpeed && speedCost <= state.Resource;

				if (canDamage == false && canSpeed == false)
				{
					return;
				}

				IdleUpgradeKind pick = canDamage && (canSpeed == false || damageCost <= speedCost)
					? IdleUpgradeKind.Damage
					: IdleUpgradeKind.AttackSpeed;

				if (IdleModel.TryRaise(state, tuning, pick, out _) == false)
				{
					return;
				}
			}
		}
	}
}
