using System.Collections.Generic;
using WitchMendokusai;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;
using NUnit.Framework;

namespace WitchMendokusai.Server.Tests
{
	/// <summary>광장에 몰려도 줄이 안 막히게 — 가까운 사람부터 몇 명까지 (TASK-WM-217).</summary>
	public class InterestCrowdTests
	{
		private static WorldDoll At(int id, float x, float z)
		{
			return new WorldDoll(id, new Vector3(x, 0f, z));
		}

		[Test]
		public void 상한보다_적으면_다_보낸다()
		{
			List<WorldDoll> people = new List<WorldDoll> { At(1, 0, 0), At(2, 5, 0) };

			WorldDoll[] seen = InterestCrowd.Nearest(people, new Vector3(0, 0, 0), 1, 48);

			Assert.That(seen.Length, Is.EqualTo(2));
		}

		[Test]
		public void 가까운_사람부터_채운다()
		{
			List<WorldDoll> people = new List<WorldDoll>
			{
				At(1, 0, 0), At(2, 30, 0), At(3, 2, 0), At(4, 10, 0),
			};

			WorldDoll[] seen = InterestCrowd.Nearest(people, new Vector3(0, 0, 0), 1, 3);

			Assert.That(System.Array.ConvertAll(seen, one => one.Id), Is.EqualTo(new[] { 1, 3, 4 }));
		}

		[Test]
		public void 내_인형은_아무리_멀어도_안_잘린다()
		{
			// 나는 저 멀리 있고, 상한은 1이다 — 그래도 내가 안 보이면 화면이 통째로 멎는다.
			List<WorldDoll> people = new List<WorldDoll> { At(7, 200, 200), At(1, 0, 0), At(2, 1, 0) };

			WorldDoll[] seen = InterestCrowd.Nearest(people, new Vector3(200, 0, 200), 7, 1);

			Assert.That(seen.Length, Is.EqualTo(1));
			Assert.That(seen[0].Id, Is.EqualTo(7));
		}

		[Test]
		public void 같은_거리면_번호_순이라_판마다_안_뒤바뀐다()
		{
			List<WorldDoll> people = new List<WorldDoll> { At(9, 5, 0), At(3, 5, 0), At(1, 0, 0), At(6, 5, 0) };

			WorldDoll[] first = InterestCrowd.Nearest(people, new Vector3(0, 0, 0), 1, 3);
			people.Reverse();
			WorldDoll[] second = InterestCrowd.Nearest(people, new Vector3(0, 0, 0), 1, 3);

			Assert.That(System.Array.ConvertAll(first, one => one.Id), Is.EqualTo(new[] { 1, 3, 6 }));
			Assert.That(System.Array.ConvertAll(second, one => one.Id), Is.EqualTo(System.Array.ConvertAll(first, one => one.Id)));
		}

		[Test]
		public void 광장에_이백명이_모여도_한_판은_상한만큼만_나간다()
		{
			List<WorldDoll> crowd = new List<WorldDoll>();
			for (int i = 0; i < 200; i++)
				crowd.Add(At(i, i % 10, i / 10));

			WorldDoll[] seen = InterestCrowd.Nearest(crowd, new Vector3(0, 0, 0), 0, InterestCrowd.MAX_VISIBLE_DOLLS);

			Assert.That(seen.Length, Is.EqualTo(InterestCrowd.MAX_VISIBLE_DOLLS));
		}
		[Test]
		public void 한_칸_사람은_공유_목록에서_안_잘린다()
		{
			// 칸에 선 사람 3명 + 옆에서 얼쩡대는 사람 10명, 상한은 5.
			List<WorldDoll> members = new List<WorldDoll> { At(1, 1, 1), At(2, 2, 2), At(3, 3, 3) };
			List<WorldDoll> candidates = new List<WorldDoll>(members);
			for (int i = 0; i < 10; i++)
				candidates.Add(At(100 + i, 20 + i, 20 + i));

			WorldDoll[] shared = InterestCrowd.SharedForCell(candidates, members, new Vector3(8, 0, 8), 5);

			Assert.That(shared.Length, Is.EqualTo(5));
			foreach (WorldDoll member in members)
				Assert.That(System.Array.Exists(shared, one => one.Id == member.Id), Is.True);
		}

		[Test]
		public void 칸에_상한보다_많이_모이면_공유를_포기한다()
		{
			// 공유 목록으로는 누군가 자기 인형을 못 찾게 된다 — 그때는 창마다 따로 골라야 한다.
			List<WorldDoll> members = new List<WorldDoll>();
			for (int i = 0; i < 6; i++)
				members.Add(At(i, i, 0));

			Assert.That(InterestCrowd.SharedForCell(members, members, new Vector3(8, 0, 8), 5), Is.Null);
		}

		[Test]
		public void 남는_자리는_한복판에_가까운_순으로_채운다()
		{
			List<WorldDoll> members = new List<WorldDoll> { At(1, 8, 8) };
			List<WorldDoll> candidates = new List<WorldDoll>(members) { At(5, 40, 40), At(6, 10, 10), At(7, 20, 20) };

			WorldDoll[] shared = InterestCrowd.SharedForCell(candidates, members, new Vector3(8, 0, 8), 3);

			Assert.That(System.Array.ConvertAll(shared, one => one.Id), Is.EqualTo(new[] { 1, 6, 7 }));
		}

		// ── 몰린 광장에서 움직이는 사람 (TASK-WM-227) ────────────────────────────

		[Test]
		public void 몰린_자리에서_한_발짝_물러난_사람이_안_사라진다()
		{
			// 광장에 200명이 같은 자리에 서 있고, 친구 하나가 한 발짝 물러났다.
			List<WorldDoll> people = new List<WorldDoll>();
			for (int i = 1; i <= 200; i++)
				people.Add(At(i, 0, 0));

			WorldDoll friend = At(201, 1.5f, 0);
			people.Add(friend);

			HashSet<int> moving = new HashSet<int> { friend.Id };
			WorldDoll[] seen = InterestCrowd.Nearest(people, new Vector3(0, 0, 0), 1, InterestCrowd.MAX_VISIBLE_DOLLS, moving);

			Assert.That(System.Array.Exists(seen, one => one.Id == friend.Id), Is.True,
				"거리로만 자르면 물러난 순간 꼴찌가 되어 사라진다 — 옆에 있는데 안 보인다");
			Assert.That(seen.Length, Is.EqualTo(InterestCrowd.MAX_VISIBLE_DOLLS), "상한은 그대로여야 한다");
		}

		[Test]
		public void 움직이는_사람이_많아도_상한을_안_넘고_가까운_순이다()
		{
			List<WorldDoll> people = new List<WorldDoll>();
			HashSet<int> moving = new HashSet<int>();
			for (int i = 1; i <= 200; i++)
			{
				people.Add(At(i, i, 0));
				moving.Add(i);
			}

			WorldDoll[] seen = InterestCrowd.Nearest(people, new Vector3(0, 0, 0), 1, InterestCrowd.MAX_VISIBLE_DOLLS, moving);

			Assert.That(seen.Length, Is.EqualTo(InterestCrowd.MAX_VISIBLE_DOLLS));
			Assert.That(System.Array.Exists(seen, one => one.Id == 1), Is.True, "나 자신은 늘 들어간다");
			Assert.That(System.Array.Exists(seen, one => one.Id == 200), Is.False, "제일 먼 사람까지 들어오면 상한이 무의미하다");
		}

		[Test]
		public void 가만히_선_사람도_남은_자리를_채운다()
		{
			// 떼어 두는 자리는 <b>일부</b>다 — 광장이 텅 빈 것처럼 보이면 그것도 고장이다.
			List<WorldDoll> people = new List<WorldDoll>();
			for (int i = 1; i <= 200; i++)
				people.Add(At(i, i * 0.1f, 0));

			HashSet<int> moving = new HashSet<int> { 199, 200 };
			WorldDoll[] seen = InterestCrowd.Nearest(people, new Vector3(0, 0, 0), 1, InterestCrowd.MAX_VISIBLE_DOLLS, moving);

			int standingNearby = 0;
			foreach (WorldDoll one in seen)
			{
				if (moving.Contains(one.Id) == false)
					standingNearby += 1;
			}

			Assert.That(standingNearby, Is.GreaterThanOrEqualTo(InterestCrowd.MAX_VISIBLE_DOLLS - InterestCrowd.SLOTS_FOR_MOVERS),
				"가만히 선 사람이 다 밀려나면 광장이 텅 빈 것처럼 보인다");
		}

		[Test]
		public void 아무도_안_움직이면_옛날과_똑같다()
		{
			List<WorldDoll> people = new List<WorldDoll>();
			for (int i = 1; i <= 100; i++)
				people.Add(At(i, i, 0));

			WorldDoll[] withNone = InterestCrowd.Nearest(people, new Vector3(0, 0, 0), 1, 10, new HashSet<int>());
			WorldDoll[] oldWay = InterestCrowd.Nearest(people, new Vector3(0, 0, 0), 1, 10);

			Assert.That(System.Array.ConvertAll(withNone, one => one.Id),
				Is.EqualTo(System.Array.ConvertAll(oldWay, one => one.Id)));
		}
	}
}
