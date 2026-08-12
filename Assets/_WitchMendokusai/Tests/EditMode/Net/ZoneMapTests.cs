using NUnit.Framework;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 옆 세계가 어디에 있나 (TASK-WM-254).
	/// 이 지도는 모든 세계가 똑같이 알아야 한다 — 한쪽만 달리 알면 사람이 두 세계에 동시에 있거나
	/// 어느 쪽에도 없게 된다.
	/// </summary>
	public class ZoneMapTests
	{
		[Test]
		public void 이웃이_없으면_아무도_안_맡는다()
		{
			Assert.IsFalse(ZoneMap.Alone.TryOwner(new Vector3(100f, 0f, 0f), out _, out _));
		}

		[Test]
		public void 그_자리를_맡은_이웃을_찾는다()
		{
			ZoneMap map = ZoneMap.Read("서:-30,-10,-10,10=ws://one/ws;북:-10,10,10,30=ws://two/ws");

			Assert.IsTrue(map.TryOwner(new Vector3(-20f, 0f, 0f), out string name, out string address));
			Assert.AreEqual("서", name);
			Assert.AreEqual("ws://one/ws", address);

			Assert.IsTrue(map.TryOwner(new Vector3(0f, 0f, 20f), out string other, out _));
			Assert.AreEqual("북", other);
		}

		[Test]
		public void 아무_이웃도_안_맡은_자리는_없다고_한다()
		{
			ZoneMap map = ZoneMap.Read("서:-30,-10,-10,10=ws://one/ws");

			Assert.IsFalse(map.TryOwner(new Vector3(500f, 0f, 500f), out _, out _),
				"아무도 안 맡은 자리로 사람을 보내면 그 사람은 사라진다");
		}

		[Test]
		public void 잘못_적힌_조각은_건너뛰고_나머지는_산다()
		{
			// 하나가 잘못 적혔다고 나머지 이웃까지 잃으면 그 경계는 통째로 벽이 된다.
			ZoneMap map = ZoneMap.Read("망가짐;서:-30,-10,-10,10=ws://one/ws;주소없음:0,0,1,1");

			Assert.AreEqual(1, map.Count);
			Assert.IsTrue(map.TryOwner(new Vector3(-20f, 0f, 0f), out _, out string address));
			Assert.AreEqual("ws://one/ws", address);
		}

		[Test]
		public void 빈_것을_읽어도_안_터진다()
		{
			Assert.AreEqual(0, ZoneMap.Read(null).Count);
			Assert.AreEqual(0, ZoneMap.Read("").Count);
		}
	}
}
