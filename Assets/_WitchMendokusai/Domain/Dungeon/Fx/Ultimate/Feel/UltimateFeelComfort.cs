using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 연출 세기 줄이기 (광과민, 멀미 대비). 화면 연출 박자표 여럿이 같은 에셋 하나를 가리킴
	/// - 번쩍임 (노출 섬광, 임팩트 프레임) 은 눈이 아프다는 의견이 있어 따로 줄일 수 있게 둠
	/// - 1 이면 박자표 값 그대로, 0 이면 그 장치를 끔
	/// </summary>
	[CreateAssetMenu(fileName = "UltFeel_Comfort", menuName = "WM/CombatFx/Ultimate Feel Comfort")]
	public sealed class UltimateFeelComfort : ScriptableObject
	{
		[SerializeField, Range(0f, 1f), Tooltip("노출 섬광과 빛 번짐")] private float flash = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("임팩트 프레임 (흑백, 반전, 실루엣)")] private float impactFrame = 1f;
		[SerializeField, Min(0f), Tooltip("임팩트 프레임 최대 길이 (초). 1장은 24fps 기준 0.042")] private float impactFrameMaxSeconds = 0.09f;
		[SerializeField, Range(0f, 1f), Tooltip("화면 흔들림")] private float shake = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("시야각 펀치와 전진")] private float zoom = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("방사 흐림과 굴절 고리, 렌즈 왜곡")] private float distortion = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("정지와 느려짐. 0 이면 시간 배율을 바꾸지 않음")] private float timeWarp = 1f;

		public float Flash => flash;
		public float ImpactFrame => impactFrame;
		public float ImpactFrameMaxSeconds => impactFrameMaxSeconds;
		public float Shake => shake;
		public float Zoom => zoom;
		public float Distortion => distortion;
		public float TimeWarp => timeWarp;
	}
}
