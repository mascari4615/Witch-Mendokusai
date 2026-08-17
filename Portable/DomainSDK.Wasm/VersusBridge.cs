using System.Runtime.InteropServices.JavaScript;
using WitchMendokusai;

namespace WitchMendokusai.Wasm
{
	/// <summary>
	/// 브라우저와 판정 사이의 <b>얇은 창구</b> (TASK-WM-411).
	///
	/// ★ 규칙은 여기 없다. 서버·유니티가 쓰는 것과 <b>같은 소스</b>(<see cref="VersusRoundState"/> ·
	///   <see cref="VersusPredictor"/> · <see cref="VersusGuest"/>)를 웹어셈블리로 구운 것뿐이다.
	///   그래서 브라우저 창도 「미리 굴리고 되감기」를 그대로 한다 — JS 로 규칙을 다시 짜지 않는다.
	///
	/// 주고받는 것은 <b>글자(JSON)</b> 하나뿐이다. 자바스크립트가 웹소켓에서 받은 줄을 그대로 넣고,
	/// 그릴 것을 글자로 받아 간다. 경계가 좁을수록 갈릴 자리가 적다.
	/// </summary>
	public static partial class VersusBridge
	{
		private static VersusGuest guest;
		private static VersusSocketBridgeTransport transport;
		private static readonly JsonVersusCodec codec = new JsonVersusCodec();

		/// <summary> 창을 연다. 자리 번호는 서버가 알려 주기 전까지의 임시값이다. </summary>
		[JSExport]
		public static void Open(int seat)
		{
			transport = new VersusSocketBridgeTransport();
			guest = new VersusGuest(transport, codec, seat);
		}

		/// <summary> 서버에서 온 줄을 그대로 넣는다. </summary>
		[JSExport]
		public static void Receive(string message)
		{
			transport?.Deliver(message);
		}

		/// <summary> 도착한 것을 반영한다(되감기 포함). 매 프레임 한 번. </summary>
		[JSExport]
		public static void Pump()
		{
			guest?.Pump();
		}

		/// <summary>
		/// 한 틱 미리 굴리고 그 의도를 보낸다. 보낼 글자를 돌려주면 자바스크립트가 웹소켓에 흘린다
		/// (웹소켓을 C# 이 직접 잡지 않는다 — 창구는 좁게).
		/// </summary>
		[JSExport]
		public static string StepAndSend(float moveX, float moveY, float aimX, float aimY, bool fire, bool dash)
		{
			if (guest == null)
				return string.Empty;

			guest.StepAndSend(new VersusInputFrame
			{
				Move = new Numerics.Vector2(moveX, moveY),
				Aim = new Numerics.Vector2(aimX, aimY),
				Fire = fire,
				Dash = dash,
			});

			return transport.TakeOutgoing();
		}

		/// <summary> 「한 판 더」. </summary>
		[JSExport]
		public static string SendRematch()
		{
			guest?.SendRematch();
			return transport != null ? transport.TakeOutgoing() : string.Empty;
		}

		/// <summary> 카드 고르기. </summary>
		[JSExport]
		public static string SendPick(int index)
		{
			guest?.SendPick(index);
			return transport != null ? transport.TakeOutgoing() : string.Empty;
		}

		/// <summary>
		/// 지금 그릴 것 — <b>미리 굴린 판</b>에서 뽑는다(서버 그림이 아니라). 그래서 60Hz 로 부드럽고 즉시 반응한다.
		/// 라운드 재료가 아직 안 왔으면 빈 글자.
		/// </summary>
		[JSExport]
		public static string DrawState()
		{
			if (guest?.Predicted == null)
				return string.Empty;

			return codec.Encode(VersusViewPacket.From(guest));
		}

		/// <summary> 화면 위쪽에 띄울 것들(점수·카드 후보·끝났나). </summary>
		[JSExport]
		public static string HudState()
		{
			if (guest == null)
				return string.Empty;

			return codec.Encode(VersusHudPacket.From(guest));
		}
	}
}
