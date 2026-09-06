using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	// IdleBaseTests.cs 의 Intents 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 의도로 들어오는 길.
	public sealed partial class IdleBaseTests
	{
		/// <summary>
		/// ★ <b>버튼이 코어에 닿는 길</b>이 맞는지 — 의도 하나하나.
		///
		/// ⚠ 시험은 여태 코어 함수를 <b>직접</b> 불렀다(TryBuy·TryMerge…). 그래서 화면이 보내는
		///   <b>의도</b>가 엉뚱한 함수에 닿거나 값을 흘려도 <b>전부 초록</b>이었다.
		///   오늘 파티 자리 복제 버그가 정확히 그 자리에서 나왔다(Send(IdleSetPartyIntent)).
		///   여기서는 <b>보낸 결과</b>만 본다 — 판이 실제로 그렇게 됐나.
		/// </summary>
		[Test]
		public void BuyingProducer_ThroughTheIntent_Works()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Resource = 1e6d;

			IdleSession session = new IdleSession(tuning, state);
			long before = state.Owned[1];

			Assert.IsTrue(session.Send(new IdleBuyProducerIntent(1)), "샀다는 답이 안 온다");
			Assert.AreEqual(before + 1L, state.Owned[1], "<1번>을 샀는데 다른 게 늘었다");
			Assert.Less(state.Resource, 1e6d, "자원을 안 썼다");
		}

		/// <summary>★ 물러나기 의도가 <b>그 단계로</b> 옮긴다.</summary>
		[Test]
		public void GoingToStage_ThroughTheIntent_Works()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Stage = 20;
			state.BestStage = 30;

			IdleSession session = new IdleSession(tuning, state);

			Assert.IsTrue(session.Send(new IdleGoToStageIntent(7)));
			Assert.AreEqual(7, state.Stage, "엉뚱한 자리로 갔다");

			Assert.IsFalse(session.Send(new IdleGoToStageIntent(999)), "가 본 적 없는 데로 보낸다");
			Assert.AreEqual(7, state.Stage);
		}

		/// <summary>★ 차기 의도가 <b>그 자리에</b> 채운다 — 가방에서 빠지고 부위에 들어간다.</summary>
		[Test]
		public void EquippingThroughTheIntent_Works()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Bag.Add(new IdleItem(5, IdleItemSlot.Feet));

			IdleSession session = new IdleSession(tuning, state);

			Assert.IsTrue(session.Send(new IdleEquipIntent(IdleHeroes.STARTER_ID, 0)));
			Assert.AreEqual(0, state.Bag.Count, "가방에서 안 빠졌다");
			Assert.AreEqual(5, state.Worn[(int)IdleItemSlot.Feet].Tier, "엉뚱한 부위에 찼다");
		}

		/// <summary>★ 합치기 의도가 <b>그 등급·그 부위</b>를 합친다.</summary>
		[Test]
		public void MergingThroughTheIntent_Works()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Resource = 1e9d;

			for (int one = 0; one < tuning.MergeCount; one++)
			{
				state.Bag.Add(new IdleItem(2, IdleItemSlot.Head));
			}

			IdleSession session = new IdleSession(tuning, state);

			Assert.IsTrue(session.Send(new IdleMergeIntent(2, IdleItemSlot.Head)));
			Assert.AreEqual(1, state.Bag.Count, "재료가 안 없어졌다");
			Assert.AreEqual(3, state.Bag[0].Tier, "한 단계 위로 안 갔다");
			// 결과 부위는 굴림 (2026-08-31). 재료 부위와 무관
			Assert.GreaterOrEqual((int)state.Bag[0].Slot, 0);
			Assert.Less((int)state.Bag[0].Slot, IdleGear.SLOT_COUNT);
		}

		/// <summary>★ 뽑기 의도가 <b>실제로 뽑는다</b> — 값도 치른다.</summary>
		[Test]
		public void PullingThroughTheIntent_Works()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Resource = 1e9d;
			state.Stones = 5L;

			IdleSession session = new IdleSession(tuning, state);

			Assert.IsTrue(session.Send(new IdlePullHeroIntent()), "뽑았다는 답이 안 온다");

			// 시작 인형 하나 있음. 뽑은 것이 새 얼굴이면 둘, 시작 인형과 겹치면 하나 + 중복 1
			int faces = 0;
			for (int index = 0; index < state.Heroes.Count; index++)
			{
				faces += 1 + state.Heroes[index].Copies;
			}

			Assert.AreEqual(2, faces, "뽑았는데 아무도 안 왔다 (시작 인형 1 + 뽑은 것 1)");
			Assert.AreEqual(4L, state.Stones, "환생석을 안 썼다");
			Assert.Less(state.Resource, 1e9d, "자원을 안 썼다");
		}
	}
}

