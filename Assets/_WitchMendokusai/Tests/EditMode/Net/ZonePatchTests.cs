using NUnit.Framework;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 한 세계가 <b>맡은 땅</b> (TASK-WM-252).
	/// 어느 땅이 누구 것인가는 모든 세계가 똑같이 알아야 한다 — 한쪽만 달리 알면
	/// 사람이 두 세계에 동시에 있거나 어느 쪽에도 없게 된다.
	/// </summary>
	public class ZonePatchTests
	{
		[Test]
		public void 안_나눈_세계는_온_세상이_자기_땅이다()
		{
			ZonePatch all = ZonePatch.Everywhere;

			Assert.IsFalse(all.Bounded);
			Assert.IsTrue(all.Contains(new Vector3(99999f, 0f, -99999f)));
			Assert.AreEqual(12345f, all.Clamp(new Vector3(12345f, 0f, 0f)).x, "안 나눈 세계는 아무 데도 안 막는다");
			Assert.IsFalse(all.AtEdge(new Vector3(0f, 0f, 0f), 5f));
		}

		[Test]
		public void 내_땅_안팎을_가른다()
		{
			ZonePatch patch = new ZonePatch("동", -10f, -10f, 10f, 10f);

			Assert.IsTrue(patch.Contains(new Vector3(0f, 0f, 0f)));
			Assert.IsTrue(patch.Contains(new Vector3(10f, 0f, 10f)), "경계 위는 내 땅이다");
			Assert.IsFalse(patch.Contains(new Vector3(10.1f, 0f, 0f)));
			Assert.IsFalse(patch.Contains(new Vector3(0f, 0f, -10.1f)));
		}

		[Test]
		public void 밖으로_나가려_하면_경계에_세운다()
		{
			ZonePatch patch = new ZonePatch("동", -10f, -10f, 10f, 10f);

			Vector3 held = patch.Clamp(new Vector3(50f, 1f, -50f));

			Assert.AreEqual(10f, held.x);
			Assert.AreEqual(-10f, held.z);
			Assert.AreEqual(1f, held.y, "높이는 세계가 안 정한다 — 건드리지 않는다");
		}

		[Test]
		public void 경계에_붙었는지_안다()
		{
			ZonePatch patch = new ZonePatch("동", -10f, -10f, 10f, 10f);

			Assert.IsTrue(patch.AtEdge(new Vector3(9f, 0f, 0f), 2f));
			Assert.IsFalse(patch.AtEdge(new Vector3(0f, 0f, 0f), 2f));
		}

		[Test]
		public void 뒤집힌_땅도_바로_세워_읽는다()
		{
			ZonePatch patch = new ZonePatch("서", 10f, 10f, -10f, -10f);

			Assert.AreEqual(-10f, patch.FromX);
			Assert.AreEqual(10f, patch.ToX);
			Assert.IsTrue(patch.Contains(new Vector3(0f, 0f, 0f)));
		}

		[Test]
		public void 적어_둔_땅을_읽는다()
		{
			ZonePatch patch = ZonePatch.Read("동:-10,-10,10,10");

			Assert.AreEqual("동", patch.Name);
			Assert.IsTrue(patch.Bounded);
			Assert.IsTrue(patch.Contains(new Vector3(5f, 0f, 5f)));
			Assert.IsFalse(patch.Contains(new Vector3(15f, 0f, 0f)));
		}

		[Test]
		public void 못_읽는_것은_온_세상으로_친다()
		{
			// ⚠ 잘못 적힌 땅을 「아주 작은 땅」으로 읽으면 세계가 통째로 못 움직이게 된다.
			//   못 읽으면 <b>안 나눈 것</b>으로 보는 쪽이 안전하다.
			Assert.IsFalse(ZonePatch.Read("").Bounded);
			Assert.IsFalse(ZonePatch.Read(null).Bounded);
			Assert.IsFalse(ZonePatch.Read("동:1,2,3").Bounded);
			Assert.IsFalse(ZonePatch.Read("동:하나,둘,셋,넷").Bounded);
		}
	}
}
