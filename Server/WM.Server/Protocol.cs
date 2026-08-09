using System.Collections.Generic;
using System.Text;

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
		public const string CONSUME = Net.NetMessageType.CONSUME;

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
			builder.Append("export interface WorldTime {\n\tyear: number;\n\tseason: number;\n\tday: number;\n\thour: number;\n\tminute: number;\n}\n\n");

			builder.Append("/** 서버 -> 창: 지금 세계는 이렇게 생겼다. */\n");
			builder.Append("export interface WorldSnapshot {\n\ttype: '").Append(WORLD).Append("';\n\tdolls: WorldDollView[];\n\ttime?: WorldTime;\n}\n\n");

			builder.Append("/** 창 -> 서버: 이쪽으로 가고 싶다(얼마나 갈지는 서버가 정한다). */\n");
			builder.Append("export interface MoveRequest {\n\ttype: '").Append(MOVE).Append("';\n\tx: number;\n\tz: number;\n}\n\n");

			builder.Append("/** 창 -> 서버: 이 칸의 건물을 부수고 싶다. */\n");
			builder.Append("export interface RemoveRequest {\n\ttype: '").Append(REMOVE).Append("';\n\tx: number;\n\ty: number;\n\tz: number;\n}\n\n");

			builder.Append("/** 창 -> 서버: 솥을 한 번 젓는다(모두가 같은 솥). */\n");
			builder.Append("export interface BrewRequest {\n\ttype: '").Append(BREW).Append("';\n\tdx: number;\n\tdy: number;\n\tgrind: number;\n}\n\n");

			builder.Append("/** 창 -> 서버: 솥을 비운다. */\n");
			builder.Append("export interface BrewResetRequest {\n\ttype: '").Append(BREW_RESET).Append("';\n}\n\n");

			builder.Append("/** 창 -> 서버: 이 솥을 완성으로 가져가겠다(선착순 한 번). */\n");
			builder.Append("export interface BrewCompleteRequest {\n\ttype: '").Append(BREW_COMPLETE).Append("';\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 완성은 네 것이다. */\n");
			builder.Append("export interface BrewTaken {\n\ttype: '").Append(BREW_TAKEN).Append("';\n\tx: number;\n\ty: number;\n\tsteps: number;\n\tside: number;\n}\n\n");

			builder.Append("/** 창 -> 서버: 내 가방 좀 알려줘. */\n");
			builder.Append("export interface BagAsk {\n\ttype: '").Append(BAG_ASK).Append("';\n}\n\n");

			builder.Append("/** 창 -> 서버: 이걸 썼다(제작 재료 등). 안 알리면 쓴 게 다시 생긴다. */\n");
			builder.Append("export interface ConsumeRequest {\n\ttype: '").Append(CONSUME).Append("';\n\titemId: number;\n\tamount: number;\n}\n\n");

			builder.Append("/** 서버 -> 그 창에게만: 네 가방은 이렇다. */\n");
			builder.Append("export interface Bag {\n\ttype: '").Append(BAG).Append("';\n\titems: { itemId: number; amount: number }[];\n}\n\n");

			builder.Append("export type ServerMessage = Welcome | WorldSnapshot | BrewTaken | Bag;\n");
			builder.Append("export type ClientMessage = MoveRequest | RemoveRequest | BrewRequest | BrewResetRequest | BrewCompleteRequest | Hello | BagAsk | ConsumeRequest;\n");

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

		/// <summary>그 창에게만: 완성은 네 것이다(선착순 한 번).</summary>
		public static string BrewTaken(DomainSDK.Alchemy.BrewState state)
		{
			return "{\"type\":\"" + BREW_TAKEN + "\",\"x\":" + state.Position.X.ToString("F3")
				+ ",\"y\":" + state.Position.Y.ToString("F3")
				+ ",\"steps\":" + state.StepCount
				+ ",\"side\":" + state.AccruedSideEffect.ToString("F3") + "}";
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
		public static string WorldSnapshot(IEnumerable<WorldDoll> dolls, IEnumerable<PlacedBuilding> buildings, WorldCalendar calendar = null, WorldCauldron cauldron = null)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{\"type\":\"").Append(WORLD).Append("\",\"dolls\":[");

			bool first = true;
			foreach (WorldDoll doll in dolls)
			{
				if (first == false)
					builder.Append(',');

				first = false;
				builder.Append("{\"id\":").Append(doll.Id)
					.Append(",\"x\":").Append(doll.Position.x.ToString("F3"))
					.Append(",\"z\":").Append(doll.Position.z.ToString("F3"))
					.Append('}');
			}

			builder.Append("],\"buildings\":[");

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

			// 세계의 시각 — 서버가 굴린다(내가 없어도 밤이 온다). 창은 받아서 보여 주기만 한다.
			if (calendar != null)
			{
				builder.Append(",\"time\":{\"year\":").Append(calendar.Year)
					.Append(",\"season\":").Append(calendar.Season)
					.Append(",\"day\":").Append(calendar.Day)
					.Append(",\"hour\":").Append(calendar.Hour)
					.Append(",\"minute\":").Append(calendar.Minute)
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

			builder.Append('}');
			return builder.ToString();
		}
	}
}
