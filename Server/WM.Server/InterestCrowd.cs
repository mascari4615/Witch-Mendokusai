using System;
using System.Collections.Generic;
using WitchMendokusai;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// <b>광장에 사람이 몰려도 줄이 안 막히게</b> — 가까운 사람부터 몇 명까지만 보낸다 (TASK-WM-217).
	///
	/// ★ 왜 필요한가 (실측 2026-08-12): 200명이 같은 자리에 모이면 <b>모두가 모두를 본다</b> —
	///   알림이 사람 수의 <b>제곱</b>으로 커진다. 실제로 쟀더니 초당 27MB 였다(사람 200 · 판 하나 10.8KB).
	///   사람이 두 배면 넷 배가 되는 모양이라, 반경만으로는 광장을 못 버틴다.
	///
	/// ★ 왜 「가까운 순」인가: 멀리 있는 남이 한 프레임 늦게 보이는 것은 사람이 못 느낀다.
	///   바로 옆 사람이 늦게 보이는 것은 곧바로 느낀다. 그래서 자를 곳은 <b>먼 쪽</b>이다.
	///
	/// 규칙 셋: ① 나 자신은 언제나 들어간다(내가 안 보이면 화면이 통째로 멎는다)
	///          ② 가까운 순으로 채운다 ③ 같은 거리면 번호 순(판마다 뒤바뀌면 화면이 깜빡인다)
	/// </summary>
	public static class InterestCrowd
	{
		/// <summary>한 창에 한 번에 보낼 사람 수 상한 — 나를 포함해서 센다.</summary>
		public const int MAX_VISIBLE_DOLLS = 48;

		/// <summary>가까운 사람부터 <paramref name="limit"/> 명까지. 나(viewer)는 늘 들어간다.</summary>
		public static WorldDoll[] Nearest(IReadOnlyList<WorldDoll> candidates, Vector3 viewer, int viewerDollId, int limit)
		{
			if (candidates == null)
				return Array.Empty<WorldDoll>();

			if (limit <= 0 || candidates.Count <= limit)
			{
				WorldDoll[] all = new WorldDoll[candidates.Count];
				for (int i = 0; i < candidates.Count; i++)
					all[i] = candidates[i];

				return all;
			}

			List<WorldDoll> sorted = new List<WorldDoll>(candidates);
			sorted.Sort((left, right) =>
			{
				// 나는 언제나 맨 앞 — 상한이 아무리 낮아도 내 인형은 안 잘린다.
				if (left.Id == viewerDollId)
					return right.Id == viewerDollId ? 0 : -1;
				if (right.Id == viewerDollId)
					return 1;

				int byDistance = DistanceSquared(left, viewer).CompareTo(DistanceSquared(right, viewer));
				if (byDistance != 0)
					return byDistance;

				// 같은 거리 = 번호 순. 안 그러면 판마다 누가 잘릴지 뒤바뀌어 화면이 깜빡인다.
				return left.Id.CompareTo(right.Id);
			});

			WorldDoll[] nearest = new WorldDoll[limit];
			for (int i = 0; i < limit; i++)
				nearest[i] = sorted[i];

			return nearest;
		}

		private static float DistanceSquared(WorldDoll doll, Vector3 viewer)
		{
			float deltaX = doll.Position.x - viewer.x;
			float deltaZ = doll.Position.z - viewer.z;
			return deltaX * deltaX + deltaZ * deltaZ;
		}
	}
}
