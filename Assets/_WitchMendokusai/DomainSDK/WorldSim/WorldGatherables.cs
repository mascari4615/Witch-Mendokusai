using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>세계에 흩어져 있는 주울 것 한 종류 — 무엇이 몇 개 나오나.</summary>
	[Serializable]
	public class GatherableKind
	{
		public int itemId;
		public int amount = 1;

		/// <summary>다시 자라기까지 걸리는 <b>세계의 분</b>. 0 이면 안 자란다(한 번 주우면 끝).</summary>
		public int respawnMinutes = 240;
	}

	/// <summary>주울 것 하나가 서 있는 자리 — 창이 그리고, 사람이 누른다.</summary>
	public struct GatherableNode
	{
		public int Id;
		public float X;
		public float Z;
		public int ItemId;
		public int Amount;
	}

	/// <summary>
	/// 세계에 <b>주울 것이 실제로 있다</b> (TASK-WM-217).
	///
	/// ★ 왜 필요한가: 전에는 창이 「이거 3개 주웠다」고 말하면 세계가 그냥 넣어 줬다. 그건 판정이
	///   아니라 <b>신고</b>다 — 창을 고친 사람은 무엇이든 무한히 가진다. 그리고 무한히 가지는 세계엔
	///   주우러 갈 이유가 없다(놀이가 아니다).
	///
	/// 자리는 <b>세계 번호에서 계산해 낸다</b> — 어디에 무엇이 있는지 저장하지 않는다(같은 세계면 늘 같은 자리).
	/// 저장하는 건 「언제 다시 자라나」뿐이라, 세계 파일이 자리 수만큼 부풀지 않는다.
	/// </summary>
	public sealed class WorldGatherables
	{
		/// <summary>주울 수 있는 거리 — 이보다 멀면 손이 안 닿는다(창이 우겨도).</summary>
		public const float REACH = 2.5f;

		private const int SPACING = 7;   // 몇 칸마다 하나쯤 서 있나
		private const int HALF_SPAN = 6; // 원점에서 몇 칸(격자 단위)까지 흩뿌리나

		private readonly object gate = new object();
		private readonly List<GatherableKind> kinds = new List<GatherableKind>();
		private readonly Dictionary<int, int> regrowAt = new Dictionary<int, int>();
		private readonly int seed;

		public WorldGatherables(IEnumerable<GatherableKind> kinds, int seed = 20260810)
		{
			this.seed = seed;
			if (kinds == null)
				return;

			foreach (GatherableKind kind in kinds)
			{
				if (kind == null || kind.itemId == 0)
					continue;

				this.kinds.Add(kind);
			}
		}

		/// <summary>몇 종류를 아는가 — 0 이면 세계에 주울 것이 없다.</summary>
		public int KindCount => kinds.Count;

		/// <summary>지금 서 있는 것들 — 뽑아 간 자리는 다시 자랄 때까지 빠진다.</summary>
		public List<GatherableNode> Alive(int nowMinute)
		{
			List<GatherableNode> alive = new List<GatherableNode>();
			if (kinds.Count == 0)
				return alive;

			lock (gate)
			{
				for (int gx = -HALF_SPAN; gx <= HALF_SPAN; gx++)
				{
					for (int gz = -HALF_SPAN; gz <= HALF_SPAN; gz++)
					{
						int id = NodeId(gx, gz);
						if (regrowAt.TryGetValue(id, out int at) && nowMinute < at)
							continue;

						alive.Add(Describe(id, gx, gz));
					}
				}
			}

			return alive;
		}

		/// <summary>
		/// 줍는다. 세계가 본다: <b>거기 있나 · 손이 닿나</b>. 되면 무엇이 몇 개인지 돌려준다.
		/// 가방에 넣는 것은 부르는 쪽 몫이다(가방 규칙은 세계의 다른 자리에 있다).
		/// </summary>
		public bool TryTake(int nodeId, float fromX, float fromZ, int nowMinute, out int itemId, out int amount)
		{
			itemId = 0;
			amount = 0;
			if (kinds.Count == 0)
				return false;

			if (Locate(nodeId, out int gx, out int gz) == false)
				return false;

			lock (gate)
			{
				if (regrowAt.TryGetValue(nodeId, out int at) && nowMinute < at)
					return false;

				GatherableNode node = Describe(nodeId, gx, gz);
				float dx = node.X - fromX;
				float dz = node.Z - fromZ;
				if (dx * dx + dz * dz > REACH * REACH)
					return false;

				GatherableKind kind = KindOf(nodeId);
				itemId = node.ItemId;
				amount = node.Amount;
				regrowAt[nodeId] = kind.respawnMinutes > 0
					? nowMinute + kind.respawnMinutes
					: int.MaxValue; // 안 자라는 것은 영영 비어 있다
				return true;
			}
		}

		/// <summary>뽑아 간 자리들 — 세계가 잠들었다 깨어나도 그대로여야 한다.</summary>
		public List<GatherTakenSaveEntry> Save()
		{
			List<GatherTakenSaveEntry> saved = new List<GatherTakenSaveEntry>();
			lock (gate)
			{
				foreach (KeyValuePair<int, int> pair in regrowAt)
					saved.Add(new GatherTakenSaveEntry { nodeId = pair.Key, regrowAtMinute = pair.Value });
			}

			return saved;
		}

		public void Load(IEnumerable<GatherTakenSaveEntry> saved)
		{
			lock (gate)
			{
				regrowAt.Clear();
				if (saved == null)
					return;

				foreach (GatherTakenSaveEntry entry in saved)
				{
					if (entry == null)
						continue;

					regrowAt[entry.nodeId] = entry.regrowAtMinute;
				}
			}
		}

		// ── 자리 계산 — 저장하지 않고 세계 번호에서 만들어 낸다 ──────────────────
		private static int NodeId(int gx, int gz)
		{
			// 격자 칸 하나에 번호 하나. 음수도 겹치지 않게 옮겨 담는다.
			return (gx + 1000) * 100000 + (gz + 1000);
		}

		private static bool Locate(int nodeId, out int gx, out int gz)
		{
			gx = nodeId / 100000 - 1000;
			gz = nodeId % 100000 - 1000;
			return gx >= -HALF_SPAN && gx <= HALF_SPAN && gz >= -HALF_SPAN && gz <= HALF_SPAN;
		}

		private GatherableNode Describe(int id, int gx, int gz)
		{
			int scatter = Hash(id);
			GatherableKind kind = kinds[(scatter & 0x7fffffff) % kinds.Count];

			// 격자에 딱 맞춰 서면 밭처럼 보인다 — 칸 안에서 흩어 놓는다(같은 세계면 늘 같은 자리).
			float offsetX = ((scatter >> 8) & 0xff) / 255f * (SPACING - 2f);
			float offsetZ = ((scatter >> 16) & 0xff) / 255f * (SPACING - 2f);

			return new GatherableNode
			{
				Id = id,
				X = gx * SPACING + offsetX,
				Z = gz * SPACING + offsetZ,
				ItemId = kind.itemId,
				Amount = kind.amount < 1 ? 1 : kind.amount,
			};
		}

		private GatherableKind KindOf(int nodeId)
		{
			return kinds[(Hash(nodeId) & 0x7fffffff) % kinds.Count];
		}

		private int Hash(int id)
		{
			unchecked
			{
				int value = id * 73856093 ^ seed * 19349663;
				value ^= value >> 13;
				value *= 1274126177;
				return value ^ (value >> 16);
			}
		}
	}

	/// <summary>뽑아 간 자리 한 곳 — 언제 다시 자라나.</summary>
	[Serializable]
	public class GatherTakenSaveEntry
	{
		public int nodeId;
		public int regrowAtMinute;
	}
}
