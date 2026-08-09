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
		public const string WELCOME = Net.NetMessageType.WELCOME;
		public const string WORLD = Net.NetMessageType.WORLD;
		public const string MOVE = Net.NetMessageType.MOVE;
		public const string PLACE = Net.NetMessageType.PLACE;
		public const string GATHER = Net.NetMessageType.GATHER;
		public const string BAG = Net.NetMessageType.BAG;

		/// <summary>계약을 웹이 읽을 수 있는 형태로 뽑는다.</summary>
		public static string ToTypeScript()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("// 자동 생성물 — 손으로 고치지 마라 (TASK-WM-216).\n");
			builder.Append("// 정본 = WitchMendokusai/Server/WM.Server/Protocol.cs\n");
			builder.Append("// 서버가 계약을 소유하고, 이 파일은 거기서 뽑혀 나온다.\n\n");

			builder.Append("/** 서버 -> 창: 접속했다. 네 인형 번호는 이것이다. */\n");
			builder.Append("export interface Welcome {\n\ttype: '").Append(WELCOME).Append("';\n\tid: number;\n}\n\n");

			builder.Append("/** 세계에 있는 인형 하나. */\n");
			builder.Append("export interface WorldDollView {\n\tid: number;\n\tx: number;\n\tz: number;\n}\n\n");

			builder.Append("/** 서버 -> 창: 지금 세계는 이렇게 생겼다. */\n");
			builder.Append("export interface WorldSnapshot {\n\ttype: '").Append(WORLD).Append("';\n\tdolls: WorldDollView[];\n}\n\n");

			builder.Append("/** 창 -> 서버: 이쪽으로 가고 싶다(얼마나 갈지는 서버가 정한다). */\n");
			builder.Append("export interface MoveRequest {\n\ttype: '").Append(MOVE).Append("';\n\tx: number;\n\tz: number;\n}\n\n");

			builder.Append("export type ServerMessage = Welcome | WorldSnapshot;\n");
			builder.Append("export type ClientMessage = MoveRequest;\n");

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

		/// <summary>서버가 보내는 인사말.</summary>
		public static string Welcome(int dollId)
		{
			return "{\"type\":\"" + WELCOME + "\",\"id\":" + dollId + "}";
		}

		/// <summary>서버가 보내는 세계 모습.</summary>
		public static string WorldSnapshot(IEnumerable<WorldDoll> dolls, IEnumerable<PlacedBuilding> buildings)
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

			builder.Append("]}");
			return builder.ToString();
		}
	}
}
