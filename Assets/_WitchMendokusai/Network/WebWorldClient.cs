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
	public sealed partial class WebWorldClient : MonoBehaviour, IWorldLink
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

		/// <summary>세계에 서 있는 솥들 — 알림마다 갈아 끼운다.</summary>
		public CauldronView[] Cauldrons { get; private set; } = Array.Empty<CauldronView>();

		/// <summary>마지막으로 받은 상자 안 — 아직 없으면 null.</summary>
		public ChestView Chest { get; private set; }

		/// <summary>세계의 마도서 — 들어올 때 한 번 받는다 (TASK-WM-217).</summary>
		public SpellbookPage[] Spellbook { get; private set; } = System.Array.Empty<SpellbookPage>();

		/// <summary>세계가 아는 지을 것 목록 — 재료까지 (TASK-WM-217).</summary>
		public BuildCatalogEntryView[] BuildCatalog { get; private set; } = System.Array.Empty<BuildCatalogEntryView>();

		/// <summary>세계가 아는 제작표 — 재료·성공률까지 (TASK-WM-217).</summary>
		public CraftBookEntryView[] CraftBook { get; private set; } = System.Array.Empty<CraftBookEntryView>();

		/// <summary>세계가 아는 아이템 이름 — 「나무 1/2」의 「나무」.</summary>
		public CatalogEntry[] ItemNames { get; private set; } = System.Array.Empty<CatalogEntry>();

		/// <summary>세계에 서 있는 주울 것들 — 알림마다 갈아 끼운다.</summary>
		public GatherableView[] Gatherables { get; private set; } = Array.Empty<GatherableView>();

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
