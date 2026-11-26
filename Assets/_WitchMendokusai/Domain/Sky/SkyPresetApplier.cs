using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// SkyPresetSO 한 시각 평가 결과를 하늘 재질, 셰이더 전역값, 해 빛, 안개와 환경광에 적용
	// SkyDirector (월드 시계) 와 TitleScenery (고정 시각) 공용
	public static class SkyPresetApplier
	{
		private static readonly int ZenithColorId = Shader.PropertyToID("_ZenithColor");
		private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
		private static readonly int SunDiscColorId = Shader.PropertyToID("_SunDiscColor");
		private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
		private static readonly int StarAlphaId = Shader.PropertyToID("_StarAlpha");
		private static readonly int CloudColorId = Shader.PropertyToID("_CloudColor");
		private static readonly int CloudCoverageId = Shader.PropertyToID("_CloudCoverage");
		private static readonly int CloudSoftnessId = Shader.PropertyToID("_CloudSoftness");
		private static readonly int CloudHeightId = Shader.PropertyToID("_CloudHeight");
		private static readonly int CloudScaleId = Shader.PropertyToID("_CloudScale");
		private static readonly int CloudSpeedId = Shader.PropertyToID("_CloudSpeed");

		// Shader Modding Contract (C6). 모더 셰이더가 받는 전역 uniform 7개
		private static readonly int WMSkyZenithId = Shader.PropertyToID("_WMSkyZenith");
		private static readonly int WMSkyHorizonId = Shader.PropertyToID("_WMSkyHorizon");
		private static readonly int WMSkySunId = Shader.PropertyToID("_WMSkySun");
		private static readonly int WMSkyStarAlphaId = Shader.PropertyToID("_WMSkyStarAlpha");
		private static readonly int WMSunAltitudeId = Shader.PropertyToID("_WMSunAltitude");
		private static readonly int WMSunDirectionId = Shader.PropertyToID("_WMSunDirection");
		private static readonly int WMNormalizedTimeId = Shader.PropertyToID("_WMNormalizedTime");

		public static void ApplyShaderGlobals(SkyPresetSO preset, float normalizedTime)
		{
			Shader.SetGlobalColor(WMSkyZenithId, preset.ZenithColor.Evaluate(normalizedTime));
			Shader.SetGlobalColor(WMSkyHorizonId, preset.HorizonColor.Evaluate(normalizedTime));
			Shader.SetGlobalColor(WMSkySunId, preset.SunDiscColor.Evaluate(normalizedTime));
			Shader.SetGlobalFloat(WMSkyStarAlphaId, preset.StarAlpha.Evaluate(normalizedTime));

			float altitude = preset.SunAltitude.Evaluate(normalizedTime);
			Shader.SetGlobalFloat(WMSunAltitudeId, altitude);
			Shader.SetGlobalVector(WMSunDirectionId, ComputeSunDirection(altitude));

			Shader.SetGlobalFloat(WMNormalizedTimeId, normalizedTime);
			SpriteLighting.Apply(preset.SpriteLighting);
		}

		public static void ApplySky(SkyPresetSO preset, Material skyboxMaterial, float t)
		{
			if (skyboxMaterial == null)
				return;

			skyboxMaterial.SetColor(ZenithColorId, preset.ZenithColor.Evaluate(t));
			skyboxMaterial.SetColor(HorizonColorId, preset.HorizonColor.Evaluate(t));
			skyboxMaterial.SetColor(SunDiscColorId, preset.SunDiscColor.Evaluate(t));

			float altitude = preset.SunAltitude.Evaluate(t);
			skyboxMaterial.SetVector(SunDirectionId, ComputeSunDirection(altitude));

			skyboxMaterial.SetFloat(StarAlphaId, preset.StarAlpha.Evaluate(t));

			skyboxMaterial.SetColor(CloudColorId, preset.CloudColor.Evaluate(t));
			skyboxMaterial.SetFloat(CloudCoverageId, preset.CloudCoverage.Evaluate(t));
			skyboxMaterial.SetFloat(CloudSoftnessId, preset.CloudSoftness);
			skyboxMaterial.SetFloat(CloudHeightId, preset.CloudHeight);
			skyboxMaterial.SetFloat(CloudScaleId, preset.CloudScale);
			skyboxMaterial.SetFloat(CloudSpeedId, preset.CloudSpeed);
		}

		public static void ApplyDirectionalLight(SkyPresetSO preset, Light directionalLight, float t, float sunAzimuth, bool survivalLighting = false)
		{
			directionalLight.color = preset.SunLightColor.Evaluate(t);
			directionalLight.intensity = preset.SunIntensity.Evaluate(t);
			if (survivalLighting)
				directionalLight.intensity = Mathf.Max(directionalLight.intensity, preset.SurvivalSunMinimumIntensity);

			float altitude = preset.SunAltitude.Evaluate(t);
			float pitchDegrees = ComputeLightPitch(altitude, preset.MinLightPitch);
			directionalLight.transform.rotation = Quaternion.Euler(pitchDegrees, sunAzimuth, 0f);
		}

		// 실내. 주변광과 안개 색을 한낮 쪽으로 섞고 주변광에 하한
		public static void ApplyIndoorEnvironment(SkyPresetSO preset, float t, IndoorLight indoor)
		{
			ApplyAmbient(preset, indoor.Ambient(preset.AmbientColor, t));
			ApplyFog(preset, t, indoor.Blend(preset.FogColor, t));
		}

		public static void ApplyEnvironment(SkyPresetSO preset, float t, bool survivalLighting = false)
		{
			Color ambient = preset.AmbientColor.Evaluate(t);
			if (survivalLighting)
			{
				ambient *= preset.SurvivalAmbientMultiplier;
				float luminance = ambient.r * .2126f + ambient.g * .7152f + ambient.b * .0722f;
				if (luminance > .0001f && luminance < preset.SurvivalAmbientMinimumLuminance)
					ambient *= preset.SurvivalAmbientMinimumLuminance / luminance;
			}
			ApplyAmbient(preset, ambient);
			ApplyFog(preset, t, preset.FogColor.Evaluate(t));
		}

		private static void ApplyAmbient(SkyPresetSO preset, Color ambient)
		{
			RenderSettings.ambientMode = preset.UseHemisphereAmbient ? AmbientMode.Trilight : AmbientMode.Flat;
			if (preset.UseHemisphereAmbient)
			{
				RenderSettings.ambientSkyColor = ambient * preset.AmbientSkyTint;
				RenderSettings.ambientEquatorColor = ambient * preset.AmbientEquatorTint;
				RenderSettings.ambientGroundColor = ambient * preset.AmbientGroundTint;
			}
			else RenderSettings.ambientLight = ambient;
		}

		private static void ApplyFog(SkyPresetSO preset, float t, Color fogColor)
		{
			RenderSettings.fog = true;
			RenderSettings.fogMode = preset.FogMode;
			RenderSettings.fogColor = fogColor;
			RenderSettings.fogDensity = preset.FogDensity.Evaluate(t);
		}

		public static Vector3 ComputeSunDirection(float altitude)
		{
			// altitude 범위: -1 수평선 아래, 0 수평선, 1 천정
			float pitch = altitude * 90f * Mathf.Deg2Rad;
			return new Vector3(0f, Mathf.Sin(pitch), Mathf.Cos(pitch));
		}

		// 밤의 달빛 겸용. 해가 지평선 아래여도 빛 방향은 위쪽
		public static float ComputeLightPitch(float altitude, float minPitch) => Mathf.Max(altitude * 90f, minPitch);
	}
}
