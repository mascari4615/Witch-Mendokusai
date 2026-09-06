using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	// IdleBaseTests.cs 의 Snapshot 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 사진(스냅샷) 계약.
	public sealed partial class IdleBaseTests
	{
		/// <summary>
		/// ★ 화면이 <b>때리는 장단</b>을 코어에서 받는다 — 지어내면 올려도 빨라진 게 안 보인다.
		/// </summary>
		[Test]
		public void Snapshot_CarriesAttackSpeed()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleSession session = new IdleSession(tuning, state);

			double before = session.Capture().AttacksPerSecond;
			Assert.Greater(before, 0d, "안 때리는 걸로 보인다");

			state.Resource = 1e12d;
			session.Send(new IdleRaiseUpgradeIntent(IdleHeroes.STARTER_ID, IdleUpgradeKind.AttackSpeed, 1));

			Assert.Greater(session.Capture().AttacksPerSecond, before, "속도를 올렸는데 장단이 그대로다");
		}

		/// <summary>
		/// ★ 사진 찍기가 <b>쓰레기를 안 만든다</b> — 방치형은 밤새 켜 두는 게 기본값이다.
		///
		/// 실측(2026-08-17): 고치기 전엔 <b>한 번에 2472 바이트</b>였다(가방 40칸·영웅 16).
		/// 60프레임 x 8시간이면 <b>4 GB</b>어치다. 지금은 판을 돌려 써서 0 이다.
		///
		/// ⚠ 그 대가로 <b>사진은 다음 사진을 찍을 때까지만 살아 있다</b>. 들고 있다가 나중에
		///   보면 그때는 다른 판이다 — 들고 있어야 하면 복사해서 들어라.
		///   그 성질을 아래 시험이 같이 못 박는다.
		/// </summary>
		[Test]
		public void TakingThePicture_MakesNoGarbage()
		{
			IdleSession session = Loaded(out IdleTuning _);

			session.Capture();

			long before = System.GC.GetAllocatedBytesForCurrentThread();

			for (int again = 0; again < 100; again++)
			{
				session.Capture();
			}

			long each = (System.GC.GetAllocatedBytesForCurrentThread() - before) / 100L;

			TestContext.WriteLine("[할당] 사진 한 번 = " + each + " 바이트");

			Assert.LessOrEqual(each, 64L,
				"사진 한 번에 " + each + " 바이트를 만든다 — 밤새 켜 두면 그게 그대로 쌓인다");
		}

		/// <summary>
		/// ★ 그 대신 <b>사진은 다음 사진까지만</b> 유효하다 — 성질을 못 박아 둔다.
		///
		/// 이걸 안 적어 두면 다음 사람이 사진을 들고 있다가 <b>조용히 다른 판</b>을 보게 된다.
		/// </summary>
		[Test]
		public void AnOldPicture_ShowsTheNewBoard()
		{
			IdleSession session = Loaded(out IdleTuning tuning);

			IdleSnapshot old = session.Capture();
			int wasBag = old.Bag.Length;

			session.State.Bag[0] = new IdleItem(9, IdleItemSlot.Feet);
			session.Capture();

			Assert.AreEqual(wasBag, old.Bag.Length, "길이는 그대로여야 한다(판을 돌려 쓴다)");
			Assert.AreEqual(9, old.Bag[0].Tier,
				"들고 있던 사진이 옛 판을 보여준다 — 판을 돌려 쓰는 성질이 사라졌다면 이 시험을 지워라");
		}

		/// <summary>가방·영웅이 들어찬 판 — 사진이 제일 커지는 자리.</summary>
		private static IdleSession Loaded(out IdleTuning tuning)
		{
			tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.EnsureTierRoom(12);

			for (int one = 0; one < tuning.BagCapacity; one++)
			{
				state.Bag.Add(new IdleItem(3, IdleItemSlot.Head));
			}

			for (int id = 0; id < 16; id++)
			{
				state.Heroes.Add(new IdleHeroOwned(id));
			}

			return new IdleSession(tuning, state);
		}

		/// <summary>
		/// ★ <b>사진을 찍는다고 판이 바뀌면 안 된다</b> — 조회가 판을 건드리던 자리가 있었다 (회귀).
		///
		/// 「사면 몇 배」를 재려고 생산자를 하나 얹었다 되돌렸다. 그 사이에 무슨 일이 나면
		/// 공짜 생산자가 남는다. 지금은 안 건드린다 — 그걸 못 박는다.
		/// </summary>
		[Test]
		public void TakingThePicture_DoesNotTouchTheBoard()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			state.Owned[0] = 3L;
			state.Owned[1] = 1L;
			state.Resource = 500d;

			state.Stage = 6;
			state.BestStage = 25;
			state.Heroes.Add(new IdleHeroOwned(1));
			state.Bag.Add(new IdleItem(2, IdleItemSlot.Feet));

			// 세션 생성은 사진이 아님 (시작 인형 착석). 사진은 Capture
			IdleSession session = new IdleSession(tuning, state);
			IdleSaveData before = state.Save();
			session.Capture();
			IdleSaveData after = state.Save();

			// ⚠ 네 칸만 보면 <b>반만 보는 감시</b>다 — 사진이 어느 칸을 건드려도 잡히게 전부 본다.
			//   (오늘 이 자리에서만 둘을 잡았다: 「사면 몇 배」와 「어디서 파는 게 빠른가」.
			//    둘 다 판을 잠깐 바꿔 놓고 되돌리는 방식이었다.)
			System.Reflection.FieldInfo[] fields = typeof(IdleSaveData)
				.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

			Assert.Greater(fields.Length, 10, "저장 꼴이 비었다 — 시험이 아무것도 안 보고 있다");

			foreach (System.Reflection.FieldInfo field in fields)
			{
				object one = field.GetValue(before);
				object other = field.GetValue(after);

				if (one is System.Array first)
				{
					System.Array second = (System.Array)other;
					Assert.AreEqual(first.Length, second.Length, field.Name + " 의 길이가 달라졌다");

					for (int at = 0; at < first.Length; at++)
					{
						Assert.AreEqual(first.GetValue(at), second.GetValue(at),
							"사진을 찍었더니 " + field.Name + " 의 " + at + "번째가 달라졌다");
					}

					continue;
				}

				Assert.AreEqual(one, other, "사진을 찍었더니 " + field.Name + " 가 달라졌다");
			}
		}
	}
}

