using System.Collections.Generic;
using System.Text;

namespace WitchMendokusai
{
	/// <summary>
	/// 원고 자산 번호가 겹치는지 본다 (TASK-WM-052).
	///
	/// ★ 왜 이게 사고인가: 「이 대화 봤나」·「그때 뭐라고 했나」는 전부 **번호로** 기록된다.
	///   번호가 겹치면 서로 다른 두 원고가 **한 칸을 같이 쓴다** — 한쪽을 보면 다른 쪽도 본 것이 되고,
	///   「처음 만났을 때만」 대사가 엉뚱한 데서 안 나온다.
	///   자산을 복제해서 새 원고를 만들면(제일 흔한 방법이다) 번호까지 복제된다.
	///
	/// ★ 안 터지고 흔적도 없다. 저장 파일에서만 보이는 종류라, 만들 때 잡지 못하면 아무도 못 찾는다.
	///
	/// 판정은 순수하게 여기서 한다 — 자산을 긁어 오는 일은 에디터 쪽이 하고,
	/// **무엇이 문제인가**는 화면 없이 시험되는 이쪽이 안다.
	/// </summary>
	public static class DialogueScriptIdAudit
	{
		/// <summary>원고 하나 — 번호와 (사람이 알아볼) 이름.</summary>
		public readonly struct Entry
		{
			public int Id { get; }
			public string Name { get; }

			public Entry(int id, string name)
			{
				Id = id;
				Name = name;
			}
		}

		/// <summary>
		/// 겹친 번호마다 한 줄씩. 겹친 게 없으면 빈 목록.
		///
		/// 번호를 안 매긴 것(<see cref="DataSO.NONE_ID"/>)은 **안 센다** — 그건 다른 흠이고,
		/// 여기서 같이 잡으면 새 자산을 만들 때마다 「겹쳤다」가 떠서 곧 무시당한다.
		/// </summary>
		public static List<string> FindDuplicates(IReadOnlyList<Entry> entries)
		{
			List<string> problems = new();
			if (entries == null)
			{
				return problems;
			}

			Dictionary<int, List<string>> namesById = new();
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i].Id == DataSO.NONE_ID)
				{
					continue;
				}
				if (namesById.TryGetValue(entries[i].Id, out List<string> names) == false)
				{
					names = new List<string>();
					namesById[entries[i].Id] = names;
				}
				names.Add(entries[i].Name);
			}

			foreach (KeyValuePair<int, List<string>> pair in namesById)
			{
				if (pair.Value.Count < 2)
				{
					continue;
				}

				StringBuilder builder = new();
				builder.Append("원고 번호 ").Append(pair.Key).Append(" 이(가) 겹친다: ");
				for (int i = 0; i < pair.Value.Count; i++)
				{
					if (i > 0)
					{
						builder.Append(" · ");
					}
					builder.Append(pair.Value[i]);
				}
				builder.Append(" — 「봤나」와 「뭐라고 했나」가 한 칸을 같이 쓰게 된다");
				problems.Add(builder.ToString());
			}
			return problems;
		}
	}
}
