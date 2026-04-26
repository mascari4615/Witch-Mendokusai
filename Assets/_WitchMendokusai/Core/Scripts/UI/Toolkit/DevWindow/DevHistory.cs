using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발자 콘솔 명령 히스토리. 세션 in-memory, 100개 cap, 연속 중복은 dedupe.
	/// 디스크 영속화는 1차 미포함.
	/// </summary>
	public class DevHistory
	{
		public const int MAX_HISTORY = 100;

		private readonly List<string> entries = new();

		// cursor == -1: 신규 입력 모드 (히스토리 탐색 안 함)
		// cursor 0..count-1: 해당 인덱스 항목 표시 중
		private int cursor = -1;

		public bool IsNavigating => cursor != -1;

		public void Add(string command)
		{
			if (string.IsNullOrWhiteSpace(command))
				return;

			// 직전 항목과 동일하면 스킵 (bash 와 동일)
			if (entries.Count > 0 && entries[entries.Count - 1] == command)
			{
				cursor = -1;
				return;
			}

			entries.Add(command);
			if (entries.Count > MAX_HISTORY)
				entries.RemoveAt(0);

			cursor = -1;
		}

		public void ResetCursor() => cursor = -1;

		/// <summary>↑ 키. 히스토리 비어있으면 null. 가장 위면 그대로 머무름.</summary>
		public string Previous()
		{
			if (entries.Count == 0)
				return null;

			if (cursor == -1)
				cursor = entries.Count - 1;
			else if (cursor > 0)
				cursor--;

			return entries[cursor];
		}

		/// <summary>↓ 키. 신규 모드면 null. 가장 아래면 빈 문자열 반환 + 신규 모드 복귀.</summary>
		public string Next()
		{
			if (cursor == -1)
				return null;

			if (cursor < entries.Count - 1)
			{
				cursor++;
				return entries[cursor];
			}

			cursor = -1;
			return string.Empty;
		}
	}
}
