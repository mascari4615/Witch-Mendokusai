using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 3D 연출 한 번 동안 장면 빛을 다루는 도구. 진행기 (UltimateFxRun) 하나에 하나, 끝나면 전부 되돌림
	/// - 해와 환경광 낮추기: 하늘 감독이 매 Update 에 넣는 값을 연출 코루틴 (Update 뒤) 이 배율로 덮음. 연출 광원이 주 광원이 됨
	/// - 후처리 Volume: 전역 Volume 을 하나 만들어 빛 번짐, 노출, 색수차, 가장자리 어둡게. 이 카메라는 Volume 을 반영함 (2026-11-20 MOIS2 실측, 이전 주석의 "반영 안 됨" 은 장면에 Volume 이 없던 탓)
	/// - 빛 안 받는 인형 그림 (빌보드 셰이더) 에 연출 광원 세기를 색 배율로 넣음. 해가 낮아지면 인형도 같이 어두워지고 광원 근처면 물듦
	/// - 카메라 깊이 텍스처 켜기 (소프트 입자용)
	/// </summary>
	public sealed class UltimateWorldLighting
	{
		private const string BILLBOARD_SHADER = "WitchMendokusai/BillboardShader";
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

		private readonly List<Light> lights = new();
		private readonly List<SpriteRenderer> sprites = new();
		private readonly MaterialPropertyBlock block = new();
		private readonly float spriteLightGain;
		private readonly float spriteTintCap;
		private readonly float spriteRefreshSeconds;

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
		private float spriteRefreshTimer;
		private bool disposed;

		public UltimateWorldLighting(Camera camera, float spriteLightGain, float spriteTintCap, float spriteRefreshSeconds)
		{
			this.spriteLightGain = spriteLightGain;
			this.spriteTintCap = spriteTintCap;
			this.spriteRefreshSeconds = spriteRefreshSeconds;
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

		/// <summary> 인형 그림 물들임에 반영할 연출 광원 </summary>
		public void Track(Light light) => lights.Add(light);

		/// <summary> 매 프레임 (하늘 감독 Update 뒤) 부름 </summary>
		public void Apply(float delta)
		{
			if (disposed)
				return;
			ApplySky();
			spriteRefreshTimer -= delta;
			if (spriteRefreshTimer <= 0f)
			{
				spriteRefreshTimer = spriteRefreshSeconds;
				CollectSprites();
			}
			ApplySprites();
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
			foreach (SpriteRenderer sprite in sprites)
				RestoreSprite(sprite);
			sprites.Clear();
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

		private void CollectSprites()
		{
			foreach (SpriteRenderer sprite in Object.FindObjectsByType<SpriteRenderer>())
			{
				if (sprite == null || sprites.Contains(sprite))
					continue;
				Material material = sprite.sharedMaterial;
				if (material == null || material.shader == null || material.shader.name != BILLBOARD_SHADER)
					continue;
				sprites.Add(sprite);
			}
		}

		private void ApplySprites()
		{
			// 해와 환경광이 낮아진 만큼 인형도 어둡게. 해 기여는 대략 절반
			float sceneScale = Mathf.Lerp(ambientFactor, sunFactor, 0.5f);
			for (int i = sprites.Count - 1; i >= 0; i--)
			{
				SpriteRenderer sprite = sprites[i];
				if (sprite == null)
				{
					sprites.RemoveAt(i);
					continue;
				}
				Color baseColor = sprite.sharedMaterial.GetColor(COLOR_ID);
				Color received = ReceivedLight(sprite.bounds.center) * spriteLightGain;
				Color tint = new(
					Mathf.Min(spriteTintCap, sceneScale + received.r),
					Mathf.Min(spriteTintCap, sceneScale + received.g),
					Mathf.Min(spriteTintCap, sceneScale + received.b),
					1f);
				sprite.GetPropertyBlock(block);
				block.SetColor(COLOR_ID, baseColor * tint);
				sprite.SetPropertyBlock(block);
			}
		}

		// URP 점광 감쇠와 같은 꼴 (거리 제곱 역수 x 범위 끝 창). 스포트는 원뿔 각도 감쇠를 곱함
		private Color ReceivedLight(Vector3 position)
		{
			Color sum = Color.black;
			foreach (Light light in lights)
			{
				if (light == null || light.isActiveAndEnabled == false || light.intensity <= 0f)
					continue;
				Vector3 offset = position - light.transform.position;
				float distanceSquared = Mathf.Max(0.0001f, offset.sqrMagnitude);
				float rangeRatio = distanceSquared / (light.range * light.range);
				float window = Mathf.Clamp01(1f - rangeRatio * rangeRatio);
				float attenuation = window * window / distanceSquared;
				if (light.type == LightType.Spot)
				{
					float cosine = Vector3.Dot(light.transform.forward, offset / Mathf.Sqrt(distanceSquared));
					float outer = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
					float inner = Mathf.Cos(light.innerSpotAngle * 0.5f * Mathf.Deg2Rad);
					attenuation *= Mathf.Clamp01((cosine - outer) / Mathf.Max(0.0001f, inner - outer));
				}
				sum += light.color * light.intensity * attenuation;
			}
			return sum;
		}

		private void RestoreSprite(SpriteRenderer sprite)
		{
			if (sprite == null)
				return;
			sprite.GetPropertyBlock(block);
			block.SetColor(COLOR_ID, sprite.sharedMaterial.GetColor(COLOR_ID));
			UnitBodyProperties.Commit(sprite, block);
		}
	}
}
