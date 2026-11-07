namespace WitchMendokusai
{
	// 글자 표시, 넘김, 자동 진행 판정. 시간은 밖에서 주입
	public sealed class DialogueLineAdvancer
	{
		private string text = string.Empty;
		private float typingElapsed;
		private float autoElapsed;

		public float CharactersPerSecond { get; set; } = 26f;
		public float AutoDelaySeconds { get; set; } = 1f;
		public float AutoSecondsPerCharacter { get; set; } = 0.03f;
		public bool AutoEnabled { get; set; }

		public int VisibleCharacters { get; private set; }
		public bool IsTyping => VisibleCharacters < text.Length;
		public string Text => text;

		public void Begin(string lineText)
		{
			text = lineText ?? string.Empty;
			VisibleCharacters = CharactersPerSecond <= 0f ? text.Length : 0;
			typingElapsed = 0f;
			autoElapsed = 0f;
		}

		public void Complete()
		{
			VisibleCharacters = text.Length;
			autoElapsed = 0f;
		}

		// 눌림 처리. 글자 진행 중이면 완성, 다 나왔으면 다음 줄 신호
		public bool Press()
		{
			if (IsTyping)
			{
				Complete();
				return false;
			}
			return true;
		}

		// true면 자동 진행 신호
		public bool Tick(float deltaTime)
		{
			if (IsTyping)
			{
				typingElapsed += deltaTime;
				int count = (int)(typingElapsed * CharactersPerSecond);
				VisibleCharacters = System.Math.Min(text.Length, SkipHalfSurrogate(count));
				return false;
			}

			if (AutoEnabled == false)
			{
				return false;
			}

			autoElapsed += deltaTime;
			return autoElapsed >= AutoDelaySeconds + text.Length * AutoSecondsPerCharacter;
		}

		private int SkipHalfSurrogate(int count)
		{
			if (count > 0 && count < text.Length && char.IsHighSurrogate(text[count - 1]))
			{
				return count + 1;
			}
			return count;
		}
	}
}
