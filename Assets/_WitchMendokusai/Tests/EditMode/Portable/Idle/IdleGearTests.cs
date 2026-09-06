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

		[Test]
		public void Merging_UsesTheTierOnly_AndRollsTheOutputSlot()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Resource = 1e9d;

			for (int index = 0; index < tuning.MergeCount; index++)
			{
				state.Bag.Add(new IdleItem(1, (IdleItemSlot)(index % IdleGear.SLOT_COUNT)));
			}

			Assert.IsTrue(IdleGear.TryMerge(state, tuning, 1, IdleItemSlot.Head, out IdleItem made));
			Assert.AreEqual(2, made.Tier);
			Assert.AreEqual(1, state.Bag.Count, "재료 9 이 사라지고 결과 하나만 남아야 한다");
			Assert.GreaterOrEqual((int)made.Slot, 0);
			Assert.Less((int)made.Slot, IdleGear.SLOT_COUNT);
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

			// 장비 배수는 전장에 선 인형 것만 (2026-08-31 인형별). 시작 인형 착석
			IdleHeroes.EnsureStarter(state);

			IdleItem one = new IdleItem(4, IdleItemSlot.Head);
			one.PotentialValue = 0.3d;

			double inTheBag = IdleGear.MultiplierOfItem(one, tuning);

			state.Bag.Add(one);
			Assert.IsTrue(IdleGear.TryEquip(state, IdleHeroes.STARTER_ID, 0));

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
			state.Worn[IdleGear.WornAt(IdleHeroes.STARTER_ID, (int)IdleItemSlot.Head)] = new IdleItem(6, IdleItemSlot.Head);

			// 가방을 여러 칸 채운 <b>뒤쪽</b>에 머리 장비를 둔다 — 옛 실수가 드러나는 자리.
			for (int filler = 0; filler < 7; filler++)
			{
				state.Bag.Add(new IdleItem(1, IdleItemSlot.Feet));
			}

			IdleItem candidate = new IdleItem(2, IdleItemSlot.Head);
			state.Bag.Add(candidate);

			IdleItem[] mine = new IdleItem[IdleGear.SLOT_COUNT];
			IdleGear.CopyWornOf(state, IdleHeroes.STARTER_ID, mine);
			IdleGear.CompareToWorn(mine, state.Bag[state.Bag.Count - 1], tuning,
				out double now, out double after);

			Assert.AreEqual(IdleGear.MultiplierOfItem(mine[(int)IdleItemSlot.Head], tuning), now, 1e-9d,
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

		/// <summary>
		/// ★ 부위 수가 <b>세 군데의 약속</b>이다 — enum · 상수 · 착용 칸.
		///
		/// 화면은 이 수에 맞춘 이름표를 들고 있고(머리·몸·손·발), 합칠 것을 세는 판도
		/// 「등급 x 부위수」로 자리를 잡는다. 여기가 어긋나면 화면이 엉뚱한 이름을 붙이거나
		/// 맨 위 등급이 조용히 빠진다(오늘 뒤엣것을 실제로 겪었다).
		/// </summary>
		[Test]
		public void TheSlotCount_IsOnePromise()
		{
			Assert.AreEqual(IdleGear.SLOT_COUNT, System.Enum.GetValues(typeof(IdleItemSlot)).Length,
				"부위 상수와 enum 이 어긋났다");

			IdleState state = new IdleState();
			Assert.AreEqual(IdleHeroes.Count * IdleGear.SLOT_COUNT, state.Worn.Length,
				"착용 칸이 인형 수 x 부위 수가 아니다 (2026-08-31 인형별 장비)");
		}

		/// <summary>
		/// ★ 저장에 <b>범위 밖 부위 번호</b>가 있어도 판이 선다 — 그 장비만 버린다.
		///
		/// 부위 번호도 저장에서 그대로 온다. 범위를 벗어난 값이 섞이면 차는 순간
		/// Worn[그 번호] 가 배열 밖을 짚어 터지고, 화면도 이름표를 짚다 터진다.
		/// 영웅 번호와 같은 자리의 같은 병이라 같은 곳(문 앞)에서 거른다.
		/// </summary>
		[Test]
		public void AnImpossibleSlotInTheSave_IsDropped()
		{
			IdleSaveData saved = new IdleState().Save();

			IdleItem good = new IdleItem(2, IdleItemSlot.Hands);
			IdleItem bad = new IdleItem(3, IdleItemSlot.Head);
			bad.Slot = (IdleItemSlot)77;

			saved.BagItems = new IdleItem[] { good, bad };

			IdleState state = new IdleState();
			state.Load(saved);

			Assert.AreEqual(1, state.Bag.Count, "있을 수 없는 부위를 그대로 받았다");
			Assert.AreEqual(IdleItemSlot.Hands, state.Bag[0].Slot);

			// 그리고 <b>차 봐도</b> 안 터진다 — 여기서 터지면 위 검사는 아무 뜻이 없다.
			Assert.IsTrue(IdleGear.TryEquip(state, IdleHeroes.STARTER_ID, 0));
		}

		/// <summary>★ 차고 있던 것이 <b>그 자리의 부위</b>가 아니면 빈 자리로 받는다.</summary>
		[Test]
		public void AWornItemInTheWrongSlot_ComesBackEmpty()
		{
			IdleSaveData saved = new IdleState().Save();

			IdleItem[] worn = new IdleItem[IdleGear.SLOT_COUNT];
			// 머리 자리에 <b>손</b> 장비가 적혀 있다.
			worn[(int)IdleItemSlot.Head] = new IdleItem(4, IdleItemSlot.Hands);
			worn[(int)IdleItemSlot.Body] = new IdleItem(2, IdleItemSlot.Body);
			saved.WornItems = worn;

			IdleState state = new IdleState();
			state.Load(saved);

			// 옛 저장(판 공용 4칸)은 시작 인형 것으로 이관
			Assert.IsTrue(IdleGear.WornOf(state, IdleHeroes.STARTER_ID, (int)IdleItemSlot.Head).IsEmpty,
				"엉뚱한 부위가 그 자리에 앉아 있다 — 배수를 엉뚱한 축에 준다");
			Assert.AreEqual(2, IdleGear.WornOf(state, IdleHeroes.STARTER_ID, (int)IdleItemSlot.Body).Tier,
				"멀쩡한 것까지 버렸다");
		}


		/// <summary>
		/// ★ 부위가 <b>화면에 적힌 그 축</b>을 올린다 — 「머리(공격력) 몸(기지) 손(속도) 발(떨구기)」.
		///
		/// ⚠ 그 넷은 화면이 사람에게 <b>글자로</b> 하는 약속인데, 시험이 하나도 없었다
		///   (실측 2026-08-17). 코어에서 한 줄만 바꿔 머리를 속도에 걸어도 아무도 안 죽고,
		///   화면은 그대로 「머리(공격력)」이라 적는다 — 사람은 값을 못 믿게 되고, 그때는
		///   무엇이 거짓말인지 찾을 방법이 없다.
		///
		/// ★ <b>올랐나</b>만 보지 않는다 — 나머지 셋이 <b>안 움직였나</b>까지 본다.
		///   한 부위가 여러 축을 밀면 그것도 적힌 말과 다른 것이다.
		/// </summary>
		[Test]
		public void EachSlot_LiftsTheAxisTheScreenNames()
		{
			IdleItemSlot[] slots =
			{
				IdleItemSlot.Head, IdleItemSlot.Body, IdleItemSlot.Hands, IdleItemSlot.Feet
			};

			// 화면의 SLOT_ROLES 와 <b>같은 순서</b>다 — 머리·몸·손·발.
			string[] names = { "머리(공격력)", "몸(기지)", "손(속도)", "발(떨구기)" };

			for (int which = 0; which < slots.Length; which++)
			{
				IdleTuning tuning = new IdleTuning();
				IdleState state = new IdleState();
				IdleHeroes.EnsureStarter(state);

				double[] before = Axes(state, tuning);

				state.Bag.Add(new IdleItem(3, slots[which]));
				Assert.IsTrue(IdleGear.TryEquip(state, IdleHeroes.STARTER_ID, 0), names[which] + " 를 못 찼다");

				double[] after = Axes(state, tuning);

				for (int axis = 0; axis < after.Length; axis++)
				{
					if (axis == which)
					{
						Assert.Greater(after[axis], before[axis],
							names[which] + " 를 찼는데 그 축이 안 올랐다");
						continue;
					}

					Assert.AreEqual(before[axis], after[axis], 1e-12d,
						names[which] + " 를 찼는데 " + names[axis] + " 까지 움직였다");
				}
			}
		}

		/// <summary>네 축을 화면이 적은 순서대로 — 공격력·기지·속도·떨구기.</summary>
		/// <summary>★ 잠근 것은 합치기 재료에서 빠진다. 세는 것도 (사용자 2026-09-05)</summary>
		[Test]
		public void Locked_IsNeverMerged()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Resource = 1e9d;

			for (int spare = 0; spare < tuning.MergeCount; spare++)
			{
				state.Bag.Add(new IdleItem(2, IdleItemSlot.Head));
			}

			Assert.IsTrue(IdleGear.TrySetLocked(state, 0, true));
			Assert.AreEqual(0, IdleGear.CountMergeable(state, tuning, 2, IdleItemSlot.Head), "하나 잠갔으니 한 벌이 안 된다");
			Assert.IsFalse(IdleGear.TryMerge(state, tuning, 2, IdleItemSlot.Head, out IdleItem _));

			state.Bag.Add(new IdleItem(2, IdleItemSlot.Head));
			Assert.IsTrue(IdleGear.TryMerge(state, tuning, 2, IdleItemSlot.Head, out IdleItem _));
			Assert.IsTrue(state.Bag[0].Locked, "잠근 것이 남아야 한다");
		}

		/// <summary>★ 분해도 나쁜 것부터, 잠근 것은 안 건드리고, 골드는 등급 곡선대로</summary>
		[Test]
		public void Salvage_EatsWorstFirst_SkipsLocked_PaysByTier()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.Resource = 0d;

			IdleItem treasure = new IdleItem(3, IdleItemSlot.Head);
			treasure.PotentialValue = 0.9d;
			treasure.PotentialGradeValue = 3;
			state.Bag.Add(treasure);
			IdleItem kept = new IdleItem(3, IdleItemSlot.Body);
			kept.Locked = true;
			state.Bag.Add(kept);
			state.Bag.Add(new IdleItem(3, IdleItemSlot.Hands));
			state.Bag.Add(new IdleItem(3, IdleItemSlot.Feet));

			Assert.AreEqual(3, IdleGear.CountSalvageable(state, 3), "잠근 것 빼고 셋");

			Assert.IsTrue(IdleGear.TrySalvage(state, tuning, 3, 2, out int salvaged, out double gold));
			Assert.AreEqual(2, salvaged);
			Assert.AreEqual(IdleGear.SalvageGold(3, tuning) * 2d, gold, 1e-9d);
			Assert.AreEqual(gold, state.Resource, 1e-9d);
			Assert.AreEqual(2, state.Bag.Count);
			Assert.IsTrue(state.Bag[0].PotentialValue > 0.8d, "좋은 것은 남는다");
			Assert.IsTrue(state.Bag[1].Locked, "잠근 것은 남는다");

			Assert.IsTrue(IdleGear.TrySalvage(state, tuning, 3, 0, out salvaged, out gold), "전부는 남은 하나");
			Assert.AreEqual(1, salvaged);
			Assert.IsFalse(IdleGear.TrySalvage(state, tuning, 3, 0, out salvaged, out gold), "잠근 것만 남으면 실패");
		}

		/// <summary>★ 정렬은 등급 내림차순, 같으면 부위 순. 같은 것끼리는 자리 유지</summary>
		[Test]
		public void SortBag_TierDescThenSlot_Stable()
		{
			IdleState state = new IdleState();
			IdleItem first = new IdleItem(2, IdleItemSlot.Feet);
			first.PotentialValue = 0.1d;
			IdleItem second = new IdleItem(2, IdleItemSlot.Feet);
			second.PotentialValue = 0.2d;
			state.Bag.Add(first);
			state.Bag.Add(new IdleItem(1, IdleItemSlot.Head));
			state.Bag.Add(new IdleItem(3, IdleItemSlot.Body));
			state.Bag.Add(second);
			state.Bag.Add(new IdleItem(2, IdleItemSlot.Head));

			IdleGear.SortBag(state);

			Assert.AreEqual(3, state.Bag[0].Tier);
			Assert.AreEqual(IdleItemSlot.Head, state.Bag[1].Slot);
			Assert.AreEqual(0.1d, state.Bag[2].PotentialValue, 1e-9d, "같은 등급 같은 부위는 원래 순서");
			Assert.AreEqual(0.2d, state.Bag[3].PotentialValue, 1e-9d);
			Assert.AreEqual(1, state.Bag[4].Tier);
		}

		private static double[] Axes(IdleState state, IdleTuning tuning)
		{
			return new double[]
			{
				IdleGear.DamageMultiplier(state, tuning),
				IdleGear.BaseMultiplier(state, tuning),
				IdleGear.SpeedMultiplier(state, tuning),
				IdleGear.DropMultiplier(state, tuning)
			};
		}
	}
}
