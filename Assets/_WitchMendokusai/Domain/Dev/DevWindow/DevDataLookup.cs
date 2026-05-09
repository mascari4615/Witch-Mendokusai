using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// 어떤 DataSO 타입이든 공통으로 reference 매칭/자동완성 후보 계산.
	/// 코드 prefix 는 호출자가 정의 (Items=I_, Mobs=M_, Stages=D_, Quests=Q_ 등).
	/// 매칭 규칙:
	/// - 정수 → ID 직접
	/// - "&lt;prefix&gt;&lt;num&gt;" → ID 매칭
	/// - 그 외 → Name 부분매칭 (한글·영문)
	/// </summary>
	public static class DevDataLookup
	{
		/// <summary>reference 를 단일 T 로 해결. 정확히 한 개 매칭이면 반환, 그 외 null + matches 채움.</summary>
		public static T Resolve<T>(string reference, string codePrefix, out List<T> matches) where T : DataSO
		{
			List<T> results = new();

			if (string.IsNullOrEmpty(reference))
			{
				matches = results;
				return null;
			}

			if (int.TryParse(reference, out int directId))
			{
				T byId = SOHelper.Get<T>(directId);
				if (byId != null)
					results.Add(byId);
				matches = results;
				return byId;
			}

			if (reference.StartsWith(codePrefix, StringComparison.OrdinalIgnoreCase))
			{
				string idStr = reference.Substring(codePrefix.Length);
				if (int.TryParse(idStr, out int codeId))
				{
					T byCode = SOHelper.Get<T>(codeId);
					if (byCode != null)
					{
						results.Add(byCode);
						matches = results;
						return byCode;
					}
				}
			}

			SOHelper.ForEach<T>(item =>
			{
				if (item == null || string.IsNullOrEmpty(item.Name))
					return;
				if (item.Name.IndexOf(reference, StringComparison.OrdinalIgnoreCase) >= 0)
					results.Add(item);
			});

			matches = results;
			return results.Count == 1 ? results[0] : null;
		}

		/// <summary>autocomplete 후보 — 코드명 형태로 (`&lt;prefix&gt;&lt;id&gt;`) 반환. ID 오름차순.</summary>
		public static IEnumerable<string> SuggestRefs<T>(string partial, string codePrefix) where T : DataSO
		{
			List<(int id, string code)> entries = new();

			SOHelper.ForEach<T>(item =>
			{
				if (item == null)
					return;
				string codeForm = $"{codePrefix}{item.ID}";
				if (string.IsNullOrEmpty(partial)
					|| codeForm.StartsWith(partial, StringComparison.OrdinalIgnoreCase)
					|| (item.Name != null && item.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase)))
				{
					entries.Add((item.ID, codeForm));
				}
			});

			return entries.OrderBy(entry => entry.id).Select(entry => entry.code);
		}
	}
}
