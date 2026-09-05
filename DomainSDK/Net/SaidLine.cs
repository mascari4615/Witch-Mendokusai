namespace WitchMendokusai.Net
{
	/// <summary>
	/// 사람이 <b>한 말</b>을 세계가 받아들이는 규칙 (TASK-WM-250).
	///
	/// ★ 왜 판정이 필요한가: 말은 사람이 직접 짓는 유일한 것이다. 그래서 세계가 안 보면
	///   한 줄로 남의 화면을 부술 수 있다 — 끝없이 긴 줄, 줄바꿈으로 밀어 올리기,
	///   보이지 않는 글자로 이름 흉내 내기. 「창이 알아서 자르겠지」는 <b>창을 고친 사람</b>에게 안 통한다.
	///
	/// ★ 자르는 것이 아니라 <b>거절</b>도 한다: 빈 줄은 말이 아니다(누르기만 해도 남에게 알림이 가면
	///   그건 곧 소음 장치가 된다).
	/// </summary>
	public static class SaidLine
	{
		/// <summary>한 번에 할 수 있는 말의 길이 — 이보다 길면 자른다.</summary>
		public const int LONGEST = 140;

		/// <summary>
		/// 이 말을 세계가 받아들이나. 받아들이면 <b>다듬은 말</b>을, 아니면 <c>null</c>.
		/// 다듬기 = 앞뒤 공백 버리기 · 줄바꿈과 보이지 않는 글자를 한 칸으로 · 길면 자르기.
		/// </summary>
		public static string Clean(string said)
		{
			if (string.IsNullOrEmpty(said))
				return null;

			System.Text.StringBuilder builder = new System.Text.StringBuilder(said.Length);
			bool lastWasSpace = true;   // 앞의 공백은 통째로 버린다

			foreach (char letter in said)
			{
				// 줄바꿈·탭·보이지 않는 조종 글자는 <b>한 칸</b>으로 — 남의 화면을 밀어 올리지 못하게.
				bool blank = char.IsControl(letter) || char.IsWhiteSpace(letter);
				if (blank)
				{
					if (lastWasSpace == false)
						builder.Append(' ');

					lastWasSpace = true;
					continue;
				}

				builder.Append(letter);
				lastWasSpace = false;
			}

			string clean = builder.ToString().TrimEnd();
			if (clean.Length == 0)
				return null;

			return clean.Length <= LONGEST ? clean : clean.Substring(0, LONGEST);
		}
	}
}
