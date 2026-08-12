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

		/// <summary>
		/// 그중 <b>움직이는 사람</b>에게 떼어 두는 자리 (TASK-WM-227).
		///
		/// ★ 왜 필요한가 (실측 2026-08-12): 광장에 200명이 한자리에 모이면 「가까운 48명」은
		///   전부 <b>가만히 선 사람</b>으로 찬다. 그 상태에서 친구가 한 발짝 물러나면 그 순간
		///   거리 순위가 꼴찌가 되어 <b>화면에서 사라진다</b> — 옆에 있는데 안 보인다.
		///   진짜 시험에서 200명일 때 걷는 사람이 <b>한 판도</b> 안 실렸다.
		///
		/// ★ 왜 「움직임」인가: 가만히 선 48명은 한 번 받으면 그다음 판에 아무 소식도 없다(안 바뀌니까).
		///   자리는 차지하되 정보는 0 이다. 움직이는 사람은 매 판 새 소식이다 — 같은 한 자리가
		///   훨씬 값지다. 그래서 거리로만 자르지 않고, 움직이는 쪽에 자리를 떼어 둔다.
		/// </summary>
		public const int SLOTS_FOR_MOVERS = MAX_VISIBLE_DOLLS / 3;

		/// <summary>가까운 사람부터 <paramref name="limit"/> 명까지. 나(viewer)는 늘 들어간다.</summary>
		public static WorldDoll[] Nearest(IReadOnlyList<WorldDoll> candidates, Vector3 viewer, int viewerDollId, int limit)
		{
			return Nearest(candidates, viewer, viewerDollId, limit, null);
		}

		/// <summary>
		/// 가까운 사람부터 <paramref name="limit"/> 명까지. 단 <paramref name="moving"/> 인 사람에게는
		/// <see cref="SLOTS_FOR_MOVERS"/> 만큼 자리를 <b>떼어 둔다</b> — 몰린 자리에서 움직이는 사람이
		/// 통째로 잘려 나가지 않게(위 § 참조). 나(viewer)는 늘 들어간다.
		/// </summary>
		public static WorldDoll[] Nearest(IReadOnlyList<WorldDoll> candidates, Vector3 viewer, int viewerDollId, int limit, ISet<int> moving)
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

			// 떼어 둘 자리가 없으면(움직이는 사람을 안 알려 줬으면) 옛날처럼 거리 순으로만 자른다.
			if (moving == null || moving.Count == 0)
			{
				WorldDoll[] justNearest = new WorldDoll[limit];
				for (int i = 0; i < limit; i++)
					justNearest[i] = sorted[i];

				return justNearest;
			}

			int keptForMovers = SLOTS_FOR_MOVERS < limit ? SLOTS_FOR_MOVERS : limit;
			HashSet<int> taken = new HashSet<int>();
			List<WorldDoll> chosen = new List<WorldDoll>(limit);

			// ① 먼저 움직이는 사람을 가까운 순으로 떼어 둔 자리만큼 (나 자신은 이미 맨 앞이다).
			for (int i = 0; i < sorted.Count && chosen.Count < keptForMovers; i++)
			{
				WorldDoll one = sorted[i];
				if (one.Id != viewerDollId && moving.Contains(one.Id) == false)
					continue;

				if (taken.Add(one.Id))
					chosen.Add(one);
			}

			// ② 남은 자리는 가까운 순으로 채운다 — 가만히 선 사람도 봐야 광장이 광장이다.
			for (int i = 0; i < sorted.Count && chosen.Count < limit; i++)
			{
				if (taken.Add(sorted[i].Id))
					chosen.Add(sorted[i]);
			}

			return chosen.ToArray();
		}

		/// <summary>
		/// 한 칸(interest cell) 사람들이 <b>같이 쓸</b> 보이는 목록 — 한 번 만들어 여럿에게 보낸다.
		///
		/// ★ 왜: 지금은 창 하나마다 목록을 고르고 글(JSON)을 새로 짓는다. 사람 400명이면
		///   그 일을 400번 하고, 400번 다 거의 같은 글이다(같은 칸에 서 있으면 보는 것도 거의 같다).
		///
		/// ★ 안 잘리는 것: <b>이 칸에 서 있는 사람은 전부 들어간다.</b> 그래야 각자 자기 인형을
		///   목록에서 찾는다 — 못 찾으면 그 창은 화면이 통째로 멎는다. 남는 자리는 가까운 순으로 채운다.
		///   칸 사람 수가 상한을 넘으면 <c>null</c> — 그때는 창마다 따로 골라야 한다(공유 불가).
		/// </summary>
		public static WorldDoll[] SharedForCell(IReadOnlyList<WorldDoll> candidates, IReadOnlyList<WorldDoll> cellMembers, Vector3 cellCenter, int limit)
		{
			if (candidates == null || cellMembers == null)
				return null;

			if (cellMembers.Count > limit)
				return null;

			HashSet<int> taken = new HashSet<int>();
			List<WorldDoll> chosen = new List<WorldDoll>(limit);
			for (int i = 0; i < cellMembers.Count; i++)
			{
				if (taken.Add(cellMembers[i].Id))
					chosen.Add(cellMembers[i]);
			}

			if (chosen.Count >= limit)
				return chosen.ToArray();

			List<WorldDoll> others = new List<WorldDoll>();
			for (int i = 0; i < candidates.Count; i++)
			{
				if (taken.Contains(candidates[i].Id) == false)
					others.Add(candidates[i]);
			}

			others.Sort((left, right) =>
			{
				int byDistance = DistanceSquared(left, cellCenter).CompareTo(DistanceSquared(right, cellCenter));
				return byDistance != 0 ? byDistance : left.Id.CompareTo(right.Id);
			});

			for (int i = 0; i < others.Count && chosen.Count < limit; i++)
				chosen.Add(others[i]);

			return chosen.ToArray();
		}

		private static float DistanceSquared(WorldDoll doll, Vector3 viewer)
		{
			float deltaX = doll.Position.x - viewer.x;
			float deltaZ = doll.Position.z - viewer.z;
			return deltaX * deltaX + deltaZ * deltaZ;
		}
	}
}
