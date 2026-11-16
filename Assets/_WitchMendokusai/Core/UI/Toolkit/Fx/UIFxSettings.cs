using UnityEngine;

namespace WitchMendokusai
{
	public enum ScreenWipeStyle
	{
		Fade,
		Diamond,
	}

	// UI 연출 수치 한 곳. Resources/UIFxSettings.asset, 실행 중 인스펙터에서 조절
	[CreateAssetMenu(fileName = "UIFxSettings", menuName = "WM/UI/UI Fx Settings")]
	public sealed class UIFxSettings : ScriptableObject
	{
		private const string RESOURCE = "UIFxSettings";
		private static UIFxSettings instance;

		public static UIFxSettings Instance
		{
			get
			{
				if (instance == null)
				{
					instance = Resources.Load<UIFxSettings>(RESOURCE);
					Debug.Assert(instance != null, $"Resources/{RESOURCE}.asset 없음");
				}
				return instance;
			}
		}

		[Header("화면 전환 (씬 교체, 포털)")]
		public ScreenWipeStyle wipeStyle = ScreenWipeStyle.Diamond;
		public Color wipeColor = new(0.10f, 0.08f, 0.16f, 1f);
		[Tooltip("마름모 한 칸 크기, 기준 해상도 픽셀")]
		public float wipeCellSize = 72f;
		[Tooltip("대각선 끝 칸이 첫 칸보다 늦게 시작하는 비율. 0 이면 동시")]
		[Range(0f, 3f)] public float wipeSpread = 1.2f;
		public float wipeCoverSeconds = 0.55f;
		public float wipeRevealSeconds = 0.6f;

		[Header("창 열기 반짝임")]
		public bool windowShine = true;
		public float shineDelaySeconds = 0.06f;
		public float shineSeconds = 0.42f;
		[Range(0f, 1f)] public float shineAlpha = 0.28f;
		[Tooltip("빛 띠 너비, 창 너비 대비")]
		[Range(0.05f, 1f)] public float shineWidthRatio = 0.22f;
		public float shineAngle = 18f;

		[Header("반짝이 입자 (창 열기, 패널 열기)")]
		public int sparkleCount = 10;
		public float sparkleLifeSeconds = 0.8f;
		public Vector2 sparkleSizeRange = new(6f, 14f);
		[Tooltip("위로 떠오르는 속도, 픽셀/초")]
		public float sparkleRiseSpeed = 28f;
		public Color sparkleColor = new(1f, 0.96f, 0.82f, 1f);

		[Header("떠다니는 빛 입자 (전체 패널 뒤)")]
		public bool ambientMotes = true;
		public int moteCount = 22;
		public Vector2 moteSizeRange = new(3f, 8f);
		public float moteRiseSpeed = 14f;
		[Range(0f, 1f)] public float moteAlpha = 0.55f;
		public Color moteColor = new(1f, 0.93f, 0.75f, 1f);

		[Header("자식 순차 등장 (전체 패널)")]
		public bool staggerChildren = true;
		public float staggerStepSeconds = 0.04f;
		public float staggerItemSeconds = 0.22f;
		public float staggerRisePixels = 14f;
	}
}
