using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// 명령행 텍스트에서 현재 토큰의 자동완성 후보 계산.
	/// 토큰 0 = 명령명 → DevCommandRegistry.AllNames 에서 prefix 매칭.
	/// 토큰 1+ = 인자 → 해당 명령의 IDevCommand.Suggest(partialArgs) 호출 후 prefix 매칭.
	/// </summary>
	public static class DevAutocomplete
	{
		public class Result
		{
			public string Prefix;
			public string[] Candidates;
			public bool HasMatch => Candidates != null && Candidates.Length > 0;
		}

		public static Result Compute(string input)
		{
			if (input == null)
				input = string.Empty;

			bool endsWithSpace = input.Length > 0 && input[input.Length - 1] == ' ';
			string[] tokens = DevCommandParser.Tokenize(input);

			// Case 1: 빈 입력 → 명령명 완성, prefix=""
			if (tokens.Length == 0)
				return new Result { Prefix = string.Empty, Candidates = MatchCommandNames(string.Empty) };

			// Case 2: 명령명 입력 중 (토큰 1개, 공백 없음) → 명령명 완성
			if (tokens.Length == 1 && endsWithSpace == false)
				return new Result { Prefix = tokens[0], Candidates = MatchCommandNames(tokens[0]) };

			// Case 3: 인자 자동완성 — 토큰 0 으로 명령 lookup
			if (DevCommandRegistry.Instance.TryGet(tokens[0], out IDevCommand command) == false)
			{
				string fallbackPrefix = endsWithSpace ? string.Empty : tokens[tokens.Length - 1];
				return new Result { Prefix = fallbackPrefix, Candidates = Array.Empty<string>() };
			}

			List<string> partialArgs = new();
			for (int i = 1; i < tokens.Length; i++)
				partialArgs.Add(tokens[i]);
			if (endsWithSpace)
				partialArgs.Add(string.Empty);

			string prefix = partialArgs[partialArgs.Count - 1];
			string[] candidates = command.Suggest(partialArgs.ToArray())
				.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				.OrderBy(c => c)
				.ToArray();

			return new Result { Prefix = prefix, Candidates = candidates };
		}

		/// <summary>입력에서 마지막 partial 토큰을 candidate 으로 교체. 끝에 공백 추가 옵션.</summary>
		public static string ApplyCandidate(string input, string prefix, string candidate, bool addTrailingSpace)
		{
			string suffix = addTrailingSpace ? " " : string.Empty;

			if (string.IsNullOrEmpty(prefix))
				return input + candidate + suffix;

			int idx = input.LastIndexOf(prefix, StringComparison.Ordinal);
			if (idx == -1)
				return input + candidate + suffix;

			return input.Substring(0, idx) + candidate + suffix;
		}

		private static string[] MatchCommandNames(string prefix)
		{
			return DevCommandRegistry.Instance.AllNames
				.Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				.OrderBy(name => name)
				.ToArray();
		}
	}
}
