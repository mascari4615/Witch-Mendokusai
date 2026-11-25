using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 전기가 건너갈 적 하나의 자리 (바닥 평면)
	public readonly struct LightningPoint
	{
		public readonly float X;
		public readonly float Z;

		public LightningPoint(float x, float z)
		{
			X = x;
			Z = z;
		}
	}

	// 전기 한 줄. From 이 NO_POINT 면 폭발 자리에서
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

	// 알리사 십자 수리검의 전기 연쇄 정본
	// - 이어짐 (Chain): 적에서 적으로 건너감. 십자 수리검 갈림길 B
	// - 퍼짐 (Burst): 폭발 자리에서 가까운 적 여럿에게 동시에. 뇌격 마무리
	// 폭발에 직접 맞은 적 (used) 은 다시 맞히지 않음. 폭발 하나에 한 번만 부름 (맞은 적마다 부르면 무리에서 수가 곱해짐)
	public static class LightningChainRules
	{
		public const int NO_POINT = -1;

		public static void Chain(LightningPoint origin, IReadOnlyList<LightningPoint> foes, bool[] used, int maxHops, float range, List<LightningHop> result)
		{
			result.Clear();
			LightningPoint from = origin;
			int fromIndex = NO_POINT;
			for (int i = 0; i < maxHops; i++)
			{
				int next = Nearest(from, foes, used, range);
				if (next == NO_POINT)
					return;
				used[next] = true;
				result.Add(new LightningHop(fromIndex, next));
				from = foes[next];
				fromIndex = next;
			}
		}

		public static void Burst(LightningPoint origin, IReadOnlyList<LightningPoint> foes, bool[] used, int maxTargets, float range, List<LightningHop> result)
		{
			result.Clear();
			for (int i = 0; i < maxTargets; i++)
			{
				int next = Nearest(origin, foes, used, range);
				if (next == NO_POINT)
					return;
				used[next] = true;
				result.Add(new LightningHop(NO_POINT, next));
			}
		}

		// 전기 피해. 0 이 아닌 피해는 최소 1
		public static int Damage(int blastDamage, int percent)
		{
			if (blastDamage <= 0 || percent <= 0)
				return 0;
			return Math.Max(1, (int)Math.Round(blastDamage * percent / 100f));
		}

		// 사거리 끝은 닿음. 같은 거리면 앞 번호
		private static int Nearest(LightningPoint from, IReadOnlyList<LightningPoint> foes, bool[] used, float range)
		{
			int nearest = NO_POINT;
			float best = range * range;
			for (int i = 0; i < foes.Count; i++)
			{
				if (used[i])
					continue;
				float x = foes[i].X - from.X;
				float z = foes[i].Z - from.Z;
				float distance = x * x + z * z;
				bool closer = nearest == NO_POINT ? distance <= best : distance < best;
				if (closer == false)
					continue;
				best = distance;
				nearest = i;
			}
			return nearest;
		}
	}
}
