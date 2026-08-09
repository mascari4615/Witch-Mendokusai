using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Building;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 낙관 배치의 빠진 반쪽 (TASK-WM-217) — 세계가 거절한 것을 되돌리는 판정.
	/// 이게 없으면 내 화면에만 있는 유령 건물이 남는다(남에겐 빈 자리, 나는 거기 못 지음).
	/// </summary>
	public sealed class PlacementReconcilerTests
	{
		private static Vector3Int Cell(int x, int z) => new Vector3Int(x, 0, z);

		[Test]
		public void 세계가_인정하면_되돌리지_않는다()
		{
			PlacementReconciler reconciler = new PlacementReconciler();
			reconciler.Predicted(Cell(1, 1), now: 0f);

			IReadOnlyList<Vector3Int> rollback = reconciler.Reconcile(new[] { Cell(1, 1) }, now: 0.3f);

			Assert.That(rollback, Is.Empty);
			Assert.That(reconciler.PendingCount, Is.EqualTo(0));
		}

		[Test]
		public void 답이_늦어도_잠깐은_기다린다()
		{
			PlacementReconciler reconciler = new PlacementReconciler();
			reconciler.Predicted(Cell(2, 2), now: 0f);

			IReadOnlyList<Vector3Int> rollback = reconciler.Reconcile(new Vector3Int[0], now: 0.5f);

			Assert.That(rollback, Is.Empty);
			Assert.That(reconciler.PendingCount, Is.EqualTo(1));
		}

		[Test]
		public void 끝내_안_보이면_되돌린다()
		{
			PlacementReconciler reconciler = new PlacementReconciler();
			reconciler.Predicted(Cell(3, 3), now: 0f);

			IReadOnlyList<Vector3Int> rollback = reconciler.Reconcile(new Vector3Int[0], now: PlacementReconciler.GRACE_SECONDS + 0.1f);

			Assert.That(rollback, Is.EqualTo(new[] { Cell(3, 3) }));
			Assert.That(reconciler.PendingCount, Is.EqualTo(0), "한 번 되돌린 것을 또 되돌리지 않는다.");
		}

		[Test]
		public void 남이_지은_것은_내_것이_아니다()
		{
			PlacementReconciler reconciler = new PlacementReconciler();

			IReadOnlyList<Vector3Int> rollback = reconciler.Reconcile(new[] { Cell(9, 9) }, now: 10f);

			Assert.That(rollback, Is.Empty);
			Assert.That(reconciler.PendingCount, Is.EqualTo(0));
		}

		[Test]
		public void 같은_자리를_다시_누르면_시계도_다시_잰다()
		{
			PlacementReconciler reconciler = new PlacementReconciler();
			reconciler.Predicted(Cell(4, 4), now: 0f);
			reconciler.Predicted(Cell(4, 4), now: PlacementReconciler.GRACE_SECONDS);

			IReadOnlyList<Vector3Int> rollback = reconciler.Reconcile(new Vector3Int[0], now: PlacementReconciler.GRACE_SECONDS + 0.1f);

			Assert.That(rollback, Is.Empty, "직전 요청이 늦는 중일 수 있다 — 다시 기다려 준다.");
		}

		[Test]
		public void 여러_자리를_한꺼번에_판정한다()
		{
			PlacementReconciler reconciler = new PlacementReconciler();
			reconciler.Predicted(Cell(1, 0), now: 0f);
			reconciler.Predicted(Cell(2, 0), now: 0f);
			reconciler.Predicted(Cell(3, 0), now: 5f);

			IReadOnlyList<Vector3Int> rollback = reconciler.Reconcile(new[] { Cell(1, 0) }, now: 5f);

			Assert.That(rollback, Is.EqualTo(new[] { Cell(2, 0) }));
			Assert.That(reconciler.PendingCount, Is.EqualTo(1), "방금 세운 것은 아직 기다린다.");
		}
	}
}
