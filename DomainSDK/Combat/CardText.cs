using System;
using System.Collections.Generic;
using System.Text;

namespace WitchMendokusai
{
	/// <summary>
	/// 카드 설명의 수치 자리 채우기. 문구에 수치를 손으로 적으면 효과 값을 바꿀 때 어긋남.
	/// </summary>
	public static class CardText
	{
		// 자리 번호로 읽는 최대 자릿수. {0} ~ {999}
		private const int MAX_INDEX_DIGITS = 3;

		// {0}, {1} 을 같은 순서의 효과 값으로 바꿈. 숫자가 아닌 중괄호는 원문 유지.
		// 자리가 가리키는 값이 없으면 예외. 화면에 {1} 이 그대로 나가는 것보다 자료 오류가 바로 드러나는 쪽
		public static string Format(string template, IReadOnlyList<int> values)
		{
			if (string.IsNullOrEmpty(template))
				return string.Empty;
			if (template.IndexOf('{') < 0)
				return template;

			int valueCount = values != null ? values.Count : 0;
			StringBuilder text = new(template.Length);
			int position = 0;
			while (position < template.Length)
			{
				char character = template[position];
				if (character == '{' && TryReadIndex(template, position, out int index, out int close))
				{
					if (index >= valueCount)
						throw new ArgumentException($"카드 설명의 {{{index}}} 자리에 넣을 효과 값이 없음 (효과 {valueCount}개): {template}");
					text.Append(values[index]);
					position = close + 1;
					continue;
				}
				text.Append(character);
				position++;
			}
			return text.ToString();
		}

		private static bool TryReadIndex(string template, int open, out int index, out int close)
		{
			index = 0;
			close = open;
			int position = open + 1;
			int digits = 0;
			while (position < template.Length && template[position] >= '0' && template[position] <= '9' && digits < MAX_INDEX_DIGITS)
			{
				index = index * 10 + (template[position] - '0');
				position++;
				digits++;
			}
			if (digits == 0 || position >= template.Length || template[position] != '}')
				return false;
			close = position;
			return true;
		}
	}
}
