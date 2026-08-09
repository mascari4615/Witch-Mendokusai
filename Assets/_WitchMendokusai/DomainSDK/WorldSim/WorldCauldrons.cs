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

		/// <summary>솥 안이 바뀌었다고 알린다 — 젓기·비우기·완성이 지나간 뒤 부른다.</summary>
		public void Touch()
		{
			lock (gate)
			{
				Version++;
			}
		}
	}
}
