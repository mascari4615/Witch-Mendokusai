using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.DomainSDK.Building
{
	/// <summary>
	/// 내가 먼저 세운 것을 <b>세계가 인정했는지</b> 맞춰 보는 자리 (TASK-WM-217).
	///
	/// ★ 왜 필요한가: 손맛 때문에 창은 세계의 답을 기다리지 않고 <b>먼저</b> 세운다.
	///   그런데 세계가 거절하면(겹침·규칙) 내 화면에만 있는 <b>유령 건물</b>이 남는다 —
	///   그 자리는 남에게 비어 보이고, 나는 거기 다시 못 짓는다. 낙관 배치의 빠진 반쪽이 이것이다.
	///
	/// 판정만 한다: 「이건 인정됐다 / 이건 되돌려라」. 실제로 지우는 일은 화면 쪽이 한다.
	/// </summary>
	public sealed class PlacementReconciler
	{
		/// <summary>이만큼은 기다려 준다 — 세계의 답은 한 박자 늦게 온다(초).</summary>
		public const float GRACE_SECONDS = 2f;

		private readonly Dictionary<Vector3Int, float> pending = new Dictionary<Vector3Int, float>();
		private readonly HashSet<Vector3Int> present = new HashSet<Vector3Int>();
		private readonly List<Vector3Int> rollback = new List<Vector3Int>();

		/// <summary>아직 세계의 답을 못 받은 것 개수.</summary>
		public int PendingCount => pending.Count;

		/// <summary>내 화면에 먼저 세웠다 — 언제 세웠는지 기억해 둔다.</summary>
		public void Predicted(Vector3Int cell, float now)
		{
			// 같은 자리를 다시 눌렀으면 시계를 다시 잰다(직전 요청이 늦는 중일 수 있다).
			pending[cell] = now;
		}

		/// <summary>
		/// 세계가 보낸 목록과 맞춰 본다. <b>되돌려야 할 자리들</b>을 돌려준다.
		/// 인정된 것은 목록에서 빠지고, 아직 기다릴 만한 것은 그대로 둔다.
		/// </summary>
		public IReadOnlyList<Vector3Int> Reconcile(IEnumerable<Vector3Int> worldCells, float now)
		{
			rollback.Clear();
			present.Clear();

			if (worldCells != null)
			{
				foreach (Vector3Int cell in worldCells)
					present.Add(cell);
			}

			foreach (KeyValuePair<Vector3Int, float> entry in pending)
			{
				if (present.Contains(entry.Key))
				{
					// 인정됐다 — 더 볼 일 없다.
					rollback.Add(entry.Key);
					continue;
				}

				if (now - entry.Value >= GRACE_SECONDS)
					rollback.Add(entry.Key);
			}

			// 인정된 것과 되돌릴 것 모두 대기 목록에서는 빠진다. 되돌릴 것만 골라 돌려준다.
			List<Vector3Int> result = new List<Vector3Int>();
			for (int i = 0; i < rollback.Count; i++)
			{
				bool confirmed = present.Contains(rollback[i]);
				pending.Remove(rollback[i]);
				if (confirmed == false)
					result.Add(rollback[i]);
			}

			return result;
		}

		/// <summary>세계에서 나올 때 — 기다리던 것을 잊는다.</summary>
		public void Clear() => pending.Clear();
	}
}
