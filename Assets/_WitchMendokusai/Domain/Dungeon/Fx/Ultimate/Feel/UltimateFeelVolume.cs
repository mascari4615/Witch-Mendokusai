using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 연출 구동기의 후처리 손. 가장 높은 우선순위 전역 Volume 하나를 두고, 매 프레임 아래 Volume 들이 정한 값에 연출 값을 더해 덮음
	/// - 기존 연출 (UltimateWorldLighting, UltimateWorldGrade) 의 장면 빛 곡선을 지우지 않고 그 위에 더함. 아래 값은 매 프레임 다시 읽음 (그 연출도 매 프레임 씀)
	/// - 렌즈 왜곡은 아래에서 아무도 안 쓰므로 그대로 넣음
	/// </summary>
	public sealed class UltimateFeelVolume
	{
		private const float PRIORITY = 90f;

		private readonly GameObject volumeObject;
		private readonly Volume volume;
		private readonly VolumeProfile profile;
		private readonly ColorAdjustments colorAdjustments;
		private readonly Bloom bloom;
		private readonly ChromaticAberration chromaticAberration;
		private readonly LensDistortion lensDistortion;
		private readonly Vignette vignette;
		private readonly List<Volume> lower = new();
		private bool disposed;

		public UltimateFeelVolume()
		{
			volumeObject = new GameObject("UltimateFeelVolume");
			volume = volumeObject.AddComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = PRIORITY;
			profile = ScriptableObject.CreateInstance<VolumeProfile>();
			colorAdjustments = profile.Add<ColorAdjustments>(true);
			bloom = profile.Add<Bloom>(true);
			chromaticAberration = profile.Add<ChromaticAberration>(true);
			lensDistortion = profile.Add<LensDistortion>(true);
			vignette = profile.Add<Vignette>(true);
			// 덮을 칸만 켬. 나머지 칸 (색 필터, 문턱, 가장자리 색) 은 아래 Volume 값 그대로
			foreach (VolumeComponent component in profile.components)
			{
				foreach (VolumeParameter parameter in component.parameters)
					parameter.overrideState = false;
			}
			colorAdjustments.postExposure.overrideState = true;
			colorAdjustments.saturation.overrideState = true;
			colorAdjustments.contrast.overrideState = true;
			bloom.intensity.overrideState = true;
			chromaticAberration.intensity.overrideState = true;
			lensDistortion.intensity.overrideState = true;
			lensDistortion.scale.overrideState = true;
			vignette.intensity.overrideState = true;
			volume.sharedProfile = profile;
		}

		public void Apply(float exposure, float bloomAdd, float saturation, float contrast, float chromatic, float lens, float lensScaleCompensation, float vignetteAdd)
		{
			if (disposed)
				return;
			CollectLower();
			colorAdjustments.postExposure.value = Lower<ColorAdjustments>(component => component.postExposure, 0f) + exposure;
			colorAdjustments.saturation.value = Mathf.Clamp(Lower<ColorAdjustments>(component => component.saturation, 0f) + saturation, -100f, 100f);
			colorAdjustments.contrast.value = Mathf.Clamp(Lower<ColorAdjustments>(component => component.contrast, 0f) + contrast, -100f, 100f);
			bloom.intensity.value = Mathf.Max(0f, Lower<Bloom>(component => component.intensity, 0f) + bloomAdd);
			chromaticAberration.intensity.value = Mathf.Clamp01(Lower<ChromaticAberration>(component => component.intensity, 0f) + chromatic);
			lensDistortion.intensity.value = Mathf.Clamp(lens, -1f, 1f);
			// 왜곡으로 화면 가장자리가 비지 않게 살짝 확대
			lensDistortion.scale.value = 1f - Mathf.Abs(lensDistortion.intensity.value) * lensScaleCompensation;
			vignette.intensity.value = Mathf.Clamp01(Lower<Vignette>(component => component.intensity, 0f) + vignetteAdd);
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			Object.Destroy(volumeObject);
			Object.Destroy(profile);
		}

		private void CollectLower()
		{
			lower.Clear();
			foreach (Volume candidate in VolumeManager.instance.GetVolumes(~0))
			{
				if (candidate == null || candidate == volume || candidate.isGlobal == false || candidate.isActiveAndEnabled == false || candidate.weight <= 0f)
					continue;
				lower.Add(candidate);
			}
			lower.Sort((left, right) => left.priority.CompareTo(right.priority));
		}

		/// <summary> 기본 프로필부터 우선순위 순으로 겹친 값 (이 Volume 제외) </summary>
		private float Lower<T>(Func<T, VolumeParameter<float>> pick, float fallback) where T : VolumeComponent
		{
			float value = fallback;
			VolumeProfile defaults = VolumeManager.instance.globalDefaultProfile;
			if (defaults != null && defaults.TryGet(out T defaultComponent))
				value = pick(defaultComponent).value;
			foreach (Volume candidate in lower)
			{
				VolumeProfile candidateProfile = candidate.HasInstantiatedProfile() ? candidate.profile : candidate.sharedProfile;
				if (candidateProfile == null || candidateProfile.TryGet(out T component) == false || component.active == false)
					continue;
				VolumeParameter<float> parameter = pick(component);
				if (parameter.overrideState == false)
					continue;
				value = Mathf.Lerp(value, parameter.value, Mathf.Clamp01(candidate.weight));
			}
			return value;
		}
	}
}
