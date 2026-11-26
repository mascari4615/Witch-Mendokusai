using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>
	/// 성질 놀이 자료 한 벌 (조합, 인형, 종이, 번갈아). 게임과 시험이 같은 json 을 이 모양으로 읽음.
	/// 성질은 번호만. 이름과 아이콘은 화면이 AspectData 에서 찾음 (종류가 확정 전이라 규칙은 목록을 모름)
	/// </summary>
	public sealed class AspectPuzzleBook
	{
		public List<AspectRecipe> Recipes { get; set; } = new List<AspectRecipe>();
		public List<TurnDoll> Dolls { get; set; } = new List<TurnDoll>();
		public List<HexPaperData> Papers { get; set; } = new List<HexPaperData>();
		public List<TurnBoardData> Boards { get; set; } = new List<TurnBoardData>();
	}

	/// <summary>재료 둘을 섞으면 나오는 성질 하나</summary>
	public sealed class AspectRecipe
	{
		public int Result { get; set; }
		public int First { get; set; }
		public int Second { get; set; }
	}

	/// <summary>육각 종이의 칸 자리 (축 좌표)</summary>
	public class HexSpot
	{
		public int Q { get; set; }
		public int R { get; set; }

		public HexCoord ToCoord() => new HexCoord(Q, R);
	}

	/// <summary>성질이 놓인 육각 칸</summary>
	public sealed class HexAspect : HexSpot
	{
		public int Aspect { get; set; }
	}

	public sealed class HexPaperData
	{
		public string Id { get; set; }
		public int Radius { get; set; }
		public int Ink { get; set; }
		public List<HexSpot> Holes { get; set; } = new List<HexSpot>();
		public List<HexAspect> Anchors { get; set; } = new List<HexAspect>();
		public List<HexAspect> Solution { get; set; } = new List<HexAspect>();
	}

	/// <summary>번갈아 판에서 성질이 놓인 칸</summary>
	public sealed class TurnCell
	{
		public int Cell { get; set; }
		public int Aspect { get; set; }
	}

	/// <summary>번갈아를 같이 두는 인형. DollId 는 게임의 인형 번호, 아직 인형이 없으면 음수</summary>
	public sealed class TurnDoll
	{
		public int DollId { get; set; }
		public TurnHabitType Habit { get; set; }
	}

	/// <summary>한 판을 한 버릇과 두는 조건. 버릇마다 푸는 길이 달라 제한도 다름</summary>
	public sealed class TurnRun
	{
		public TurnHabitType Habit { get; set; }
		public int Limit { get; set; }
		public List<TurnCell> Solution { get; set; } = new List<TurnCell>();
	}

	public sealed class TurnBoardData
	{
		public string Id { get; set; }
		public List<int> Holes { get; set; } = new List<int>();
		public List<TurnCell> Anchors { get; set; } = new List<TurnCell>();
		public List<int> Pool { get; set; } = new List<int>();
		public List<int> Queue { get; set; } = new List<int>();
		public List<TurnRun> Runs { get; set; } = new List<TurnRun>();
	}
}
