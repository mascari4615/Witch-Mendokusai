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
		public const string CATALOG = Net.NetMessageType.CATALOG;
		public const string BUILD_CATALOG = Net.NetMessageType.BUILD_CATALOG;
		public const string BREW_SHELF = Net.NetMessageType.BREW_SHELF;
		public const string SPELLBOOK = Net.NetMessageType.SPELLBOOK;
		public const string DENIED = Net.NetMessageType.DENIED;

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

		/// <summary>계약을 웹이 읽을 수 있는 형태로 뽑는다.</summary>
		public static string ToTypeScript()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("// 자동 생성물 — 손으로 고치지 마라 (TASK-WM-216).\n");
			builder.Append("// 정본 = WitchMendokusai/Server/WM.Server/Protocol.cs\n");
			builder.Append("// 서버가 계약을 소유하고, 이 파일은 거기서 뽑혀 나온다.\n\n");

			builder.Append("/** 창 -> 서버: 나 왔다(열쇠가 있으면 같이). 첫 말이다. */\n");
			builder.Append("export interface Hello {\n\ttype: '").Append(HELLO).Append("';\n\tsecret: string;\n}\n\n");

			builder.Append("/** 서버 -> 창: 접속했다. secret 이 비어있지 않으면 새로 받은 열쇠(적어 둘 것). */\n");
			builder.Append("export interface Welcome {\n\ttype: '").Append(WELCOME).Append("';\n\tid: number;\n\tidentityId: number;\n\tsecret: string;\n}\n\n");

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
			builder.Append("export interface WorldSnapshot {\n\ttype: '").Append(WORLD).Append("';\n\tdolls: WorldDollView[];\n\tbuildings?: WorldBuildingView[];\n\tgatherables?: GatherableView[];\n\tcauldrons?: CauldronView[];\n\ttime?: WorldTime;\n\tbrew?: BrewView;\n}\n\n");

			builder.Append("/** 창 -> 서버: 이쪽으로 가고 싶다(얼마나 갈지는 서버가 정한다). */\n");
			builder.Append("export interface MoveRequest {\n\ttype: '").Append(MOVE).Append("';\n\tx: number;\n\tz: number;\n}\n\n");

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

			builder.Append("export type ServerMessage = Welcome | WorldSnapshot | BrewTaken | Bag | Catalog | BuildCatalog | BrewShelf | Spellbook | Chest | Denied | Invite | Linked | Kicked;\n");
			builder.Append("export type ClientMessage = MoveRequest | PlaceRequest | RemoveRequest | GatherRequest | ChestAsk | ChestPut | ChestTake | BrewRequest | BrewResetRequest | BrewCompleteRequest | Hello | BagAsk | ConsumeRequest | InviteAsk | LinkRequest;\n");

			return builder.ToString();
		}

		/// <summary>그 창에게만 보내는 가방 상태.</summary>
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
		public static string Kicked()
		{
			return "{\"type\":\"" + KICKED + "\",\"reason\":\"다른 곳에서 접속했다\"}";
		}

		/// <summary>서버가 보내는 인사말.</summary>
		public static string Welcome(int dollId, string newSecret = "", int identityId = 0)
		{
			string secret = string.IsNullOrEmpty(newSecret) ? string.Empty : newSecret;
			return "{\"type\":\"" + WELCOME + "\",\"id\":" + dollId
				+ ",\"identityId\":" + identityId
				+ ",\"secret\":\"" + secret + "\"}";
		}

		/// <summary>서버가 보내는 세계 모습.</summary>
		public static string WorldSnapshot(IEnumerable<WorldDoll> dolls, IEnumerable<PlacedBuilding> buildings, WorldCalendar calendar = null, WorldCauldron cauldron = null, IEnumerable<GatherableNode> gatherables = null, System.Func<int, string> nameOf = null, WorldCauldrons cauldrons = null)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(WORLD).Append("\",\"dolls\":[");

			bool first = true;
			foreach (WorldDoll doll in dolls)
			{
				if (first == false)
					builder.Append(',');

				first = false;
				string who = nameOf == null ? string.Empty : (nameOf(doll.IdentityId) ?? string.Empty);
				builder.Append("{\"id\":").Append(doll.Id)
					.Append(",\"x\":").Append(doll.Position.x.ToString("F3"))
					.Append(",\"z\":").Append(doll.Position.z.ToString("F3"))
					.Append(",\"name\":").Append(JsonSerializer.Serialize(who, textOptions))
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
			if (gatherables != null)
			{
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
				foreach (Vector3Int cell in cauldrons.Cells())
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
