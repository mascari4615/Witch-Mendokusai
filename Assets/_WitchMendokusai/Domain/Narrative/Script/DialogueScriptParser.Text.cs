using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 대본 읽기의 글자 손질 부분. 같은 클래스의 partial 조각이다.
	public static partial class DialogueScriptParser
	{
		/// <summary>
		/// 앞머리의 `(지문)` 을 떼어낸다 — 「욘: (한숨) "응."」. 실측(2026-08-08): 원고에 흔한 모양이다.
		/// 안 떼면 **말풍선에 「(한숨) "응."」 이 통째로 뜬다** — 지문은 말이 아니다.
		/// 지문만 있고 말이 없는 줄(「(오래 바라본다)」)도 정상으로 본다.
		/// </summary>
		private static string SplitStageDirection(string text, out string stageDirection)
		{
			stageDirection = null;
			string trimmed = StripEmphasis(text);
			if (trimmed.StartsWith("(", StringComparison.Ordinal) == false)
			{
				return trimmed;
			}

			int close = trimmed.IndexOf(')');
			if (close < 0)
			{
				return trimmed;
			}

			stageDirection = trimmed.Substring(0, close + 1).Trim();
			return trimmed.Substring(close + 1).Trim();
		}

		/// <summary>`*기울임*` 표시를 걷어낸다 — 원고의 강조는 글쓰기 표기지 대사 글자가 아니다.</summary>
		private static string StripEmphasis(string text)
		{
			string trimmed = text.Trim();
			while (trimmed.Length >= 2 && trimmed[0] == '*' && trimmed[trimmed.Length - 1] == '*')
			{
				trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
			}
			return trimmed;
		}

		/// <summary>따옴표(곧은 것·굽은 것 양쪽)로 감싼 대사는 벗긴다 — 원고는 둘을 섞어 쓴다.</summary>
		private static string StripQuotes(string text) => TryStripQuotes(text, out string stripped) ? stripped : text;

		/// <summary>
		/// 감싼 따옴표를 벗겼으면 true. 「벗겨졌는가」 자체가 판단 근거라서 따로 낸다 —
		/// 이름 없는 인용줄이 *대사인지 경구인지* 를 이걸로 가른다.
		/// </summary>
		private static bool TryStripQuotes(string text, out string stripped)
		{
			stripped = text;
			if (text.Length < 2)
			{
				return false;
			}
			char first = text[0];
			char last = text[text.Length - 1];
			bool straight = first == '"' && last == '"';
			bool curly = first == '“' && last == '”';
			if (straight == false && curly == false)
			{
				return false;
			}
			stripped = text.Substring(1, text.Length - 2).Trim();
			return true;
		}
	}
}
