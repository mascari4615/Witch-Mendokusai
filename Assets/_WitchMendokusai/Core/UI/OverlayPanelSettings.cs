using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 씬과 무관한 상주 오버레이 (FPS 숫자, 씬 전환 막) 의 패널 설정. 리소스 없으면 같은 값으로 생성
	public static class OverlayPanelSettings
	{
		private const string RESOURCE = "BuildStampPanelSettings";
		private static readonly Vector2Int FALLBACK_REFERENCE_RESOLUTION = new(1200, 800);

		public static PanelSettings Load()
		{
			PanelSettings settings = Resources.Load<PanelSettings>(RESOURCE);
			if (settings != null)
				return settings;

			settings = ScriptableObject.CreateInstance<PanelSettings>();
			settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
			settings.referenceResolution = FALLBACK_REFERENCE_RESOLUTION;
			settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
			settings.match = 1f;
			return settings;
		}
	}
}
