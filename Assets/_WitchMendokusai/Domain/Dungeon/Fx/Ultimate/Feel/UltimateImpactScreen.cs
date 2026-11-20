using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 임팩트 화면 왜곡 값. 화면 연출 구동기가 매 프레임 넣고 끝에 끔 </summary>
	public static class UltimateImpactScreen
	{
		private static readonly int CENTER_ID = Shader.PropertyToID("_WMImpactCenter");
		private static readonly int RADIAL_ID = Shader.PropertyToID("_WMImpactRadial");
		private static readonly int RIPPLE_ID = Shader.PropertyToID("_WMImpactRipple");
		private static readonly int FRAME_ID = Shader.PropertyToID("_WMImpactFrame");

		public static bool Active { get; private set; }

		/// <summary> 왜곡을 받는 카메라 (연출 진행기의 주 카메라). 미리보기 카메라와 겹친 카메라는 제외 </summary>
		public static Camera TargetCamera { get; set; }

		/// <summary> 카메라 손이 줌과 흔들림을 얹은 뒤 표적의 화면 위치로 다시 넣음 </summary>
		public static void SetCenter(Vector2 viewportCenter, float aspect) => Shader.SetGlobalVector(CENTER_ID, new Vector4(viewportCenter.x, viewportCenter.y, aspect, 0f));

		public static void Set(Vector2 viewportCenter, float aspect, float radial, float radialReach, float rippleRadius, float rippleStrength, float rippleWidth, UltimateScreenFeel.ImpactFrameKind frameKind, float frameStrength)
		{
			Shader.SetGlobalVector(CENTER_ID, new Vector4(viewportCenter.x, viewportCenter.y, aspect, 0f));
			Shader.SetGlobalVector(RADIAL_ID, new Vector4(radial, radialReach, 0f, 0f));
			Shader.SetGlobalVector(RIPPLE_ID, new Vector4(rippleRadius, rippleStrength, rippleWidth, 0f));
			Shader.SetGlobalVector(FRAME_ID, new Vector4((float)frameKind, frameStrength, 0f, 0f));
			Active = radial > 0.001f || rippleStrength > 0.0001f || (frameKind != UltimateScreenFeel.ImpactFrameKind.None && frameStrength > 0.001f);
		}

		public static void Clear()
		{
			Active = false;
			TargetCamera = null;
			Shader.SetGlobalVector(RADIAL_ID, Vector4.zero);
			Shader.SetGlobalVector(RIPPLE_ID, Vector4.zero);
			Shader.SetGlobalVector(FRAME_ID, Vector4.zero);
		}
	}
}
