using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 이벤트를 실제로 <b>배달</b>하는 자리 (TASK-WM-214).
	///
	/// DomainSDK 는 "무슨 일이 일어났는가" 만 안다. 그걸 누구에게 어떻게 나르는지는 호스트의 몫이다 —
	/// Unity 는 MessagePipe(VContainer 배선), 헤드리스 서버·웹 백엔드는 자기 배달 통로를 꽂는다.
	/// 이 한 겹이 있어야 같은 판정 코드가 엔진 안팎에서 그대로 돈다.
	/// </summary>
	public interface IEventTransport
	{
		void Publish<T>(T evt);

		/// <summary>반환된 <see cref="IDisposable"/> 을 버리면 구독이 끊긴다.</summary>
		IDisposable Subscribe<T>(Action<T> handler);
	}
}
