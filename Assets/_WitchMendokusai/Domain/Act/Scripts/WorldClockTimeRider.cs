using System;
using WitchMendokusai.DomainSDK.Act;

namespace WitchMendokusai
{
	// 행동이 먹은 시간을 벽시계에 민다 (TASK-WM-410) — 「밭을 갈았더니 해가 기울었다」.
	//
	// ★ 왜 얇은 어댑터인가: 시간 규칙은 WorldClock 것이고 대가 판정은 원장 것이다.
	//   여기는 「이만큼 흘렀다」를 시계의 말로 옮기기만 한다.
	// ★ 시계를 지연으로 받는 이유: 시계는 스테이지 스코프라 이 자리보다 늦게 설 수 있다
	//   (init-order 규약 — 준비 안 된 값을 스냅샷으로 캐싱하지 않는다).
	public sealed class WorldClockTimeRider : IActTimeRider
	{
		private readonly Func<WorldClock> clockSource;

		public WorldClockTimeRider(Func<WorldClock> clockSource)
		{
			this.clockSource = clockSource;
		}

		public void RideMinutes(int minutes, bool dayChanged)
		{
			if (minutes <= 0 || clockSource == null)
			{
				return;
			}

			WorldClock clock = clockSource();
			if (clock == null)
			{
				return;
			}

			clock.AdvanceMinutes(minutes);
		}
	}
}
