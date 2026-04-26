using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// 명령 인자를 ItemData 로 매핑. 마인크래프트의 모호 매칭 패턴 차용.
	/// 매칭 규칙:
	/// - 정수 → ID 직접 (`give 71`)
	/// - "I_<num>" prefix → ID 매칭 (`give I_71`)
	/// - 그 외 문자열 → Name 부분 매칭 (한글·영문) (`give 잘린`)
	/// 매칭 결과 0/1/2+ 에 따라 호출자가 처리.
	/// </summary>
	public static class DevItemResolver
	{
		/// <summary>reference 를 ItemData 로 해결. 정확히 한 개 매칭 시 반환, 그 외 null 반환 + matches 채움.</summary>
		public static ItemData Resolve(string reference, out List<ItemData> matches)
		{
			List<ItemData> results = new();

			if (string.IsNullOrEmpty(reference))
			{
				matches = results;
				return null;
			}

			// 정수 ID 직접
			if (int.TryParse(reference, out int directId))
			{
				ItemData byId = SOHelper.Get<ItemData>(directId);
				if (byId != null)
					results.Add(byId);
				matches = results;
				return byId;
			}

			// I_<num> 코드명
			if (reference.StartsWith("I_", StringComparison.OrdinalIgnoreCase))
			{
				string idStr = reference.Substring(2);
				if (int.TryParse(idStr, out int codeId))
				{
					ItemData byCode = SOHelper.Get<ItemData>(codeId);
					if (byCode != null)
					{
						results.Add(byCode);
						matches = results;
						return byCode;
					}
				}
			}

			// Name 부분 매칭 (out 파라미터는 람다에서 못 쓰니 로컬 변수로 받고 끝에 대입)
			SOHelper.ForEach<ItemData>(item =>
			{
				if (item == null || string.IsNullOrEmpty(item.Name))
					return;
				if (item.Name.IndexOf(reference, StringComparison.OrdinalIgnoreCase) >= 0)
					results.Add(item);
			});

			matches = results;
			return results.Count == 1 ? results[0] : null;
		}

		/// <summary>자동완성 후보. partial 이 빈 문자열이면 전체 ItemData 의 코드명 반환.</summary>
		public static IEnumerable<string> SuggestReferences(string partial)
		{
			List<string> results = new();

			SOHelper.ForEach<ItemData>(item =>
			{
				if (item == null)
					return;

				string codeForm = $"I_{item.ID}";

				if (string.IsNullOrEmpty(partial)
					|| codeForm.StartsWith(partial, StringComparison.OrdinalIgnoreCase)
					|| (item.Name != null && item.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase)))
				{
					results.Add(codeForm);
				}
			});

			return results.OrderBy(name => name);
		}
	}
}
