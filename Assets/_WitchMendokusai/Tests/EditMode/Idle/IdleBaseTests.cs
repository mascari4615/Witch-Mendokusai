using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 기지(클리커 층)와 모험(스쿼드 층)이 <b>서로를 부르는가</b> (TASK-WM-406).
	///
	/// ★ 사용자 지적에서 나온 판이다 — 「쿠키 클리커 같은데 아직 잘 안 녹아든다」.
	///   원인은 잡기 하나가 자원도 장비도 다 냈다는 것. 그러면 기지가 있을 이유가 없다.
	///   그래서 갈랐다: <b>자원은 기지가, 장비는 모험이</b>.
	///   여기서 지키는 것은 그 <b>갈라짐</b>과 <b>맞물림</b>이다.
	/// </summary>
	public sealed class IdleBaseTests
	{
		private const double TOLERANCE = 1e-9d;

		/// <summary>★ 잡기는 자원을 안 낸다 — 안 그러면 기지가 있을 이유가 없다.</summary>
		[Test]
		public void Killing_EarnsGear_NotResource()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			IdleModel.Step(state, tuning, 600d);

			Assert.Greater(state.Kills, 0L, "10분을 돌렸는데 아무것도 안 잡았다");
			Assert.AreEqual(0d, state.Resource, TOLERANCE, "잡기가 자원을 냈다 — 두 층이 도로 합쳐졌다");
			Assert.Greater(state.Bag.Count, 0, "잡았는데 가방에 아무것도 안 들어왔다");
		}

		/// <summary>★ 기지는 잡든 안 잡든 자원을 낸다 — 시간이 내는 것이다.</summary>
		[Test]
		public void Base_EarnsResourceOverTime()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 10L;

			double expected = 10L * IdleBase.OutputOf(0, tuning) * 60d;
			IdleModel.Step(state, tuning, 60d);

			Assert.AreEqual(expected, state.Resource, 1e-6d, "기지가 시간만큼 안 냈다");
		}

		/// <summary>살수록 값이 오른다 — 쿠키 클리커의 1.15배 그대로.</summary>
		[Test]
		public void Producers_GetPricierAsYouBuy()
		{
			IdleTuning tuning = new IdleTuning();

			double first = IdleBase.CostOf(0, 0L, tuning);
			double eleventh = IdleBase.CostOf(0, 10L, tuning);

			Assert.AreEqual(first * System.Math.Pow(1.15d, 10d), eleventh, 1e-6d);
			Assert.Greater(IdleBase.CostOf(1, 0L, tuning), first, "위 번호가 더 싸다");
			Assert.Greater(IdleBase.OutputOf(1, tuning), IdleBase.OutputOf(0, tuning), "위 번호가 덜 낸다");
		}

		/// <summary>자원이 모자라면 안 사진다.</summary>
		[Test]
		public void Buying_NeedsResource()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			Assert.IsFalse(IdleBase.TryBuy(state, tuning, 0), "빈손인데 사졌다");

			state.Resource = IdleBase.CostOf(0, 0L, tuning);
			Assert.IsTrue(IdleBase.TryBuy(state, tuning, 0));
			Assert.AreEqual(0d, state.Resource, 1e-9d, "값을 안 치렀다");
			Assert.AreEqual(1L, state.Owned[0]);
		}

		/// <summary>
		/// ★ <b>한 층만으로는 못 큰다</b> — 이게 「합쳐졌다」의 실체다.
		///
		/// 기지만 굴린 판과 두 층을 다 굴린 판을 비교한다.
		/// 기지만 굴리면 자원은 쌓여도 <b>장비가 안 모인다</b>(용병이 약해 깊이 못 감).
		/// </summary>
		[Test]
		public void OneLayerAlone_CannotGrow()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState baseOnly = new IdleState();
			IdleState both = new IdleState();

			const double TICK = 10d;
			for (double elapsed = 0d; elapsed < 3d * 3600d; elapsed += TICK)
			{
				IdleModel.Step(baseOnly, tuning, TICK);
				IdlePlay.BuyProducers(baseOnly, tuning);

				IdleModel.Step(both, tuning, TICK);
				IdlePlay.BuyEverything(both, tuning);
			}

			Debug.Log("[IdleBase] 3시간 — 기지만: " + baseOnly.Stage + "단계 · 가방 " + baseOnly.Bag.Count
				+ "  ||  두 층: " + both.Stage + "단계 · 가방 " + both.Bag.Count);

			Assert.Greater(both.Stage, baseOnly.Stage, "용병을 올려도 더 못 내려간다 — 자원이 모험과 안 물린다");
		}

		/// <summary>
		/// ★ <b>감정에 자원이 든다</b> — 이 하나가 두 층을 같은 저울에 올린다.
		/// 공짜면 「올릴까 감정할까」가 결정이 아니다.
		/// </summary>
		[Test]
		public void Appraising_CostsResource()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureTierRoom(4);
			state.DroppedByTier[3] = 5L;

			Assert.IsFalse(IdlePotentials.TryAppraise(state, tuning, 4, out PotentialRoll _),
				"빈손인데 감정이 됐다");

			state.Resource = IdleGear.AppraiseCost(4, tuning);
			Assert.IsTrue(IdlePotentials.TryAppraise(state, tuning, 4, out PotentialRoll roll));
			Assert.AreEqual(0d, state.Resource, 1e-6d, "감정 값을 안 치렀다");
			Assert.Greater(roll.Value, 0d);
		}

		/// <summary>기지가 저장을 건넌다 — 안 그러면 껐다 켤 때마다 처음부터 짓는다.</summary>
		[Test]
		public void Base_SurvivesSaveLoad()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 7L;
			state.Owned[2] = 3L;

			IdleState restored = new IdleState();
			restored.Load(state.Save());

			Assert.AreEqual(7L, restored.Owned[0]);
			Assert.AreEqual(3L, restored.Owned[2]);
		}

		/// <summary>옛 저장에는 기지가 없다 — 빈 기지로 들어온다(터지지 않는다).</summary>
		[Test]
		public void OldSaves_LoadWithEmptyBase()
		{
			IdleState fromOld = new IdleState();
			fromOld.Load(new IdleSaveData { Resource = 5d });

			Assert.IsNotNull(fromOld.Owned);
			Assert.AreEqual(0d, IdleBase.OutputPerSecond(fromOld, new IdleTuning()), TOLERANCE);
		}
	}
}
