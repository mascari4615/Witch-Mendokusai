namespace WitchMendokusai.Server
{
	/// <summary>
	/// <b>다음 차례까지 얼마나 잘까</b> — 세계가 「초당 20번」을 지키게 하는 셈 (TASK-WM-220).
	///
	/// ★ 왜 필요한가: 잠자기는 늘 늦게 깬다(윈도우의 알갱이가 15.6ms 라 50ms 를 재우면 62ms 를 잔다).
	///   매번 50ms 를 재우면 적어 놓은 20번이 실제로는 16번이 된다 — 20% 손해다.
	///
	/// ★ 왜 시험을 시간으로 안 재나: 「초당 몇 번 오나」를 재면 <b>기계 상태</b>를 재게 된다
	///   (다른 프로그램이 알갱이를 1ms 로 바꿔 놓으면 옛 코드도 초록이다 — 실제로 그랬다).
	///   그래서 재는 것은 시간이 아니라 <b>셈</b>이다.
	/// </summary>
	public static class TickSchedule
	{
		/// <summary>
		/// 지금이 <paramref name="nowMilliseconds"/> 이고 다음 차례가 <paramref name="dueMilliseconds"/> 일 때,
		/// 얼마나 자고 그 다음 차례는 언제인가.
		///
		/// 규칙 셋: ① 늦었으면 안 잔다(0) ② 한 차례 넘게 밀렸으면 차례를 지금 기준으로 다시 잡는다
		///          (안 그러면 밀린 만큼 쉼 없이 돌며 따라잡느라 세계가 헐떡인다)
		///          ③ 제때면 남은 만큼만 잔다 — 늦음이 쌓이지 않는다.
		/// </summary>
		public static (double WaitMilliseconds, double NextDueMilliseconds) Next(
			double nowMilliseconds, double dueMilliseconds, double periodMilliseconds)
		{
			double wait = dueMilliseconds - nowMilliseconds;

			if (wait < -periodMilliseconds)
				return (0.0, nowMilliseconds + periodMilliseconds);

			if (wait < 0.0)
				return (0.0, dueMilliseconds + periodMilliseconds);

			return (wait, dueMilliseconds + periodMilliseconds);
		}
	}
}
