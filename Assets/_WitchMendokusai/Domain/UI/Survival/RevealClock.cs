using UnityEngine;

namespace WitchMendokusai
{
	// 공개 연출 시계. 시간 정지 중에도 흐르는 실제 시간
	// - 프레임 녹화 중 (DevFrameRecorder) 에는 고정 간격이라 녹화본이 실제 속도와 같음
	public static class RevealClock
	{
		public static float Delta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
	}
}
