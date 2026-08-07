namespace WitchMendokusai
{
	/// <summary>
	/// 대사 한 줄이 화면에 머무를 시간 (TASK-WM-052).
	///
	/// ★ 왜 필요한가: 여태 모든 대사가 **똑같이 3초**였다. 「응.」도 3초, 두 줄짜리 설명도 3초 —
	///   짧은 건 지루하고 **긴 건 다 읽기 전에 사라진다.** 글자 수에 맞춰야 읽을 수 있다.
	///
	/// 순수 계산 — 그래서 「몇 초 머무르나」가 화면 없이 검증된다.
	/// 값(읽는 속도·최소·최대)은 <see cref="DialogueRunner"/> 가 인스펙터로 노출한다(수치 하드코딩 금지).
	/// </summary>
	public static class DialogueReadingTime
	{
		/// <summary>
		/// 글자 수 ÷ 읽는 속도. 너무 짧으면 <paramref name="minimumSeconds"/>, 너무 길면 <paramref name="maximumSeconds"/>.
		///
		/// 속도가 0 이하면 0 을 준다 = 「자동으로 안 넘김」(눌러서 넘기는 연출) — 부르는 쪽이 그 뜻으로 쓴다.
		/// 최대값이 최소값보다 작게 잡혀 있어도 뒤집지 않고 **최소값을 지킨다**(짧게 스치는 것보다 낫다).
		/// </summary>
		public static float For(string text, float charactersPerSecond, float minimumSeconds, float maximumSeconds)
		{
			if (charactersPerSecond <= 0f)
			{
				return 0f;
			}

			int length = text == null ? 0 : text.Trim().Length;
			float seconds = length / charactersPerSecond;

			if (seconds < minimumSeconds)
			{
				seconds = minimumSeconds;
			}
			if (maximumSeconds > 0f && seconds > maximumSeconds)
			{
				seconds = maximumSeconds < minimumSeconds ? minimumSeconds : maximumSeconds;
			}
			return seconds;
		}
	}
}
