using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 세계 하나 — <b>판정만</b> 있고 화면은 없다 (TASK-WM-216 → 217).
	///
	/// ★ 판정 층에 둔 이유 (TASK-WM-217): 「혼자 놀기」를 별도 모드로 만들지 않으려면
	///   게임 자신이 이 세계를 품고 돌 수 있어야 한다. 서버 프로세스를 따로 끼워 배포하는 대신,
	///   같은 클래스를 .NET 서버가 호스팅하거나 유니티가 자기 안에서 돌린다 — <b>코드는 한 벌</b>.
	///
	/// 여기 있는 규칙은 게임과 같은 것을 쓴다(좌표·수학·가방·건축 = DomainSDK).
	/// 「어떻게 보이나」는 각 창(Unity · 웹)이 알아서 한다.
	/// </summary>
	public sealed partial class WorldSim
	{
		/// <summary>
		/// 한 번 움직임에 갈 수 있는 거리 상한 — 순간이동 방지(서버 권위의 최소선).
		///
		/// ★ 값의 정본은 <see cref="Net.StepLimit.MOST_PER_STEP"/> 이다. 여기 이름을 남겨 두는 건
		///   부르던 자리를 안 흔들기 위해서다 — 회선 층이 이 값을 읽고 세계가 회선 층을 읽어서,
		///   값을 세계에 두면 어셈블리가 <b>순환</b>한다(유니티는 순환을 거부한다).
		/// </summary>
		public const float MAX_STEP = Net.StepLimit.MOST_PER_STEP;

		/// <summary>솥 건물의 번호 — 이걸 지으면 그 자리에 솥이 하나 생긴다 (TASK-WM-217).</summary>
		public const int CAULDRON_BUILDING_ID = 4000;

		// ★ 여러 갈래가 동시에 만진다 (TASK-WM-216): 접속·퇴장은 각 연결의 흐름에서, 훑기는 알림 루프에서.
		//   자물쇠 없이 두었더니 알림 루프가 훑는 도중 목록이 바뀌어 **터졌다**(NullReference).
		//   화면 없는 서버라 터져도 티가 안 난다 — 그래서 상태를 만지는 자리를 전부 한 자물쇠 아래 둔다.
		private readonly object gate = new object();
		private readonly Dictionary<int, WorldDoll> dolls = new Dictionary<int, WorldDoll>();
		private readonly Dictionary<Vector3Int, int> occupiedCells = new Dictionary<Vector3Int, int>();
		private readonly List<PlacedBuilding> placed = new List<PlacedBuilding>();
		private int nextId = 1;

		/// <summary>
		/// 세계의 시계 — <b>사람이 없어도 흐른다</b> (TASK-WM-217).
		/// 자릿수는 게임의 WorldClockSO 가 정본이고, 서버는 그 값을 받아 여기 꽂는다.
		/// </summary>
		public WorldCalendar Calendar { get; } = new WorldCalendar(24, 28, 4, 6, 0);

		/// <summary>
		/// ⚠ <b>폐기</b> — 세계에 하나뿐이던 솥 (TASK-WM-217). 지은 자리마다의 <see cref="Cauldrons"/> 로 옮겼다.
		/// 규칙이 두 벌이면 「내 솥에 넣었는데 남의 화면에선 딴 솥이 움직이는」 일이 생긴다.
		/// 아직 지우지 않은 이유는 하나뿐: 옛 시험이 이걸 부른다(그 시험이 옮겨지면 지운다).
		/// </summary>
		public WorldCauldron Cauldron { get; } = new WorldCauldron();

		/// <summary>
		/// 세계에 흩어져 있는 주울 것 (TASK-WM-217). 서버가 「무엇이 자라는 세계인가」를 정해 꽂아 준다.
		/// 안 꽂으면 빈 들판이다 — 아무것도 안 자라는 세계에서는 아무도 못 줍는다(우겨도).
		/// </summary>
		public WorldGatherables Gatherables { get; set; } = new WorldGatherables(null);

		/// <summary>
		/// 세계가 아는 건물 목록 (TASK-WM-217) — 「그건 몇 칸짜리인가」의 정본.
		/// 안 꽂으면 아무것도 못 짓는다(세계가 모르는 것은 서지 않는다).
		/// </summary>
		public WorldBuildingCatalog Buildables { get; set; } = new WorldBuildingCatalog(null);

		/// <summary>
		/// 솥에 넣을 수 있는 재료들 (TASK-WM-217) — 「무엇을 넣으면 어디로 가나」의 정본.
		/// 안 꽂으면 아무것도 못 넣는다(창이 방향을 우기던 길을 대신한다).
		/// </summary>
		public WorldIngredients Ingredients { get; set; } = new WorldIngredients(null);

		/// <summary>
		/// 세계에 놓인 상자들 (TASK-WM-217 후속) — 내가 넣고 친구가 꺼낸다.
		/// 상자인지·몇 칸인지는 건물 목록이 정한다.
		/// </summary>
		public WorldStorages Storages { get; } = new WorldStorages();

		/// <summary>
		/// 지은 자리마다의 솥 (TASK-WM-217) — 여럿이 <b>동시에</b> 조리하려면 솥도 여럿이어야 한다.
		/// 세계에 하나뿐인 <see cref="Cauldron"/> 은 옛 경로로 남는다(아직 그걸 쓰는 창이 있다).
		/// </summary>
		public WorldCauldrons Cauldrons { get; } = new WorldCauldrons();

		/// <summary>시간을 흘린다. 하루가 바뀌었으면 true.</summary>
		public bool AdvanceMinutes(float minutes)
		{
			bool moved;
			lock (gate)
			{
				moved = Calendar.AdvanceMinutes(minutes);
			}

			// ★ 시간이 흐르면 들판도 자란다 (TASK-WM-217). 전에는 재생이 「들판을 훑을 때」만 일어났고,
			//   훑는 쪽은 「바뀌었을 때만」 훑었다 — 서로를 기다리다 다시 자란 것이 창에 안 돌아왔다.
			Gatherables?.Tick(Calendar.TotalMinutes());
			return moved;
		}

		/// <summary>훑을 때는 <b>그 순간의 사본</b>을 준다 — 훑는 동안 목록이 바뀌어도 안전하다.</summary>
		public WorldDoll[] Snapshot()
		{
			lock (gate)
			{
				WorldDoll[] copy = new WorldDoll[dolls.Count];
				dolls.Values.CopyTo(copy, 0);
				return copy;
			}
		}
	}
}


