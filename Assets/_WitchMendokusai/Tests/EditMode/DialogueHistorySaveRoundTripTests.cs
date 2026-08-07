using Newtonsoft.Json;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 대화 이력이 **저장 파일을 거쳐 살아 돌아오는지**.
	///
	/// ★ 왜 따로 필요한가: 이력 자체는 순수 로직이라 이미 잠겨 있지만, 저장은 **다른 층**이다.
	///   이 게임의 저장은 Json.NET 이 `GameData` 를 통째로 굽는다 — 새로 넣은 칸이 그 굽기에서
	///   빠지거나 모양이 달라지면 **껐다 켤 때만** 티가 난다(그때 「처음 만남」이 다시 나온다).
	///   그건 제일 늦게 발견되는 종류라, 실제로 굽고 다시 읽어 본다.
	///
	/// 파일까지 쓰지는 않는다 — 저장 도구가 쓰는 **같은 직렬화기**로 문자열 왕복만 한다
	/// (경로·권한은 이 시험의 관심사가 아니다).
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueHistorySaveRoundTripTests
	{
		private const int GREETING_ID = 4615;
		private const int UNFINISHED_ID = 4616;

		private static GameData RoundTrip(GameData source)
		{
			string json = JsonConvert.SerializeObject(source);
			return JsonConvert.DeserializeObject<GameData>(json);
		}

		[Test]
		public void HistorySurvivesSaveAndLoad()
		{
			DialogueHistory history = new();
			history.MarkCompleted(GREETING_ID);
			history.MarkStarted(UNFINISHED_ID);

			GameData saved = new() { dialogueHistory = history.ToSaveData() };
			GameData loaded = RoundTrip(saved);

			DialogueHistory restored = new();
			restored.FromSaveData(loaded.dialogueHistory);

			Assert.That(restored.HasSeen(GREETING_ID, DialogueSeenKind.Completed), Is.True,
				"껐다 켜도 「들었다」가 남아야 조건부 대사가 뜻을 갖는다");
			Assert.That(restored.HasSeen(UNFINISHED_ID, DialogueSeenKind.Started), Is.True);
			Assert.That(restored.HasSeen(UNFINISHED_ID, DialogueSeenKind.Completed), Is.False,
				"도중에 접은 대화는 다음에 다시 보여줘야 한다");
		}

		[Test]
		public void FreshGameData_HasUsableHistoryField()
		{
			GameData loaded = RoundTrip(new GameData());

			DialogueHistory restored = new();
			Assert.That(() => restored.FromSaveData(loaded.dialogueHistory), Throws.Nothing,
				"대화를 한 번도 안 한 새 저장도 그냥 읽혀야 한다");
			Assert.That(restored.HasSeen(GREETING_ID, DialogueSeenKind.Started), Is.False);
		}

		[Test]
		public void OldSaveWithoutTheField_StillLoads()
		{
			// 이 칸이 생기기 전의 저장 파일 — 없는 칸은 그냥 비어 온다. 옛 저장이 안 열리면 그게 제일 큰 사고다.
			GameData loaded = JsonConvert.DeserializeObject<GameData>("{\"curDollIndex\":0}");

			DialogueHistory restored = new();
			Assert.That(() => restored.FromSaveData(loaded.dialogueHistory), Throws.Nothing);
			Assert.That(restored.HasSeen(GREETING_ID, DialogueSeenKind.Started), Is.False);
		}
	}
}
