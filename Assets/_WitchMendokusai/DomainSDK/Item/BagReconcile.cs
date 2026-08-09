using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>「이만큼 채우고, 이만큼 뺀다」 (TASK-WM-218).</summary>
	public readonly struct BagAdjustment
	{
		public BagAdjustment(int itemId, int add, int remove)
		{
			ItemId = itemId;
			Add = add;
			Remove = remove;
		}

		public int ItemId { get; }

		/// <summary>부족해서 채울 개수(0 이면 채울 것 없음).</summary>
		public int Add { get; }

		/// <summary>남아서 뺄 개수(0 이면 뺄 것 없음).</summary>
		public int Remove { get; }
	}

	/// <summary>
	/// 화면의 가방을 <b>세계가 아는 가방에 맞추는</b> 계산 (TASK-WM-218).
	///
	/// ★ 왜 판정 층인가: 여기가 틀리면 아이템이 <b>불어나거나 사라진다</b> — 눈으로는 한참 뒤에야
	///   「어? 아까보다 적네」로 나타나는 종류다. 통째로 비우고 다시 채우는 방법도 있지만,
	///   그러면 칸 배치가 매번 뒤집혀 사람이 물건을 못 찾는다. 그래서 <b>차이만</b> 만진다.
	///
	/// 세계가 모르는 물건(목록에 없는 번호)은 여기서 판단하지 않는다 — 부르는 쪽이 걸러 낸다.
	/// </summary>
	public static class BagReconcile
	{
		/// <summary>
		/// 지금 가진 것(<paramref name="current"/>)을 세계가 아는 것(<paramref name="target"/>)으로
		/// 맞추기 위한 조정 목록. <b>같은 것은 안 만진다</b>(조정 0건 = 이미 맞다).
		///
		/// 세계에 없는데 내가 갖고 있으면 <b>뺀다</b> — 안 빼면 쓴 것이 화면에서 되살아난다.
		/// </summary>
		public static List<BagAdjustment> Plan(IReadOnlyDictionary<int, int> current, IReadOnlyDictionary<int, int> target)
		{
			List<BagAdjustment> plan = new List<BagAdjustment>();
			if (target != null)
			{
				foreach (KeyValuePair<int, int> want in target)
				{
					int have = 0;
					if (current != null)
						current.TryGetValue(want.Key, out have);

					int amount = want.Value < 0 ? 0 : want.Value;
					if (amount > have)
						plan.Add(new BagAdjustment(want.Key, amount - have, 0));
					else if (amount < have)
						plan.Add(new BagAdjustment(want.Key, 0, have - amount));
				}
			}

			if (current == null)
				return plan;

			foreach (KeyValuePair<int, int> have in current)
			{
				if (have.Value <= 0)
					continue;

				if (target != null && target.ContainsKey(have.Key))
					continue;

				// 세계가 모르는 = 내가 가지면 안 되는 것.
				plan.Add(new BagAdjustment(have.Key, 0, have.Value));
			}

			return plan;
		}
	}
}
