using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 3D 연출 한 번 동안 장면 빛을 다루는 도구. 진행기 (UltimateFxRun) 하나에 하나, 끝나면 전부 되돌림
	/// - 해와 환경광 낮추기: 하늘 감독이 매 Update 에 넣는 값을 연출 코루틴 (Update 뒤) 이 배율로 덮음. 연출 광원이 주 광원이 됨
	/// - 후처리 Volume: 전역 Volume 을 하나 만들어 빛 번짐, 노출, 색수차, 가장자리 어둡게. 이 카메라는 Volume 을 반영함 (2026-11-20 MOIS2 실측, 이전 주석의 "반영 안 됨" 은 장면에 Volume 이 없던 탓)
	/// - 인형 그림 (빌보드 셰이더) 은 셰이더가 해, 환경광, 연출 광원을 직접 받음 (SpriteLighting). 여기서는 연출 동안 추가 광원 배율과 상한만 덮음
	/// - 카메라 깊이 텍스처 켜기 (소프트 입자용)
	/// </summary>
	public sealed class UltimateWorldLighting
	{
		private readonly float spriteLightGain;
		private readonly float spriteTintCap;
		private readonly float savedPointGain;
		private readonly float savedCap;

		private Light sun;
		private float sunIntensity;
		private Color ambientSky;
		private Color ambientEquator;
		private Color ambientGround;
		private Color ambientFlat;
		private float sunFactor = 1f;
		private float ambientFactor = 1f;

		private GameObject volumeObject;
		private VolumeProfile profile;
		private Bloom bloom;
		private ColorAdjustments colorAdjustments;
		private ChromaticAberration chromaticAberration;
		private Vignette vignette;

		private UniversalAdditionalCameraData cameraData;
		private bool depthWasRequested;
		private bool disposed;

		public UltimateWorldLighting(Camera camera, float spriteLightGain, float spriteTintCap)
		{
			this.spriteLightGain = spriteLightGain;
			this.spriteTintCap = spriteTintCap;
			savedPointGain = Shader.GetGlobalFloat(SpriteLighting.POINT_GAIN_ID);
			savedCap = Shader.GetGlobalFloat(SpriteLighting.CAP_ID);
			sun = RenderSettings.sun;
			CaptureSky();
			CreateVolume();
			if (camera != null && camera.TryGetComponent(out cameraData))
			{
				depthWasRequested = cameraData.requiresDepthTexture;
				cameraData.requiresDepthTexture = true;
			}
		}

		/// <summary> 해 세기 배율 (0~1) 과 환경광 배율 </summary>
		public void SetSkyDim(float sunScale, float ambientScale)
		{
			sunFactor = sunScale;
			ambientFactor = ambientScale;
		}

		public void SetBloom(float intensity, float threshold, Color tint)
		{
			bloom.intensity.value = intensity;
			bloom.threshold.value = threshold;
			bloom.tint.value = tint;
		}

		/// <summary> 노출 (EV). 카메라가 받는 빛 전체가 밝아짐. 섬광을 화면 위 판 대신 카메라 노출로 </summary>
		public void SetExposure(float exposure) => colorAdjustments.postExposure.value = exposure;

		public void SetChromatic(float amount) => chromaticAberration.intensity.value = amount;

		public void SetVignette(float amount, Color color)
		{
			vignette.intensity.value = amount;
			vignette.color.value = color;
		}

		/// <summary> 매 프레임 (하늘 감독 Update 뒤) 부름 </summary>
		public void Apply()
		{
			if (disposed)
				return;
			ApplySky();
			SpriteLighting.OverridePoint(spriteLightGain, spriteTintCap);
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			if (sun != null)
				sun.intensity = sunIntensity;
			RenderSettings.ambientSkyColor = ambientSky;
			RenderSettings.ambientEquatorColor = ambientEquator;
			RenderSettings.ambientGroundColor = ambientGround;
			RenderSettings.ambientLight = ambientFlat;
			SpriteLighting.OverridePoint(savedPointGain, savedCap);
			if (cameraData != null)
				cameraData.requiresDepthTexture = depthWasRequested;
			if (volumeObject != null)
				Object.Destroy(volumeObject);
			if (profile != null)
				Object.Destroy(profile);
		}

		private void CaptureSky()
		{
			sunIntensity = sun != null ? sun.intensity : 0f;
			ambientSky = RenderSettings.ambientSkyColor;
			ambientEquator = RenderSettings.ambientEquatorColor;
			ambientGround = RenderSettings.ambientGroundColor;
			ambientFlat = RenderSettings.ambientLight;
		}

		private void CreateVolume()
		{
			volumeObject = new GameObject("UltimateWorldVolume");
			Volume volume = volumeObject.AddComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = 50f;
			profile = ScriptableObject.CreateInstance<VolumeProfile>();
			bloom = profile.Add<Bloom>(true);
			bloom.intensity.Override(0f);
			bloom.threshold.Override(1f);
			bloom.scatter.Override(0.7f);
			bloom.tint.Override(Color.white);
			colorAdjustments = profile.Add<ColorAdjustments>(true);
			colorAdjustments.postExposure.Override(0f);
			chromaticAberration = profile.Add<ChromaticAberration>(true);
			chromaticAberration.intensity.Override(0f);
			vignette = profile.Add<Vignette>(true);
			vignette.intensity.Override(0f);
			vignette.smoothness.Override(0.6f);
			vignette.color.Override(Color.black);
			volume.sharedProfile = profile;
		}

		// 하늘 감독이 해 시각이 바뀔 때만 값을 다시 넣을 수 있어, 처음 값에 배율을 곱해 매 프레임 덮음
		private void ApplySky()
		{
			if (sun != null)
				sun.intensity = sunIntensity * sunFactor;
			RenderSettings.ambientSkyColor = ambientSky * ambientFactor;
			RenderSettings.ambientEquatorColor = ambientEquator * ambientFactor;
			RenderSettings.ambientGroundColor = ambientGround * ambientFactor;
			RenderSettings.ambientLight = ambientFlat * ambientFactor;
		}
	}
}
