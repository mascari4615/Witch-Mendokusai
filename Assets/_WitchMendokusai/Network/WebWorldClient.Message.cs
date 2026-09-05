using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	// WebWorldClient 의 받은 말 풀기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 WebWorldClient.cs 를 본다.
	public sealed partial class WebWorldClient : MonoBehaviour, IWorldLink
	{
		private void HandleMessage(string json)
		{
			NetMessageEnvelope envelope;
			try
			{
				envelope = JsonUtility.FromJson<NetMessageEnvelope>(json);
			}
			catch (ArgumentException)
			{
				Debug.LogWarning($"{nameof(WebWorldClient)}: invalid protocol message");
				return;
			}

			string type = envelope == null ? string.Empty : envelope.type;
			if (type == NetMessageType.WELCOME)
			{
				WelcomeMessage welcome = JsonUtility.FromJson<WelcomeMessage>(json);
				MyDollId = welcome.id;
				// The server sends a provisional welcome before hello, then sends the
				// identity-bearing welcome after adopting this connection.
				if (welcome.identityId != 0)
					receivedIdentityWelcome = true;

				// 0 이면 아직 인사 전이다 — 덮어쓰지 않는다(첫 환영에는 신원이 없다).
				if (welcome.identityId != 0)
					MyIdentityId = welcome.identityId;

				// 새 열쇠를 줬으면 적어 둔다 — 이게 있어야 다음에 「나」로 들어간다.
				if (string.IsNullOrEmpty(welcome.secret) == false)
					WorldKeyStore.Save(welcome.secret);

				return;
			}

			if (type == NetMessageType.CRAFT_BOOK)
			{
				CraftBookMessage book = JsonUtility.FromJson<CraftBookMessage>(json);
				CraftBook = book?.recipes ?? System.Array.Empty<CraftBookEntryView>();
				return;
			}

			if (type == NetMessageType.CRAFTED)
			{
				crafted = JsonUtility.FromJson<CraftedMessage>(json);
				return;
			}

			// ★ 말·싸움·국경 (TASK-WM-261). 웹 창만 다루고 게임 창이 안 다루면, 컴파일은 멀쩡한데
			//   게임 쪽 사람에게는 그 기능이 <b>조용히 없다</b> — 같은 세계에 사는 두 창이 갈린다.
			if (type == NetMessageType.SAID)
			{
				SaidMessage said = JsonUtility.FromJson<SaidMessage>(json);
				if (said != null)
					heard.Add(said);

				return;
			}

			if (type == NetMessageType.HURT)
			{
				HurtMessage hurt = JsonUtility.FromJson<HurtMessage>(json);
				if (hurt != null)
					hurts.Add(hurt);

				return;
			}

			if (type == NetMessageType.MOVE_ON)
			{
				// ⚠ 여기서 붙잡아 두기만 한다 — 저 세계로 옮겨 붙는 것은 줄을 쥔 쪽의 일이다.
				//   안 읽으면 국경에서 그 창만 멈춰 선다(세계는 이미 내보냈다).
				moveOn = JsonUtility.FromJson<MoveOnMessage>(json);
				return;
			}

			if (type == NetMessageType.BUILD_CATALOG)
			{
				// ★ 짓기 목록도 세계 것이어야 한다 (TASK-WM-217) — 자기 자산으로 늘어놓으면
				//   세계가 모르는 것을 고르게 되고, 그건 내 화면에만 섰다가 사라진다.
				BuildCatalogMessage catalog = JsonUtility.FromJson<BuildCatalogMessage>(json);
				BuildCatalog = catalog?.buildings ?? System.Array.Empty<BuildCatalogEntryView>();
				return;
			}

			// ★ 「catalog」는 「buildcatalog」 안에도 들어 있다 — 이름만 찾으면 순서에 기대는 코드가 된다.
			//   누가 위아래를 바꾸는 순간 건물 목록이 아이템 이름으로 읽힌다.
			if (type == NetMessageType.CATALOG)
			{
				CatalogMessage names = JsonUtility.FromJson<CatalogMessage>(json);
				ItemNames = names?.items ?? System.Array.Empty<CatalogEntry>();
				return;
			}

			if (type == NetMessageType.SPELLBOOK)
			{
				// ★ 화면의 목표도 세계 것이어야 한다 (TASK-WM-217) — 안 그러면 표시대로 저은 사람이 딴 것을 받는다.
				SpellbookMessage book = JsonUtility.FromJson<SpellbookMessage>(json);
				Spellbook = book?.pages ?? System.Array.Empty<SpellbookPage>();
				return;
			}

			if (type == NetMessageType.BREW_TAKEN)
			{
				BrewTakenMessage taken = JsonUtility.FromJson<BrewTakenMessage>(json);
				completed = new WorldBrewView
				{
					x = taken.x, y = taken.y, steps = taken.steps, side = taken.side,
					itemId = taken.itemId, amount = taken.amount, grade = taken.grade, recipe = taken.recipe,
				};
				return;
			}

			if (type == NetMessageType.KICKED)
			{
				// 다른 곳에서 같은 사람이 들어왔다. ★ 여기서 다시 붙으면 두 창이 서로를 밀어내며
				//   영원히 왕복한다 — 그래서 <b>다시 붙기를 끈다</b>.
				Debug.LogWarning($"{nameof(WebWorldClient)}: 다른 곳에서 접속했다 — 이 창은 세계에서 나간다.");
				Kicked = true;
				Disconnect();
				return;
			}

			if (type == NetMessageType.FULL)
			{
				// ★ 세계가 가득 찼다 (TASK-WM-349). 고장이 아니라 <b>상태</b>다 —
				//   말없이 끊기면 사람은 자기 인터넷을 의심한다. 그리고 여기서 다시 붙으면
				//   가득 찬 문을 계속 두드리는 꼴이라 <b>다시 붙기를 끈다</b>(밀려남과 같은 처리).
				FullMessage full = JsonUtility.FromJson<FullMessage>(json);
				Debug.LogWarning($"{nameof(WebWorldClient)}: 세계가 가득 찼다 — {full?.most ?? 0}명까지. 잠시 뒤 다시 오라.");
				Kicked = true;
				Disconnect();
				return;
			}

			if (type == NetMessageType.DENIED)
			{
				// 거절도 대답이다 — 게임 창에서도 사람이 이유를 봐야 한다.
				DeniedMessage denied = JsonUtility.FromJson<DeniedMessage>(json);
				WorldNoticeBridge.Deliver(denied?.why);
				return;
			}

			if (type == NetMessageType.GHOSTS)
			{
				// 세계가 「그 사람들 여기 없다」고 했다 (TASK-WM-329) — 그 자리에서 지운다.
				// ★ 왜 필요한가: 「나갔다」(gone)는 한 번밖에 안 온다. 그 한 번을 놓친 창은 그 사람을
				//   영영 유령으로 그린다 — 오류도 없이. 웹 창에는 이 덮개가 있는데 게임 창에만 없으면
				//   같은 세계에서 <b>보이는 것이 갈린다</b>.
				GhostsMessage ghosts = JsonUtility.FromJson<GhostsMessage>(json);
				if (ghosts?.ids != null && ghosts.ids.Length > 0)
					Dolls = WithoutThese(Dolls, ghosts.ids);

				return;
			}

			if (type == NetMessageType.CHEST)
			{
				Chest = JsonUtility.FromJson<ChestView>(json);
				return;
			}

			if (type == NetMessageType.BAG)
			{
				BagMessage bag = JsonUtility.FromJson<BagMessage>(json);
				DeliverBag(bag);
				return;
			}

			if (type == NetMessageType.NAMES)
			{
				// 이름은 바뀔 때만 온다 (TASK-WM-220) — 들고 있다가 인형에 붙인다.
				NamesMessage named = JsonUtility.FromJson<NamesMessage>(json);
				if (named?.dolls != null)
				{
					for (int i = 0; i < named.dolls.Length; i++)
						dollNames[named.dolls[i].id] = named.dolls[i].name ?? string.Empty;
				}

				return;
			}

			if (type == NetMessageType.ME)
			{
				// 몰린 자리에서는 소식 한 벌을 여럿이 같이 쓴다 — 그 한 벌에 내가 안 들어갔을 때
				// 세계가 내 자리만 따로 알려 준다(내가 안 보이면 화면이 통째로 멎는다).
				MeMessage mine = JsonUtility.FromJson<MeMessage>(json);
				if (mine?.doll != null)
					myPlaceFromWorld = mine.doll;

				return;
			}

			if (type == NetMessageType.WORLD)
			{
				WorldMessage world = JsonUtility.FromJson<WorldMessage>(json);
				if (world.sequence > 0 && world.sequence <= lastWorldSequence)
					return;

				if (world.sequence > 0)
					lastWorldSequence = world.sequence;

				receivedInitialWorld = true;
				Dolls = WithNames(WithMyself(MergeDolls(world)));
				// ★ 안 실려 온 목록은 「비었다」가 아니라 「안 바뀌었다」다 (TASK-WM-217).
				//   비운 것으로 읽으면 집과 들판이 매 프레임 사라졌다 나타난다.
				// ⚠ 반대로 <b>빈 목록이 실려 온 것</b>은 진짜로 비었다는 뜻이다 — 길이로 거르면
				//   마지막 하나를 부순 순간 그것이 화면에 영영 남는다(실측 2026-08-10).
				if (world.buildings != null)
					Buildings = world.buildings;

				if (world.gatherables != null || world.fieldGone != null)
					Gatherables = MergeField(world);

				if (world.cauldrons != null)
					Cauldrons = world.cauldrons;

				// 시각은 서버가 보낼 때만 갱신한다 — 안 보낸 스냅샷 하나에 세계 시간이 0시로 튀면 안 된다.
				if (world.time != null)
					Time = world.time;

				if (world.brew != null)
					Brew = world.brew;
			}
		}

		/// <summary>
		/// 「바뀐 것만」 온 판을 지난 판 위에 얹는다 (TASK-WM-220).
		/// ⚠ 안 실린 사람을 「사라졌다」로 읽으면 광장의 사람들이 매 판 깜빡인다.
		/// </summary>
		private WorldDollView[] MergeDolls(WorldMessage world)
		{
			WorldDollView[] coming = world.dolls ?? Array.Empty<WorldDollView>();
			if (world.changed == false)
				return coming;

			System.Collections.Generic.Dictionary<int, WorldDollView> byId =
				new System.Collections.Generic.Dictionary<int, WorldDollView>();
			for (int i = 0; i < Dolls.Length; i++)
				byId[Dolls[i].id] = Dolls[i];

			for (int i = 0; i < coming.Length; i++)
				byId[coming[i].id] = coming[i];

			if (world.gone != null)
			{
				for (int i = 0; i < world.gone.Length; i++)
					byId.Remove(world.gone[i]);
			}

			WorldDollView[] merged = new WorldDollView[byId.Count];
			byId.Values.CopyTo(merged, 0);
			return merged;
		}

		/// <summary>세계가 「없다」고 한 사람을 뺀 목록 (TASK-WM-329).</summary>
		private static WorldDollView[] WithoutThese(WorldDollView[] dolls, int[] ids)
		{
			System.Collections.Generic.List<WorldDollView> left =
				new System.Collections.Generic.List<WorldDollView>(dolls.Length);

			for (int i = 0; i < dolls.Length; i++)
			{
				bool ghost = false;
				for (int k = 0; k < ids.Length; k++)
				{
					if (dolls[i].id != ids[k])
						continue;

					ghost = true;
					break;
				}

				if (ghost == false)
					left.Add(dolls[i]);
			}

			return left.ToArray();
		}

		/// <summary>
		/// 「내가 이 사람들을 그리고 있다」를 세계에 묻는다 (TASK-WM-329).
		/// 부르는 쪽(장면)이 <b>이따금</b> 부른다 — 여기서 시계를 돌리면 시험이 어려워진다.
		/// </summary>
		public void AskWhoIsReal()
		{
			if (IsConnected == false || Dolls.Length == 0)
				return;

			int[] ids = new int[Dolls.Length];
			for (int i = 0; i < Dolls.Length; i++)
				ids[i] = Dolls[i].id;

			Send(JsonUtility.ToJson(new RosterMessage { ids = ids }));
		}

		/// <summary>「바뀐 자리만」 온 들판을 지난 것 위에 얹는다 (TASK-WM-220).</summary>
		private GatherableView[] MergeField(WorldMessage world)
		{
			GatherableView[] coming = world.gatherables ?? Array.Empty<GatherableView>();
			if (world.fieldChanged == false)
				return coming;

			System.Collections.Generic.Dictionary<int, GatherableView> byId =
				new System.Collections.Generic.Dictionary<int, GatherableView>();
			for (int i = 0; i < Gatherables.Length; i++)
				byId[Gatherables[i].id] = Gatherables[i];

			for (int i = 0; i < coming.Length; i++)
				byId[coming[i].id] = coming[i];

			if (world.fieldGone != null)
			{
				for (int i = 0; i < world.fieldGone.Length; i++)
					byId.Remove(world.fieldGone[i]);
			}

			GatherableView[] merged = new GatherableView[byId.Count];
			byId.Values.CopyTo(merged, 0);
			return merged;
		}

		/// <summary>따로 온 이름표를 인형에 붙인다 — 자리 소식에는 이름이 안 실린다 (TASK-WM-220).</summary>
		private WorldDollView[] WithNames(WorldDollView[] dolls)
		{
			for (int i = 0; i < dolls.Length; i++)
			{
				if (dollNames.TryGetValue(dolls[i].id, out string named))
					dolls[i].name = named;
			}

			return dolls;
		}

		/// <summary>공유 소식에 내 인형이 없으면 끼워 넣는다 — 내가 안 보이면 화면이 통째로 멎는다.</summary>
		private WorldDollView[] WithMyself(WorldDollView[] dolls)
		{
			if (myPlaceFromWorld == null)
				return dolls;

			for (int i = 0; i < dolls.Length; i++)
			{
				if (dolls[i].id == myPlaceFromWorld.id)
					return dolls;
			}

			WorldDollView[] withMe = new WorldDollView[dolls.Length + 1];
			Array.Copy(dolls, withMe, dolls.Length);
			withMe[dolls.Length] = myPlaceFromWorld;
			return withMe;
		}

		/// <summary>세계가 알려준 가방을 화면 쪽으로 넘긴다 — 다시 들어왔을 때 「내 것」이 보이게.</summary>
		private static void DeliverBag(BagMessage bag)
		{
			if (bag?.items == null)
				return;

			int[] ids = new int[bag.items.Length];
			int[] amounts = new int[bag.items.Length];
			for (int i = 0; i < bag.items.Length; i++)
			{
				ids[i] = bag.items[i].itemId;
				amounts[i] = bag.items[i].amount;
			}

			WorldBagBridge.DeliverBag(ids, amounts);
		}
	}
}
