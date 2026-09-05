using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 세상이 이어져 보이나 (사용자 2026-09-05 지적: 웨이브마다 뚝뚝 움직임)
	///
	/// ★ 판은 좌표를 작게 유지하려고 웨이브마다 0 기준으로 다시 깎음. 그 몫이 OriginX 에 쌓여야
	///   화면이 카메라를 같은 만큼 워프시켜 이음새를 지움. 여기서 재는 것은 <b>합이 보존되나</b>
	/// </summary>
	public sealed class IdleWorldSeamTests
	{
		/// <summary>자리의 절대 위치 (판 좌표 + 여태 민 거리)</summary>
		private static double AbsoluteOf(IdleState state, int seat)
		{
			return state.Battle.X[seat] + state.Battle.OriginX;
		}

		[Test]
		public void RebasingAWave_KeepsTheAbsolutePlace()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleHeroes.EnsureStarter(state);
			IdleBattleSim.Reset(state, tuning);

			double before = AbsoluteOf(state, 0);
			double firstFoe = state.Battle.Foes[0].X + state.Battle.OriginX;

			// 앞으로 걸어간 뒤 다음 웨이브
			state.Battle.X[0] += 40d;
			state.Battle.Foes.Clear();
			IdleBattleSim.Advance(state, tuning, 0.1d);

			// 0.1초 사이 걸어간 몫은 봐준다. 재는 것은 다시 깎기가 절대 좌표를 안 흔드나
			Assert.AreEqual(before + 40d, AbsoluteOf(state, 0), 0.5d, "다시 깎을 때 자리가 절대 좌표에서 튀었다");
			Assert.Greater(state.Battle.OriginX, 0d, "민 거리가 안 쌓였다");
			Assert.Greater(state.Battle.Foes[0].X + state.Battle.OriginX, firstFoe, "새 웨이브가 앞에 안 섰다");
		}

		[Test]
		public void TheRebase_KeepsCoordinatesSmall()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleHeroes.EnsureStarter(state);
			IdleBattleSim.Reset(state, tuning);

			for (int step = 0; step < 400; step++)
			{
				IdleBattleSim.Advance(state, tuning, 1d);
			}

			double biggest = 0d;
			for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
			{
				double size = state.Battle.X[seat] < 0d ? -state.Battle.X[seat] : state.Battle.X[seat];
				if (size > biggest) { biggest = size; }
			}

			TestContext.WriteLine("[이음새] 400초 뒤 판 좌표 최대 " + biggest.ToString("0.0")
				+ ", 여태 민 거리 " + state.Battle.OriginX.ToString("0.0"));

			Assert.Less(biggest, 1000d, "판 좌표가 커졌다. 오래 켜 두면 수가 흔들린다");
		}

		/// <summary>★ 사진이 민 거리를 실어 나른다. 화면은 이것만 보고 카메라를 워프시킨다</summary>
		[Test]
		public void ThePicture_CarriesTheShift()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState state = new IdleState();
			IdleHeroes.EnsureStarter(state);
			IdleSession session = new IdleSession(tuning, state);

			IdleSnapshot first = session.Capture();
			for (int step = 0; step < 60; step++)
			{
				session.AdvanceLive(1d);
			}

			IdleSnapshot later = session.Capture();

			Assert.AreEqual(state.Battle.OriginX, later.OriginX, 1e-9d);
			Assert.GreaterOrEqual(later.OriginX, first.OriginX, "민 거리가 줄었다");
		}
	}
}
