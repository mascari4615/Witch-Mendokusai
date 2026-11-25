using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 전기가 건너갈 수 있는 적 하나. 죽은 적과 무적인 적은 부르는 쪽이 뺌
	public readonly struct LightningTarget
	{
		public readonly float X;
		public readonly float Z;
		// 폭발에 이미 맞음. 안 맞은 적이 모자랄 때만 고름
		public readonly bool Struck;

		public LightningTarget(float x, float z, bool struck)
		{
			X = x;
			Z = z;
			Struck = struck;
		}
	}

	// 전기 한 줄. From 이 BLAST 면 폭발 자리에서
	public readonly struct LightningHop
	{
		public readonly int From;
		public readonly int To;

		public LightningHop(int from, int to)
		{
			From = from;
			To = to;
		}
	}

	// 알리사 십자 수리검의 전기 정본. 폭발 하나에 한 번만 부름 (맞은 적마다 부르면 무리에서 수가 곱해짐)
	// - 이어짐 (Chain): 적에서 적으로 건너감. 십자 수리검 갈림길 B. 첫 줄은 폭발 가장자리부터 사거리를 잼
	// - 퍼짐 (Burst): 폭발 자리에서 가까운 적 여럿에게 동시에. 뇌격 마무리. 사거리는 폭발 중심에서
	// 안 맞은 적을 먼저 고르고, 모자라면 폭발에 맞은 적도 고름. 무리가 폭발 안에 다 들어와도 빈 카드가 되지 않게
	public static class LightningChainRules
	{
		public const int BLAST = -1;
		private const int NONE = -2;

		public static void Chain(float originX, float originZ, float blastRadius, IReadOnlyList<LightningTarget> targets, int maxHops, float range, List<LightningHop> result)
		{
			result.Clear();
			int from = BLAST;
			for (int i = 0; i < maxHops; i++)
			{
				float x = from == BLAST ? originX : targets[from].X;
				float z = from == BLAST ? originZ : targets[from].Z;
				int next = Pick(x, z, targets, from == BLAST ? blastRadius + range : range, result);
				if (next == NONE)
					return;
				result.Add(new LightningHop(from, next));
				from = next;
			}
		}

		public static void Burst(float originX, float originZ, IReadOnlyList<LightningTarget> targets, int maxTargets, float range, List<LightningHop> result)
		{
			result.Clear();
			for (int i = 0; i < maxTargets; i++)
			{
				int next = Pick(originX, originZ, targets, range, result);
				if (next == NONE)
					return;
				result.Add(new LightningHop(BLAST, next));
			}
		}

		// 전기 피해. 0 이 아닌 피해는 최소 1, 반은 올림
		public static int Damage(int blastDamage, int percent)
		{
			if (blastDamage <= 0 || percent <= 0)
				return 0;
			return Math.Max(1, (int)Math.Round((long)blastDamage * percent / 100d, MidpointRounding.AwayFromZero));
		}

		private static int Pick(float x, float z, IReadOnlyList<LightningTarget> targets, float range, List<LightningHop> taken)
		{
			int fresh = Nearest(x, z, targets, range, taken, false);
			return fresh != NONE ? fresh : Nearest(x, z, targets, range, taken, true);
		}

		// 사거리 끝은 닿음. 같은 거리면 앞 번호
		private static int Nearest(float x, float z, IReadOnlyList<LightningTarget> targets, float range, List<LightningHop> taken, bool struck)
		{
			if (range < 0f)
				return NONE;
			int nearest = NONE;
			float best = range * range;
			for (int i = 0; i < targets.Count; i++)
			{
				if (targets[i].Struck != struck || IsTaken(i, taken))
					continue;
				float offsetX = targets[i].X - x;
				float offsetZ = targets[i].Z - z;
				float distance = offsetX * offsetX + offsetZ * offsetZ;
				bool closer = nearest == NONE ? distance <= best : distance < best;
				if (closer == false)
					continue;
				best = distance;
				nearest = i;
			}
			return nearest;
		}

		private static bool IsTaken(int index, List<LightningHop> taken)
		{
			foreach (LightningHop hop in taken)
			{
				if (hop.To == index)
					return true;
			}
			return false;
		}
	}
}
