namespace WitchMendokusai.Server
{
	/// <summary>
	/// <b>밀리는 창에게 이번 판을 어떻게 보낼까</b> — 순수 셈 (TASK-WM-340).
	///
	/// ★ 왜 떼어냈나: 이 판단이 알림 루프 한복판에 인라인으로 있었다. 그래서 이틀 동안 <b>두 번</b>
	///   반응적으로 고쳤고, 그때마다 시험은 「관문을 몇 분 돌려 보기」뿐이었다 —
	///   ① 좁히기만 넣었더니 CI 에서 나쁜 회선이 <b>더</b> 날랐다(16.0 vs 8.4KB/s: 좁힌 판은
	///      「작은 <b>전체</b> 그림」이라 델타보다 클 수 있다).
	///   ② 판을 5개마다 하나로 줄였더니 초당 판 수가 7.3장까지 떨어져 「끊긴다」가 됐다.
	///   정책이 순수 함수면 이런 것을 <b>밀리초</b>에 가른다 — 그래서 여기로 옮긴다.
	///
	/// 규칙 셋뿐이다:
	///   ① 안 밀리면 그대로 (모두 · 매 판)
	///   ② 밀리면 <b>좁히고</b>(작게) <b>박자도 절반</b>(20Hz → 10Hz) — 좁히기만 하면 양이 되레 는다
	///   ③ 그래도 <b>초당 여덟 판</b> 아래로는 안 내려간다 — 덜어 내는 것과 끊기는 것은 다르다
	/// </summary>
	public static class SendPlan
	{
		/// <summary>
		/// 「밀린다」는 <b>그 회선의 바닥과의 차</b>다 (TASK-WM-341).
		///
		/// ★ 절대 밀리초로 자르면 안 된다 (2026-08-14 CI 실측): 왕복 400ms 회선에서는 <b>모두가</b>
		///   밀린 것으로 잡혀 좁혀졌고, 그러자 보던 사람이 판에서 빠져 화면이 <b>100% 멎었다</b>.
		///   회선이 먼 것과 밀리는 것은 다르다 — 바닥보다 이만큼 더 걸릴 때만 밀린 것이다.
		/// </summary>
		public const long LAG_OVER_BEST_MILLISECONDS = 250;

		/// <summary>바닥이 이 값보다 좋으면(작으면) 이 값을 바닥으로 친다 — 너무 좋은 바닥은 흔들림에 약하다.</summary>
		public const long BEST_FLOOR_MILLISECONDS = 60;

		/// <summary>밀리는 창은 이만큼 뒤처진 것으로 쳐서 좁힌다.</summary>
		public const int BEHIND_STEPS_WHEN_LAGGING = 3;

		/// <summary>밀릴 때 몇 판마다 한 번 보내나 (2 = 절반 박자).</summary>
		public const long EVERY_NTH_WHEN_LAGGING = 2;

		/// <summary>이번 판을 그 창에게 어떻게 할까.</summary>
		public readonly struct Choice
		{
			public Choice(bool send, int behindSteps)
			{
				Send = send;
				BehindSteps = behindSteps;
			}

			/// <summary>이번 판을 보내나 — false 면 건너뛴다(다음 판은 전체를 준다).</summary>
			public bool Send { get; }

			/// <summary>몇 걸음 뒤처진 것으로 칠까 — 좁히는 정도가 여기서 나온다.</summary>
			public int BehindSteps { get; }
		}

		/// <param name="roundTripMs">그 창의 왕복 (0 = 아직 모른다)</param>
		/// <param name="missedInARow">연달아 건너뛴 판 수 (이미 세고 있던 값)</param>
		/// <param name="sequence">이번 판 번호</param>
		public static Choice For(long roundTripMs, long bestRoundTripMs, int missedInARow, long sequence)
		{
			long floor = bestRoundTripMs < BEST_FLOOR_MILLISECONDS ? BEST_FLOOR_MILLISECONDS : bestRoundTripMs;
			bool lagging = bestRoundTripMs > 0 && roundTripMs > floor + LAG_OVER_BEST_MILLISECONDS;
			if (lagging == false)
				return new Choice(true, missedInARow);

			int behind = missedInARow < BEHIND_STEPS_WHEN_LAGGING ? BEHIND_STEPS_WHEN_LAGGING : missedInARow;

			// 절반 박자 — 홀수 판은 건너뛴다.
			bool send = sequence % EVERY_NTH_WHEN_LAGGING == 0;
			return new Choice(send, behind);
		}
	}
}
