using System;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 시작 인형 셋과 편성 바꿀 때의 전투 좌표 (사용자 2026-09-21: 최소 셋은 있어야 시험이 된다).
	///
	/// ★ 지키는 것: 새 판은 카탈로그의 시작 인형을 다 가지고 빈 칸부터 편성.
	///   이미 가진 판에도 새 시작 인형은 온다. 편성이 바뀐 칸은 다른 인형 뒤에서 시작한다
	/// </summary>
	public sealed class IdleStarterTests
	{
		private static readonly IdleDollKind[] KINDS =
		{
			new IdleDollKind(0, "세모", IdleDollAxis.Damage, IdleDollGrade.Common, 3),
			new IdleDollKind(1, "네모", IdleDollAxis.Base, IdleDollGrade.Common, 4),
			new IdleDollKind(2, "다섯모", IdleDollAxis.Drop, IdleDollGrade.Common, 5),
			new IdleDollKind(3, "여섯모", IdleDollAxis.Speed, IdleDollGrade.Common, 6),
		};

		/// <summary>시작 셋을 든 카탈로그로 바꿔 돌리고, 끝나면 시험 공용 카탈로그로 되돌린다</summary>
		private static void WithStarters(int[] starters, Action<IdleTuning> body)
		{
			IdleDolls.Configure(new IdleDollCatalog(KINDS, starters));
			try
			{
				body(new IdleTuning());
			}
			finally
			{
				new IdleDollCatalogFixture().ConfigureCatalog();
			}
		}

		[Test]
		public void FreshState_OwnsAllStarters_AndSeatsThemInOrder()
		{
			WithStarters(new[] { 3, 1, 2 }, tuning =>
			{
				IdleState state = new IdleState();
				IdleDolls.EnsureStarter(state);

				Assert.AreEqual(3, state.Dolls.Count, "시작 인형 셋을 다 안 줬다");
				Assert.AreEqual(3, state.Party[0], "첫 칸이 대표 시작 인형이 아니다");
				Assert.AreEqual(1, state.Party[1]);
				Assert.AreEqual(2, state.Party[2]);
				Assert.AreEqual(3, IdleDolls.StarterId);
			});
		}

		/// <summary>둘만 가진 옛 판. 새 시작 인형은 오고, 빈 칸에만 들어간다. 사람이 짠 편성은 안 건드린다</summary>
		[Test]
		public void OldSave_GetsMissingStarters_IntoEmptySlotsOnly()
		{
			WithStarters(new[] { 3, 1, 2 }, tuning =>
			{
				IdleState state = new IdleState();
				state.Dolls.Add(new IdleDollOwned(0));
				state.Dolls.Add(new IdleDollOwned(2));
				state.Party[0] = -1;
				state.Party[1] = 2;
				state.Party[2] = 0;

				Assert.IsTrue(IdleDolls.EnsureStarter(state));

				Assert.AreEqual(4, state.Dolls.Count, "없던 시작 인형 둘 (3, 1) 이 안 왔다");
				Assert.AreEqual(3, state.Party[0], "빈 칸에 대표 시작 인형이 안 들어갔다");
				Assert.AreEqual(2, state.Party[1], "사람이 짠 칸을 건드렸다");
				Assert.AreEqual(0, state.Party[2], "사람이 짠 칸을 건드렸다");
				Assert.IsFalse(IdleDolls.EnsureStarter(state), "두 번째는 바꿀 게 없어야 한다");
			});
		}

		[Test]
		public void CatalogWithoutStarters_FallsBackToZero()
		{
			WithStarters(null, tuning =>
			{
				Assert.AreEqual(0, IdleDolls.StarterId);
				IdleState state = new IdleState();
				IdleDolls.EnsureStarter(state);
				Assert.AreEqual(1, state.Dolls.Count);
				Assert.AreEqual(0, state.Party[0]);
			});
		}

		[Test]
		public void Catalog_RejectsUnknownStarter()
		{
			Assert.Throws<ArgumentException>(() => new IdleDollCatalog(KINDS, new[] { 9 }));
		}

		/// <summary>
		/// 한참 걸어간 뒤 빈 칸에 인형을 넣으면 옛 좌표 (0 근처) 가 아니라 다른 인형의 한 걸음 뒤에서 시작한다.
		/// 안 그러면 카메라가 둘의 가운데를 봐 아무도 안 보인다 (피드백 11 실측 2026-09-21)
		/// </summary>
		[Test]
		public void SeatingMidBattle_PlacesTheDollBehindTheOthers()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleDolls.EnsureStarter(state);
			state.Dolls.Add(new IdleDollOwned(1));
			state.Party[1] = -1;
			state.Party[2] = -1;
			state.EnsureSeatRoom(tuning);
			IdleBattleSim.Reset(state, tuning);

			// 적을 멀리 두어 앞 인형이 한참 걷게
			IdleBattle battle = state.Battle;
			battle.Foes[0].X = 200d;
			battle.Foes[0].Speed = 0d;
			IdleBattleSim.Advance(state, tuning, 20d);
			double frontX = battle.X[0];
			Assert.Greater(frontX, 20d, "앞 인형이 안 걸었다 — 시험이 아무것도 안 쟀다");

			state.Party[1] = 1;
			IdleBattleSim.Advance(state, tuning, tuning.BattleTickSeconds);

			double gap = battle.X[0] - battle.X[1];
			Assert.LessOrEqual(gap, tuning.SeatBackStep + tuning.DollMoveSpeed * tuning.BattleTickSeconds + 1e-6,
				"새로 편성한 인형이 " + gap + " 뒤에 있다. 한 걸음 뒤여야 한다");
			Assert.GreaterOrEqual(gap, 0d, "새 인형이 앞 인형보다 앞에 섰다");
		}
	}
}
