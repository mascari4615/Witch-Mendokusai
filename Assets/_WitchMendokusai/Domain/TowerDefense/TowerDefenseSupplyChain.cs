using System.Collections.Generic;
using UnityEngine;
// ★ 이 파일의 좌표는 「판정 쪽」이다 (TASK-WM-214). 엔진에서 쓰는 건 Transform 같은 씬 손잡이뿐.
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai
{
	/// <summary>
	/// 개척 판의 보급 사슬 — *누가 코어까지 이어져 있나* (TASK-WM-194).
	///
	/// ★ 왜 떼어냈나: 매치 본체가 4000줄이 넘어 「한 덩어리가 너무 많은 걸 아는」 병이 실제 결함으로
	///   여러 번 나왔다(전기 층에 이은 두 번째 증분). 사슬은 자립적인 질문이다 —
	///   *이어졌나*만 답하면 되고, 「그래서 얼마 버나」는 다른 층(채집 정산)의 일이다.
	/// ★ 전기와 다른 층이다: 전기는 「덮였나 + 총량이 남았나」, 보급은 「코어까지 닿았나」.
	///   둘을 합치면 「넓힌다」의 대가가 한 종류로 뭉개진다.
	///
	/// 계산 자체는 규칙층(<see cref="TowerDefenseSupply"/>)이 한다 — 여기는 *씬의 것들*을 그 계산에
	/// 넣고, 「몇 번째가 이어졌나」를 되물을 수 있게 들고 있는 일만 한다.
	/// </summary>
	public sealed class TowerDefenseSupplyChain
	{
		private readonly List<Transform> buildings = new();
		private readonly List<Vector3> positions = new();
		private readonly List<Vector3> seeds = new();
		private readonly HashSet<int> connected = new();

		/// <summary> 사슬 후보 건물들 — 세운 것은 전부 징검다리가 된다. </summary>
		public IReadOnlyList<Transform> Buildings => buildings;

		/// <summary> 지금 코어까지 이어진 건물 수. </summary>
		public int ConnectedCount => connected.Count;

		public void Clear()
		{
			buildings.Clear();
			positions.Clear();
			seeds.Clear();
			connected.Clear();
		}

		public void Add(Transform building) => buildings.Add(building);
		public bool Remove(Transform building) => buildings.Remove(building);
		public bool Contains(Transform building) => buildings.Contains(building);

		/// <summary> 그 자리(목록 번호)가 이어져 있나. </summary>
		public bool IsConnected(int index) => connected.Contains(index);

		/// <summary>
		/// 사슬을 다시 계산한다 — 사라진 것은 걷어내고, 원점(코어·전초기지)에서 뻗어 나간다.
		/// 사슬 중간이 사라지면 그 너머가 통째로 끊긴다(그래서 매번 전부 다시 본다).
		/// </summary>
		public void Compute(Vector3 corePosition, IReadOnlyList<Transform> outposts, float reach)
		{
			for (int index = buildings.Count - 1; index >= 0; index--)
			{
				if (buildings[index] == null)
					buildings.RemoveAt(index);
			}

			positions.Clear();
			foreach (Transform building in buildings)
				positions.Add(building.position.ToSim());

			seeds.Clear();
			seeds.Add(corePosition);
			if (outposts != null)
			{
				foreach (Transform outpost in outposts)
				{
					if (outpost != null)
						seeds.Add(outpost.position.ToSim());
				}
			}

			TowerDefenseSupply.Compute(seeds, positions, reach, connected);
		}

		/// <summary>
		/// 그 자리가 「내 땅」 안인가 — 원점(코어·전초기지)이나 *이어진* 건물에서 뻗어 닿는가.
		/// ★ 안 이어진 건물에서 뻗으면 끊긴 섬이 무한히 자란다 — 징검다리는 이어져 있을 때만 다리다.
		/// </summary>
		public bool IsWithinReach(Vector3 worldPosition, Vector3 corePosition,
			IReadOnlyList<Transform> outposts, float reach)
		{
			float reachSqr = reach * reach;
			if ((worldPosition - corePosition).sqrMagnitude <= reachSqr)
				return true;

			if (outposts != null)
			{
				foreach (Transform outpost in outposts)
				{
					if (outpost != null && (worldPosition - outpost.position.ToSim()).sqrMagnitude <= reachSqr)
						return true;
				}
			}

			for (int index = 0; index < buildings.Count; index++)
			{
				Transform building = buildings[index];
				if (building == null || connected.Contains(index) == false)
					continue;
				if ((worldPosition - building.position.ToSim()).sqrMagnitude <= reachSqr)
					return true;
			}

			return false;
		}
	}
}
