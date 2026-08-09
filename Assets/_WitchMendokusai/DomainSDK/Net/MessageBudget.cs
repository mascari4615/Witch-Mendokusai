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
		/// <summary>1초에 채워지는 양 — 평소 말수(움직임 10/초 + 여유).</summary>
		public const float REFILL_PER_SECOND = 30f;

		/// <summary>한 번에 몰아 쓸 수 있는 최대 — 잠깐의 몰림은 봐준다.</summary>
		public const float BURST = 60f;

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
