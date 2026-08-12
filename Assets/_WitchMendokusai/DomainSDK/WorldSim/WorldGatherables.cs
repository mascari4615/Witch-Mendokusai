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
	/// <summary>줍기가 안 된 <b>이유</b> — 사람에게도 다르고, 고칠 자리도 다르다 (TASK-WM-220).</summary>
	public enum GatherDenial
	{
		NONE = 0,

		/// <summary>그런 자리가 없다(번호가 엉뚱하다).</summary>
		NO_SUCH_PLACE = 1,

		/// <summary>손이 안 닿는다 — 더 가까이 가면 된다.</summary>
		OUT_OF_REACH = 2,

		/// <summary>아직 다시 자라는 중이다 — 기다리면 된다(남이 방금 가져갔을 때도 이쪽).</summary>
		STILL_REGROWING = 3,
	}

	public sealed class WorldGatherables
	{
		/// <summary>주울 수 있는 거리 — 이보다 멀면 손이 안 닿는다(창이 우겨도).</summary>
		public const float REACH = 2.5f;

		private const int SPACING = 7;   // 몇 칸마다 하나쯤 서 있나
		private const int HALF_SPAN = 6; // 원점에서 몇 칸(격자 단위)까지 흩뿌리나

		private readonly object gate = new object();
		private readonly List<GatherableKind> kinds = new List<GatherableKind>();
		private readonly Dictionary<int, int> regrowAt = new Dictionary<int, int>();

		/// <summary>
		/// 손에 다 못 든 만큼 <b>도로 놓아 둔 개수</b> (TASK-WM-217) — 자리 번호 → 남은 개수.
		///
		/// ★ 왜 필요한가 (실측 2026-08-10): 3개짜리 자리를 가방에 1칸만 남기고 주웠더니 1개만 들어가고
		///   <b>2개가 증발했다</b>. 자리는 「있다/없다」 둘뿐이라 「2개만 남았다」를 적을 데가 없었기 때문이다.
		///   비었으면 그냥 사라지므로, 이 장부는 <b>덜 가져간 자리만큼만</b> 커진다.
		/// </summary>
		private readonly Dictionary<int, int> leftBehind = new Dictionary<int, int>();
		private readonly int seed;

		public WorldGatherables(IEnumerable<GatherableKind> kinds, int seed = 20260810)
		{
			this.seed = seed;
			if (kinds == null)
				return;

			foreach (GatherableKind kind in kinds)
			{
				// ⚠ 번호 0 을 「없음」으로 거르면 안 된다 (실측 2026-08-10): 게임의 <b>나무</b>가 0 이라
				//   씨앗 넷 중 하나가 조용히 빠진 채 몇 판을 돌았다. 없음은 null 로만 판단한다.
				if (kind == null)
					continue;

				this.kinds.Add(kind);
			}
		}

		/// <summary>몇 종류를 아는가 — 0 이면 세계에 주울 것이 없다.</summary>
		public int KindCount => kinds.Count;

		/// <summary>
		/// 들판이 바뀔 때마다 오르는 수 (TASK-WM-217) — 뽑히거나 다시 자라면 오른다.
		/// ★ 왜: 안 바뀐 들판(169자리)을 20Hz 로 나르면 그건 세계가 아니라 소음이다.
		///   창은 이 수가 그대로면 지난 그림을 그대로 쓴다.
		/// </summary>
		public int Version { get; private set; }

		/// <summary>
		/// 시간이 흘렀다 — <b>때가 된 것을 다시 세운다</b> (TASK-WM-217).
		///
		/// ★ 왜 따로 부르나 (실측 2026-08-10): 재생이 <c>Alive()</c> 안에서만 일어났다. 그런데 방송은
		///   「버전이 올랐을 때만」 <c>Alive()</c> 를 부른다 — <b>아무도 안 부르니 버전이 안 오르고,
		///   버전이 안 오르니 아무도 안 부른다.</b> 다시 자란 것이 창에 영영 안 돌아오는 자물쇠였다.
		///   그래서 재생은 <b>시간이 흐르는 자리</b>에서 굴린다(세계 시계가 부른다).
		/// </summary>
		public void Tick(int nowMinute)
		{
			lock (gate)
			{
				RegrowUnlocked(nowMinute);
			}
		}

		/// <summary>지금 서 있는 것들 — 뽑아 간 자리는 다시 자랄 때까지 빠진다.</summary>
		public List<GatherableNode> Alive(int nowMinute)
		{
			List<GatherableNode> alive = new List<GatherableNode>();
			if (kinds.Count == 0)
				return alive;

			lock (gate)
			{
				// 다시 자란 것이 있으면 그것도 「바뀐 것」이다 — 안 그러면 창에 영영 안 돌아온다.
				RegrowUnlocked(nowMinute);

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
			return TryTake(nodeId, fromX, fromZ, nowMinute, out itemId, out amount, out _);
		}

		/// <summary>
		/// 줍는다 — <b>안 되면 왜 안 되는지</b>도 말한다 (TASK-WM-220).
		///
		/// ★ 왜 이유를 가르나: 세 가지가 한 문장으로 뭉쳐 있었다 —
		///   「없는 자리 / 손이 안 닿음 / 아직 다시 자라는 중」. 사람에게도 다른 말이고,
		///   고칠 때도 다른 자리다. 뭉쳐 두면 관문이 빨개져도 어디를 봐야 할지 모른다
		///   (실제로 그 자리에서 하루를 썼다).
		/// </summary>
		public bool TryTake(int nodeId, float fromX, float fromZ, int nowMinute,
			out int itemId, out int amount, out GatherDenial why)
		{
			why = GatherDenial.NONE;
			itemId = 0;
			amount = 0;
			if (kinds.Count == 0)
			{
				why = GatherDenial.NO_SUCH_PLACE;
				return false;
			}

			if (Locate(nodeId, out int gx, out int gz) == false)
			{
				why = GatherDenial.NO_SUCH_PLACE;
				return false;
			}

			lock (gate)
			{
				if (regrowAt.TryGetValue(nodeId, out int at) && nowMinute < at)
				{
					why = GatherDenial.STILL_REGROWING;
					return false;
				}

				GatherableNode node = Describe(nodeId, gx, gz);
				float dx = node.X - fromX;
				float dz = node.Z - fromZ;
				if (dx * dx + dz * dz > REACH * REACH)
				{
					why = GatherDenial.OUT_OF_REACH;
					return false;
				}

				GatherableKind kind = KindOf(nodeId);
				itemId = node.ItemId;
				amount = node.Amount;

				// 남겨 뒀던 것을 다 가져가면 그 장부는 지운다 — 다음엔 온전한 자리로 다시 자란다.
				leftBehind.Remove(nodeId);
				regrowAt[nodeId] = kind.respawnMinutes > 0
					? nowMinute + kind.respawnMinutes
					: int.MaxValue; // 안 자라는 것은 영영 비어 있다
				Version++;
				return true;
			}
		}

		/// <summary>
		/// 뽑은 것을 <b>도로 세운다</b> (TASK-WM-217).
		///
		/// ★ 왜 필요한가: 가방이 꽉 차면 주운 것이 <b>그냥 사라졌다</b> — 자리는 비었는데 손에도 없다.
		///   사람 눈엔 「주웠는데 없어졌다」다. 못 받으면 세계로 되돌리는 게 맞다.
		/// </summary>
		public void Restore(int nodeId)
		{
			lock (gate)
			{
				bool changed = regrowAt.Remove(nodeId);
				changed |= leftBehind.Remove(nodeId);
				if (changed)
					Version++;
			}
		}

		/// <summary>
		/// 손에 다 못 들어서 <b>얼마만 도로 놓는다</b> (TASK-WM-217).
		///
		/// ★ 왜: 가방에 한 칸만 남았는데 3개짜리를 주우면, 전엔 1개만 들어가고 2개가 증발했다.
		///   그건 사람이 손해 보는 방향의 조용한 사고다 — 못 든 만큼은 그 자리에 그대로 있어야 한다.
		///   <paramref name="amount"/> 가 0 이하면 다 가져간 것과 같다(자리는 비어 있는 채로 둔다).
		/// </summary>
		public void RestorePartial(int nodeId, int amount)
		{
			if (amount <= 0)
				return;

			lock (gate)
			{
				regrowAt.Remove(nodeId);
				leftBehind[nodeId] = amount;
				Version++;
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

				// 덜 가져간 자리도 적어 둔다 — 세계가 잠들었다 깨면 남은 개수가 도로 늘어나면 안 된다.
				foreach (KeyValuePair<int, int> pair in leftBehind)
					saved.Add(new GatherTakenSaveEntry { nodeId = pair.Key, regrowAtMinute = 0, remaining = pair.Value });
			}

			return saved;
		}

		/// <summary>때가 된 자리를 장부에서 지운다 — 지워지면 다시 서 있는 것이 된다(버전도 오른다).</summary>
		private void RegrowUnlocked(int nowMinute)
		{
			List<int> regrown = null;
			foreach (KeyValuePair<int, int> pair in regrowAt)
			{
				if (nowMinute < pair.Value)
					continue;

				if (regrown == null)
					regrown = new List<int>();

				regrown.Add(pair.Key);
			}

			if (regrown == null)
				return;

			for (int i = 0; i < regrown.Count; i++)
				regrowAt.Remove(regrown[i]);

			Version++;
		}

		public void Load(IEnumerable<GatherTakenSaveEntry> saved)
		{
			lock (gate)
			{
				regrowAt.Clear();
				leftBehind.Clear();
				Version++;
				if (saved == null)
					return;

				foreach (GatherTakenSaveEntry entry in saved)
				{
					if (entry == null)
						continue;

					// 남은 개수가 적힌 줄은 「덜 가져간 자리」다 — 뽑힌 자리가 아니다.
					if (entry.remaining > 0)
					{
						leftBehind[entry.nodeId] = entry.remaining;
						continue;
					}

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
				Amount = leftBehind.TryGetValue(id, out int left)
					? left
					: (kind.amount < 1 ? 1 : kind.amount),
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

		/// <summary>덜 가져가서 그 자리에 남겨 둔 개수 (TASK-WM-217). 0 = 뽑힌 자리(옛 저장도 이 값).</summary>
		public int remaining;
	}
}
