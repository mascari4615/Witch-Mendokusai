using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기의 월드 효과. 충전 입자, 임팩트 (충격파 링, 지면 균열, 빛기둥, 먼지, 파편, 불꽃, 광원), 환경 반응, 잔여 (불씨, 연기)
	/// 월드 효과의 시간은 게임 시간이라 히트스톱에 같이 멈춤
	/// </summary>
	public sealed partial class UltimateFxRun
	{
		private readonly List<GameObject> foliageObjects = new();

		// ---- 환경: 시연용 임시 풀과 나무 ----
		private void SpawnFoliage()
		{
			if (FxFoliage.Count > 0)
				return;
			foreach (UltimateFx.FoliageKind kind in fx.Foliage)
			{
				if (kind.Prefab == null)
					continue;
				for (int i = 0; i < kind.Count; i++)
				{
					Vector3 offset = UltimateFxStage.RandomRingPoint(Mathf.Max(1f, kind.MinDistance), fx.ImpactRadius * 2.1f);
					Vector3 position = center + offset;
					position.y = UltimateFxStage.GroundHeight(position);
					float scale = Mathf.Lerp(kind.ScaleRange.x, kind.ScaleRange.y, UnityEngine.Random.value);
					GameObject instance = UnityEngine.Object.Instantiate(kind.Prefab, position, Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f), root.transform);
					instance.transform.localScale = Vector3.one * scale;
					FxFoliage foliage = instance.AddComponent<FxFoliage>();
					foliage.Flexibility = kind.Flexibility;
					foliageObjects.Add(instance);
					// 중심에서 먼 것부터 차례로 솟아나는 느낌: 처음엔 숨기고 거리에 따라 켬
					instance.SetActive(false);
					float delay = Mathf.Clamp01(offset.magnitude / (fx.ImpactRadius * 2.1f)) * fx.TelegraphSeconds * 0.9f;
					GameObject target = instance;
					AddTrack(true, delay, 0f, _ =>
					{
						if (target != null)
							target.SetActive(true);
					});
				}
			}
		}

		// ---- 충전: 안으로 빨려드는 빛 ----
		private void BuildChargeParticles(float start, float duration)
		{
			Material sparkMaterial = NewMaterial(UltimateFxTextures.Spark, true, Color.white);
			ParticleSystem sparks = UltimateFxStage.CreateParticles("ChargeSparks", root.transform, sparkMaterial, 1200, 0f);
			UltimateFxStage.SetLifetimeLook(sparks, UltimateFxStage.FadeGradient(Color.white, 0.12f, 0.7f), null);
			ParticleSystem.TrailModule trails = sparks.trails;
			trails.enabled = true;
			trails.ratio = 0.6f;
			trails.lifetime = 0.22f;
			trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.35f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
			trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(UltimateFxStage.FadeGradient(new Color(1f, 0.8f, 0.5f, 0.6f), 0.01f, 0.3f));
			sparks.GetComponent<ParticleSystemRenderer>().trailMaterial = sparkMaterial;

			Material orbMaterial = NewMaterial(UltimateFxTextures.Soft, true, Color.white);
			ParticleSystem orb = UltimateFxStage.CreateParticles("ChargeOrb", root.transform, orbMaterial, 8, 0f);

			Light light = CreateLight("ChargeLight");
			ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[1200];
			float emitCarry = 0f;
			AddTrack(true, start, duration + 0.02f, t =>
			{
				Vector3 target = CasterPosition + Vector3.up * 0.9f;
				// 빨려드는 입자 방출: 충전이 갈수록 빠르고 촘촘
				emitCarry += fx.ChargeSparksPerSecond * Mathf.Lerp(0.5f, 2.6f, t) * realDelta;
				while (emitCarry >= 1f)
				{
					emitCarry -= 1f;
					Vector3 direction = UnityEngine.Random.onUnitSphere;
					direction.y = Mathf.Abs(direction.y) * 0.7f;
					Vector3 spawn = target + direction.normalized * UnityEngine.Random.Range(fx.ChargeSparkRadius * 0.5f, fx.ChargeSparkRadius);
					Color color = Color.Lerp(Colors.Mid, Colors.Core, UnityEngine.Random.value);
					color.a = 1f;
					UltimateFxStage.Emit(sparks, spawn, Vector3.zero, UnityEngine.Random.Range(0.18f, 0.5f), UnityEngine.Random.Range(0.45f, 0.9f), color);
				}
				int count = sparks.GetParticles(buffer);
				for (int i = 0; i < count; i++)
				{
					float remaining = Mathf.Max(0.04f, buffer[i].remainingLifetime);
					buffer[i].velocity = (target - buffer[i].position) / remaining;
				}
				sparks.SetParticles(buffer, count);

				// 시전자 위 커지는 빛 구
				float size = Mathf.Lerp(0.5f, 4.4f, EaseIn(t)) * (1f + 0.12f * Mathf.Sin(realTime * 40f));
				Color orbColor = Color.Lerp(Colors.Mid, Colors.Core, t);
				orbColor.a = Mathf.Lerp(0.4f, 1f, t);
				UltimateFxStage.Emit(orb, target, Vector3.zero, size, 0.05f, orbColor);
				light.transform.position = target;
				light.intensity = Mathf.Lerp(0f, fx.LightIntensity * 0.45f, t);
			}, () => light.intensity = 0f);
		}

		private Light CreateLight(string name)
		{
			GameObject lightObject = new(name);
			lightObject.transform.SetParent(root.transform, false);
			Light light = lightObject.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = Colors.Mid;
			light.range = fx.LightRange;
			light.intensity = 0f;
			light.shadows = LightShadows.None;
			return light;
		}

		// ---- 임팩트: 월드 ----
		private void BuildImpactWorld()
		{
			Vector3 origin = center;
			float radius = fx.ImpactRadius;
			float start = worldTime;

			BuildLight(start);
			BuildCrack(origin, radius, start);
			BuildRings(origin, radius, start);
			BuildPillar(origin, start);
			BuildDust(origin, radius, start);
			BuildDebris(origin, radius, start);
			BuildSparks(origin, start);
			BuildCoreBurst(origin, start);

			// 환경 반응: 첫 고리의 속도로 퍼지는 충격파에 풀과 나무가 휘어짐
			float speed = radius * fx.RingReach / fx.RingSeconds * 0.8f;
			FxFoliage.React(origin, speed, radius * fx.RingReach, fx.FoliageBend, fx.FoliageDecaySeconds, fx.FoliageHertz);
		}

		private void BuildLight(float start)
		{
			Light light = CreateLight("ImpactLight");
			light.transform.position = center + Vector3.up * 2.4f;
			AddTrack(false, start, fx.LightSeconds, t =>
			{
				float envelope = Mathf.Pow(1f - t, 2.2f);
				light.intensity = fx.LightIntensity * envelope;
				light.color = Color.Lerp(Colors.Core, Colors.Deep, Mathf.Clamp01(t * 1.6f));
				light.range = fx.LightRange * Mathf.Lerp(0.6f, 1.2f, EaseOut(t));
			}, () => light.intensity = 0f);
		}

		// 임팩트 순간 가운데에 터지는 빛 덩어리와 땅 위 눌린 빛 판
		private void BuildCoreBurst(Vector3 origin, float start)
		{
			Material burstMaterial = NewMaterial(UltimateFxTextures.Soft, true, Color.white);
			ParticleSystem burst = UltimateFxStage.CreateParticles("CoreBurst", root.transform, burstMaterial, 16, 0f);
			UltimateFxStage.SetLifetimeLook(burst, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.3f), AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.5f));
			Color core = Colors.Core;
			core.a = 1f;
			UltimateFxStage.Emit(burst, origin + Vector3.up * 1.2f, Vector3.zero, fx.ImpactRadius * 0.9f, 0.4f, core);
			Color mid = Colors.Mid;
			mid.a = 0.5f;
			UltimateFxStage.Emit(burst, origin + Vector3.up * 0.8f, Vector3.zero, fx.ImpactRadius * 1.9f, 0.8f, mid);
		}

		// ---- 지면 균열: 그을음, 붉은 번짐, 하얀 속. 균열은 가운데에서 바깥으로 번져 나가며 식음 ----
		private void BuildCrack(Vector3 origin, float radius, float start)
		{
			UltimateFxTextures.Crack(fx.CrackSeed, out Texture2D core, out Texture2D glow, out Texture2D scorch);
			Material scorchMaterial = NewMaterial(scorch, false, Color.white);
			Material glowMaterial = NewMaterial(glow, true, Color.clear);
			Material coreMaterial = NewMaterial(core, true, Color.clear);
			float diameter = radius * 2.3f;
			GameObject scorchObject = CreatePlate("CrackScorch", scorchMaterial, origin, 0.02f, diameter, 2);
			GameObject glowObject = CreatePlate("CrackGlow", glowMaterial, origin, 0.03f, diameter, 3);
			GameObject coreObject = CreatePlate("CrackCore", coreMaterial, origin, 0.04f, diameter, 4);
			float randomYaw = UnityEngine.Random.value * 360f;
			scorchObject.transform.rotation = glowObject.transform.rotation = coreObject.transform.rotation = Quaternion.Euler(0f, randomYaw, 0f);

			float total = fx.CrackHeatSeconds;
			AddTrack(false, start, total + 2.5f, t =>
			{
				float seconds = t * (total + 2.5f);
				float reveal = EaseOut(Mathf.Clamp01(seconds / fx.CrackRevealSeconds));
				float size = diameter * Mathf.Lerp(0.15f, 1f, reveal);
				Vector3 scale = new(size, 1f, size);
				scorchObject.transform.localScale = scale;
				glowObject.transform.localScale = scale;
				coreObject.transform.localScale = scale;

				// 식는 곡선: 하얀 속 -> 주색 -> 식은 색 -> 어둠
				float heat = Mathf.Clamp01(seconds / total);
				float flicker = 0.85f + 0.15f * Mathf.Sin(seconds * 23f) * Mathf.Sin(seconds * 9.7f + 1f);
				Color glowColor = Color.Lerp(Colors.Mid, Colors.Deep, Mathf.Clamp01(heat * 1.6f));
				float glowAlpha = Mathf.Lerp(1f, 0.28f, EaseOut(heat)) * Mathf.Pow(1f - heat, 0.8f) * flicker * Mathf.Clamp01(reveal * 2f);
				glowMaterial.SetColor("_Color", new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha));
				float coreAlpha = Mathf.Pow(1f - Mathf.Clamp01(heat * 1.8f), 1.3f) * Mathf.Clamp01(reveal * 3f);
				Color coreColor = Color.Lerp(Colors.Core, Colors.Mid, heat);
				coreMaterial.SetColor("_Color", new Color(coreColor.r, coreColor.g, coreColor.b, coreAlpha));
				// 그을음은 마지막 2.5초에 걷힘
				float scorchAlpha = seconds < total ? Mathf.Clamp01(reveal * 1.5f) : 1f - (seconds - total) / 2.5f;
				scorchMaterial.SetColor("_Color", new Color(1f, 1f, 1f, scorchAlpha));
			}, () =>
			{
				UnityEngine.Object.Destroy(scorchObject);
				UnityEngine.Object.Destroy(glowObject);
				UnityEngine.Object.Destroy(coreObject);
			});
		}

		private GameObject CreatePlate(string name, Material material, Vector3 origin, float lift, float diameter, int sortingOrder)
		{
			GameObject plate = UltimateFxStage.CreateMeshObject(name, root.transform, UltimateFxStage.GroundQuad, material);
			// 바닥 판은 스프라이트 (큐 3000) 보다 먼저 그려 인물이 위에 오게 함
			material.renderQueue = 2940 + sortingOrder;
			plate.transform.position = origin + Vector3.up * lift;
			plate.transform.localScale = new Vector3(diameter, 1f, diameter);
			return plate;
		}

		// ---- 충격파: 굴절 링과 빛 링을 겹쳐 여러 겹 ----
		private void BuildRings(Vector3 origin, float radius, float start)
		{
			float maxRadius = radius * fx.RingReach;
			float diameter = maxRadius * 2.2f;
			for (int i = 0; i < fx.RingCount; i++)
			{
				float delay = i * fx.RingDelay;
				float reachScale = 1f - i * 0.12f;
				float strength = fx.Distortion * (1f - i * 0.22f);
				Material shockMaterial = NewShockwaveMaterial();
				GameObject shockObject = null;
				if (shockMaterial != null)
				{
					shockObject = UltimateFxStage.CreateMeshObject("ShockDistort" + i, root.transform, UltimateFxStage.GroundQuad, shockMaterial);
					shockMaterial.renderQueue = 2930 + i;
					shockObject.transform.position = origin + Vector3.up * 0.1f;
					shockObject.transform.localScale = new Vector3(diameter, 1f, diameter);
				}
				Material glowMaterial = NewMaterial(UltimateFxTextures.Ring, true, Color.clear);
				GameObject glowObject = CreatePlate("ShockGlow" + i, glowMaterial, origin, 0.06f + i * 0.005f, diameter, 6 + i);
				float shockRadius = maxRadius * reachScale;
				AddTrack(false, start + delay, fx.RingSeconds, t =>
				{
					float expand = EaseOut(t);
					float ringRadius = shockRadius * expand;
					float fade = Mathf.Pow(1f - t, 1.6f);
					if (shockMaterial != null)
					{
						shockMaterial.SetFloat("_Ring", Mathf.Clamp01(ringRadius / (diameter * 0.5f)));
						shockMaterial.SetFloat("_Width", fx.RingWidth * Mathf.Lerp(0.6f, 1.3f, t));
						shockMaterial.SetFloat("_Strength", strength);
						shockMaterial.SetFloat("_Fade", fade);
						shockMaterial.SetColor("_RimColor", Colors.Mid);
						shockMaterial.SetFloat("_Aspect", Screen.height > 0 ? Screen.width / (float)Screen.height : 1.7778f);
						if (mainCamera != null)
						{
							Vector3 viewport = mainCamera.WorldToViewportPoint(origin);
							shockMaterial.SetVector("_ScreenCenter", new Vector4(viewport.x, viewport.y, 0f, 0f));
						}
					}
					// 빛 링: 텍스처 고리 반지름이 판 반지름의 0.86 이므로 맞춰 키움
					float glowSize = ringRadius / 0.86f * 2f;
					glowObject.transform.localScale = new Vector3(glowSize, 1f, glowSize);
					Color ringColor = Color.Lerp(Colors.Core, Colors.Mid, Mathf.Clamp01(t * 1.8f));
					glowMaterial.SetColor("_Color", new Color(ringColor.r, ringColor.g, ringColor.b, fade * (1f - i * 0.2f)));
				}, () =>
				{
					if (shockObject != null)
						UnityEngine.Object.Destroy(shockObject);
					UnityEngine.Object.Destroy(glowObject);
				});
			}
		}

		// ---- 빛기둥: 세운 판 둘 (바깥 넓은 빛, 안쪽 흰 속) 을 카메라 쪽으로 돌림 ----
		private void BuildPillar(Vector3 origin, float start)
		{
			Material outerMaterial = NewMaterial(UltimateFxTextures.Pillar, true, Color.clear);
			Material innerMaterial = NewMaterial(UltimateFxTextures.Pillar, true, Color.clear);
			GameObject outer = UltimateFxStage.CreateMeshObject("PillarOuter", root.transform, UltimateFxStage.StandingQuad, outerMaterial, 7);
			GameObject inner = UltimateFxStage.CreateMeshObject("PillarInner", root.transform, UltimateFxStage.StandingQuad, innerMaterial, 8);
			outer.transform.position = origin;
			inner.transform.position = origin;
			AddTrack(false, start, fx.PillarSeconds, t =>
			{
				float yaw = mainCamera != null ? mainCamera.transform.eulerAngles.y : 0f;
				Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
				outer.transform.rotation = facing;
				inner.transform.rotation = facing;
				// 위로 치솟으며 처음엔 굵고 점점 가늘어지며 사라짐
				float rise = EaseOut(Mathf.Clamp01(t * 6f));
				float thin = Mathf.Lerp(1.15f, 0.25f, EaseIn(t));
				float fade = t < 0.1f ? 1f : Mathf.Pow(1f - (t - 0.1f) / 0.9f, 1.5f);
				outer.transform.localScale = new Vector3(fx.PillarWidth * thin, fx.PillarHeight * rise, 1f);
				inner.transform.localScale = new Vector3(fx.PillarWidth * thin * 0.38f, fx.PillarHeight * rise * 1.05f, 1f);
				outerMaterial.SetColor("_Color", new Color(Colors.Mid.r, Colors.Mid.g, Colors.Mid.b, fade * 0.9f));
				innerMaterial.SetColor("_Color", new Color(Colors.Core.r, Colors.Core.g, Colors.Core.b, fade));
			}, () =>
			{
				UnityEngine.Object.Destroy(outer);
				UnityEngine.Object.Destroy(inner);
			});
		}

		// ---- 먼지: 바닥에서 바깥으로 퍼지는 고리 + 솟는 기둥 ----
		private void BuildDust(Vector3 origin, float radius, float start)
		{
			Material dustMaterial = NewMaterial(UltimateFxTextures.Smoke, false, Color.white);
			ParticleSystem dust = UltimateFxStage.CreateParticles("Dust", root.transform, dustMaterial, fx.DustCount + 16, -0.05f);
			UltimateFxStage.SetLifetimeLook(dust, UltimateFxStage.FadeGradient(Color.white, 0.12f, 0.45f), AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.7f));
			for (int i = 0; i < fx.DustCount; i++)
			{
				float angle = UnityEngine.Random.value * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				float speed = UnityEngine.Random.Range(radius * 0.9f, radius * 2.1f);
				Vector3 position = origin + direction * UnityEngine.Random.Range(0.3f, radius * 0.4f) + Vector3.up * 0.2f;
				Vector3 velocity = direction * speed + Vector3.up * UnityEngine.Random.Range(0.4f, 3.2f);
				Color color = Color.Lerp(Colors.Dust, Color.white * Colors.Dust.a, UnityEngine.Random.value * 0.3f);
				color.a = Colors.Dust.a * UnityEngine.Random.Range(0.55f, 1f);
				UltimateFxStage.Emit(dust, position, velocity, UnityEngine.Random.Range(1.4f, 3.4f), UnityEngine.Random.Range(1.4f, 2.8f), color, UnityEngine.Random.value * 360f, UnityEngine.Random.Range(-40f, 40f));
			}
			ParticleSystem.NoiseModule noise = dust.noise;
			noise.enabled = true;
			noise.strength = 1.2f;
			noise.frequency = 0.35f;
			ParticleSystem.LimitVelocityOverLifetimeModule limit = dust.limitVelocityOverLifetime;
			limit.enabled = true;
			limit.dampen = 0.14f;
			limit.limit = 1.5f;
			dust.Play();
		}

		// ---- 파편: 돌 (알파 합성 네 종류) 과 달아오른 돌 (더하기) ----
		private void BuildDebris(Vector3 origin, float radius, float start)
		{
			Texture2D[] shards = UltimateFxTextures.Shards;
			ParticleSystem[] rock = new ParticleSystem[shards.Length];
			ParticleSystem[] hot = new ParticleSystem[shards.Length];
			for (int i = 0; i < shards.Length; i++)
			{
				rock[i] = UltimateFxStage.CreateParticles("Rock" + i, root.transform, NewMaterial(shards[i], false, Color.white), 128, 2.6f);
				UltimateFxStage.SetLifetimeLook(rock[i], UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.82f), null);
				hot[i] = UltimateFxStage.CreateParticles("Hot" + i, root.transform, NewMaterial(shards[i], true, Color.white), 64, 2.2f);
				UltimateFxStage.SetLifetimeLook(hot[i], UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.4f), null);
			}
			for (int i = 0; i < fx.DebrisCount; i++)
			{
				float angle = UnityEngine.Random.value * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				float horizontal = UnityEngine.Random.Range(0.25f, 1f) * fx.DebrisSpeed * 0.7f;
				Vector3 velocity = direction * horizontal + Vector3.up * UnityEngine.Random.Range(0.55f, 1f) * fx.DebrisSpeed;
				Vector3 position = origin + direction * UnityEngine.Random.Range(0.2f, radius * 0.5f) + Vector3.up * 0.3f;
				int kind = UnityEngine.Random.Range(0, shards.Length);
				float size = UnityEngine.Random.Range(0.25f, 0.85f);
				float lifetime = UnityEngine.Random.Range(1.3f, 2.5f);
				float spin = UnityEngine.Random.Range(-600f, 600f);
				if (UnityEngine.Random.value < 0.3f)
				{
					Color hotColor = Color.Lerp(Colors.Mid, Colors.Core, UnityEngine.Random.value);
					hotColor.a = 1f;
					UltimateFxStage.Emit(hot[kind], position, velocity, size * 0.8f, lifetime, hotColor, UnityEngine.Random.value * 360f, spin);
				}
				else
				{
					float shade = UnityEngine.Random.Range(0.7f, 1.15f);
					Color rockColor = new(Colors.Rock.r * shade, Colors.Rock.g * shade, Colors.Rock.b * shade, 1f);
					UltimateFxStage.Emit(rock[kind], position, velocity, size, lifetime, rockColor, UnityEngine.Random.value * 360f, spin);
				}
			}
		}

		// ---- 불꽃: 길게 늘어난 빛 알갱이가 사방으로 ----
		private void BuildSparks(Vector3 origin, float start)
		{
			Material sparkMaterial = NewMaterial(UltimateFxTextures.Spark, true, Color.white);
			ParticleSystem sparks = UltimateFxStage.CreateParticles("ImpactSparks", root.transform, sparkMaterial, fx.SparkCount + 16, 0.7f, true);
			UltimateFxStage.SetLifetimeLook(sparks, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.45f), AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
			for (int i = 0; i < fx.SparkCount; i++)
			{
				Vector3 direction = UnityEngine.Random.onUnitSphere;
				direction.y = Mathf.Abs(direction.y) * 1.2f + 0.2f;
				Vector3 velocity = direction.normalized * UnityEngine.Random.Range(9f, 26f);
				Color color = Color.Lerp(Colors.Mid, Colors.Core, UnityEngine.Random.value);
				color.a = 1f;
				UltimateFxStage.Emit(sparks, origin + Vector3.up * 0.8f, velocity, UnityEngine.Random.Range(0.18f, 0.5f), UnityEngine.Random.Range(0.8f, 1.9f), color);
			}
		}

		// ---- 잔여: 식는 땅 위에 떠오르는 불씨와 연기 ----
		private void BuildAftermath()
		{
			float radius = fx.ImpactRadius;
			Material emberMaterial = NewMaterial(UltimateFxTextures.Spark, true, Color.white);
			ParticleSystem embers = UltimateFxStage.CreateParticles("Embers", root.transform, emberMaterial, fx.EmberCount + 16, -0.08f);
			UltimateFxStage.SetLifetimeLook(embers, UltimateFxStage.FadeGradient(Color.white, 0.15f, 0.5f), AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
			ParticleSystem.NoiseModule emberNoise = embers.noise;
			emberNoise.enabled = true;
			emberNoise.strength = 1.1f;
			emberNoise.frequency = 0.5f;

			Material smokeMaterial = NewMaterial(UltimateFxTextures.Smoke, false, Color.white);
			ParticleSystem smoke = UltimateFxStage.CreateParticles("Smoke", root.transform, smokeMaterial, fx.SmokeCount + 8, -0.12f);
			UltimateFxStage.SetLifetimeLook(smoke, UltimateFxStage.FadeGradient(Color.white, 0.2f, 0.4f), AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.2f));

			// 입자가 다 사라질 때까지 붙잡아 둠 (뿌리 오브젝트가 입자계를 들고 있음)
			AddTrack(false, worldTime, fx.AftermathSeconds, null);

			float emberCarry = 0f;
			float smokeCarry = 0f;
			float span = fx.CrackHeatSeconds;
			AddTrack(false, worldTime + 0.15f, span, t =>
			{
				float rate = (1f - t) * fx.EmberCount / span * 1.6f;
				emberCarry += rate * worldDelta;
				while (emberCarry >= 1f)
				{
					emberCarry -= 1f;
					Vector3 point = center + UltimateFxStage.RandomRingPoint(0.5f, radius * 0.85f) + Vector3.up * 0.1f;
					Color color = Color.Lerp(Colors.Mid, Colors.Core, UnityEngine.Random.value * 0.6f);
					color.a = 1f;
					UltimateFxStage.Emit(embers, point, new Vector3(0f, UnityEngine.Random.Range(0.5f, 1.6f), 0f), UnityEngine.Random.Range(0.12f, 0.32f), UnityEngine.Random.Range(1.6f, 3.4f), color);
				}
				smokeCarry += (1f - t) * fx.SmokeCount / span * 1.5f * worldDelta;
				while (smokeCarry >= 1f)
				{
					smokeCarry -= 1f;
					Vector3 point = center + UltimateFxStage.RandomRingPoint(0.5f, radius * 0.7f);
					Color color = new(0.16f, 0.13f, 0.12f, UnityEngine.Random.Range(0.28f, 0.5f));
					UltimateFxStage.Emit(smoke, point, new Vector3(0f, UnityEngine.Random.Range(0.8f, 1.8f), 0f), UnityEngine.Random.Range(2f, 4f), UnityEngine.Random.Range(2.6f, 4.2f), color, UnityEngine.Random.value * 360f, UnityEngine.Random.Range(-15f, 15f));
				}
			});
		}
	}
}
