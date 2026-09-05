using System;

namespace WitchMendokusai
{
	// Fixed-accumulator simulation tick scheduler (SimCity Phase 1 substrate).
	// TimeManager.UpdateTime(0.05s WaitForSeconds 루프) = *클럭* — Time.timeScale 추종 + render 종속.
	// SimulationTickScheduler = *스케줄러* — unscaledDeltaTime 입력만 받아 결정적 tick 산출:
	//
	//  ① Time.timeScale ⊥ — 일시정지/배속은 SpeedMultiplier 한 곳에서, dt 는 항상 unscaled.
	//  ② render 프레임율 ⊥ — 느린 프레임 = 다중 catch-up tick, 빠른 프레임 = 0 tick + 누적.
	//  ③ SpeedMultiplier (SimCity 1/2/3 배속) = dt 곱셈, 결정성 보존.
	//  ④ Spiral-of-death 캡 (MaxStepsPerAdvance) — 극단 dt 시 잉여 accumulator drop → freeze 후
	//     catch-up 폭주 차단(사용자 체감 동결 회피).
	//
	// 순수 POCO — UnityEngine 무관. 같은 (dt seq, SpeedMultiplier seq) → 결정적 동일 (반환 seq,
	// Accumulator 종착) — 멀티 클라이언트·재현 디버그 토대 (Phase 4 멀티 seam).
	//
	// 사용:
	//   SimulationTickScheduler scheduler = new(secondsPerTick: 1f / 60f);
	//   // Update() 안:
	//   int ticks = scheduler.Advance(Time.unscaledDeltaTime);
	//   for (int i = 0; i < ticks; i++) { SimStep(); }
	public sealed class SimulationTickScheduler
	{
		public const int DEFAULT_MAX_STEPS_PER_ADVANCE = 8;

		// 한 tick = 시뮬 1 step 의 게임 내 길이 (초). 양수 강제.
		public float SecondsPerTick { get; }

		// 한 Advance() 호출에서 최대로 실행할 tick 수 (spiral-of-death 캡).
		// 초과분 dt 는 drop — accumulator 에 누적 X (이후 catch-up 가속 X — 사용자 체감 freeze 회피).
		public int MaxStepsPerAdvance { get; }

		// 게임 배속 (1 = 실시간, 2 = 2x, 0 = pause). 음수 입력 시 pause 동등 처리(rewind X).
		public float SpeedMultiplier { get; set; } = 1f;

		// 누적 시뮬 시간 (sub-tick) — 다음 tick fire 까지 남은 잔여분.
		public float Accumulator { get; private set; }

		// 누적 실행된 tick 수 (모노토닉). 결정적 시드·동기화에 사용 가능.
		public long TickCount { get; private set; }

		public SimulationTickScheduler(float secondsPerTick, int maxStepsPerAdvance = DEFAULT_MAX_STEPS_PER_ADVANCE)
		{
			if (secondsPerTick <= 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(secondsPerTick), secondsPerTick, "tick 길이 > 0 필수");
			}

			if (maxStepsPerAdvance < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(maxStepsPerAdvance), maxStepsPerAdvance, "최소 1");
			}

			SecondsPerTick = secondsPerTick;
			MaxStepsPerAdvance = maxStepsPerAdvance;
		}

		// 실시간 dt 를 입력 받아 산출할 sim tick 수 반환 (호출자가 tick 만큼 SimStep 루프).
		// 결정성: 같은 (dt seq, SpeedMultiplier seq) → 같은 (반환 seq, Accumulator 종착).
		public int Advance(float unscaledDeltaTime)
		{
			if (unscaledDeltaTime <= 0f)
			{
				return 0; // 음수/0 dt = 무시 (rewind X, paused frame noop).
			}

			float speed = SpeedMultiplier;
			if (speed <= 0f)
			{
				return 0; // 일시정지 (배속 ≤ 0). dt 무시 — accumulator 동결.
			}

			Accumulator += unscaledDeltaTime * speed;

			int steps = 0;
			while (Accumulator >= SecondsPerTick && steps < MaxStepsPerAdvance)
			{
				Accumulator -= SecondsPerTick;
				steps++;
			}

			// Spiral-of-death — cap 초과 잔여분 drop. 사용자 freeze 시 다음 Advance 가 폭주 catch-up 안 함.
			if (Accumulator >= SecondsPerTick)
			{
				Accumulator = 0f;
			}

			TickCount += steps;
			return steps;
		}

		// 외부 시점 점프 (저장 로드·디버그) — accumulator/tickCount 초기화.
		public void Reset(long tickCount = 0L, float accumulator = 0f)
		{
			if (tickCount < 0L)
			{
				throw new ArgumentOutOfRangeException(nameof(tickCount), tickCount, "tickCount 음수 X");
			}

			if (accumulator < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(accumulator), accumulator, "accumulator 음수 X");
			}

			TickCount = tickCount;
			Accumulator = accumulator;
		}
	}
}
