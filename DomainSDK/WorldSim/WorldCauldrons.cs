using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계에 <b>여러 개의 솥</b> — 지은 자리마다 하나 (TASK-WM-217).
	///
	/// ★ 왜: 솥이 세계에 하나뿐이면 둘이 동시에 조리할 수 없다. 한 사람이 젓는 동안 다른 사람은
	///   그 솥을 망치거나 기다려야 한다 — 그건 같이 노는 게 아니라 순서 다투기다.
	///   그리고 「솥을 짓는다」가 아무 뜻이 없다(이미 하나 있으니까).
	///
	/// 자리별로 나누되 규칙은 <b>같은 것</b>(<see cref="WorldCauldron"/>)을 그대로 쓴다.
	/// </summary>
	public sealed class WorldCauldrons
	{
		/// <summary>솥에 손이 닿는 거리 — 멀리서 젓지 못한다.</summary>
		public const float REACH = 3f;

		private readonly object gate = new object();
		private readonly Dictionary<Vector3Int, WorldCauldron> pots = new Dictionary<Vector3Int, WorldCauldron>();

		/// <summary>어느 솥이든 바뀌면 오른다 — 창이 「내 화면이 낡았나」를 이 수로 안다.</summary>
		public int Version { get; private set; }

		/// <summary>그 자리에 솥이 있나.</summary>
		public bool Has(Vector3Int cell)
		{
			lock (gate)
			{
				return pots.ContainsKey(cell);
			}
		}

		/// <summary>그 자리에 솥을 놓는다 — 이미 있으면 그대로 둔다(젓던 것을 지우지 않는다).</summary>
		public void Place(Vector3Int cell)
		{
			lock (gate)
			{
				if (pots.ContainsKey(cell))
					return;

				pots[cell] = new WorldCauldron();
				Version++;
			}
		}

		/// <summary>솥을 치운다 — 젓던 것도 사라진다.</summary>
		public bool Remove(Vector3Int cell)
		{
			lock (gate)
			{
				if (pots.Remove(cell) == false)
					return false;

				Version++;
				return true;
			}
		}

		/// <summary>그 자리의 솥 — 없으면 null. 손이 닿는지는 부르는 쪽이 본다.</summary>
		public WorldCauldron At(Vector3Int cell)
		{
			lock (gate)
			{
				return pots.TryGetValue(cell, out WorldCauldron pot) ? pot : null;
			}
		}

		/// <summary>손이 닿는 자리의 솥 — 멀면 null(창이 우겨도 못 젓는다).</summary>
		public WorldCauldron Reachable(Vector3Int cell, float fromX, float fromZ)
		{
			float dx = cell.x - fromX;
			float dz = cell.z - fromZ;
			if (dx * dx + dz * dz > REACH * REACH)
				return null;

			return At(cell);
		}

		/// <summary>지금 있는 솥들의 자리 — 창이 그리거나 목록을 만들 때 쓴다.</summary>
		public List<Vector3Int> Cells()
		{
			lock (gate)
			{
				return new List<Vector3Int>(pots.Keys);
			}
		}

		/// <summary>솥들을 적어 둔다 — 자리와 <b>젓던 자국</b>까지(껐다 켜도 이어진다).</summary>
		public List<CauldronSaveEntry> Save()
		{
			List<CauldronSaveEntry> saved = new List<CauldronSaveEntry>();
			lock (gate)
			{
				foreach (KeyValuePair<Vector3Int, WorldCauldron> pair in pots)
				{
					List<DomainSDK.Alchemy.BrewStep> steps = new List<DomainSDK.Alchemy.BrewStep>();
					pair.Value.ReadSteps(steps);

					BrewStepSaveEntry[] path = new BrewStepSaveEntry[steps.Count];
					for (int i = 0; i < steps.Count; i++)
					{
						path[i] = new BrewStepSaveEntry
						{
							dx = steps[i].Direction.X,
							dy = steps[i].Direction.Y,
							grind = steps[i].Grind,
						};
					}

					saved.Add(new CauldronSaveEntry { x = pair.Key.x, y = pair.Key.y, z = pair.Key.z, path = path });
				}
			}

			return saved;
		}

		/// <summary>
		/// 기억에서 되살린다 — <b>저은 길을 그대로 다시 젓는다</b>.
		/// 마커 좌표를 그냥 적어 두면 규칙이 바뀌었을 때 그 자국이 거짓이 된다(길이 정본이다).
		/// </summary>
		public void Load(IEnumerable<CauldronSaveEntry> saved)
		{
			lock (gate)
			{
				pots.Clear();
				Version++;

				if (saved == null)
					return;

				foreach (CauldronSaveEntry entry in saved)
				{
					if (entry == null)
						continue;

					WorldCauldron pot = new WorldCauldron();
					if (entry.path != null)
					{
						for (int i = 0; i < entry.path.Length; i++)
						{
							pot.AddStep(new DomainSDK.Alchemy.BrewStep
							{
								Direction = new DomainSDK.Alchemy.BrewVector(entry.path[i].dx, entry.path[i].dy),
								Grind = entry.path[i].grind,
							});
						}
					}

					pots[new Vector3Int(entry.x, entry.y, entry.z)] = pot;
				}
			}
		}

		/// <summary>솥 안이 바뀌었다고 알린다 — 젓기·비우기·완성이 지나간 뒤 부른다.</summary>
		public void Touch()
		{
			lock (gate)
			{
				Version++;
			}
		}
	}

	/// <summary>솥 하나가 기억하는 것 — 어느 자리에, 어떻게 저었나.</summary>
	[System.Serializable]
	public class CauldronSaveEntry
	{
		public int x;
		public int y;
		public int z;
		public BrewStepSaveEntry[] path = System.Array.Empty<BrewStepSaveEntry>();
	}

	/// <summary>저은 한 걸음 — 방향과 세기.</summary>
	[System.Serializable]
	public class BrewStepSaveEntry
	{
		public float dx;
		public float dy;
		public float grind = 1f;
	}
}
