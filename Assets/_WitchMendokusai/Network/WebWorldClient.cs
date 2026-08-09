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
		[SerializeField] private string serverUrl = "ws://127.0.0.1:5199/ws";
		[SerializeField] private bool connectOnStart = false;

		private ClientWebSocket socket;
		private CancellationTokenSource cancellation;

		/// <summary>서버가 준 내 인형 번호. 아직 못 받았으면 0.</summary>
		public int MyDollId { get; private set; }

		/// <summary>서버가 마지막으로 알려준 세계. 그리는 쪽이 읽어 간다.</summary>
		public WorldDollView[] Dolls { get; private set; } = Array.Empty<WorldDollView>();

		/// <summary>서버가 마지막으로 알려준 건물들.</summary>
		public BuildingView[] Buildings { get; private set; } = Array.Empty<BuildingView>();

		/// <summary>서버가 마지막으로 알려준 세계의 시각. 아직 못 받았으면 null.</summary>
		public WorldTimeView Time { get; private set; }

		public bool IsConnected => socket != null && socket.State == WebSocketState.Open;

		/// <summary>같은 줄 규약 — 게임은 어디에 붙었는지 묻지 않는다 (TASK-WM-217).</summary>
		public bool IsLinked => IsConnected;

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

			cancellation = new CancellationTokenSource();
			socket = new ClientWebSocket();
			_ = RunAsync(cancellation.Token);
		}

		public void Disconnect()
		{
			cancellation?.Cancel();
			socket?.Dispose();
			socket = null;
			MyDollId = 0;
			Dolls = Array.Empty<WorldDollView>();
		}

		private void OnDestroy() => Disconnect();

		private async Task RunAsync(CancellationToken token)
		{
			try
			{
				await socket.ConnectAsync(new Uri(serverUrl), token);

				byte[] buffer = new byte[8192];
				while (token.IsCancellationRequested == false && socket.State == WebSocketState.Open)
				{
					WebSocketReceiveResult received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
					if (received.MessageType == WebSocketMessageType.Close)
						break;

					HandleMessage(Encoding.UTF8.GetString(buffer, 0, received.Count));
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
			if (json.Contains("\"" + NetMessageType.WELCOME + "\""))
			{
				WelcomeMessage welcome = JsonUtility.FromJson<WelcomeMessage>(json);
				MyDollId = welcome.id;
				return;
			}

			if (json.Contains("\"" + NetMessageType.WORLD + "\""))
			{
				WorldMessage world = JsonUtility.FromJson<WorldMessage>(json);
				Dolls = world.dolls ?? Array.Empty<WorldDollView>();
				Buildings = world.buildings ?? Array.Empty<BuildingView>();

				// 시각은 서버가 보낼 때만 갱신한다 — 안 보낸 스냅샷 하나에 세계 시간이 0시로 튀면 안 된다.
				if (world.time != null)
					Time = world.time;
			}
		}

		/// <summary>「이쪽으로 가고 싶다」를 보낸다. 얼마나 갈지는 서버가 정한다.</summary>
		public void RequestMove(float x, float z) => Send(JsonUtility.ToJson(new MoveMessage { x = x, z = z }));

		/// <summary>「여기에 짓고 싶다」 — 겹치는지는 서버가 본다.</summary>
		public void RequestPlace(int cellX, int cellY, int cellZ, int width, int length, int buildingId)
		{
			PlaceMessage message = new PlaceMessage
			{
				x = cellX,
				y = cellY,
				z = cellZ,
				w = width,
				l = length,
				buildingId = buildingId,
			};

			Send(JsonUtility.ToJson(message));
		}

		/// <summary>「이걸 줍고 싶다」 — 가방에 들어갈지는 서버가 본다.</summary>
		public void RequestGather(int itemId, int amount) => Send(JsonUtility.ToJson(new GatherMessage { itemId = itemId, amount = amount }));

		private void Send(string json)
		{
			if (IsConnected == false)
				return;

			byte[] payload = Encoding.UTF8.GetBytes(json);
			_ = socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellation.Token);
		}
	}
}
