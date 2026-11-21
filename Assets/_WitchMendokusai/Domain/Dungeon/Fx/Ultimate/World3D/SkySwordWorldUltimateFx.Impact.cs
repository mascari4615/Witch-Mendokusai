using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	/// <summary>
	/// 하늘 검 3D 판의 충돌. 입자 층 7개 (빛 알갱이 광선, 메시 파편, 먼지 고리, 연기 기둥, 불씨, 뜯긴 풀잎, 핵 섬광) 와 솟는 바위, 균열 데칼, 지면 광원, 흔들림
	/// - 먼지와 연기는 빛 받는 입자라 지면 광원 쪽이 밝음. 바위와 파편은 실제 메시라 그림자와 광원 음영이 생김
	/// </summary>
	public sealed partial class SkySwordWorldUltimateFx
	{
		private void ComposeImpact(UltimateFxRun run, UltimateWorldLighting lighting, Vector3 target, Transform sword)
		{
			Light impactLight = run.PointLight("ImpactLight", impactLightColor, impactLightRange);
			impactLight.transform.position = target + Vector3.up * impactLightHeight;
			float lightSeconds = VanishStart - ImpactTime + vanishSeconds;
			run.Track(ImpactTime, lightSeconds, progress =>
			{
				float age = progress * lightSeconds;
				float rise = Mathf.Lerp(contactRatio, 1f, Mathf.Clamp01(age / flashDelay));
				float fall = age <= flashDelay ? 1f : Mathf.Pow(0.5f, (age - flashDelay) / impactHalfLife);
				impactLight.intensity = impactLightIntensity * rise * fall;
			}, () => impactLight.enabled = false);
			// 끄기만 하고 지우지 않음 (연출 끝에 Root 와 함께 정리). 지운 프레임에 바닥 판 하나가 밝은 사각으로 번쩍임 (2026-11-20 밤 촬영, 지운 시각과 일치)

			run.At(ImpactTime, () =>
			{
				run.ShakeSeries(run.Now, shakeCount, shakeInterval, shakeStrength, shakeDecay);
				// 뜯긴 풀잎은 빛 안 받는 평면 입자라 화면에 뿌린 조각처럼 읽혀 끔 (독립 검토 지적). 풀이 휘는 반응만
				run.React(target, windSpeed, windReach, false);
				ImpactCore(run, target);
				run.SparkBurst(target + Vector3.up * 0.3f, rayCount, new Vector2(8f, 20f), new Vector2(0.25f, 0.6f), new Vector2(0.12f, 0.3f), swordLightColor, impactLightColor, 1.2f);
				MeshDebris(run, target);
				DustRing(run, target);
				SmokeColumn(run, target);
				Embers(run, target);
			});
			Upheaval(run, target);
			CrackDecal(run, target);
		}

		// 접지 순간의 짧은 핵 빛. 월드 안 입자라 인물 앞뒤 순서를 따름
		private void ImpactCore(UltimateFxRun run, Vector3 target)
		{
			ParticleSystem core = run.Particles("ImpactCore", run.Additive(UltimateFxTextures.Soft, Color.white), 16, 0f);
			UltimateFxStage.SetLifetimeLook(core, UltimateFxStage.FadeGradient(Color.white, 0.05f, 0.25f), AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));
			UltimateFxStage.Emit(core, target + Vector3.up * 0.6f, Vector3.zero, 4.5f, 0.28f, swordLightColor);
			for (int i = 0; i < 8; i++)
				UltimateFxStage.Emit(core, target + UltimateFxStage.RandomRingPoint(0.2f, 1.2f) + Vector3.up * 0.4f, Random.insideUnitSphere * 2f, Random.Range(1.2f, 2.4f), Random.Range(0.2f, 0.35f), impactLightColor);
		}

		// 실제 바위 메시가 튀어 오르고 바닥에 부딪혀 굴러감. 일부는 달아오름 (꼭짓점 알파가 열 세기)
		private void MeshDebris(UltimateFxRun run, Vector3 target)
		{
			if (rockMesh == null)
				return;
			Material material = GlowMaterial(run, Color.white, rockHeat, Color.black);
			material.SetFloat(USE_VERTEX_COLOR_ID, 1f);
			ParticleSystem debris = run.Particles("MeshDebris", material, debrisCount + 4, 2.4f);
			ParticleSystemRenderer debrisRenderer = debris.GetComponent<ParticleSystemRenderer>();
			debrisRenderer.renderMode = ParticleSystemRenderMode.Mesh;
			debrisRenderer.mesh = rockMesh;
			debrisRenderer.alignment = ParticleSystemRenderSpace.Local;
			debrisRenderer.shadowCastingMode = ShadowCastingMode.On;
			debrisRenderer.receiveShadows = true;
			ParticleSystem.MainModule main = debris.main;
			main.startRotation3D = true;
			ParticleSystem.CollisionModule collision = debris.collision;
			collision.enabled = true;
			collision.type = ParticleSystemCollisionType.World;
			collision.mode = ParticleSystemCollisionMode.Collision3D;
			collision.bounce = 0.35f;
			collision.dampen = 0.45f;
			collision.lifetimeLoss = 0f;
			collision.radiusScale = 0.5f;
			for (int i = 0; i < debrisCount; i++)
			{
				float angle = Random.value * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				bool hot = Random.value < debrisHotChance;
				Color color = rockColor * Random.Range(0.8f, 1.2f);
				color.a = hot ? Random.Range(0.4f, 1f) : 0f;
				ParticleSystem.EmitParams emitParams = new()
				{
					position = target + direction * Random.Range(0.3f, 1f) + Vector3.up * 0.4f,
					velocity = direction * debrisSpeed * Random.Range(0.3f, 0.9f) + Vector3.up * debrisSpeed * Random.Range(0.6f, 1.1f),
					startSize = Random.Range(0.6f, 1.3f),
					startLifetime = Random.Range(2.2f, 3f),
					startColor = color,
					rotation3D = Random.insideUnitSphere * 180f,
					angularVelocity3D = Random.insideUnitSphere * 540f,
				};
				debris.Emit(emitParams, 1);
			}
		}

		// 바닥을 따라 퍼지는 먼지 고리. 빛 받는 입자
		private void DustRing(UltimateFxRun run, Vector3 target)
		{
			ParticleSystem dust = run.Particles("DustRing", LitParticleMaterial(run, 0.6f), dustCount + 8, -0.04f);
			UltimateFxStage.SetLifetimeLook(dust, UltimateFxStage.FadeGradient(Color.white, 0.08f, 0.45f), AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.8f));
			for (int i = 0; i < dustCount; i++)
			{
				float angle = (i + Random.value * 0.6f) / dustCount * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				Color tone = dustColor;
				tone.a = dustColor.a * Random.Range(0.6f, 1f);
				UltimateFxStage.Emit(dust, target + direction * Random.Range(0.5f, 1.2f) + Vector3.up * 0.3f, direction * Random.Range(dustRadius * 1.2f, dustRadius * 2.2f) + Vector3.up * Random.Range(0.3f, 1.6f), Random.Range(1.4f, 2.8f), Random.Range(1.4f, 2.4f), tone, Random.value * 360f, Random.Range(-50f, 50f));
			}
			ParticleSystem.LimitVelocityOverLifetimeModule limit = dust.limitVelocityOverLifetime;
			limit.enabled = true;
			limit.dampen = 0.16f;
			limit.limit = 1.2f;
		}

		// 검 둘레에서 천천히 오르는 연기
		private void SmokeColumn(UltimateFxRun run, Vector3 target)
		{
			ParticleSystem smoke = run.Particles("SmokeColumn", LitParticleMaterial(run, 0.8f), smokeCount + 4, -0.06f);
			UltimateFxStage.SetLifetimeLook(smoke, UltimateFxStage.FadeGradient(Color.white, 0.15f, 0.5f), AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));
			for (int i = 0; i < smokeCount; i++)
			{
				Vector3 offset = UltimateFxStage.RandomRingPoint(0.3f, 1.6f);
				Color tone = smokeColor;
				tone.a = smokeColor.a * Random.Range(0.6f, 1f);
				UltimateFxStage.Emit(smoke, target + offset + Vector3.up * Random.Range(0.2f, 1.2f), offset * 0.5f + Vector3.up * Random.Range(0.6f, 1.8f), Random.Range(1.6f, 3f), Random.Range(2f, 3f), tone, Random.value * 360f, Random.Range(-30f, 30f));
			}
		}

		// 달아오른 작은 불씨가 위로 떠오름
		private void Embers(UltimateFxRun run, Vector3 target)
		{
			ParticleSystem embers = run.Particles("Embers", run.Additive(UltimateFxTextures.Soft, Color.white), emberCount + 8, -0.15f);
			UltimateFxStage.SetLifetimeLook(embers, UltimateFxStage.FadeGradient(Color.white, 0.05f, 0.6f), AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
			ParticleSystem.NoiseModule noise = embers.noise;
			noise.enabled = true;
			noise.strength = 1.2f;
			noise.frequency = 0.8f;
			for (int i = 0; i < emberCount; i++)
			{
				Vector3 offset = UltimateFxStage.RandomRingPoint(0.2f, 2.6f);
				UltimateFxStage.Emit(embers, target + offset + Vector3.up * 0.2f, offset * 0.8f + Vector3.up * Random.Range(1.5f, 4.5f), Random.Range(0.08f, 0.18f), Random.Range(1.4f, 2.6f), emberColor);
			}
		}

		private Material LitParticleMaterial(UltimateFxRun run, float softDistance)
		{
			Material material = new(litParticleShader) { name = "UltimateFx_LitParticle", mainTexture = UltimateFxTextures.Smoke };
			material.SetFloat("_SoftDistance", softDistance);
			material.renderQueue = 3010;
			run.OnRelease(() => Object.Destroy(material));
			return material;
		}

		// 검 둘레에서 바위가 바깥으로 기울며 솟아오름. 남았다가 가라앉음
		private void Upheaval(UltimateFxRun run, Vector3 target)
		{
			if (rockMesh == null || upheavalCount <= 0)
				return;
			Material material = GlowMaterial(run, rockColor, Color.black, Color.black);
			Transform[] rocks = new Transform[upheavalCount];
			Vector3[] raised = new Vector3[upheavalCount];
			Vector3[] buried = new Vector3[upheavalCount];
			for (int i = 0; i < upheavalCount; i++)
			{
				float angle = (i + Random.Range(-0.3f, 0.3f)) / upheavalCount * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				Vector3 position = target + direction * Random.Range(upheavalRadius.x, upheavalRadius.y);
				position.y = UltimateFxStage.GroundHeight(position);
				float scale = Random.Range(upheavalScale.x, upheavalScale.y);
				GameObject rock = UltimateFxStage.CreateMeshObject("Upheaval" + i, run.Root, rockMesh, material);
				MeshRenderer rockRenderer = rock.GetComponent<MeshRenderer>();
				rockRenderer.shadowCastingMode = ShadowCastingMode.On;
				rockRenderer.receiveShadows = true;
				Vector3 tiltAxis = Vector3.Cross(Vector3.up, direction);
				rock.transform.rotation = Quaternion.AngleAxis(Random.Range(upheavalTilt.x, upheavalTilt.y), tiltAxis) * Quaternion.Euler(0f, Random.value * 360f, 0f);
				rock.transform.localScale = new Vector3(scale, scale * Random.Range(2.5f, 4f), scale);
				raised[i] = position + Vector3.up * scale * 0.04f;
				buried[i] = position - Vector3.up * scale * 0.8f;
				rock.transform.position = buried[i];
				rock.SetActive(false);
				rocks[i] = rock.transform;
			}
			run.At(ImpactTime, () =>
			{
				foreach (Transform rock in rocks)
					rock.gameObject.SetActive(true);
			});
			run.Track(ImpactTime, upheavalRiseSeconds, progress =>
			{
				for (int i = 0; i < rocks.Length; i++)
					rocks[i].position = Vector3.LerpUnclamped(buried[i], raised[i], OverShoot(progress));
			});
			run.Track(VanishStart + vanishSeconds * 0.3f, vanishSeconds, progress =>
			{
				for (int i = 0; i < rocks.Length; i++)
					rocks[i].position = Vector3.Lerp(raised[i], buried[i], UltimateFxRun.EaseIn(progress));
			});
		}

		private static float OverShoot(float progress)
		{
			float clamped = Mathf.Clamp01(progress);
			return 1f + 0.12f * Mathf.Sin(clamped * Mathf.PI) - Mathf.Pow(1f - clamped, 3f);
		}

		// 그을림과 균열을 바닥에 붙인 메시 데칼 (바닥 판) 로. 풀잎 메시가 위를 가림. 빛이 꺼지고 검이 사라진 뒤에도 남음
		// - projectorDecal 이 켜진 에셋 (강한 검) 은 URP DecalProjector 로 지형 굴곡을 따라 투영 (ProjectorDecal)
		private void CrackDecal(UltimateFxRun run, Vector3 target)
		{
			Texture2D texture = ScorchTexture();
			run.OnRelease(() => Object.Destroy(texture));
			if (projectorDecal)
			{
				ProjectorDecal(run, target, texture);
				return;
			}
			Material material = run.Alpha(texture, new Color(1f, 1f, 1f, 0f));
			GameObject plate = run.Plate("CrackDecal", material, target + Vector3.up * 0.03f, decalSize, 0);
			plate.transform.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
			run.Track(ImpactTime, 0.08f, progress => material.SetColor("_Color", new Color(1f, 1f, 1f, progress)));
			run.Track(VanishStart + vanishSeconds, scorchLingerSeconds, progress => material.SetColor("_Color", new Color(1f, 1f, 1f, 1f - UltimateFxRun.EaseIn(progress))));
		}

		// 균열 무늬 (UltimateFxTextures.Crack) 를 그을음 원 위에 얹은 데칼 그림
		private Texture2D ScorchTexture()
		{
			UltimateFxTextures.Crack(crackSeed, out Texture2D core, out Texture2D _);
			Color[] crack = core.GetPixels();
			int size = core.width;
			Color[] pixels = new Color[crack.Length];
			float half = (size - 1) * 0.5f;
			for (int row = 0; row < size; row++)
			for (int column = 0; column < size; column++)
			{
				int index = row * size + column;
				float radius = new Vector2((column - half) / half, (row - half) / half).magnitude;
				float scorch = Mathf.Pow(Mathf.Clamp01(1f - radius * 1.25f), 1.4f) * scorchColor.a;
				float line = crack[index].a;
				Color color = Color.Lerp(scorchColor, Color.black, line);
				color.a = Mathf.Max(scorch, line);
				pixels[index] = color;
			}
			Texture2D texture = new(size, size, TextureFormat.RGBA32, true) { name = "UltimateFx_Scorch", wrapMode = TextureWrapMode.Clamp };
			texture.SetPixels(pixels);
			texture.Apply(true);
			return texture;
		}
	}
}
