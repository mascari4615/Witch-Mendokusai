using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 말이 오가는 <b>구멍</b> (TASK-WM-411). 이 층은 「어떻게 나르는지」를 모른다 —
	/// 웹소켓이든, P2P(WebRTC/FishNet)든, 같은 프로세스 안이든 여기 꽂히면 똑같이 돈다.
	///
	/// ★ 왜 이렇게까지 얇은가: 대결을 <b>서버 심판</b>으로도 <b>P2P 호스트 심판</b>으로도 돌리고 싶기 때문이다.
	///   나르는 방법이 판정 코드에 스며들면 둘 중 하나를 고른 뒤 되돌릴 수 없다.
	/// </summary>
	public interface IVersusTransport
	{
		/// <summary>아직 살아 있나. 끊긴 구멍에 보내는 것은 조용히 버린다.</summary>
		bool IsOpen { get; }

		/// <summary>한 줄 보낸다.</summary>
		void Send(string message);

		/// <summary>그동안 도착한 것을 모두 꺼낸다(꺼내면 비워진다).</summary>
		void Drain(List<string> into);
	}

	/// <summary>
	/// 말을 글자로 바꾸고 되돌리는 법. 호스트마다 쓰는 도구가 달라서(서버 = System.Text.Json,
	/// 유니티 = JsonUtility, 웹 = JSON) 판정 층은 <b>모양만</b> 정하고 도구는 밖에서 꽂는다.
	/// </summary>
	public interface IVersusCodec
	{
		string Encode(object message);

		/// <summary>이 줄이 무슨 말인지(type 값). 못 알아들으면 빈 문자열.</summary>
		string TypeOf(string message);

		T Decode<T>(string message) where T : class;
	}

	/// <summary>
	/// 같은 프로세스 안에서 둘을 잇는 구멍 — 시험과 「혼자 연습」에 쓴다.
	/// 네트워크 없이도 심판·손님 코드를 <b>진짜로</b> 돌려 볼 수 있어야 회귀를 잡는다.
	/// </summary>
	public sealed class VersusLoopbackTransport : IVersusTransport
	{
		private readonly Queue<string> inbox = new Queue<string>();

		/// <summary>반대쪽 구멍. <see cref="Pair"/> 로 이어 준다.</summary>
		public VersusLoopbackTransport Other { get; private set; }

		public bool IsOpen { get; private set; } = true;

		/// <summary> 두 구멍을 마주 이어 준다 — 한쪽이 보내면 다른 쪽 받은함에 쌓인다. </summary>
		public static (VersusLoopbackTransport left, VersusLoopbackTransport right) Pair()
		{
			VersusLoopbackTransport left = new VersusLoopbackTransport();
			VersusLoopbackTransport right = new VersusLoopbackTransport();
			left.Other = right;
			right.Other = left;
			return (left, right);
		}

		public void Send(string message)
		{
			if (IsOpen == false || Other == null)
				return;

			Other.inbox.Enqueue(message);
		}

		public void Drain(List<string> into)
		{
			into.Clear();

			while (inbox.Count > 0)
				into.Add(inbox.Dequeue());
		}

		/// <summary> 끊긴 척한다 — 「상대가 나갔다」를 시험할 때. </summary>
		public void Close()
		{
			IsOpen = false;
		}
	}
}
