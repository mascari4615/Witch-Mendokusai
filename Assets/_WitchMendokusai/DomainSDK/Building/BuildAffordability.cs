using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 지을 수 있는 것 한 칸이 화면에 어떻게 보이나 (TASK-WM-217) — 이름 · 재료 · <b>지금 지을 수 있나</b>.
	/// </summary>
	public struct BuildOption
	{
		public int BuildingId;
		public string Name;
		public int CostItemId;
		public int CostAmount;

		/// <summary>지금 가방에 든 그 재료 수 — 「나무 0/2」의 앞 숫자다.</summary>
		public int Carrying;

		/// <summary>재료가 되나. 공짜(비용 0)면 언제나 된다.</summary>
		public bool Affordable => CostAmount <= 0 || Carrying >= CostAmount;
	}

	/// <summary>
	/// <b>세계가 아는 것만</b> 짓기 목록에 올린다 (TASK-WM-217).
	///
	/// ★ 왜 필요한가 (실측 2026-08-10): 게임의 짓기 바는 자기 자산 전부를 늘어놓았다. 세계가 모르는
	///   건물을 고르면 <b>내 화면에만 섰다가 사라진다</b>(세계가 거절하고 화면이 되돌린다) —
	///   사람은 「짓기가 고장 났다」로 읽는다. 재료도 안 보여서 <b>왜 안 지어지는지</b>도 알 수 없었다.
	///   웹 창은 이미 「나무 0/2」를 보여 준다. 같은 세계라면 게임 창도 같은 것을 봐야 한다.
	///
	/// 여기는 <b>목록을 만드는 규칙</b>만 안다 — 무엇을 지을 수 있는지는 세계(<see cref="WorldBuildingCatalog"/>)가,
	/// 무엇을 들고 있는지는 가방이 정한다.
	/// </summary>
	public static class BuildAffordability
	{
		/// <summary>
		/// 세계의 목록 + 내 가방 → 화면에 늘어놓을 칸들.
		/// <paramref name="carrying"/> 은 「그 아이템을 몇 개 들고 있나」를 답한다(모르면 0).
		/// </summary>
		public static List<BuildOption> Options(
			IReadOnlyList<BuildingCatalogEntry> catalog,
			System.Func<int, int> carrying,
			List<BuildOption> into = null)
		{
			List<BuildOption> options = into ?? new List<BuildOption>();
			options.Clear();

			if (catalog == null)
				return options;

			for (int i = 0; i < catalog.Count; i++)
			{
				BuildingCatalogEntry entry = catalog[i];
				if (entry == null)
					continue;

				// ⚠ 아이템 번호 0(나무)은 진짜 재료다 — 「없음」으로 거르면 나무로 짓는 것이 전부 공짜가 된다.
				int cost = entry.costAmount < 0 ? 0 : entry.costAmount;

				options.Add(new BuildOption
				{
					BuildingId = entry.id,
					Name = string.IsNullOrEmpty(entry.name) ? "이름 없는 것" : entry.name,
					CostItemId = entry.costItemId,
					CostAmount = cost,
					Carrying = cost <= 0 || carrying == null ? 0 : carrying(entry.costItemId),
				});
			}

			return options;
		}

		/// <summary>「나무 0/2」 — 왜 안 지어지는지 사람이 읽을 수 있게 (재료가 없으면 빈 글).</summary>
		public static string CostText(BuildOption option, System.Func<int, string> nameOf)
		{
			if (option.CostAmount <= 0)
				return string.Empty;

			string material = nameOf == null ? null : nameOf(option.CostItemId);
			if (string.IsNullOrEmpty(material))
				material = "재료";

			return material + " " + option.Carrying + "/" + option.CostAmount;
		}
	}
}
