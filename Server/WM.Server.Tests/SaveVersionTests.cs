using System.IO;
using System.Text.Json;
using NUnit.Framework;
using WitchMendokusai;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 기억에 <b>판 번호</b>가 있고, 더 새로운 판은 안 읽는다 (TASK-WM-360).
	///
	/// ★ 왜: 판 번호가 없으면 <b>되돌리기 한 번이 기억 파괴</b>가 된다 —
	///   새 세계가 적은 기억을 옛 세계가 읽으면 모르는 칸을 조용히 버리고 뜨고,
	///   5초 뒤 저장 루프가 그 반쪽짜리를 원본 위에 덮는다(상자도 사람도 통째로).
	///   안 뜨는 것이 잃는 것보다 낫다(WM-333 과 같은 정신).
	/// </summary>
	public sealed class SaveVersionTests
	{
		private static string FreshFile()
		{
			string folder = Path.Combine(Path.GetTempPath(), "wm-savever-" + Path.GetRandomFileName());
			Directory.CreateDirectory(folder);
			return Path.Combine(folder, "world.json");
		}

		[Test]
		public void 적을_때_판_번호를_같이_적는다()
		{
			string path = FreshFile();
			WorldStore store = new WorldStore(path);

			Assert.That(store.TrySave(new WorldSaveData()), Is.True);

			// ⚠ 이 꾸러미는 <b>필드</b>로 되어 있다(서버·유니티 둘 다 이 모양만 읽는다) —
			//   IncludeFields 를 안 켜면 <b>전부 0</b> 으로 읽혀 시험이 엉뚱하게 빨개진다(실제로 그랬다).
			WorldSaveData onDisk = JsonSerializer.Deserialize<WorldSaveData>(
				File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
			Assert.That(onDisk.saveVersion, Is.EqualTo(WorldStore.KNOWN_SAVE_VERSION),
				"판 번호가 없으면 다음에 읽는 세계가 이 기억이 어느 판인지 모른다");
		}

		[Test]
		public void 판_번호가_없던_옛_기억은_그대로_읽는다()
		{
			string path = FreshFile();
			File.WriteAllText(path, "{\"buildings\":[],\"year\":3,\"day\":9}");

			WorldStore store = new WorldStore(path);
			WorldSaveData read = store.TryLoad();

			Assert.That(read, Is.Not.Null, "옛 기억은 읽어야 한다 — 그때는 칸이 더 적었을 뿐이다");
			Assert.That(read.year, Is.EqualTo(3));
			Assert.That(store.BrokenMemory, Is.False);
		}

		/// <summary>★ 이 시험이 이 판의 전부다 — 되돌린 세계가 새 기억을 덮지 않는다.</summary>
		[Test]
		public void 더_새로운_판의_기억은_안_읽고_깨진_것으로_친다()
		{
			string path = FreshFile();
			File.WriteAllText(path, "{\"saveVersion\":" + (WorldStore.KNOWN_SAVE_VERSION + 1) + ",\"buildings\":[],\"year\":7}");

			WorldStore store = new WorldStore(path);
			WorldSaveData read = store.TryLoad();

			Assert.That(read, Is.Null, "모르는 판을 읽으면 모르는 칸을 버린 채 뜨고, 그 반쪽이 원본을 덮는다");
			Assert.That(store.BrokenMemory, Is.True,
				"「못 읽었다」로 서야 세계가 안 뜨고 원본이 남는다 (WM-333)");
		}
	}
}
