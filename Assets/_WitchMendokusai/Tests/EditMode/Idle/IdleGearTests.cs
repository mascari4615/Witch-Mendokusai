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
	}
}
