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
	public sealed class WebWorldClient : MonoBehaviour
	{
		[Header("붙을 서버")]
		[SerializeField] private string serverUrl = "ws://127.0.0.1:5199/ws";
		[SerializeField] private bool connectOnStart = false;

		[Header("보내는 간격 (초)")]
		[SerializeField] private float sendInterval = 0.1f;

		private ClientWebSocket socket;
		private CancellationTokenSource cancellation;
		private float sendTimer;

		/// <summary>서버가 준 내 인형 번호. 아직 못 받았으면 0.</summary>
		public int MyDollId { get; private set; }

		/// <summary>서버가 마지막으로 알려준 세계. 그리는 쪽이 읽어 간다.</summary>
		public DollView[] Dolls { get; private set; } = Array.Empty<DollView>();

		public bool IsConnected => socket != null && socket.State == WebSocketState.Open;

		private void Start()
		{
			if (connectOnStart)
				Connect();
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
			Dolls = Array.Empty<DollView>();
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
				Dolls = world.dolls ?? Array.Empty<DollView>();
			}
		}

		private void Update()
		{
			if (IsConnected == false)
				return;

			sendTimer += Time.deltaTime;
			if (sendTimer < sendInterval)
				return;

			sendTimer = 0f;
		}

		/// <summary>「이쪽으로 가고 싶다」를 보낸다. 얼마나 갈지는 서버가 정한다.</summary>
		public void RequestMove(float x, float z)
		{
			if (IsConnected == false)
				return;

			MoveMessage message = new MoveMessage { x = x, z = z };
			byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
			_ = socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellation.Token);
		}
	}
}
