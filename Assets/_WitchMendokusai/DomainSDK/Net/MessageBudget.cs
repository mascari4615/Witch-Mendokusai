namespace WitchMendokusai.Net
{
	/// <summary>
	/// 한 창이 <b>얼마나 자주 말할 수 있나</b> (TASK-WM-218).
	///
	/// ★ 왜 필요한가: 서버는 창이 보낸 말을 그대로 처리한다. 한 창이 초당 수천 번 보내면
	///   <b>모두의 세계가 느려진다</b> — 악의가 없어도(버그 난 창 하나로도) 그렇게 된다.
	///   막는 쪽이 서버여야 하는 이유는 명확하다: 창은 자기를 못 막는다.
	///
	/// 물통 모형: 초당 <see cref="REFILL_PER_SECOND"/> 만큼 차고, 한 번 말할 때 하나 쓴다.
	/// 가득 찼을 때를 넘겨 담지 않는다(가만히 있다가 한꺼번에 쏟는 것도 막는다).
	/// </summary>
	public sealed class MessageBudget
	{
		/// <summary>
		/// 걸음은 <b>세계의 박자</b>로 나간다 — 창은 판마다 한 걸음을 보낸다 (초당 20).
		///
		/// ⚠ 이 숫자를 손으로 적어 두면 안 된다(2026-08-14): 예전 예산은 「움직임 10/초 + 여유」로
		///   잡혀 있었는데 <b>창은 실제로 20/초</b>를 보내고 있었다. 실측 결과 성한 창 하나가
		///   초당 23.9 마디(걸음 20 · 숨소리 4)를 써서 예산 30 의 <b>80%</b>를 먹고 있었다 —
		///   줍기·때리기·말하기가 겹치는 순간 성한 사람의 말이 <b>조용히</b> 버려진다.
		/// </summary>
		public const float STEPS_PER_SECOND = 20f;

		/// <summary>숨소리 — 창은 0.25초마다 도장을 돌려준다 (TASK-WM-303·343).</summary>
		public const float BEATS_PER_SECOND = 4f;

		/// <summary>
		/// 사람이 <b>손으로 하는 일</b>에 남겨 두는 몫 — 줍기·때리기·짓기·말하기·다시 물어보기.
		/// 걸으면서 두드리는 사람을 기준으로 잡는다(걸음을 멈추게 하는 예산은 예산이 아니라 벌이다).
		/// </summary>
		public const float ROOM_FOR_DOING = 16f;

		/// <summary>1초에 채워지는 양 — <b>제품 상수에서 유도한다</b>(손으로 적은 숫자 X).</summary>
		public const float REFILL_PER_SECOND = STEPS_PER_SECOND + BEATS_PER_SECOND + ROOM_FOR_DOING;

		/// <summary>한 번에 몰아 쓸 수 있는 최대 — 잠깐의 몰림은 봐준다(1초치의 두 배).</summary>
		public const float BURST = REFILL_PER_SECOND * 2f;

		private float tokens = BURST;

		/// <summary>남은 말 수(디버그·시험용).</summary>
		public float Remaining => tokens;

		/// <summary>시간이 흘렀다 — 그만큼 물통을 채운다.</summary>
		public void Refill(float deltaSeconds)
		{
			if (deltaSeconds <= 0f)
				return;

			tokens += REFILL_PER_SECOND * deltaSeconds;
			if (tokens > BURST)
				tokens = BURST;
		}

		/// <summary>한 마디 해도 되나. 안 되면 그 말은 <b>버린다</b>(끊지는 않는다 — 잠깐 몰릴 수도 있다).</summary>
		public bool TrySpend()
		{
			if (tokens < 1f)
				return false;

			tokens -= 1f;
			return true;
		}
	}
}
