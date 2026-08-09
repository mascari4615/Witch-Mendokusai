using NUnit.Framework;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 「누가 왔고 누가 갔나」 — 유령 몸·안 보이는 사람이 안 생기는지 (TASK-WM-217 단계 3).
	/// FishNet 스폰/디스폰이 하던 일을 우리가 대신하므로, 그 자리를 시험이 지킨다.
	/// </summary>
	public sealed class DollRosterTests
	{
		private static DollView Doll(int id, float x, float z) => new DollView { id = id, x = x, z = z };

		[Test]
		public void 처음_보인_인형은_왔다고_알린다()
		{
			DollRoster roster = new DollRoster();

			RosterChange change = roster.Sync(new[] { Doll(2, 1f, 2f) }, myDollId: 1);

			Assert.That(change.Appeared, Is.EqualTo(new[] { 2 }));
			Assert.That(change.Left, Is.Empty);
			Assert.That(roster.Count, Is.EqualTo(1));
		}

		[Test]
		public void 이미_있던_인형은_다시_왔다고_하지_않는다()
		{
			DollRoster roster = new DollRoster();
			roster.Sync(new[] { Doll(2, 0f, 0f) }, myDollId: 1);

			RosterChange change = roster.Sync(new[] { Doll(2, 5f, 5f) }, myDollId: 1);

			Assert.That(change.Appeared, Is.Empty);
			Assert.That(change.Left, Is.Empty);
			Assert.That(roster.TryGetPosition(2, out Vector3 position), Is.True);
			Assert.That(position.x, Is.EqualTo(5f));
			Assert.That(position.z, Is.EqualTo(5f));
		}

		[Test]
		public void 목록에서_빠진_인형은_갔다고_알리고_자리도_지운다()
		{
			DollRoster roster = new DollRoster();
			roster.Sync(new[] { Doll(2, 0f, 0f), Doll(3, 1f, 1f) }, myDollId: 1);

			RosterChange change = roster.Sync(new[] { Doll(3, 1f, 1f) }, myDollId: 1);

			Assert.That(change.Left, Is.EqualTo(new[] { 2 }));
			Assert.That(roster.TryGetPosition(2, out Vector3 _), Is.False);
			Assert.That(roster.Count, Is.EqualTo(1));
		}

		[Test]
		public void 내_인형은_대역을_세우지_않는다()
		{
			DollRoster roster = new DollRoster();

			RosterChange change = roster.Sync(new[] { Doll(1, 0f, 0f), Doll(2, 1f, 1f) }, myDollId: 1);

			Assert.That(change.Appeared, Is.EqualTo(new[] { 2 }));
			Assert.That(roster.TryGetPosition(1, out Vector3 _), Is.False);
		}

		[Test]
		public void 같은_번호가_두_번_와도_몸은_하나()
		{
			DollRoster roster = new DollRoster();

			RosterChange change = roster.Sync(new[] { Doll(2, 0f, 0f), Doll(2, 9f, 9f) }, myDollId: 1);

			Assert.That(change.Appeared, Is.EqualTo(new[] { 2 }));
			Assert.That(roster.Count, Is.EqualTo(1));
		}

		[Test]
		public void 빈_목록이_오면_전부_갔다고_알린다()
		{
			DollRoster roster = new DollRoster();
			roster.Sync(new[] { Doll(2, 0f, 0f), Doll(3, 0f, 0f) }, myDollId: 1);

			RosterChange change = roster.Sync(new DollView[0], myDollId: 1);

			Assert.That(change.Left, Is.EquivalentTo(new[] { 2, 3 }));
			Assert.That(roster.Count, Is.EqualTo(0));
		}

		[Test]
		public void 목록이_없어도_터지지_않는다()
		{
			DollRoster roster = new DollRoster();
			roster.Sync(new[] { Doll(2, 0f, 0f) }, myDollId: 1);

			RosterChange change = roster.Sync(null, myDollId: 1);

			Assert.That(change.Left, Is.EqualTo(new[] { 2 }));
			Assert.That(roster.Count, Is.EqualTo(0));
		}
	}

	/// <summary>걸음이 세계가 받아 줄 크기로 잘리는지 (TASK-WM-217 단계 3).</summary>
	public sealed class MoveIntentTests
	{
		[Test]
		public void 이미_도착했으면_보내지_않는다()
		{
			bool send = MoveIntent.TryStep(new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, 1f), WorldSim.MAX_STEP, out Vector3 delta);

			Assert.That(send, Is.False);
			Assert.That(delta.magnitude, Is.EqualTo(0f));
		}

		[Test]
		public void 먼_곳은_한_걸음_크기로_잘린다()
		{
			bool send = MoveIntent.TryStep(Vector3.zero, new Vector3(100f, 0f, 0f), WorldSim.MAX_STEP, out Vector3 delta);

			Assert.That(send, Is.True);
			Assert.That(delta.magnitude, Is.EqualTo(WorldSim.MAX_STEP).Within(0.0001f));
		}

		[Test]
		public void 가까운_곳은_그대로_간다()
		{
			bool send = MoveIntent.TryStep(Vector3.zero, new Vector3(0.5f, 0f, 0f), WorldSim.MAX_STEP, out Vector3 delta);

			Assert.That(send, Is.True);
			Assert.That(delta.x, Is.EqualTo(0.5f).Within(0.0001f));
		}

		[Test]
		public void 높이는_걸음에_섞이지_않는다()
		{
			MoveIntent.TryStep(new Vector3(0f, 10f, 0f), new Vector3(1f, -5f, 0f), WorldSim.MAX_STEP, out Vector3 delta);

			Assert.That(delta.y, Is.EqualTo(0f));
		}
	}
}
