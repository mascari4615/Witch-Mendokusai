using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary> 하늘 검 3D 판의 검 메시, 검 광원, 스포트 예고, 낙하 </summary>
	public sealed partial class SkySwordWorldUltimateFx
	{
		private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
		private static readonly int EMISSION_ID = Shader.PropertyToID("_EmissionColor");
		private static readonly int RIM_ID = Shader.PropertyToID("_RimColor");
		private static readonly int DISSOLVE_ID = Shader.PropertyToID("_Dissolve");
		private static readonly int DISSOLVE_DIRECTION_ID = Shader.PropertyToID("_DissolveDirection");
		private static readonly int USE_VERTEX_COLOR_ID = Shader.PropertyToID("_UseVertexColor");

		private Material GlowMaterial(UltimateFxRun run, Color baseColor, Color emission, Color rim)
		{
			Material material = new(meshGlowShader) { name = "UltimateFx_MeshGlow" };
			material.SetColor(BASE_COLOR_ID, baseColor);
			material.SetColor(EMISSION_ID, emission);
			material.SetColor(RIM_ID, rim);
			run.OnRelease(() => Object.Destroy(material));
			return material;
		}

		private Transform ComposeSword(UltimateFxRun run, UltimateWorldLighting lighting, Vector3 target)
		{
			if (swordModel == null || meshGlowShader == null)
				throw new System.InvalidOperationException($"{name}: 검 모델 또는 메시 셰이더 미연결. 메뉴 WM/CombatFx/Build Ultimate 3D Presets");

			Material blade = GlowMaterial(run, bladeColor, Color.black, Color.black);
			// 칼날 면과 모서리가 갈리게 반사를 넓고 강하게
			blade.SetFloat("_Specular", bladeSpecular);
			blade.SetFloat("_Smoothness", bladeSmoothness);
			Material hilt = GlowMaterial(run, hiltColor, Color.black, Color.black);
			Material grip = GlowMaterial(run, gripColor, Color.black, Color.black);
			Material[] all = { blade, hilt, grip };
			foreach (Material material in all)
			{
				material.SetFloat(DISSOLVE_ID, 1f);
				// 나타날 때는 칼끝 (아래) 부터, 사라질 때는 위로 녹아 올라감
				material.SetFloat(DISSOLVE_DIRECTION_ID, -1f);
			}

			GameObject sword = Object.Instantiate(swordModel, run.Root);
			sword.name = "SkySword3D";
			foreach (MeshRenderer meshRenderer in sword.GetComponentsInChildren<MeshRenderer>())
			{
				Material[] sources = meshRenderer.sharedMaterials;
				Material[] replaced = new Material[sources.Length];
				for (int i = 0; i < sources.Length; i++)
					replaced[i] = PickMaterial(sources[i] != null ? sources[i].name : string.Empty, blade, hilt, grip);
				meshRenderer.sharedMaterials = replaced;
				meshRenderer.shadowCastingMode = ShadowCastingMode.On;
				meshRenderer.receiveShadows = true;
			}
			Transform swordTransform = sword.transform;
			swordTransform.localScale = new Vector3(swordLength * swordWidthScale, swordLength, swordLength * swordThicknessScale);
			swordTransform.position = target + Vector3.up * spawnHeight;
			sword.SetActive(false);

			// 칼날 면이 카메라를 향하는 방향. 도는 동안 이 방향으로 감속하며 멈춤
			Vector3 toCamera = run.MainCamera != null ? run.MainCamera.transform.position - target : -run.ScreenForward;
			toCamera.y = 0f;
			float faceYaw = Quaternion.LookRotation(toCamera.sqrMagnitude > 0.0001f ? toCamera.normalized : Vector3.back).eulerAngles.y + faceYawOffset;

			Light swordLight = run.PointLight("SwordLight", swordLightColor, swordLightRange);
			swordLight.transform.SetParent(swordTransform, false);
			// 칼날 안에 두면 칼날이 하얗게 타서 형태가 사라짐. 칼끝 아래에 두어 바닥과 주변을 비춤
			swordLight.transform.localPosition = new Vector3(0f, -0.12f, 0f);
			lighting.Track(swordLight);

			Light spot = run.PointLight("SwordSpot", swordLightColor, spawnHeight + 4f);
			spot.type = LightType.Spot;
			spot.spotAngle = spotAngle;
			spot.innerSpotAngle = spotAngle * 0.55f;
			spot.transform.rotation = Quaternion.LookRotation(Vector3.down);
			lighting.Track(spot);

			Vector3 hangPosition = target + Vector3.up * hangHeight;
			Vector3 plantedPosition = target - Vector3.up * swordLength * sinkRatio;

			// 나타남과 하강: 화면 밖에서 감속하며 내려와 돌다가 칼날 면을 보이며 멈춤
			run.At(summonStart, () => sword.SetActive(true));
			run.Track(summonStart, descendSeconds, progress =>
			{
				float ease = UltimateFxRun.EaseOut(progress);
				swordTransform.position = Vector3.Lerp(target + Vector3.up * spawnHeight, hangPosition, ease);
				swordTransform.rotation = Quaternion.AngleAxis(-hangTilt, run.ScreenRight) * Quaternion.Euler(0f, faceYaw + spinDegrees * (1f - ease), 0f);
				float appear = Mathf.Clamp01(progress * descendSeconds / materializeSeconds);
				foreach (Material material in all)
					material.SetFloat(DISSOLVE_ID, 1f - UltimateFxRun.EaseOut(appear));
				blade.SetColor(EMISSION_ID, bladeGlow * (0.4f + 0.6f * appear));
				blade.SetColor(RIM_ID, bladeRim * appear);
				hilt.SetColor(RIM_ID, bladeRim * 0.25f * appear);
				swordLight.intensity = swordLightIntensity * 0.6f * appear;
				spot.intensity = spotIntensity * spotIdleRatio * ease;
				spot.transform.position = swordTransform.position;
			});

			// 떠 있음과 예고: 미세한 떨림. 충돌 telegraphSeconds 전부터 스포트 원이 크기 그대로 밝아짐
			run.Track(DescendEnd, hangSeconds, _ =>
			{
				float tremble = Mathf.Sin(run.Now * 55f) * 0.025f;
				swordTransform.position = hangPosition + new Vector3(tremble, 0f, -tremble);
				spot.transform.position = swordTransform.position;
			});
			run.Track(ImpactTime - telegraphSeconds, telegraphSeconds, progress =>
			{
				spot.intensity = spotIntensity * Mathf.Lerp(spotIdleRatio, 1f, UltimateFxRun.EaseIn(progress));
				swordLight.intensity = swordLightIntensity * Mathf.Lerp(0.6f, 1f, progress);
				blade.SetColor(EMISSION_ID, bladeGlow * Mathf.Lerp(1f, 1.5f, progress));
			});

			// 낙하: 가속하며 칼끝이 땅 아래까지
			run.Track(FallStart, fallSeconds, progress =>
			{
				swordTransform.position = Vector3.Lerp(hangPosition, plantedPosition, UltimateFxRun.EaseIn(progress));
				swordTransform.rotation = Quaternion.AngleAxis(-hangTilt * (1f - UltimateFxRun.EaseOut(progress)), run.ScreenRight) * Quaternion.Euler(0f, faceYaw, 0f);
				spot.transform.position = swordTransform.position;
			}, () =>
			{
				swordTransform.position = plantedPosition;
				// 충돌 뒤에는 지면 광원 하나만. 광원이 겹치면 바닥 판마다 받는 광원 (물체당 4개 상한) 이 달라 사각 얼룩이 생김 (2026-11-20 밤 촬영)
				run.Destroy(spot.gameObject);
				run.Destroy(swordLight.gameObject);
			});
			ComposeFallTrail(run, hangPosition, target);

			// 꽂힌 뒤 식음, 그리고 위로 녹아 사라짐
			run.Track(ImpactTime, VanishStart - ImpactTime, progress =>
			{
				float cool = Mathf.Pow(0.5f, progress * (VanishStart - ImpactTime) / 0.9f);
				blade.SetColor(EMISSION_ID, bladeGlow * Mathf.Lerp(0.35f, 2.4f, cool));
				blade.SetColor(RIM_ID, bladeRim * Mathf.Lerp(0.4f, 1f, cool));
			});
			run.Track(VanishStart, vanishSeconds, progress =>
			{
				foreach (Material material in all)
				{
					material.SetFloat(DISSOLVE_DIRECTION_ID, 1f);
					material.SetFloat(DISSOLVE_ID, UltimateFxRun.EaseIn(progress));
				}
			}, () => run.Destroy(sword));
			return swordTransform;
		}

		private static Material PickMaterial(string sourceName, Material blade, Material hilt, Material grip)
		{
			if (sourceName.Contains("Red"))
				return blade;
			if (sourceName.Contains("Steel"))
				return hilt;
			return grip;
		}

		// 낙하 길을 따라 늘어난 빛 줄
		private void ComposeFallTrail(UltimateFxRun run, Vector3 from, Vector3 to)
		{
			ParticleSystem trail = run.Particles("FallTrail", run.Additive(UltimateFxTextures.Spark, Color.white), trailCount + 8, 0f, true);
			UltimateFxStage.SetLifetimeLook(trail, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.2f), AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
			ParticleSystemRenderer trailRenderer = trail.GetComponent<ParticleSystemRenderer>();
			trailRenderer.velocityScale = 0.12f;
			trailRenderer.lengthScale = 1f;
			run.At(FallStart, () =>
			{
				for (int i = 0; i < trailCount; i++)
				{
					Vector3 offset = UltimateFxStage.RandomRingPoint(0.1f, 0.7f);
					Vector3 position = Vector3.Lerp(from, to, Random.value) + offset + Vector3.up * swordLength * Random.Range(0.2f, 1f);
					UltimateFxStage.Emit(trail, position, Vector3.down * Random.Range(30f, 60f), Random.Range(0.08f, 0.16f), Random.Range(0.15f, 0.3f), swordLightColor);
				}
			});
		}
	}
}
