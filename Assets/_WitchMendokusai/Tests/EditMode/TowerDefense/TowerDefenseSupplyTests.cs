using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 보급 사슬 회귀 — 「넓히면 번다」를 「넓히면 지킬 것이 는다」로 바꾸는 규칙.
	/// 사슬이 잘못 이어지면 먼 노드가 공짜가 되고, 잘못 끊기면 정상 배치가 벌을 받는다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseSupplyTests
	{
		private static readonly Vector3 Core = Vector3.zero;
		private const float REACH = 5f;

		private static HashSet<int> Compute(params Vector3[] buildings)
		{
			HashSet<int> supplied = new();
			TowerDefenseSupply.Compute(Core, buildings, REACH, supplied);
			return supplied;
		}

		[Test]
		public void 코어_옆_건물은_이어진다()
		{
			HashSet<int> supplied = Compute(new Vector3(3f, 0f, 0f));

			Assert.IsTrue(supplied.Contains(0));
		}

		[Test]
		public void 너무_먼_건물은_안_이어진다()
		{
			HashSet<int> supplied = Compute(new Vector3(20f, 0f, 0f));

			Assert.IsFalse(supplied.Contains(0));
		}

		[Test]
		public void 징검다리로_이으면_멀어도_닿는다()
		{
			// 사슬의 본질 — 중간에 건물을 놓으면 먼 곳까지 보급이 흐른다.
			HashSet<int> supplied = Compute(
				new Vector3(4f, 0f, 0f),
				new Vector3(8f, 0f, 0f),
				new Vector3(12f, 0f, 0f));

			Assert.AreEqual(3, supplied.Count);
		}

		[Test]
		public void 중간이_비면_그_너머가_통째로_끊긴다()
		{
			// 「방어선을 길게 늘이면 어딘가는 얇아진다」 — 중간 하나가 부서지면 끝이 다 죽는다.
			HashSet<int> supplied = Compute(
				new Vector3(4f, 0f, 0f),
				new Vector3(30f, 0f, 0f),
				new Vector3(34f, 0f, 0f));

			Assert.IsTrue(supplied.Contains(0));
			Assert.IsFalse(supplied.Contains(1));
			Assert.IsFalse(supplied.Contains(2), "끊긴 너머는 저희끼리 이어져도 코어와는 무관하다.");
		}

		[Test]
		public void 건물이_없으면_아무것도_안_이어진다()
		{
			HashSet<int> supplied = new();
			TowerDefenseSupply.Compute(Core, new List<Vector3>(), REACH, supplied);

			Assert.AreEqual(0, supplied.Count);
		}

		[Test]
		public void 사거리0이면_사슬이_성립하지_않는다()
		{
			HashSet<int> supplied = new();
			TowerDefenseSupply.Compute(Core, new[] { new Vector3(1f, 0f, 0f) }, 0f, supplied);

			Assert.AreEqual(0, supplied.Count);
		}

		[Test]
		public void 전초기지가_새_보급_원점이_된다()
		{
			// 멀리 나간 채집이 코어까지 이어질 필요 없이 가까운 전초기지에 붙으면 된다 —
			// 「넓히면 지킬 곳이 늘지만 살림은 편해진다」의 교환.
			Vector3 outpost = new Vector3(40f, 0f, 0f);
			Vector3 farHarvester = new Vector3(43f, 0f, 0f);
			HashSet<int> supplied = new();

			TowerDefenseSupply.Compute(new[] { Core, outpost }, new[] { farHarvester }, REACH, supplied);

			Assert.IsTrue(supplied.Contains(0), "전초기지 옆인데 안 이어지면 전초기지의 의미가 없다.");
		}

		[Test]
		public void 시작점이_코어뿐이면_먼_채집은_여전히_끊긴다()
		{
			HashSet<int> supplied = new();

			TowerDefenseSupply.Compute(new[] { Core }, new[] { new Vector3(43f, 0f, 0f) }, REACH, supplied);

			Assert.AreEqual(0, supplied.Count);
		}

	}
}
