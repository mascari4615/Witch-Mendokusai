using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// DomainSDK 의 말(<see cref="SdkLog"/>)을 Unity 콘솔에 잇는다 (TASK-WM-214).
	///
	/// SDK 는 엔진을 모르므로 스스로 콘솔에 못 찍는다. 이 설치기가 호스트 쪽 짝이다 —
	/// 플레이 시작 전(AfterAssembliesLoaded)과 에디터 로드 시 각각 한 번 꽂아,
	/// 씬을 열기만 해도(에디터 도구·EditMode) 경고가 보이게 한다.
	/// </summary>
	public static class SdkLogInstaller
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		public static void Install()
		{
			SdkLog.InfoSink = message => Debug.Log(message);
			SdkLog.WarningSink = message => Debug.LogWarning(message);
			SdkLog.ErrorSink = message => Debug.LogError(message);
		}

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
		private static void InstallInEditor() => Install();
#endif
	}
}
