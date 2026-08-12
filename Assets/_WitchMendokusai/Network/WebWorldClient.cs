using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임을 <b>웹 창과 같은 서버</b>에 붙인다 (TASK-WM-216).
	///
	/// 기존 FishNet 경로는 그대로 둔다 — 이미 라이브로 검증된 자산이고(둘이 만나 같이 걷는다),
	/// 이 통로는 아직 골격이다. **공존**시키고, 서버가 기능적으로 앞설 때 교체를 다시 판단한다.
	/// 그래야 회귀 0 으로 붙일 수 있다.
	///
	/// 말의 모양은 판정 층(<see cref="WitchMendokusai.Net"/>)이 정본 — 웹·서버와 같은 소스다.
	/// </summary>
	public sealed class WebWorldClient : MonoBehaviour, IWorldLink
	{
		[Header("붙을 서버")]
		[SerializeField] private string serverUrl = "wss://wm.mascari4615.com/ws";
		[SerializeField] private bool connectOnStart = false;

		private ClientWebSocket socket;
		private CancellationTokenSource cancellation;

		// 끊기면 스스로 다시 붙는다 (TASK-WM-217) — 간격 규칙은 판정 층이 정한다.
		private readonly ReconnectBackoff backoff = new ReconnectBackoff();
		private bool wantConnection;
		private bool receivedIdentityWelcome;
		private bool receivedInitialWorld;
		private long lastWorldSequence;

		/// <summary>몰린 자리에서 세계가 따로 알려 준 내 인형 — 공유 소식에 내가 없을 때 끼워 넣는다.</summary>
		private WorldDollView myPlaceFromWorld;

		/// <summary>번호 → 이름 (TASK-WM-220) — 자리와 달리 바뀔 때만 온다.</summary>
		private readonly System.Collections.Generic.Dictionary<int, string> dollNames =
			new System.Collections.Generic.Dictionary<int, string>();

		/// <summary>서버가 준 내 인형 번호. 아직 못 받았으면 0.</summary>
		public int MyDollId { get; private set; }

		/// <summary>세계가 아는 나(신원 번호). 인사에 답이 오면 채워진다.</summary>
		public int MyIdentityId { get; private set; }

		/// <summary>서버가 마지막으로 알려준 세계. 그리는 쪽이 읽어 간다.</summary>
		public WorldDollView[] Dolls { get; private set; } = Array.Empty<WorldDollView>();

		/// <summary>서버가 마지막으로 알려준 건물들.</summary>
		public BuildingView[] Buildings { get; private set; } = Array.Empty<BuildingView>();

		/// <summary>서버가 마지막으로 알려준 세계의 시각. 아직 못 받았으면 null.</summary>
		public WorldTimeView Time { get; private set; }

		/// <summary>다른 곳에서 같은 사람이 들어와 밀려났나 — 화면이 사람에게 알려 줄 때 쓴다.</summary>
		public bool Kicked { get; private set; }

		/// <summary>서버가 마지막으로 알려준 솥. 아직 못 받았으면 null.</summary>
		public WorldBrewView Brew { get; private set; }

		private WorldBrewView completed;

		public bool IsConnected => socket != null && socket.State == WebSocketState.Open;

		/// <summary>같은 줄 규약 — 게임은 어디에 붙었는지 묻지 않는다 (TASK-WM-217).</summary>
		public bool IsLinked => IsConnected && receivedIdentityWelcome && receivedInitialWorld;

		private void Start()
		{
			if (connectOnStart)
				Connect();
		}

		/// <summary>붙을 곳을 바꾼다 — 들어가기 전에만 뜻이 있다.</summary>
		public void SetServerUrl(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return;

			serverUrl = url;
		}

		public void Connect()
		{
			if (socket != null)
				return;

			wantConnection = true;
			Kicked = false;
			ResetHandshakeState();
			cancellation = new CancellationTokenSource();
			_ = RunUntilStoppedAsync(cancellation.Token);
		}

		/// <summary>
		/// 붙어 있는 동안 계속 듣고, 끊기면 <b>기다렸다 다시</b> 붙는다.
		/// 사람이 그만두라고 하기 전까지(<see cref="Disconnect"/>) 포기하지 않는다 —
		/// 서버가 잠깐 재시작하는 동안 게임이 죽어 있으면 안 된다.
		/// </summary>
		private async Task RunUntilStoppedAsync(CancellationToken token)
		{
			while (wantConnection && token.IsCancellationRequested == false)
			{
				socket = new ClientWebSocket();
				ResetHandshakeState();
				await RunAsync(token);

				socket?.Dispose();
				socket = null;
				MyDollId = 0;
				MyIdentityId = 0;
				Dolls = Array.Empty<WorldDollView>();
				ResetHandshakeState();

				if (wantConnection == false || token.IsCancellationRequested)
					break;

				float delay = backoff.NextDelay();
				Debug.Log($"{nameof(WebWorldClient)}: {delay:0.#}초 뒤 다시 붙어 본다 (헛걸음 {backoff.Attempts}회)");
				try
				{
					await Task.Delay((int)(delay * 1000f), token);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		public void Disconnect()
		{
			wantConnection = false;
			cancellation?.Cancel();
			socket?.Dispose();
			socket = null;
			MyDollId = 0;
			MyIdentityId = 0;
			Dolls = Array.Empty<WorldDollView>();
			ResetHandshakeState();
		}

		private void ResetHandshakeState()
		{
			receivedIdentityWelcome = false;
			receivedInitialWorld = false;
			lastWorldSequence = 0;
		}

		private void OnDestroy() => Disconnect();

		private async Task RunAsync(CancellationToken token)
		{
			try
			{
				await socket.ConnectAsync(new Uri(serverUrl), token);
				backoff.Reset(); // 붙었다 — 다음에 끊기면 다시 빠르게 시도한다.

				// 첫 말은 인사 (TASK-WM-218) — 기기에 적어 둔 열쇠가 있으면 같이 낸다.
				// 없으면 세계가 새 사람으로 받고 새 열쇠를 준다.
				// KarmoLab 연결 코드가 적혀 있으면 같이 낸다 — 그러면 어느 기기에서든 그 계정의 나다.
				// (쿠키는 도메인이 달라 게임 창에서 못 읽는다. 그래서 코드로 온다.)
				SendRaw(JsonUtility.ToJson(new HelloMessage
				{
					secret = WorldKeyStore.Load(),
					klCode = WorldKeyStore.LoadAccountCode(),
				}));

				// ★ 한 번의 Receive 가 <b>말 한 마디를 다 주지 않는다</b> (실측 2026-08-10).
				//   낱말표(139종)·들판(169자리)이 실리면서 알림이 8KB 를 넘자, 조각난 앞부분만 파싱하다
				//   매번 「JSON parse error」로 끊겼다 — 게임은 세계에 <b>영영 못 붙었다</b>.
				//   (웹 창은 브라우저가 조각을 합쳐 줘서 멀쩡해 보였다 = 한쪽만 도는 세계.)
				//   그래서 <b>끝 표시가 올 때까지</b> 이어 붙인다.
				byte[] buffer = new byte[8192];
				StringBuilder inbox = new StringBuilder();
				while (token.IsCancellationRequested == false && socket.State == WebSocketState.Open)
				{
					WebSocketReceiveResult received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
					if (received.MessageType == WebSocketMessageType.Close)
						break;

					inbox.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
					if (received.EndOfMessage == false)
						continue;

					string message = inbox.ToString();
					inbox.Clear();
					HandleMessage(message);
				}
			}
			catch (OperationCanceledException)
			{
				// 우리가 끊은 것 — 사고 아님.
			}
			catch (Exception exception)
			{
				// 조용히 죽는 통로가 이 프로젝트의 단골 실패 모양이라, 반드시 남긴다.
				Debug.LogWarning($"{nameof(WebWorldClient)}: 연결이 끊겼다 — {exception.Message}");
			}
		}

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

			if (type == NetMessageType.DENIED)
			{
				// 거절도 대답이다 — 게임 창에서도 사람이 이유를 봐야 한다.
				DeniedMessage denied = JsonUtility.FromJson<DeniedMessage>(json);
				WorldNoticeBridge.Deliver(denied?.why);
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
				Dolls = WithNames(WithMyself(world.dolls ?? Array.Empty<WorldDollView>()));
				// ★ 안 실려 온 목록은 「비었다」가 아니라 「안 바뀌었다」다 (TASK-WM-217).
				//   비운 것으로 읽으면 집과 들판이 매 프레임 사라졌다 나타난다.
				// ⚠ 반대로 <b>빈 목록이 실려 온 것</b>은 진짜로 비었다는 뜻이다 — 길이로 거르면
				//   마지막 하나를 부순 순간 그것이 화면에 영영 남는다(실측 2026-08-10).
				if (world.buildings != null)
					Buildings = world.buildings;

				if (world.gatherables != null)
					Gatherables = world.gatherables;

				if (world.cauldrons != null)
					Cauldrons = world.cauldrons;

				// 시각은 서버가 보낼 때만 갱신한다 — 안 보낸 스냅샷 하나에 세계 시간이 0시로 튀면 안 된다.
				if (world.time != null)
					Time = world.time;

				if (world.brew != null)
					Brew = world.brew;
			}
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

		/// <summary>「이쪽으로 가고 싶다」를 보낸다. 얼마나 갈지는 서버가 정한다.</summary>
		public void RequestMove(float x, float z) => Send(JsonUtility.ToJson(new MoveMessage { x = x, z = z }));

		/// <summary>솥을 한 번 젓는다 — 모두가 같은 솥을 젓는다.</summary>
		public void RequestBrewStep(int itemId) => Send(JsonUtility.ToJson(new BrewMessage { itemId = itemId }));

		/// <summary>세계에 서 있는 솥들 — 알림마다 갈아 끼운다.</summary>
		public CauldronView[] Cauldrons { get; private set; } = Array.Empty<CauldronView>();

		public void RequestBrewStepAt(int itemId, int cellX, int cellY, int cellZ)
		{
			Send(JsonUtility.ToJson(new CauldronMessage { type = NetMessageType.BREW, itemId = itemId, x = cellX, y = cellY, z = cellZ }));
		}

		public void RequestBrewResetAt(int cellX, int cellY, int cellZ)
		{
			Send(JsonUtility.ToJson(new CauldronMessage { type = NetMessageType.BREW_RESET, x = cellX, y = cellY, z = cellZ }));
		}

		public void RequestBrewCompleteAt(int cellX, int cellY, int cellZ)
		{
			Send(JsonUtility.ToJson(new CauldronMessage { type = NetMessageType.BREW_COMPLETE, x = cellX, y = cellY, z = cellZ }));
		}

		/// <summary>솥을 비운다.</summary>
		public void RequestBrewReset() => Send(JsonUtility.ToJson(new BrewResetMessage()));

		/// <summary>완성을 달라고 한다 — 줄지는 서버가 정한다(선착순 한 번).</summary>
		public void RequestBrewComplete() => Send(JsonUtility.ToJson(new BrewCompleteMessage()));

		/// <summary>서버가 내준 완성. 한 번 읽으면 비운다(두 번 채점하지 않게).</summary>
		public WorldBrewView TakeCompletedBrew()
		{
			WorldBrewView taken = completed;
			completed = null;
			return taken;
		}

		/// <summary>「이 칸을 부수고 싶다」 — 정말 사라질지는 서버가 정한다.</summary>
		public void RequestRemove(int cellX, int cellY, int cellZ)
		{
			Send(JsonUtility.ToJson(new RemoveMessage { x = cellX, y = cellY, z = cellZ }));
		}

		/// <summary>「여기에 짓고 싶다」 — 겹치는지는 서버가 본다.</summary>
		public void RequestPlace(int cellX, int cellY, int cellZ, int buildingId)
		{
			PlaceMessage message = new PlaceMessage
			{
				x = cellX,
				y = cellY,
				z = cellZ,
				buildingId = buildingId,
			};

			Send(JsonUtility.ToJson(message));
		}

		/// <summary>「이걸 줍고 싶다」 — 가방에 들어갈지는 서버가 본다.</summary>
		public void RequestGather(int nodeId) => Send(JsonUtility.ToJson(new GatherMessage { nodeId = nodeId }));

		/// <summary>마지막으로 받은 상자 안 — 아직 없으면 null.</summary>
		public ChestView Chest { get; private set; }

		/// <summary>세계의 마도서 — 들어올 때 한 번 받는다 (TASK-WM-217).</summary>
		public SpellbookPage[] Spellbook { get; private set; } = System.Array.Empty<SpellbookPage>();

		/// <summary>세계가 아는 지을 것 목록 — 재료까지 (TASK-WM-217).</summary>
		public BuildCatalogEntryView[] BuildCatalog { get; private set; } = System.Array.Empty<BuildCatalogEntryView>();

		/// <summary>세계가 아는 제작표 — 재료·성공률까지 (TASK-WM-217).</summary>
		public CraftBookEntryView[] CraftBook { get; private set; } = System.Array.Empty<CraftBookEntryView>();

		private CraftedMessage crafted;

		/// <summary>나를 이렇게 불러 달라 — 되나 안 되나는 세계가 본다.</summary>
		public void RequestRename(string name)
		{
			Send(JsonUtility.ToJson(new RenameMessage { name = name }));
		}

		/// <summary>이 줄대로 만들겠다 — 되나 안 되나는 세계가 정한다.</summary>
		public void RequestCraft(int recipeId)
		{
			Send(JsonUtility.ToJson(new CraftMessage { recipeId = recipeId }));
		}

		/// <summary>세계가 돌려준 제작 결과 — 한 번 읽으면 비운다(두 번 표시되지 않게).</summary>
		public CraftedMessage TakeCraftResult()
		{
			CraftedMessage taken = crafted;
			crafted = null;
			return taken;
		}

		/// <summary>세계가 아는 아이템 이름 — 「나무 1/2」의 「나무」.</summary>
		public CatalogEntry[] ItemNames { get; private set; } = System.Array.Empty<CatalogEntry>();

		public void RequestChest(int cellX, int cellY, int cellZ)
		{
			Send(JsonUtility.ToJson(new ChestMessage { type = NetMessageType.CHEST_ASK, x = cellX, y = cellY, z = cellZ }));
		}

		public void RequestChestPut(int cellX, int cellY, int cellZ, int itemId, int amount)
		{
			Send(JsonUtility.ToJson(new ChestMessage { type = NetMessageType.CHEST_PUT, x = cellX, y = cellY, z = cellZ, itemId = itemId, amount = amount }));
		}

		public void RequestChestTake(int cellX, int cellY, int cellZ, int itemId, int amount)
		{
			Send(JsonUtility.ToJson(new ChestMessage { type = NetMessageType.CHEST_TAKE, x = cellX, y = cellY, z = cellZ, itemId = itemId, amount = amount }));
		}

		/// <summary>세계에 서 있는 주울 것들 — 알림마다 갈아 끼운다.</summary>
		public GatherableView[] Gatherables { get; private set; } = Array.Empty<GatherableView>();

		/// <summary>「내 가방 뭐 있냐」고 묻는다 — 다시 들어왔을 때 화면을 채우려면 물어야 한다.</summary>
		public void AskBag() => Send(JsonUtility.ToJson(new BagAskMessage()));

		/// <summary>「이걸 썼다」 — 정말 있었는지는 서버가 본다.</summary>
		public void RequestConsume(int itemId, int amount)
		{
			Send(JsonUtility.ToJson(new ConsumeMessage { itemId = itemId, amount = amount }));
		}

		private void Send(string json)
		{
			if (IsLinked == false)
				return;

			SendRaw(json);
		}

		private void SendRaw(string json)
		{
			if (IsConnected == false)
				return;

			byte[] payload = Encoding.UTF8.GetBytes(json);
			_ = socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellation.Token);
		}
	}
}
