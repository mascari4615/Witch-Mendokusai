using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 깊이가 등급의 관문인가 (TASK-WM-406).
	///
	/// ★ 울티마 스쿼드에서 이 규칙 하나가 「같은 자리 반복은 성장이 아니다」를 만든다.
	///   아무리 오래 서 있어도 상한 위는 안 나오므로, 더 좋은 것을 원하면 내려가는 수밖에 없다.
	///   그래서 여기서 가장 중요한 판은 <b>상한 위가 절대 안 나온다</b>이다.
	///
	/// ★ 주사위를 안 굴린다 — 잔여분을 들고 가는 누적이다. 굴렸다면 8시간 오프라인이
	///   처치 수만큼 굴려야 하고 스텝 불변이 깨진다(오프라인이 그 위에 서 있다).
	/// </summary>
	public sealed class IdleDropTests
	{
		/// <summary>근거가 된 표 그대로 — 5단계마다 등급이 하나 열린다.</summary>
		[Test]
		public void TierCap_OpensEveryFiveStages()
		{
			IdleTuning tuning = new IdleTuning();

			Assert.AreEqual(1, IdleDrops.MaxTierAt(1, tuning));
			Assert.AreEqual(1, IdleDrops.MaxTierAt(5, tuning));
			Assert.AreEqual(2, IdleDrops.MaxTierAt(6, tuning));
			Assert.AreEqual(2, IdleDrops.MaxTierAt(10, tuning));
			Assert.AreEqual(3, IdleDrops.MaxTierAt(11, tuning));
		}

		/// <summary>상한이 있다 — 끝없이 열리면 목표가 사라진다.</summary>
		[Test]
		public void TierCap_StopsAtMaxTier()
		{
			IdleTuning tuning = new IdleTuning();

			Assert.AreEqual(tuning.MaxTier, IdleDrops.MaxTierAt(9999, tuning));
		}

		/// <summary>
		/// ★ 핵심 — <b>얕은 데서는 상한 위가 한 개도 안 나온다.</b>
		/// 이게 깨지면 내려갈 이유가 통째로 사라진다.
		/// </summary>
		[Test]
		public void ShallowGrinding_NeverYieldsHighTier()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState { Stage = 1 };

			IdleDrops.Accrue(state, tuning, 1_000_000L, state.Stage);

			Assert.Greater(state.DroppedByTier[0], 0L, "1등급조차 안 나왔다 — 시험이 아무것도 안 쟀다");

			for (int tier = 2; tier <= tuning.MaxTier; tier++)
			{
				Assert.AreEqual(0L, state.DroppedByTier[tier - 1],
					tier + "등급이 1단계에서 나왔다 — 깊이가 관문이 아니게 됐다");
			}
		}

		/// <summary>깊이 가면 위 등급이 실제로 섞인다.</summary>
		[Test]
		public void DeepGrinding_MixesInHigherTiers()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState { Stage = 26 };

			IdleDrops.Accrue(state, tuning, 100_000L, state.Stage);

			int cap = IdleDrops.MaxTierAt(26, tuning);
			Assert.AreEqual(6, cap);
			Assert.Greater(state.DroppedByTier[cap - 1], 0L, "열린 상한 등급이 하나도 안 나왔다 — 상한이 장식이다");
			Assert.AreEqual(0L, state.DroppedByTier[cap], "상한 위가 나왔다");
		}

		/// <summary>위로 갈수록 귀하다 — 다 흔하면 상한이 열려도 감흥이 없다.</summary>
		[Test]
		public void HigherTiers_AreRarer()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState { Stage = 26 };

			IdleDrops.Accrue(state, tuning, 1_000_000L, state.Stage);

			for (int tier = 2; tier <= IdleDrops.MaxTierAt(26, tuning); tier++)
			{
				Assert.Less(state.DroppedByTier[tier - 1], state.DroppedByTier[tier - 2],
					tier + "등급이 아래 등급보다 흔하다");
			}
		}

		/// <summary>몫의 합이 1 — 상한이 열려도 총 개수는 안 늘고 나눠 갖는다.</summary>
		[Test]
		public void Shares_SumToOne_SoDepthChangesQualityNotQuantity()
		{
			IdleTuning tuning = new IdleTuning();

			foreach (int cap in new[] { 1, 3, 8 })
			{
				double total = 0d;
				for (int tier = 1; tier <= cap; tier++)
				{
					total += IdleDrops.ShareOf(tier, cap, tuning);
				}

				Assert.AreEqual(1d, total, 1e-12d, "상한 " + cap + "에서 몫의 합이 1 이 아니다");
			}
		}

		/// <summary>★ 쪼개 밟아도 총합이 같다 — 잔여분을 들고 가는 것의 존재 이유.</summary>
		[Test]
		public void Accrual_IsStepInvariant()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState atOnce = new IdleState { Stage = 12 };
			IdleDrops.Accrue(atOnce, tuning, 10_000L, 12);

			IdleState bitByBit = new IdleState { Stage = 12 };
			for (int i = 0; i < 10_000; i++)
			{
				IdleDrops.Accrue(bitByBit, tuning, 1L, 12);
			}

			for (int tier = 0; tier < atOnce.DroppedByTier.Length; tier++)
			{
				Assert.AreEqual(atOnce.DroppedByTier[tier], bitByBit.DroppedByTier[tier],
					(tier + 1) + "등급이 쪼개 밟았을 때 달라졌다");
			}
		}

		/// <summary>판을 흘려도 같은 성질이 산다 — 실제 경로(Step)로 확인한다.</summary>
		[Test]
		public void RunningTheGame_DropsThings_AndKeepsStepInvariance()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState atOnce = new IdleState();
			IdleModel.Step(atOnce, tuning, 600d);

			IdleState bitByBit = new IdleState();
			for (int i = 0; i < 6000; i++)
			{
				IdleModel.Step(bitByBit, tuning, 0.1d);
			}

			Assert.Greater(atOnce.DroppedByTier.Length, 0, "10분을 돌렸는데 칸조차 안 생겼다");

			long total = 0L;
			for (int tier = 0; tier < atOnce.DroppedByTier.Length; tier++)
			{
				total += atOnce.DroppedByTier[tier];
				Assert.AreEqual(atOnce.DroppedByTier[tier], bitByBit.DroppedByTier[tier],
					(tier + 1) + "등급이 쪼개 밟았을 때 달라졌다");
			}

			Assert.Greater(total, 0L, "10분을 돌렸는데 떨어진 게 0 개다");
			Debug.Log("[IdleDrops] 10분 · " + atOnce.Stage + "단계 · 상한 "
				+ IdleDrops.MaxTierAt(atOnce.Stage, tuning) + "등급 · 총 " + total + "개");
		}

		/// <summary>떨어진 것은 저장을 건너고, 리셋도 건넌다 — 「깊이 갔다 온 값어치」의 증거라서.</summary>
		[Test]
		public void Drops_SurviveSaveAndPrestige()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState { Stage = 20 };
			IdleDrops.Accrue(state, tuning, 5_000L, 20);

			long[] before = (long[])state.DroppedByTier.Clone();

			IdleState restored = new IdleState();
			restored.Load(state.Save());
			for (int tier = 0; tier < before.Length; tier++)
			{
				Assert.AreEqual(before[tier], restored.DroppedByTier[tier], "저장을 건너며 " + (tier + 1) + "등급이 샜다");
			}

			Assert.IsTrue(IdleModel.TryPrestige(restored, tuning, out long _));
			for (int tier = 0; tier < before.Length; tier++)
			{
				Assert.AreEqual(before[tier], restored.DroppedByTier[tier], "리셋에 " + (tier + 1) + "등급이 지워졌다");
			}
		}

		/// <summary>등급 칸이 없던 옛 저장도 멀쩡히 들어온다.</summary>
		[Test]
		public void OldSave_WithoutTiers_LoadsClean()
		{
			IdleState fromOld = new IdleState();
			fromOld.Load(new IdleSaveData { Resource = 3d });

			Assert.IsNotNull(fromOld.DroppedByTier);
			Assert.IsNotNull(fromOld.DropProgressByTier);

			IdleTuning tuning = new IdleTuning();
			IdleDrops.Accrue(fromOld, tuning, 100L, 1);
			Assert.Greater(fromOld.DroppedByTier[0], 0L, "옛 저장에서 이어붙인 판이 안 떨군다");
		}

		/// <summary>사진에 상한이 실린다 — 「왜 더 내려가야 하나」를 화면이 직접 말할 수 있게.</summary>
		[Test]
		public void Snapshot_CarriesTierCap()
		{
			IdleTuning tuning = new IdleTuning();
			IdleSession session = new IdleSession(tuning, new IdleState { Stage = 11 });

			IdleSnapshot snapshot = session.Capture();

			Assert.AreEqual(3, snapshot.MaxTierNow);
			Assert.IsNotNull(snapshot.DroppedByTier);
		}
	}
}
