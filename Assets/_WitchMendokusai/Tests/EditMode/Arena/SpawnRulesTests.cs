using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 스폰 겹침 판정 회귀. 순수 함수라 MonoBehaviour/물리 없이 검증.
	///
	/// 왜 이 검사가 있는가: 겹쳐 세운 캡슐 둘은 물리가 밀어낼 방향을 못 찾아 값이 튄다.
	/// 실측으로 유닛이 맵 밖까지 날아가 **죽지도 않고 남아** 매치가 안 끝난 적이 있다 —
	/// 화면에선 「다 잡았는데 안 넘어간다」로만 보이는, 원인 짐작이 어려운 부류다.
	/// </summary>
	public class SpawnRulesTests
	{
		private static List<Vector3> Points(params float[] xs)
		{
			List<Vector3> result = new();
			foreach (float x in xs)
				result.Add(new Vector3(x, 0f, 0f));
			return result;
		}

		[Test]
		public void 충분히_떨어져_있으면_통과()
		{
			Assert.IsFalse(SpawnRules.TryFindOverlap(Points(-5f, 0f, 5f), 1f, out _, out _));
		}

		[Test]
		public void 같은_자리는_잡는다()
		{
			Assert.IsTrue(SpawnRules.TryFindOverlap(Points(0f, 0f), 1f, out int first, out int second));
			Assert.AreEqual(0, first);
			Assert.AreEqual(1, second);
		}

		// 「완전히 같은 자리」만 잡으면 부족하다 — 0.01 만큼 떨어진 둘도 물리적으로는 겹친 것이다.
		[Test]
		public void 아주_가까운_것도_겹침으로_본다()
		{
			Assert.IsTrue(SpawnRules.TryFindOverlap(Points(0f, 0.01f), 1f, out _, out _));
		}

		// 맵이 여백을 폭보다 크게 잡으면 스폰이 한 점으로 모인다 — 그 판을 시작 전에 거절하는 게 목적.
		[Test]
		public void 한_점으로_모인_스폰을_잡는다()
		{
			Assert.IsTrue(SpawnRules.TryFindOverlap(Points(0f, 0f, 0f), 1f, out _, out _));
		}

		[Test]
		public void 겹치는_쌍이_뒤쪽에_있어도_찾는다()
		{
			Assert.IsTrue(SpawnRules.TryFindOverlap(Points(-9f, 0f, 9f, 9f), 1f, out int first, out int second));
			Assert.AreEqual(2, first);
			Assert.AreEqual(3, second);
		}

		[Test]
		public void 최소거리_0_이하면_검사하지_않는다()
		{
			Assert.IsFalse(SpawnRules.TryFindOverlap(Points(0f, 0f), 0f, out _, out _), "0 = 끄는 탈출구");
			Assert.IsFalse(SpawnRules.TryFindOverlap(Points(0f, 0f), -1f, out _, out _));
		}


		// ── 팀 *사이* 겹침 ────────────────────────────────────────────────────────────
		//
		// ★ 왜 따로 필요한가: 팀 안 검사만 하면 **가장 나쁜 경우를 통째로 놓친다.**
		//   RectangleArenaMap 은 스폰 z 를 `±(Length/2 - SpawnInset)` 로 잡으므로 SpawnInset 이
		//   Length/2 가 되는 순간 두 팀이 **같은 z=0 한 줄**에 서고 팀0 i번째와 팀1 i번째가 같은 점이 된다.
		//   그런데 각 팀은 여전히 X 로 잘 퍼져 있어 팀 안 검사엔 아무 이상도 안 보인다.

		[Test]
		public void 두_팀이_같은_줄에_서면_잡는다()
		{
			// SpawnInset == Length/2 일 때의 실제 모양 — 두 팀이 z=0 에서 X 좌표까지 똑같다.
			List<Vector3> teamZero = Points(-7f, 0f, 7f);
			List<Vector3> teamOne = Points(-7f, 0f, 7f);

			Assert.IsFalse(SpawnRules.TryFindOverlap(teamZero, 1f, out _, out _),
				"팀 안에서는 멀쩡하다 — 이게 팀 단위 검사가 놓치는 이유다");
			Assert.IsTrue(SpawnRules.TryFindOverlapAcross(teamZero, teamOne, 1f, out int first, out int second));
			Assert.AreEqual(0, first);
			Assert.AreEqual(0, second);
		}

		[Test]
		public void 마주보는_두_팀은_통과한다()
		{
			List<Vector3> teamZero = new() { new Vector3(-7f, 0f, -13f), new Vector3(7f, 0f, -13f) };
			List<Vector3> teamOne = new() { new Vector3(-7f, 0f, 13f), new Vector3(7f, 0f, 13f) };

			Assert.IsFalse(SpawnRules.TryFindOverlapAcross(teamZero, teamOne, 1f, out _, out _),
				"기본 배치(±13)는 통과해야 한다 — 여기서 걸리면 정상 맵이 시작을 못 한다");
		}

		[Test]
		public void 팀사이_검사도_null_과_0_이하를_방어한다()
		{
			Assert.IsFalse(SpawnRules.TryFindOverlapAcross(null, Points(0f), 1f, out _, out _));
			Assert.IsFalse(SpawnRules.TryFindOverlapAcross(Points(0f), null, 1f, out _, out _));
			Assert.IsFalse(SpawnRules.TryFindOverlapAcross(Points(0f), Points(0f), 0f, out _, out _), "0 = 끄는 탈출구");
		}

		[Test]
		public void 스폰이_null_이거나_하나면_겹칠_수_없다()
		{
			Assert.IsFalse(SpawnRules.TryFindOverlap(null, 1f, out _, out _));
			Assert.IsFalse(SpawnRules.TryFindOverlap(Points(3f), 1f, out _, out _));
		}
	}
}
