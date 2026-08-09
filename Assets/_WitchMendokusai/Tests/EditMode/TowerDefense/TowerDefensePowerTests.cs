using System.Collections.Generic;
using NUnit.Framework;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 전기 회귀 — 「범위 안 + 용량 남음」 둘 다 맞아야 돈다. 둘 중 하나만 보면
	/// 「덮였는데 왜 안 돌지」 또는 「용량은 남는데 왜 안 오지」가 미궁이 된다. TASK-WM-194.
	/// </summary>
	public class TowerDefensePowerTests
	{
		private static HashSet<int> Compute(
			IReadOnlyList<TowerDefensePower.Source> sources,
			IReadOnlyList<TowerDefensePower.Consumer> consumers)
		{
			HashSet<int> powered = new();
			TowerDefensePower.Compute(sources, consumers, powered);
			return powered;
		}

		[Test]
		public void 범위_안이고_용량이_남으면_돈다()
		{
			HashSet<int> powered = Compute(
				new[] { new TowerDefensePower.Source(Vector3.zero, 10f, 5) },
				new[] { new TowerDefensePower.Consumer(new Vector3(3f, 0f, 0f), 1) });

			Assert.IsTrue(powered.Contains(0));
		}

		[Test]
		public void 범위_밖이면_안_돈다()
		{
			HashSet<int> powered = Compute(
				new[] { new TowerDefensePower.Source(Vector3.zero, 5f, 99) },
				new[] { new TowerDefensePower.Consumer(new Vector3(20f, 0f, 0f), 1) });

			Assert.IsFalse(powered.Contains(0), "용량이 남아도 안 덮였으면 못 받는다.");
		}

		[Test]
		public void 용량이_다_차면_뒤엣것이_선다()
		{
			// 「전기 부족하면 건물 정지」의 실체 — 자리에 있어도 못 받으면 멈춘다.
			HashSet<int> powered = Compute(
				new[] { new TowerDefensePower.Source(Vector3.zero, 20f, 2) },
				new[]
				{
					new TowerDefensePower.Consumer(new Vector3(1f, 0f, 0f), 1),
					new TowerDefensePower.Consumer(new Vector3(2f, 0f, 0f), 1),
					new TowerDefensePower.Consumer(new Vector3(3f, 0f, 0f), 1),
				});

			Assert.AreEqual(2, powered.Count, "용량 2 인데 셋이 다 돌면 전기가 의미가 없다.");
		}

		[Test]
		public void 발전을_더_세우면_더_돈다()
		{
			HashSet<int> powered = Compute(
				new[]
				{
					new TowerDefensePower.Source(Vector3.zero, 20f, 1),
					new TowerDefensePower.Source(new Vector3(4f, 0f, 0f), 20f, 2),
				},
				new[]
				{
					new TowerDefensePower.Consumer(new Vector3(1f, 0f, 0f), 1),
					new TowerDefensePower.Consumer(new Vector3(2f, 0f, 0f), 1),
					new TowerDefensePower.Consumer(new Vector3(3f, 0f, 0f), 1),
				});

			Assert.AreEqual(3, powered.Count);
		}

		[Test]
		public void 전기가_필요_없는_것은_항상_돈다()
		{
			// 벽·함정처럼 먹지 않는 것까지 멈추면 규칙이 아니라 벌칙이 된다.
			HashSet<int> powered = Compute(
				System.Array.Empty<TowerDefensePower.Source>(),
				new[] { new TowerDefensePower.Consumer(new Vector3(500f, 0f, 0f), 0) });

			Assert.IsTrue(powered.Contains(0));
		}

		[Test]
		public void 공급이_아예_없으면_전부_선다()
		{
			HashSet<int> powered = Compute(
				System.Array.Empty<TowerDefensePower.Source>(),
				new[]
				{
					new TowerDefensePower.Consumer(Vector3.zero, 1),
					new TowerDefensePower.Consumer(Vector3.one, 1),
				});

			Assert.AreEqual(0, powered.Count);
		}

		[Test]
		public void 목록_순서가_결과를_바꾸지_않는다()
		{
			// 배분이 순서에 흔들리면 건물 하나를 팔았다 지을 때마다 엉뚱한 것이 멈춘다.
			TowerDefensePower.Source[] sources =
			{
				new TowerDefensePower.Source(Vector3.zero, 6f, 1),
				new TowerDefensePower.Source(new Vector3(30f, 0f, 0f), 6f, 1),
			};

			HashSet<int> forward = Compute(sources, new[]
			{
				new TowerDefensePower.Consumer(new Vector3(1f, 0f, 0f), 1),
				new TowerDefensePower.Consumer(new Vector3(29f, 0f, 0f), 1),
			});
			HashSet<int> reversed = Compute(new[] { sources[1], sources[0] }, new[]
			{
				new TowerDefensePower.Consumer(new Vector3(1f, 0f, 0f), 1),
				new TowerDefensePower.Consumer(new Vector3(29f, 0f, 0f), 1),
			});

			Assert.AreEqual(forward.Count, reversed.Count);
			Assert.AreEqual(2, forward.Count);
		}

		[Test]
		public void 총량과_총요구를_알려준다()
		{
			Assert.AreEqual(7, TowerDefensePower.TotalCapacity(new[]
			{
				new TowerDefensePower.Source(Vector3.zero, 1f, 3),
				new TowerDefensePower.Source(Vector3.zero, 1f, 4),
			}));

			Assert.AreEqual(5, TowerDefensePower.TotalDemand(new[]
			{
				new TowerDefensePower.Consumer(Vector3.zero, 2),
				new TowerDefensePower.Consumer(Vector3.zero, 3),
				new TowerDefensePower.Consumer(Vector3.zero, 0),
			}));
		}
	}
}
