using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 장비 합치기 (TASK-WM-406).
	///
	/// ★ 여기 시험이 <b>하나도 없었다</b> — 가방을 갈아 없애는 자리인데. 그래서 「앞에서부터
	///   셋을 집는다」가 몇 달 동안 아무한테도 안 걸렸다.
	/// </summary>
	public sealed class IdleGearTests
	{
		/// <summary>
		/// ★ 합치기는 <b>나쁜 것부터</b> 먹는다 — 잘 나온 잠재가 사람 모르게 사라지면 안 된다.
		///
		/// 전에는 가방 앞에서부터 셋을 집었다. 그래서 좋은 잠재를 하나 들고 있으면 합치기
		/// 한 번에 그게 먼저 먹혔다 — 사람이 고른 적도 없는데. 그건 결정이 아니라 사고다.
		/// </summary>
		[Test]
		public void Merging_EatsTheWorstOnesFirst()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Resource = 1e9d;

			// 제일 좋은 것을 <b>맨 앞</b>에 둔다 — 옛 규칙이면 이게 먼저 먹힌다.
			IdleItem treasure = new IdleItem(2, IdleItemSlot.Head);
			treasure.PotentialValue = 0.9d;
			treasure.PotentialGradeValue = 3;
			state.Bag.Add(treasure);

			for (int spare = 0; spare < tuning.MergeCount; spare++)
			{
				state.Bag.Add(new IdleItem(2, IdleItemSlot.Head));
			}

			Assert.IsTrue(IdleGear.TryMerge(state, tuning, 2, IdleItemSlot.Head, out IdleItem made));
			Assert.AreEqual(3, made.Tier);

			bool kept = false;
			for (int index = 0; index < state.Bag.Count; index++)
			{
				if (state.Bag[index].PotentialValue >= 0.9d)
				{
					kept = true;
				}
			}

			Assert.IsTrue(kept, "제일 좋은 잠재가 재료로 먹혔다");
		}

		/// <summary>★ 재료가 그것밖에 없으면 좋은 것도 먹힌다 — 「지킬까 올릴까」는 그대로 남는다.</summary>
		[Test]
		public void WhenNothingElseIsLeft_TheGoodOneIsStillSpent()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Resource = 1e9d;

			for (int index = 0; index < tuning.MergeCount; index++)
			{
				IdleItem one = new IdleItem(2, IdleItemSlot.Head);
				one.PotentialValue = 0.5d;
				one.PotentialGradeValue = 2;
				state.Bag.Add(one);
			}

			Assert.IsTrue(IdleGear.TryMerge(state, tuning, 2, IdleItemSlot.Head, out IdleItem made));
			Assert.AreEqual(3, made.Tier);
			Assert.IsTrue(made.IsRaw, "합쳤는데 잠재가 따라왔다 — 감정이 한 번짜리가 된다");
			Assert.AreEqual(1, state.Bag.Count, "재료가 안 없어졌다");
		}

		/// <summary>
		/// ★ <b>한 벌이 주는 배수</b>는 차고 있든 가방에 있든 같은 셈이다.
		///
		/// 화면이 「차면 얼마나 좋아지나」를 말하려면 가방에 있는 것의 값도 필요한데,
		/// 그 셈을 화면이 따로 쓰면 언젠가 갈린다. 그래서 한 자리에서만 센다.
		/// </summary>
		[Test]
		public void AnItemIsWorthTheSame_InTheBagOrOn()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			IdleItem one = new IdleItem(4, IdleItemSlot.Head);
			one.PotentialValue = 0.3d;

			double inTheBag = IdleGear.MultiplierOfItem(one, tuning);

			state.Bag.Add(one);
			Assert.IsTrue(IdleGear.TryEquip(state, 0));

			Assert.AreEqual(inTheBag, IdleGear.MultiplierOf(state, tuning, IdleItemSlot.Head), 1e-9d,
				"가방에서 잰 값과 차고 나서 잰 값이 다르다 — 화면이 거짓 예고를 하게 된다");
			Assert.Greater(inTheBag, 1d, "잴 것이 없다 — 배수가 1 이다");
		}

		/// <summary>★ 빈 자리는 배수 1 — 「없음」이 곧 손해가 아니라 <b>기준점</b>이다.</summary>
		[Test]
		public void AnEmptySlotIsJustOne()
		{
			IdleTuning tuning = new IdleTuning();
			Assert.AreEqual(1d, IdleGear.MultiplierOfItem(default(IdleItem), tuning), 1e-9d);
		}

		/// <summary>
		/// ★ 「차면 어떻게 되나」는 <b>부위</b>로 찾는다 — 가방 자리 번호가 아니라 (회귀).
		///
		/// 처음엔 이 셈이 화면에 있었고, 가방 자리 번호로 착용 배열의 경계를 봤다.
		/// 가방은 40칸이고 부위는 4개라, 가방 다섯 번째 칸부터는 늘 「아무것도 안 찬 것」으로
		/// 쳐서 「x1.00 → …」라는 거짓 예고를 했다.
		/// </summary>
		[Test]
		public void WearGain_LooksUpBySlot_NotByBagIndex()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			// 머리에 좋은 것을 차 둔다.
			state.Worn[(int)IdleItemSlot.Head] = new IdleItem(6, IdleItemSlot.Head);

			// 가방을 여러 칸 채운 <b>뒤쪽</b>에 머리 장비를 둔다 — 옛 실수가 드러나는 자리.
			for (int filler = 0; filler < 7; filler++)
			{
				state.Bag.Add(new IdleItem(1, IdleItemSlot.Feet));
			}

			IdleItem candidate = new IdleItem(2, IdleItemSlot.Head);
			state.Bag.Add(candidate);

			IdleGear.CompareToWorn(state.Worn, state.Bag[state.Bag.Count - 1], tuning,
				out double now, out double after);

			Assert.AreEqual(IdleGear.MultiplierOfItem(state.Worn[(int)IdleItemSlot.Head], tuning), now, 1e-9d,
				"찬 것을 못 찾았다 — 가방 자리 번호로 뒤진 것이다");
			Assert.Less(after, now, "더 못한 것인데 낫다고 한다");
		}

		/// <summary>★ 빈 부위면 <b>1 에서</b> 시작한다 — 그게 기준점이다.</summary>
		[Test]
		public void WearGain_FromAnEmptySlot_StartsAtOne()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();

			IdleGear.CompareToWorn(state.Worn, new IdleItem(3, IdleItemSlot.Body), tuning,
				out double now, out double after);

			Assert.AreEqual(1d, now, 1e-9d);
			Assert.Greater(after, 1d);
		}
	}
}
