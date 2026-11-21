using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 연출의 실제 시간 한 프레임. 프레임 녹화 중 (DevFrameRecorder, Time.captureFramerate) 에는 고정 간격, 아니면 실제 경과
	/// - 녹화가 실제보다 느리게 돌아도 흔들림 감쇠와 가장자리 사라짐이 녹화 영상에서 실제 속도로 보이게
	/// - 궁극기 진행기 (UltimateFxRun) 와 플레이어 피격 연출이 같이 씀
	/// </summary>
	public static class FeelTime
	{
		public static float Delta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
	}
}
