using System.Collections.Generic;
using System.Text;

namespace WitchMendokusai
{
	/// <summary>
	/// 명령 텍스트 → 토큰 배열. 공백 split + 큰따옴표 그룹.
	/// 닫히지 않은 따옴표는 마지막 토큰까지 통째로. 빈 입력은 빈 배열.
	/// </summary>
	public static class DevCommandParser
	{
		public static string[] Tokenize(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				return System.Array.Empty<string>();

			List<string> tokens = new();
			StringBuilder current = new();
			bool inQuote = false;

			for (int i = 0; i < input.Length; i++)
			{
				char ch = input[i];

				if (ch == '"')
				{
					inQuote = !inQuote;
					continue;
				}

				if (ch == ' ' && inQuote == false)
				{
					if (current.Length > 0)
					{
						tokens.Add(current.ToString());
						current.Clear();
					}
					continue;
				}

				current.Append(ch);
			}

			if (current.Length > 0)
				tokens.Add(current.ToString());

			return tokens.ToArray();
		}

		/// <summary>토큰 배열을 (commandName, args[]) 로 분리. 빈 배열이면 commandName = null.</summary>
		public static bool TryParse(string input, out string commandName, out string[] args)
		{
			string[] tokens = Tokenize(input);
			if (tokens.Length == 0)
			{
				commandName = null;
				args = System.Array.Empty<string>();
				return false;
			}

			commandName = tokens[0];
			args = new string[tokens.Length - 1];
			System.Array.Copy(tokens, 1, args, 0, args.Length);
			return true;
		}
	}
}
