using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 서버와 창(웹·Unity)이 주고받는 <b>말의 목록</b> — 여기가 정본이다 (TASK-WM-216).
	///
	/// ★ 두 번 적지 않는다. 웹이 쓰는 타입 선언은 <see cref="ToTypeScript"/> 가 여기서 뽑아낸다.
	///   손으로 맞추면 반드시 갈라지고, 갈라진 계약은 「같은 게임」을 깨는 가장 흔한 방법이다.
	///   갈라졌는지는 시험이 본다(생성물과 저장된 파일이 다르면 빨강).
	/// </summary>
	public static class Protocol
	{
		// 한글 이름을 \uXXXX 로 바꾸지 않는다 — 기본값은 ASCII 밖을 전부 escape 해서 낱말표가
		// 사람 눈에도, 시험 눈에도 안 읽히는 덩어리가 된다. <, >, & 는 그대로 escape 되므로
		// 창이 이름을 HTML 로 오해할 여지는 남지 않는다.
		private static readonly JsonSerializerOptions textOptions = new JsonSerializerOptions
		{
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
		};

		// 말의 이름과 모양은 판정 층(WitchMendokusai.Net)이 정본이다 — Unity 도 같은 소스를 본다.
		public const string HELLO = Net.NetMessageType.HELLO;
		public const string WELCOME = Net.NetMessageType.WELCOME;
		public const string WORLD = Net.NetMessageType.WORLD;
		public const string ME = Net.NetMessageType.ME;
		public const string NAMES = Net.NetMessageType.NAMES;
		public const string MOVE = Net.NetMessageType.MOVE;
		public const string PLACE = Net.NetMessageType.PLACE;
		public const string GATHER = Net.NetMessageType.GATHER;
		public const string REMOVE = Net.NetMessageType.REMOVE;
		public const string BREW = Net.NetMessageType.BREW;
		public const string BREW_RESET = Net.NetMessageType.BREW_RESET;
		public const string BREW_COMPLETE = Net.NetMessageType.BREW_COMPLETE;
		public const string BREW_TAKEN = Net.NetMessageType.BREW_TAKEN;
		public const string BAG = Net.NetMessageType.BAG;
		public const string BAG_ASK = Net.NetMessageType.BAG_ASK;
		public const string BEAT = Net.NetMessageType.BEAT;
		public const string ROSTER = Net.NetMessageType.ROSTER;
		public const string GHOSTS = Net.NetMessageType.GHOSTS;
		public const string CATALOG = Net.NetMessageType.CATALOG;
		public const string BUILD_CATALOG = Net.NetMessageType.BUILD_CATALOG;
		public const string BREW_SHELF = Net.NetMessageType.BREW_SHELF;
		public const string SPELLBOOK = Net.NetMessageType.SPELLBOOK;
		public const string DENIED = Net.NetMessageType.DENIED;
		public const string CRAFT = Net.NetMessageType.CRAFT;
		public const string CRAFTED = Net.NetMessageType.CRAFTED;
		public const string CRAFT_BOOK = Net.NetMessageType.CRAFT_BOOK;
		public const string RENAME = Net.NetMessageType.RENAME;
		public const string SAY = Net.NetMessageType.SAY;
		public const string SAID = Net.NetMessageType.SAID;
		public const string STRIKE = Net.NetMessageType.STRIKE;
		public const string HURT = Net.NetMessageType.HURT;
		public const string MOVE_ON = Net.NetMessageType.MOVE_ON;

		/// <summary>
		/// 세계 → <b>이웃 세계</b>: 내 국경 띠에 이 사람들이 있다 (TASK-WM-263).
		///
		/// ⚠ 이 말은 창이 쓰는 말이 아니다 — 그래서 창의 계약(NetMessages)에 안 넣는다.
		///   창에 없는 말을 계약에 적으면 「창이 안 다룬다」로 잡혀야 할 것과 섞인다.
		/// </summary>
		public const string NEARBY = "nearby";

		/// <summary>세계 → <b>이웃 세계</b>: 국경 띠에 선 사람이 이렇게 말했다 (TASK-WM-264).</summary>
		public const string HEARD = "heard";

		/// <summary>
		/// 세계 → 그 창에게만: <b>네 걸음을 여기까지 봤다</b> (TASK-WM-271).
		///
		/// ⚠ 창의 앞질러 그리기를 <b>되감는</b> 데 쓴다 — 창은 세계가 아는 자리에서 다시 시작해
		///   아직 답 안 온 걸음만 얹는다. 그래야 앞섬이 회선에 비례해 자라지 않는다.
		/// </summary>
		public const string STEP_SEEN = "stepseen";

		/// <summary>그 번호는 처리했다 (TASK-WM-305).</summary>
		public const string DID = "did";

		// 무엇이 거절됐나 — 창이 자리별로 다르게 보여 줄 수 있게 이름을 준다.
		public const string DENIED_PLACE = "place";
		public const string DENIED_GATHER = "gather";
		public const string DENIED_COMPLETE = "brewcomplete";
		public const string CHEST_ASK = Net.NetMessageType.CHEST_ASK;
		public const string CHEST = Net.NetMessageType.CHEST;
		public const string CHEST_PUT = Net.NetMessageType.CHEST_PUT;
		public const string CHEST_TAKE = Net.NetMessageType.CHEST_TAKE;
		public const string CONSUME = Net.NetMessageType.CONSUME;
		public const string INVITE_ASK = Net.NetMessageType.INVITE_ASK;
		public const string INVITE = Net.NetMessageType.INVITE;
		public const string LINK = Net.NetMessageType.LINK;
		public const string LINKED = Net.NetMessageType.LINKED;
		public const string KICKED = Net.NetMessageType.KICKED;
		public const string FULL = Net.NetMessageType.FULL;

		/// <summary>계약을 웹이 읽을 수 있는 형태로 뽑는다.</summary>
		public static string ToTypeScript()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("// 자동 생성물 — 손으로 고치지 마라 (TASK-WM-216).\n");
			builder.Append("// 정본 = WitchMendokusai/Server/WM.Server/Protocol.cs\n");
			builder.Append("// 서버가 계약을 소유하고, 이 파일은 거기서 뽑혀 나온다.\n\n");

			builder.Append("/** 창 -> 서버: 나 왔다(열쇠가 있으면 같이). 첫 말이다. */\n");
			builder.Append("export interface Hello {\n\ttype: '").Append(HELLO).Append("';\n\tsecret: string;\n\tklCode?: string;\n\tklSession?: string;\n\t/** 이미 들고 있는 낱말표·제작표의 도장 — 같으면 세계가 그것들을 다시 안 보낸다. */\n\tknownCatalogs?: string;\n}\n\n");

			builder.Append("/** 서버 -> 창: 접속했다. secret 이 비어있지 않으면 새로 받은 열쇠(적어 둘 것). */\n");
			builder.Append("export interface Welcome {\n\ttype: '").Append(WELCOME).Append("';\n\tid: number;\n\tidentityId: number;\n\tsecret: string;\n\t/** 이 서버 판의 낱말표·제작표 도장 — hello 에 되돌려 주면 그것들을 안 보낸다. */\n\tcatalogStamp: string;\n\t/** 창(웹 화면)의 판 도장 — 달라졌으면 새 판이 나간 것이다 (TASK-WM-367). */\n\twindowStamp: string;\n}\n\n");

			builder.Append("/** 세계에 있는 인형 하나. */\n");
			builder.Append("export interface WorldDollView {\n\tid: number;\n\tx: number;\n\tz: number;\n}\n\n");

			builder.Append("/** 세계의 시각 — 서버가 굴린다(사람이 없어도 흐른다). */\n");
			builder.Append("export interface WorldTime {\n\tyear: number;\n\tseason: number;\n\tday: number;\n\thour: number;\n\tminute: number;\n\thoursPerDay: number;\n}\n\n");

			builder.Append("/** 서버 -> 창: 지금 세계는 이렇게 생겼다. */\n");
			// ⚠ 여기 적힌 것이 창이 믿는 전부다 — 실제로 보내는데 안 적으면, 창은 「없는 것」으로 읽는다
			//   (건물·솥이 그 꼴이었다: 몇 주 동안 보내면서 계약엔 없었다, 2026-08-10).
			builder.Append("export interface WorldBuildingView {\n\tx: number;\n\ty: number;\n\tz: number;\n\tw: number;\n\tl: number;\n\tbuildingId: number;\n}\n\n");
			builder.Append("export interface BrewStepView {\n\tdx: number;\n\tdy: number;\n\tgrind: number;\n}\n\n");
			builder.Append("export interface BrewView {\n\tx: number;\n\ty: number;\n\tsteps: number;\n\tside: number;\n\tpath: BrewStepView[];\n}\n\n");
			builder.Append("export interface GatherableView {\n\tid: number;\n\tx: number;\n\tz: number;\n\titemId: number;\n\tamount: number;\n}\n\n");
			builder.Append("/** buildings·gatherables 는 바뀐 프레임에만 실린다 — 없으면 지난 것을 그대로 쓸 것. */\n");
			builder.Append("/** 지은 자리마다의 솥 — 여럿이 각자 젓는다. */\n");
			builder.Append("export interface CauldronView {\n\tx: number;\n\ty: number;\n\tz: number;\n\tpx: number;\n\tpy: number;\n\tsteps: number;\n\tside: number;\n}\n\n");
			builder.Append("export interface WorldSnapshot {\n\ttype: '").Append(WORLD).Append("';\n\tsequence: number;\n\t/** 세계의 시계 도장 — 창은 자기 말에 ack 로 얹는다 (TASK-WM-303). */\n\tat: number;\n\tchanged?: boolean;\n\tgone?: number[];\n\tdolls: WorldDollView[];\n\tbuildings?: WorldBuildingView[];\n\tfieldChanged?: boolean;\n\tfieldGone?: number[];\n\tgatherables?: GatherableView[];\n\tcauldrons?: CauldronView[];\n\ttime?: WorldTime;\n\tbrew?: BrewView;\n}\n\n");

			builder.Append("/** 서버 -> 창: 여기부터는 저 세계다. 그 주소로 옮겨 붙고 pass 를 hello 에 낸다. */\n");
			builder.Append("export interface MoveOn {\n\ttype: '").Append(MOVE_ON).Append("';\n\tzone: string;\n\taddress: string;\n\tx: number;\n\tz: number;\n\tpass: string;\n}\n\n");

			builder.Append("/** 창 -> 서버: 저 사람을 때린다. 거리·간격·대상은 세계가 본다. */\n");
			builder.Append("export interface Did {\n\ttype: 'did';\n\tdid: number;\n}\n\n");
			builder.Append("export interface StrikeRequest {\n\ttype: '").Append(STRIKE).Append("';\n\ttargetId: number;\n\tack?: number;\n}\n\n");

			builder.Append("/** 서버 -> 창: 누가 맞았다. down 이면 그 자리에서 다시 세워졌다. */\n");
			builder.Append("export interface Hurt {\n\ttype: '").Append(HURT).Append("';\n\tdollId: number;\n\tby: number;\n\thealth: number;\n\tdown: boolean;\n}\n\n");

			builder.Append("/** 창 -> 서버: 이렇게 말했다. 빈 줄·너무 긴 줄은 세계가 다듬거나 버린다. */\n");
			builder.Append("export interface SayRequest {\n\ttype: '").Append(SAY).Append("';\n\ttext: string;\n\tack?: number;\n\tdid?: number;\n}\n\n");

			builder.Append("/** 서버 -> 창: 누가 이렇게 말했다 — 그 사람이 보이는 사람에게만 온다. */\n");
			builder.Append("export interface Said {\n\ttype: '").Append(SAID).Append("';\n\tdollId: number;\n\tname: string;\n\ttext: string;\n}\n\n");

			builder.Append("/** 창 -> 서버: 이쪽으로 가고 싶다(얼마나 갈지는 서버가 정한다). */\n");
			builder.Append("export interface MoveRequest {\n\ttype: '").Append(MOVE).Append("';\n\tx: number;\n\tz: number;\n\tseq?: number;\n\t/** 마지막으로 본 세계 도장 (TASK-WM-303). */\n\tack?: number;\n}\n\n");

			builder.Append("/** 창 -> 서버: 이 칸의 건물을 부수고 싶다. */\n");
			builder.Append("export interface RemoveRequest {\n\ttype: '").Append(REMOVE).Append("';\n\tx: number;\n\ty: number;\n\tz: number;\n}\n\n");

			builder.Append("/** 창 -> 서버: 저기 있는 저것을 줍겠다. 손이 닿는지는 세계가 본다. */\n");
			builder.Append("export interface GatherRequest {\n\ttype: '").Append(GATHER).Append("';\n\tnodeId: number;\n}\n\n");

			builder.Append("/** 창 -> 서버: 이 재료를 솥에 넣는다(가방에서 실제로 빠진다). 어디로 밀지는 세계가 안다. */\n");
			builder.Append("/** x·y·z 를 주면 그 자리의 솥, 안 주면 세계에 하나뿐인 옛 솥(회귀 0). 손이 닿아야 한다. */\n");
			builder.Append("export interface BrewRequest {\n\ttype: '").Append(BREW).Append("';\n\titemId: number;\n\tx?: number;\n\ty?: number;\n\tz?: number;\n}\n\n");

			builder.Append("/** 서버 -> 창: 마도서 — 무엇을 만들 수 있고 어디를 겨냥하나(들어올 때 한 번). */\n");
			builder.Append("export interface Spellbook {\n\ttype: '").Append(SPELLBOOK).Append("';\n\tpages: { id: number; name: string; x: number; y: number; radius: number; itemId: number; amount: number }[];\n}\n\n");

			builder.Append("/** 서버 -> 창: 솥에 넣을 수 있는 재료 목록(들어올 때 한 번). */\n");
			builder.Append("export interface BrewShelf {\n\ttype: '").Append(BREW_SHELF).Append("';\n\titems: { itemId: number; name: string }[];\n}\n\n");

			builder.Append("/** 창 -> 서버: 솥을 비운다. */\n");
			builder.Append("export interface BrewResetRequest {\n\ttype: '").Append(BREW_RESET).Append("';\n}\n\n");

			builder.Append("/** 창 -> 서버: 이 솥을 완성으로 가져가겠다(선착순 한 번). */\n");
			builder.Append("export interface BrewCompleteRequest {\n\ttype: '").Append(BREW_COMPLETE).Append("';\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 완성은 네 것이다. */\n");
			builder.Append("export interface BrewTaken {\n\ttype: '").Append(BREW_TAKEN).Append("';\n\tx: number;\n\ty: number;\n\tsteps: number;\n\tside: number;\n\titemId: number;\n\tamount: number;\n\tgrade: number;\n\trecipe: string;\n}\n\n");

			builder.Append("/** 창 -> 서버: 내 가방 좀 알려줘. */\n");
			builder.Append("export interface BagAsk {\n\ttype: '").Append(BAG_ASK).Append("';\n}\n\n");

			builder.Append("/** 창 -> 서버: 이걸 썼다(제작 재료 등). 안 알리면 쓴 게 다시 생긴다. */\n");
			builder.Append("export interface ConsumeRequest {\n\ttype: '").Append(CONSUME).Append("';\n\titemId: number;\n\tamount: number;\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 네 가방은 이렇다. */\n");
			builder.Append("/** 창 -> 서버: 나를 이렇게 불러 달라. 짧거나 길거나 남과 겹치면 세계가 거절한다. */\n");
			builder.Append("export interface RenameRequest {\n\ttype: '").Append(RENAME).Append("';\n\tname: string;\n}\n\n");

			builder.Append("/** 창 -> 서버: 이 줄대로 만들겠다. 재료도 주사위도 세계가 본다. */\n");
			builder.Append("export interface CraftRequest {\n\ttype: '").Append(CRAFT).Append("';\n\trecipeId: number;\n}\n\n");

			builder.Append("/** 세계가 아는 제작 한 줄 — 재료는 itemIds·amounts 짝. */\n");
			builder.Append("export interface CraftBookEntry {\n\trecipeId: number;\n\tname: string;\n\tresultItemId: number;\n\tresultAmount: number;\n\tpercentage: number;\n\titemIds: number[];\n\tamounts: number[];\n}\n\n");

			builder.Append("/** 서버 -> 창: 세계가 아는 제작표(들어올 때 한 번). */\n");
			builder.Append("export interface CraftBook {\n\ttype: '").Append(CRAFT_BOOK).Append("';\n\trecipes: CraftBookEntry[];\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 만든 결과. 재료가 없어 못 한 것과 주사위를 진 것은 다른 일이다. */\n");
			builder.Append("export interface Crafted {\n\ttype: '").Append(CRAFTED).Append("';\n\trecipeId: number;\n\tattempted: boolean;\n\tsucceeded: boolean;\n\titemId: number;\n\tamount: number;\n\tdenied: string;\n}\n\n");

			builder.Append("export interface Bag {\n\ttype: '").Append(BAG).Append("';\n\titems: { itemId: number; amount: number }[];\n}\n\n");

			builder.Append("/** 서버 -> 창: 아이템 낱말표(들어올 때 한 번). 그 뒤로는 번호만 나른다. */\n");
			builder.Append("export interface Catalog {\n\ttype: '").Append(CATALOG).Append("';\n\titems: { itemId: number; name: string }[];\n}\n\n");

			builder.Append("/** 서버 -> 창: 지을 수 있는 것 목록(들어올 때 한 번). 크기의 정본은 세계다. */\n");
			builder.Append("export interface BuildCatalog {\n\ttype: '").Append(BUILD_CATALOG).Append("';\n\tbuildings: { buildingId: number; name: string; w: number; l: number }[];\n}\n\n");

			builder.Append("/** 창 -> 서버: 여기에 이걸 짓고 싶다. 크기는 세계가 안다(창이 못 우긴다). */\n");
			builder.Append("export interface PlaceRequest {\n\ttype: '").Append(PLACE).Append("';\n\tx: number;\n\ty: number;\n\tz: number;\n\tbuildingId: number;\n}\n\n");

			builder.Append("/** 창 -> 서버: 그 상자 안을 보여 줘. 손이 닿는지는 세계가 본다. */\n");
			builder.Append("export interface ChestAsk {\n\ttype: '").Append(CHEST_ASK).Append("';\n\tx: number;\n\ty: number;\n\tz: number;\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 그 상자 안은 이렇다(없는 상자면 items 가 빈다). */\n");
			builder.Append("export interface Chest {\n\ttype: '").Append(CHEST).Append("';\n\tx: number;\n\ty: number;\n\tz: number;\n\titems: { itemId: number; amount: number }[];\n}\n\n");

			builder.Append("/** 창 -> 서버: 이걸 상자에 넣겠다 / 꺼내겠다. 되는지는 세계가 본다. */\n");
			builder.Append("export interface ChestPut {\n\ttype: '").Append(CHEST_PUT).Append("';\n\tx: number;\n\ty: number;\n\tz: number;\n\titemId: number;\n\tamount: number;\n}\n\n");
			builder.Append("export interface ChestTake {\n\ttype: '").Append(CHEST_TAKE).Append("';\n\tx: number;\n\ty: number;\n\tz: number;\n\titemId: number;\n\tamount: number;\n}\n\n");

			builder.Append("/** 창 -> 서버: 다른 기기를 이을 초대 열쇠를 만들어 줘. */\n");
			builder.Append("export interface InviteAsk {\n\ttype: '").Append(INVITE_ASK).Append("';\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 그건 안 된다(무엇을·왜). 거절도 대답이다. */\n");
			builder.Append("export interface Denied {\n\ttype: '").Append(DENIED).Append("';\n\twhat: string;\n\twhy: string;\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 초대 열쇠(한 번만 쓴다). */\n");
			builder.Append("export interface Invite {\n\ttype: '").Append(INVITE).Append("';\n\tcode: string;\n}\n\n");

			builder.Append("/** 창 -> 서버: 이 초대 열쇠로 나를 그 사람에 이어 줘. */\n");
			builder.Append("export interface LinkRequest {\n\ttype: '").Append(LINK).Append("';\n\tcode: string;\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 이었나(이었으면 다시 들어와야 그 사람으로 논다). */\n");
			builder.Append("export interface Linked {\n\ttype: '").Append(LINKED).Append("';\n\tok: boolean;\n\tidentityId: number;\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 다른 곳에서 같은 사람이 들어왔다(여기서는 나간다). */\n");
			builder.Append("export interface Kicked {\n\ttype: '").Append(KICKED).Append("';\n\treason: string;\n}\n\n");
			builder.Append("export interface Full {\n\ttype: '").Append(FULL).Append("';\n\treason: string;\n\tmost: number;\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 네 인형은 여기 있다(몰린 칸에서 공유 소식에 자기가 빠졌을 때). */\n");
			builder.Append("export interface Me {\n\ttype: '").Append(ME).Append("';\n\tdoll: WorldDollView;\n}\n\n");

			builder.Append("/** 서버 -> 창: 누가 무슨 이름인가(바뀔 때만). 창이 들고 있다가 인형에 붙인다. */\n");
			builder.Append("export interface DollNameView {\n\tid: number;\n\tname: string;\n}\n\n");
			builder.Append("export interface Names {\n\ttype: '").Append(NAMES).Append("';\n\tdolls: DollNameView[];\n}\n\n");

			builder.Append("export type ServerMessage = Welcome | Me | Names | WorldSnapshot | BrewTaken | Bag | Catalog | BuildCatalog | BrewShelf | Spellbook | CraftBook | Crafted | Chest | Denied | Invite | Linked | Kicked | Said | Hurt | MoveOn | Did;\n");
			builder.Append("export type ClientMessage = MoveRequest | PlaceRequest | RemoveRequest | GatherRequest | ChestAsk | ChestPut | ChestTake | BrewRequest | BrewResetRequest | BrewCompleteRequest | Hello | BagAsk | ConsumeRequest | InviteAsk | LinkRequest | SayRequest | StrikeRequest;\n");

			return builder.ToString();
		}

		/// <summary>그 창에게만 보내는 가방 상태.</summary>
		/// <summary>
		/// 그 창에게만: <b>이 사람들은 여기 없다</b> (TASK-WM-329).
		/// 창이 「내가 그리는 사람들」을 물어봤을 때의 답이다 — 빈 목록이면 안 보낸다(조용한 게 정상).
		/// </summary>
		public static string Ghosts(IEnumerable<int> ids)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(GHOSTS).Append("\",\"ids\":[");

			bool first = true;
			foreach (int id in ids)
			{
				if (first == false)
					builder.Append(',');

				builder.Append(id);
				first = false;
			}

			builder.Append("]}");
			return builder.ToString();
		}

		public static string Bag(IEnumerable<KeyValuePair<int, int>> counts)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(BAG).Append("\",\"items\":[");

			bool first = true;
			foreach (KeyValuePair<int, int> entry in counts)
			{
				if (entry.Value <= 0)
					continue;

				if (first == false)
					builder.Append(',');

				first = false;
				builder.Append("{\"itemId\":").Append(entry.Key).Append(",\"amount\":").Append(entry.Value).Append('}');
			}

			builder.Append("]}");
			return builder.ToString();
		}

		/// <summary>
		/// 아이템 낱말표 — 들어올 때 한 번 (TASK-WM-217).
		/// 이름에 따옴표·역슬래시가 들어와도 창이 안 깨지게 <b>반드시 감싸서</b> 낸다.
		/// </summary>
		public static string Catalog(IEnumerable<KeyValuePair<int, string>> names)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(CATALOG).Append("\",\"items\":[");

			bool first = true;
			foreach (KeyValuePair<int, string> entry in names)
			{
				if (first == false)
					builder.Append(',');

				first = false;
				builder.Append("{\"itemId\":").Append(entry.Key).Append(",\"name\":")
					.Append(JsonSerializer.Serialize(entry.Value ?? string.Empty, textOptions)).Append('}');
			}

			builder.Append("]}");
			return builder.ToString();
		}

		/// <summary>
		/// 그 창에게만: 완성은 네 것이다(선착순 한 번) — <b>무엇이 나왔는지까지</b>.
		/// itemId 0 = 아무 쪽에도 못 닿았다(솥은 비고 손은 빈다). 그것도 결과라 말해 준다.
		/// </summary>
		public static string BrewTaken(BrewCompletion completion)
		{
			DomainSDK.Alchemy.BrewState state = completion.State;
			return "{\"type\":\"" + BREW_TAKEN + "\",\"x\":" + state.Position.X.ToString("F3")
				+ ",\"y\":" + state.Position.Y.ToString("F3")
				+ ",\"steps\":" + state.StepCount
				+ ",\"side\":" + state.AccruedSideEffect.ToString("F3")
				+ ",\"itemId\":" + completion.ResultItemId
				+ ",\"amount\":" + completion.Amount
				+ ",\"grade\":" + (int)completion.Grade
				+ ",\"recipe\":" + JsonSerializer.Serialize(completion.RecipeName ?? string.Empty, textOptions) + "}";
		}

		/// <summary>지을 수 있는 것 목록 — 들어올 때 한 번. 크기까지 같이 준다(창이 미리 그려 볼 수 있게).</summary>
		public static string BuildCatalog(System.Collections.Generic.IReadOnlyList<BuildingCatalogEntry> buildings)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(BUILD_CATALOG).Append("\",\"buildings\":[");

			for (int i = 0; i < buildings.Count; i++)
			{
				if (i > 0)
					builder.Append(',');

				builder.Append("{\"buildingId\":").Append(buildings[i].id)
					.Append(",\"name\":").Append(JsonSerializer.Serialize(buildings[i].name ?? string.Empty, textOptions))
					.Append(",\"w\":").Append(buildings[i].w < 1 ? 1 : buildings[i].w)
					.Append(",\"l\":").Append(buildings[i].l < 1 ? 1 : buildings[i].l)
					.Append(",\"costItemId\":").Append(buildings[i].costItemId)
					.Append(",\"costAmount\":").Append(buildings[i].costAmount < 0 ? 0 : buildings[i].costAmount)
					.Append('}');
			}

			builder.Append("]}");
			return builder.ToString();
		}

		/// <summary>세계가 아는 제작표 — 들어올 때 한 번 (TASK-WM-217).</summary>
		public static string CraftBook(System.Collections.Generic.IReadOnlyList<CraftRecipeEntry> recipes)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(CRAFT_BOOK).Append("\",\"recipes\":[");

			for (int i = 0; i < recipes.Count; i++)
			{
				if (i > 0)
					builder.Append(',');

				CraftRecipeEntry recipe = recipes[i];
				CraftIngredientEntry[] items = recipe.items ?? System.Array.Empty<CraftIngredientEntry>();

				builder.Append("{\"recipeId\":").Append(recipe.id)
					.Append(",\"name\":").Append(JsonSerializer.Serialize(recipe.name ?? string.Empty, textOptions))
					.Append(",\"resultItemId\":").Append(recipe.resultItemId)
					.Append(",\"resultAmount\":").Append(recipe.resultAmount <= 0 ? 1 : recipe.resultAmount)
					.Append(",\"percentage\":").Append((recipe.percentage <= 0f ? 100f : recipe.percentage).ToString("0.##"))
					.Append(",\"itemIds\":[");

				for (int need = 0; need < items.Length; need++)
				{
					if (need > 0)
						builder.Append(',');

					builder.Append(items[need].itemId);
				}

				builder.Append("],\"amounts\":[");
				for (int need = 0; need < items.Length; need++)
				{
					if (need > 0)
						builder.Append(',');

					builder.Append(items[need].amount);
				}

				builder.Append("]}");
			}

			builder.Append("]}");
			return builder.ToString();
		}

		/// <summary>만든 결과 — 실패도 말해 준다(조용히 아무 일도 안 일어나면 「고장」으로 읽힌다).</summary>
		public static string Crafted(CraftResult result)
		{
			return "{\"type\":\"" + CRAFTED + "\",\"recipeId\":" + result.RecipeId
				+ ",\"attempted\":" + (result.Attempted ? "true" : "false")
				+ ",\"succeeded\":" + (result.Succeeded ? "true" : "false")
				+ ",\"itemId\":" + result.ResultItemId
				+ ",\"amount\":" + result.ResultAmount
				+ ",\"denied\":" + JsonSerializer.Serialize(result.Denied ?? string.Empty, textOptions) + "}";
		}

		/// <summary>솥에 넣을 수 있는 재료 목록 — 들어올 때 한 번.</summary>
		public static string BrewShelf(System.Collections.Generic.IReadOnlyList<IngredientCatalogEntry> shelf)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(BREW_SHELF).Append("\",\"items\":[");

			for (int i = 0; i < shelf.Count; i++)
			{
				if (i > 0)
					builder.Append(',');

				builder.Append("{\"itemId\":").Append(shelf[i].itemId)
					.Append(",\"name\":").Append(JsonSerializer.Serialize(shelf[i].name ?? string.Empty, textOptions))
					.Append('}');
			}

			builder.Append("]}");
			return builder.ToString();
		}

		/// <summary>그 상자 안 — 그 창에게만. 없는 상자면 빈 목록(창이 「비었다」로 그린다).</summary>
		public static string Chest(int x, int y, int z, IEnumerable<BagSaveEntry> contents)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(CHEST).Append("\",\"x\":").Append(x)
				.Append(",\"y\":").Append(y).Append(",\"z\":").Append(z).Append(",\"items\":[");

			bool first = true;
			foreach (BagSaveEntry entry in contents)
			{
				if (entry == null || entry.amount <= 0)
					continue;

				if (first == false)
					builder.Append(',');

				first = false;
				builder.Append("{\"itemId\":").Append(entry.itemId).Append(",\"amount\":").Append(entry.amount).Append('}');
			}

			builder.Append("]}");
			return builder.ToString();
		}

		/// <summary>마도서 — 무엇을 만들 수 있고 어디를 겨냥하나. 들어올 때 한 번.</summary>
		public static string Spellbook(System.Collections.Generic.IEnumerable<RecipeCatalogEntry> pages)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(SPELLBOOK).Append("\",\"pages\":[");

			bool first = true;
			foreach (RecipeCatalogEntry page in pages)
			{
				if (first == false)
					builder.Append(',');

				first = false;
				builder.Append("{\"id\":").Append(page.id)
					.Append(",\"name\":").Append(JsonSerializer.Serialize(page.name ?? string.Empty, textOptions))
					.Append(",\"x\":").Append(page.targetX.ToString("F2"))
					.Append(",\"y\":").Append(page.targetY.ToString("F2"))
					.Append(",\"radius\":").Append(page.radius.ToString("F2"))
					.Append(",\"itemId\":").Append(page.resultItemId)
					.Append(",\"amount\":").Append(page.amount)
					.Append('}');
			}

			builder.Append("]}");
			return builder.ToString();
		}

		/// <summary>
		/// 그 창에게만: <b>그건 안 된다, 왜냐면</b> (TASK-WM-217).
		/// 조용히 무시하면 사람은 「고장났나」로 읽는다 — 거절도 대답이다.
		/// </summary>
		public static string Denied(string what, string why)
		{
			return "{\"type\":\"" + DENIED + "\",\"what\":" + JsonSerializer.Serialize(what ?? string.Empty, textOptions)
				+ ",\"why\":" + JsonSerializer.Serialize(why ?? string.Empty, textOptions) + "}";
		}

		/// <summary>그 창에게만: 초대 열쇠(한 번만 쓴다).</summary>
		public static string Invite(string code)
		{
			return "{\"type\":\"" + INVITE + "\",\"code\":\"" + (code ?? string.Empty) + "\"}";
		}

		/// <summary>그 창에게만: 이었나.</summary>
		public static string Linked(bool ok, int identityId)
		{
			return "{\"type\":\"" + LINKED + "\",\"ok\":" + (ok ? "true" : "false")
				+ ",\"identityId\":" + identityId + "}";
		}

		/// <summary>그 창에게만: 다른 곳에서 같은 사람이 들어왔다.</summary>
		/// <summary>그 창에게만: 이 세계는 지금 가득 찼다 (TASK-WM-349).</summary>
		public static string Full(int most)
		{
			return "{\"type\":\"" + FULL + "\",\"reason\":\"세계가 가득 찼다\",\"most\":" + most + "}";
		}

		/// <summary>그 창에게만: 다른 곳에서 같은 사람이 들어왔다.</summary>
		public static string Kicked()
		{
			return "{\"type\":\"" + KICKED + "\",\"reason\":\"다른 곳에서 접속했다\"}";
		}

		/// <summary>
		/// 그 창에게만: <b>네 인형은 여기 있다.</b> 몰린 칸에서 공유 소식에 자기가 빠졌을 때만 나간다.
		/// </summary>
		public static string Me(WorldDoll doll, System.Func<int, string> nameOf = null)
		{
			string who = nameOf == null ? string.Empty : (nameOf(doll.IdentityId) ?? string.Empty);
			return "{\"type\":\"" + ME + "\",\"doll\":{\"id\":" + doll.Id
				+ ",\"x\":" + doll.Position.x.ToString("F2")
				+ ",\"z\":" + doll.Position.z.ToString("F2")
				+ ",\"name\":" + JsonSerializer.Serialize(who, textOptions) + "}}";
		}

		/// <summary>
		/// 누가 이렇게 말했다 (TASK-WM-250) — <b>그 사람이 보이는 사람에게만</b> 간다.
		/// 이름을 같이 싣는다: 말은 「누가」가 붙어야 말이다(창이 이름표를 못 받았을 수도 있다).
		/// </summary>
		public static string Said(int dollId, string name, string line)
		{
			return "{\"type\":\"" + SAID + "\",\"dollId\":" + dollId
				+ ",\"name\":" + JsonSerializer.Serialize(name ?? string.Empty, textOptions)
				+ ",\"text\":" + JsonSerializer.Serialize(line ?? string.Empty, textOptions) + "}";
		}

		/// <summary>
		/// 누가 맞았다 (TASK-WM-251) — 남은 몸과 <b>쓰러졌는지</b>를 같이 낸다.
		/// 창은 이 말로만 몸을 안다(스스로 셈하면 세계와 갈라진다).
		/// </summary>
		/// <summary>
		/// <b>그 번호는 처리했다</b> (TASK-WM-305). 창은 이 말을 받을 때까지 그 행동을 들고 있다가,
		/// 다시 붙으면 또 보낸다 — 그래야 끊기는 순간 누른 것이 사라지지 않는다.
		/// </summary>
		public static string Did(long actionId)
		{
			return "{\"type\":\"" + DID + "\",\"did\":" + actionId + "}";
		}

		public static string Hurt(int dollId, int byDollId, int health, bool wentDown)
		{
			return "{\"type\":\"" + HURT + "\",\"dollId\":" + dollId
				+ ",\"by\":" + byDollId
				+ ",\"health\":" + health
				+ ",\"down\":" + (wentDown ? "true" : "false") + "}";
		}

		/// <summary>
		/// 여기부터는 저 세계다 (TASK-WM-254) — 주소와 <b>통행증</b>을 같이 준다.
		/// 창은 그걸 들고 옆 세계에 hello 한다(통행증에 도장이 찍혀 있어 가방을 못 고친다).
		/// </summary>
		public static string MoveOn(string zone, string address, float x, float z, string pass)
		{
			return "{\"type\":\"" + MOVE_ON + "\",\"zone\":" + JsonSerializer.Serialize(zone ?? string.Empty, textOptions)
				+ ",\"address\":" + JsonSerializer.Serialize(address ?? string.Empty, textOptions)
				+ ",\"x\":" + x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"z\":" + z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"pass\":" + JsonSerializer.Serialize(pass ?? string.Empty, textOptions) + "}";
		}

		/// <summary>
		/// 국경 띠에 선 내 사람들을 이웃에게 알린다 (TASK-WM-263).
		/// 도장(<paramref name="seal"/>)이 없으면 아무나 남의 세계에 사람을 그려 넣을 수 있다.
		/// </summary>
		public static string Nearby(string zone, string seal, IEnumerable<WorldDoll> people, System.Func<int, string> nameOf)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(NEARBY)
				.Append("\",\"zone\":").Append(JsonSerializer.Serialize(zone ?? string.Empty, textOptions))
				.Append(",\"seal\":").Append(JsonSerializer.Serialize(seal ?? string.Empty, textOptions))
				.Append(",\"dolls\":[");

			bool first = true;
			foreach (WorldDoll one in people)
			{
				if (first == false)
					builder.Append(',');

				first = false;
				string who = nameOf == null ? string.Empty : (nameOf(one.IdentityId) ?? string.Empty);
				builder.Append("{\"id\":").Append(one.Id)
					.Append(",\"x\":").Append(one.Position.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
					.Append(",\"z\":").Append(one.Position.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
					.Append(",\"name\":").Append(JsonSerializer.Serialize(who, textOptions))
					.Append('}');
			}

			return builder.Append("]}").ToString();
		}

		/// <summary>
		/// 국경 너머로 넘기는 말 (TASK-WM-264) — <b>어디서</b> 났는지도 같이 보낸다.
		/// 받는 세계는 그 자리 가까이 있는 제 사람들에게만 나른다(확성기가 아니다).
		/// </summary>
		public static string Heard(string zone, string seal, int dollId, string name, string line, float x, float z)
		{
			return "{\"type\":\"" + HEARD
				+ "\",\"zone\":" + JsonSerializer.Serialize(zone ?? string.Empty, textOptions)
				+ ",\"seal\":" + JsonSerializer.Serialize(seal ?? string.Empty, textOptions)
				+ ",\"dollId\":" + dollId
				+ ",\"name\":" + JsonSerializer.Serialize(name ?? string.Empty, textOptions)
				+ ",\"text\":" + JsonSerializer.Serialize(line ?? string.Empty, textOptions)
				+ ",\"x\":" + x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"z\":" + z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "}";
		}

		/// <summary>네 걸음을 여기까지 봤다 (TASK-WM-271) — 스무 바이트짜리 한 마디.</summary>
		public static string StepSeen(int seq)
		{
			return "{\"type\":\"" + STEP_SEEN + "\",\"seq\":" + seq + "}";
		}

		/// <summary>이름표 — 바뀐 사람만 담아 모두에게 보낸다 (TASK-WM-220).</summary>
		public static string Names(IEnumerable<(int DollId, string Name)> people)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(NAMES).Append("\",\"dolls\":[");

			bool first = true;
			foreach ((int dollId, string name) in people)
			{
				if (first == false)
					builder.Append(',');

				first = false;
				builder.Append("{\"id\":").Append(dollId)
					.Append(",\"name\":").Append(JsonSerializer.Serialize(name ?? string.Empty, textOptions))
					.Append('}');
			}

			builder.Append("]}");
			return builder.ToString();
		}

		/// <summary>서버가 보내는 인사말.</summary>
		/// <summary>
		/// 맞아들이는 말. <paramref name="catalogStamp"/> = 이 서버 판의 낱말표·제작표 도장 (TASK-WM-238).
		/// 창이 다음 hello 에 그 도장을 되돌려 주면 세계는 그것들을 <b>다시 안 보낸다</b>.
		/// </summary>
		public static string Welcome(int dollId, string newSecret = "", int identityId = 0, string catalogStamp = "",
			string windowStamp = "")
		{
			string secret = string.IsNullOrEmpty(newSecret) ? string.Empty : newSecret;
			return "{\"type\":\"" + WELCOME + "\",\"id\":" + dollId
				+ ",\"identityId\":" + identityId
				+ ",\"secret\":\"" + secret + "\""
				+ ",\"catalogStamp\":\"" + catalogStamp + "\""
				// 창 판 도장 (TASK-WM-367) — 새 판이 나갔는데 옛 창이 그대로면 그 사람만 옛 세계를 산다.
				+ ",\"windowStamp\":\"" + windowStamp + "\"}";
		}

		/// <summary>서버가 보내는 세계 모습.</summary>
		public static string WorldSnapshot(IEnumerable<WorldDoll> dolls, IEnumerable<PlacedBuilding> buildings, WorldCalendar calendar = null, WorldCauldron cauldron = null, IEnumerable<GatherableNode> gatherables = null, WorldCauldrons cauldrons = null, long sequence = 0, IEnumerable<Vector3Int> cauldronCells = null, bool full = true, IEnumerable<int> gone = null, bool fieldChanged = false, IEnumerable<int> fieldGone = null)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(WORLD).Append("\",\"sequence\":").Append(sequence);

			// ★ 세계의 시계 도장 (TASK-WM-303) — 창이 이걸 그대로 되돌려 주면 세계가 그 사람의 회선을 안다.
			//   그림은 <b>모두에게</b> 가므로 가만히 선 사람도 재어진다(걸음 답장은 걷는 사람에게만 갔다).
			builder.Append(",\"at\":").Append(System.Environment.TickCount64);

			// ★ 「전부」인가 「바뀐 것만」인가 (TASK-WM-220). 안 움직인 사람은 안 싣는다 —
			//   광장에 200명이 서 있어도, 그 판에 실리는 건 <b>움직인 사람</b>뿐이다.
			//   ⚠ 이 표시가 없으면 창은 「안 실림 = 사라짐」으로 읽고 사람들이 매 판 깜빡인다.
			if (full == false)
				builder.Append(",\"changed\":true");

			if (gone != null)
			{
				builder.Append(",\"gone\":[");

				bool firstGone = true;
				foreach (int dollId in gone)
				{
					if (firstGone == false)
						builder.Append(',');

					firstGone = false;
					builder.Append(dollId);
				}

				builder.Append(']');
			}

			builder.Append(",\"dolls\":[");

			bool first = true;
			foreach (WorldDoll doll in dolls)
			{
				if (first == false)
					builder.Append(',');

				first = false;
				// ★ 이름은 여기 안 싣는다 (TASK-WM-220) — 거의 안 바뀌는 글자를 초당 20번 나르지 않는다.
				//   이름표는 `names` 로 따로, 바뀔 때만 간다.
				builder.Append("{\"id\":").Append(doll.Id)
					.Append(",\"x\":").Append(doll.Position.x.ToString("F2"))
					.Append(",\"z\":").Append(doll.Position.z.ToString("F2"))
					.Append('}');
			}

			builder.Append(']');

			// 건물 목록은 <b>바뀐 프레임에만</b> 실린다 — 없으면 창은 지난 것을 그대로 쓴다.
			if (buildings != null)
			{
				builder.Append(",\"buildings\":[");

				first = true;
				foreach (PlacedBuilding building in buildings)
				{
					if (first == false)
						builder.Append(',');

					first = false;
					builder.Append("{\"x\":").Append(building.Pivot.x)
						.Append(",\"y\":").Append(building.Pivot.y)
						.Append(",\"z\":").Append(building.Pivot.z)
						.Append(",\"w\":").Append(building.Size.x)
						.Append(",\"l\":").Append(building.Size.y)
						.Append(",\"buildingId\":").Append(building.BuildingId)
						.Append('}');
				}

				builder.Append(']');
			}

			// 세계의 시각 — 서버가 굴린다(내가 없어도 밤이 온다). 창은 받아서 보여 주기만 한다.
			if (calendar != null)
			{
				builder.Append(",\"time\":{\"year\":").Append(calendar.Year)
					.Append(",\"season\":").Append(calendar.Season)
					.Append(",\"day\":").Append(calendar.Day)
					.Append(",\"hour\":").Append(calendar.Hour)
					.Append(",\"minute\":").Append(calendar.Minute)
					// 하루가 몇 시간인지도 같이 보낸다 — 창이 「지금 밤인가」를 스스로 셀 수 있어야
					// 하늘빛을 세계의 시각에 맞춘다(24를 박으면 게임이 자릿수를 바꾸는 날 어긋난다).
					.Append(",\"hoursPerDay\":").Append(calendar.HoursPerDay)
					.Append('}');
			}

			// 솥 — 모두가 같은 솥을 본다. 저은 길까지 보내야 경로선이 서로 같게 그려진다.
			if (cauldron != null)
			{
				DomainSDK.Alchemy.BrewState state = cauldron.State;
				builder.Append(",\"brew\":{\"x\":").Append(state.Position.X.ToString("F3"))
					.Append(",\"y\":").Append(state.Position.Y.ToString("F3"))
					.Append(",\"steps\":").Append(state.StepCount)
					.Append(",\"side\":").Append(state.AccruedSideEffect.ToString("F3"))
					.Append(",\"path\":[");

				List<DomainSDK.Alchemy.BrewStep> steps = new List<DomainSDK.Alchemy.BrewStep>();
				cauldron.ReadSteps(steps);
				for (int i = 0; i < steps.Count; i++)
				{
					if (i > 0)
						builder.Append(',');

					builder.Append("{\"dx\":").Append(steps[i].Direction.X.ToString("F3"))
						.Append(",\"dy\":").Append(steps[i].Direction.Y.ToString("F3"))
						.Append(",\"grind\":").Append(steps[i].Grind.ToString("F3"))
						.Append('}');
				}

				builder.Append("]}");
			}

			// 주울 것 — 뽑아 간 자리는 빠져 있다(다시 자라면 돌아온다).
			//
			// ★ 「바뀐 자리만」 실을 때는 <b>그렇다고 말해야 한다</b> (TASK-WM-230).
			//   실측 2026-08-12: 이 표시를 안 붙여 보내니, 창은 부분 목록을 <b>전체</b>로 알고
			//   통째로 갈아 끼웠다 — 들판 67자리가 한 번에 사라졌다(화면은 멀쩡, 오류도 없다).
			//   서버는 이 값을 <b>인자로 받아 놓고 쓰지 않고 있었다</b>(반쪽 배선).
			if (gatherables != null)
			{
				if (fieldChanged)
					builder.Append(",\"fieldChanged\":true");

				if (fieldGone != null)
				{
					builder.Append(",\"fieldGone\":[");
					bool firstGone = true;
					foreach (int nodeId in fieldGone)
					{
						if (firstGone == false)
							builder.Append(',');

						firstGone = false;
						builder.Append(nodeId);
					}

					builder.Append(']');
				}

				builder.Append(",\"gatherables\":[");

				bool firstNode = true;
				foreach (GatherableNode node in gatherables)
				{
					if (firstNode == false)
						builder.Append(',');

					firstNode = false;
					builder.Append("{\"id\":").Append(node.Id)
						.Append(",\"x\":").Append(node.X.ToString("F2"))
						.Append(",\"z\":").Append(node.Z.ToString("F2"))
						.Append(",\"itemId\":").Append(node.ItemId)
						.Append(",\"amount\":").Append(node.Amount)
						.Append('}');
				}

				builder.Append(']');
			}

			// 자리마다의 솥 — 바뀐 프레임에만 실린다(여럿이 각자 젓는 것을 창이 봐야 한다).
			if (cauldrons != null)
			{
				builder.Append(",\"cauldrons\":[");

				bool firstPot = true;
				IEnumerable<Vector3Int> cells = cauldronCells ?? cauldrons.Cells();
				foreach (Vector3Int cell in cells)
				{
					WorldCauldron pot = cauldrons.At(cell);
					if (pot == null)
						continue;

					if (firstPot == false)
						builder.Append(',');

					firstPot = false;
					DomainSDK.Alchemy.BrewState state = pot.State;
					builder.Append("{\"x\":").Append(cell.x)
						.Append(",\"y\":").Append(cell.y)
						.Append(",\"z\":").Append(cell.z)
						.Append(",\"px\":").Append(state.Position.X.ToString("F3"))
						.Append(",\"py\":").Append(state.Position.Y.ToString("F3"))
						.Append(",\"steps\":").Append(state.StepCount)
						.Append(",\"side\":").Append(state.AccruedSideEffect.ToString("F3"))
						.Append('}');
				}

				builder.Append(']');
			}

			builder.Append('}');
			return builder.ToString();
		}
	}
}
