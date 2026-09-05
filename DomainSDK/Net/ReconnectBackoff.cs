namespace WitchMendokusai.Net
{
	/// <summary>
	/// 끊겼을 때 <b>언제 다시 붙어 볼까</b> (TASK-WM-217).
	///
	/// ★ 왜 판정 층인가: 「곧바로 계속 다시 붙기」는 서버가 잠깐 죽었을 때 <b>수백 번/초</b>로
	///   두드리는 짓이 된다(서버가 못 일어난다). 반대로 너무 뜸하면 사람이 멈춘 화면을 오래 본다.
	///   그 사이를 정하는 규칙이라 눈으로는 못 본다 — 시험할 수 있는 자리에 둔다.
	///
	/// 규칙: 0.5초에서 시작해 실패마다 두 배, 최대 10초. 한 번 붙으면 처음으로 돌아간다.
	/// </summary>
	public sealed class ReconnectBackoff
	{
		public const float FIRST_DELAY_SECONDS = 0.5f;
		public const float MAX_DELAY_SECONDS = 10f;

		private float next = FIRST_DELAY_SECONDS;

		/// <summary>지금까지 몇 번 헛걸음했나 — 화면에 「다시 붙는 중…」을 보여줄 때 쓴다.</summary>
		public int Attempts { get; private set; }

		/// <summary>이번엔 얼마나 기다렸다 붙어 볼까(초). 부를 때마다 다음 값이 늘어난다.</summary>
		public float NextDelay()
		{
			float delay = next;
			Attempts++;

			next = next * 2f;
			if (next > MAX_DELAY_SECONDS)
				next = MAX_DELAY_SECONDS;

			return delay;
		}

		/// <summary>붙었다 — 다음에 끊기면 다시 빠르게 시도한다.</summary>
		public void Reset()
		{
			next = FIRST_DELAY_SECONDS;
			Attempts = 0;
		}
	}
}
