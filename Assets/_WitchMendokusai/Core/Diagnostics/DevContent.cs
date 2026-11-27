using UnityEngine;

namespace WitchMendokusai
{
	// 개발용 콘텐츠 (개발 창, 개발 전용 던전과 NPC와 스테이지와 포털, 개발 키, 시험 설치기, 도감의 개발 칸) 를 보일지의 판정 한 곳 (데모 정리 2026-11-27)
	// 전에는 13곳이 Debug.isDebugBuild 를 직접 읽었음. 데모에서 숨길 기준을 바꿀 때 이 한 곳만 고침
	// 지금 기준: 개발 빌드와 에디터는 보임, 릴리스 빌드는 숨김 (전과 같음)
	// 이 판정을 안 쓰는 곳
	//   빌드 종류를 설명하거나 빌드 종류로 설정을 고르는 진단 도구 (BuildInfo.channel, 성능 기록, BuildStampOverlay, DeviceLogRelay) 는 Debug.isDebugBuild 그대로
	//   WM.Idle 은 WM.Core 를 참조하지 않아 설정 팝업의 세이브 지우기 버튼이 Debug.isDebugBuild 를 직접 읽음
	public static class DevContent
	{
#if UNITY_EDITOR
		private static bool? overrideIsShown;

		public static bool IsShown => overrideIsShown ?? Debug.isDebugBuild;

		// EditMode 시험 전용. 대부분 호출 자리가 시작 때 한 번 읽으므로 실행 중 미리보기 용도로는 안 맞음. null 이면 빌드 기준
		public static void OverrideIsShownForTest(bool? isShown) => overrideIsShown = isShown;

		// 도메인 리로드를 끈 Play 에서도 이전 값이 남지 않게
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetOverride() => overrideIsShown = null;
#else
		public static bool IsShown => Debug.isDebugBuild;
#endif
	}
}
