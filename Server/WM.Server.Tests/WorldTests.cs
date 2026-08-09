using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.Server.Tests
{
	/// <summary>
	/// 서버가 굴리는 세계의 규칙 (TASK-WM-216).
	/// 핵심은 하나 — <b>클라가 보낸 값을 그대로 믿지 않는다.</b>
	/// </summary>
	public sealed class WorldTests
	{
		[Test]
		public void 들어오면_인형을_하나_받는다()
		{
			World world = new World();

			Doll first = world.Join();
			Doll second = world.Join();

			Assert.AreNotEqual(first.Id, second.Id, "번호는 겹치지 않는다");
			Assert.AreEqual(2, world.Snapshot().Length);
		}

		[Test]
		public void 나가면_세계에서_사라진다()
		{
			World world = new World();
			Doll doll = world.Join();

			world.Leave(doll.Id);

			Assert.AreEqual(0, world.Snapshot().Length);
		}

		[Test]
		public void 한_번에_갈_수_있는_거리를_넘으면_잘린다()
		{
			World world = new World();
			Doll doll = world.Join();

			world.TryMove(doll.Id, new Vector3(100f, 0f, 0f));

			Assert.AreEqual(World.MAX_STEP, doll.Position.x, 0.001f, "순간이동은 공짜가 아니다");
		}

		[Test]
		public void 상한_안쪽_움직임은_그대로_간다()
		{
			World world = new World();
			Doll doll = world.Join();

			world.TryMove(doll.Id, new Vector3(0.5f, 0f, 0.5f));

			Assert.AreEqual(0.5f, doll.Position.x, 0.001f);
			Assert.AreEqual(0.5f, doll.Position.z, 0.001f);
		}

		[Test]
		public void 없는_인형을_움직이라_하면_거절한다()
		{
			World world = new World();

			Assert.IsFalse(world.TryMove(999, new Vector3(1f, 0f, 0f)));
		}

		[Test]
		public void 훑는_동안_들락거려도_터지지_않는다()
		{
			// 실제로 이걸로 서버가 죽었다 — 알림 루프가 훑는 도중 접속·퇴장이 겹쳤다.
			World world = new World();
			for (int i = 0; i < 50; i++)
				world.Join();

			Task churn = Task.Run(() =>
			{
				for (int i = 0; i < 2000; i++)
				{
					Doll doll = world.Join();
					world.Leave(doll.Id);
				}
			});

			for (int i = 0; i < 2000; i++)
			{
				Doll[] snapshot = world.Snapshot();
				foreach (Doll doll in snapshot)
					Assert.IsNotNull(doll, "사본에 빈 자리가 섞이면 안 된다");
			}

			Assert.DoesNotThrow(() => churn.Wait());
		}
	}

	/// <summary>
	/// 계약이 갈라지지 않았나 (TASK-WM-216).
	/// 웹이 쓰는 타입 선언은 서버가 뽑아낸다 — 손으로 고치면 이 시험이 잡는다.
	/// </summary>
	public sealed class ProtocolTests
	{
		/// <summary>줄 끝 차이(윈도우/리눅스)로 갈라졌다고 오판하지 않게 맞춘다.</summary>
		private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();

		private static string GeneratedPath()
		{
			// 시험은 bin 안에서 돈다 — 저장소의 진짜 파일을 찾아 올라간다.
			System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && System.IO.Directory.Exists(System.IO.Path.Combine(directory.FullName, "WM.Server")) == false)
				directory = directory.Parent;

			Assert.IsNotNull(directory, "서버 폴더를 못 찾았다");
			return System.IO.Path.Combine(directory.FullName, "WM.Server", "wwwroot", "protocol.d.ts");
		}

		[Test]
		public void 뽑아낸_계약과_저장된_파일이_같다()
		{
			string expected = Normalize(Protocol.ToTypeScript());
			string path = GeneratedPath();

			Assert.IsTrue(System.IO.File.Exists(path), "생성물이 없다 — 계약을 한 번 뽑아 커밋할 것: " + path);

			string actual = Normalize(System.IO.File.ReadAllText(path));
			Assert.AreEqual(expected, actual, "계약이 갈라졌다 — 손으로 고치지 말고 Protocol.cs 에서 다시 뽑아라");
		}

		[Test]
		public void 계약에_세_가지_말이_들어_있다()
		{
			string typescript = Protocol.ToTypeScript();

			StringAssert.Contains("Welcome", typescript);
			StringAssert.Contains("WorldSnapshot", typescript);
			StringAssert.Contains("MoveRequest", typescript);
		}
	}
}
