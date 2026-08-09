using System;

namespace WitchMendokusai
{
	/// <summary>
	/// DomainSDK 가 "이건 이상하다" 를 알리는 유일한 통로 (TASK-WM-214).
	///
	/// 판정 코드가 <c>UnityEngine.Debug</c> 를 직접 부르면 엔진 밖에서 설 수 없다.
	/// 그래서 SDK 는 <b>말만 하고</b>, 어디에 찍을지는 호스트가 정한다 —
	/// Unity 는 콘솔(SdkLogInstaller), 서버는 자기 로거를 꽂는다.
	///
	/// 아무도 안 꽂았으면 조용하다. 경고가 사라지는 게 아니라 <b>들을 사람이 없는 것</b>이다.
	/// </summary>
	public static class SdkLog
	{
		public static Action<string> InfoSink = delegate { };
		public static Action<string> WarningSink = delegate { };
		public static Action<string> ErrorSink = delegate { };

		public static void Info(string message) => InfoSink(message);

		public static void Warning(string message) => WarningSink(message);

		public static void Error(string message) => ErrorSink(message);
	}
}
