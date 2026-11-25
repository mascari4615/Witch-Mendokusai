using System.IO;
using System.Linq;
using Newtonsoft.Json;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai.Tests
{
	/// <summary>게임이 읽는 AspectPuzzles.json 을 시험도 그대로 읽음 (사본 없음)</summary>
	public static class AspectPuzzleBookFile
	{
		private const string RELATIVE_PATH = "Assets/_WitchMendokusai/Domain/AspectPuzzle/AspectPuzzles.json";

		public static readonly AspectPuzzleBook BOOK = JsonConvert.DeserializeObject<AspectPuzzleBook>(File.ReadAllText(Path.Combine(FindRepoRoot(), RELATIVE_PATH)));
		public static readonly AspectLinks LINKS = new AspectLinks(BOOK.Recipes);

		public static HexPaperData Paper(string id) => BOOK.Papers.Single(paper => paper.Id == id);

		public static TurnBoardData Board(string id) => BOOK.Boards.Single(board => board.Id == id);

		public static TurnGame NewGame(TurnBoardData board, TurnHabitType habit, int limit) => new TurnGame(new TurnBoard(board, LINKS), TurnHabits.Get(habit), limit);

		private static string FindRepoRoot()
		{
			DirectoryInfo at = new DirectoryInfo(Directory.GetCurrentDirectory());
			while (at != null)
			{
				if (Directory.Exists(Path.Combine(at.FullName, "Assets", "_WitchMendokusai")))
				{
					return at.FullName;
				}
				at = at.Parent;
			}
			throw new DirectoryNotFoundException("저장소 뿌리를 못 찾음, cwd=" + Directory.GetCurrentDirectory());
		}
	}
}
