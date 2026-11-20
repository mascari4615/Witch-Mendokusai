using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 3D 연출의 색 보정 (채도, 대비, 색 필터). UltimateWorldLighting 의 Volume (빛 번짐, 노출) 위에 우선순위가 높은 Volume 하나를 더 둠
	/// - 이 Volume 은 채도, 대비, 색 필터만 덮어써서 아래 Volume 의 노출 섬광은 그대로 섞임
	/// - Volume 은 UI 도 같이 바꿈: 채도 -80 이하와 대비 큰 값은 1~3 프레임만, 지속 탈색은 -40 까지 (memo wm/design/ult3d-all-20261120 공통 규칙)
	/// </summary>
	public sealed class UltimateWorldGrade
	{
		private readonly GameObject volumeObject;
		private readonly VolumeProfile profile;
		private readonly ColorAdjustments adjustments;
		private bool disposed;

		public UltimateWorldGrade(float priority)
		{
			volumeObject = new GameObject("UltimateWorldGrade");
			Volume volume = volumeObject.AddComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = priority;
			profile = ScriptableObject.CreateInstance<VolumeProfile>();
			adjustments = profile.Add<ColorAdjustments>(true);
			adjustments.saturation.Override(0f);
			adjustments.contrast.Override(0f);
			adjustments.colorFilter.Override(Color.white);
			volume.sharedProfile = profile;
		}

		/// <summary> 채도 (-100~100), 대비 (-100~100), 색 필터 (곱하기) </summary>
		public void Set(float saturation, float contrast, Color filter)
		{
			if (disposed)
				return;
			adjustments.saturation.value = Mathf.Clamp(saturation, -100f, 100f);
			adjustments.contrast.value = Mathf.Clamp(contrast, -100f, 100f);
			adjustments.colorFilter.value = filter;
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			if (volumeObject != null)
				Object.Destroy(volumeObject);
			if (profile != null)
				Object.Destroy(profile);
		}
	}
}
