using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 3D 연출 (월드 공간) 의 공통 바탕. 창 왕관, 카드, 달의 약한 판과 강한 판이 이 위에 섬
	/// - 화면 위 그림 없음. 실제 메시 (코드 생성, UltimateMeshFactory), 빛 받는 입자, 연출 광원, 바닥 판 데칼, 카메라, Volume 후처리만
	/// - 장면 빛과 후처리는 매 프레임 한 곳에서 넣음 (Stage 의 곡선 함수). 연출은 곡선만 정함
	/// - 연출 광원은 같은 시각 2개까지 (URP 물체당 4개 상한, 진열장 무대 추가 광원 2개). 끌 때는 지우지 않고 enabled 만 끔 (지운 프레임 사각 얼룩)
	/// - 공통 규칙 근거: memo wm/design/ult3d-all-20261120 (광원과 공통 규칙), ult-3d-refs-20261119 (실측)
	/// </summary>
	public abstract class WorldUltimateFx : UltimateFx
	{
		protected static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
		protected static readonly int EMISSION_ID = Shader.PropertyToID("_EmissionColor");
		protected static readonly int RIM_ID = Shader.PropertyToID("_RimColor");
		protected static readonly int DISSOLVE_ID = Shader.PropertyToID("_Dissolve");
		protected static readonly int DISSOLVE_DIRECTION_ID = Shader.PropertyToID("_DissolveDirection");
		protected static readonly int DISSOLVE_RANGE_ID = Shader.PropertyToID("_DissolveRange");
		protected static readonly int DISSOLVE_EDGE_ID = Shader.PropertyToID("_DissolveEdgeColor");
		protected static readonly int USE_VERTEX_COLOR_ID = Shader.PropertyToID("_UseVertexColor");
		protected static readonly int SPECULAR_ID = Shader.PropertyToID("_Specular");
		protected static readonly int SMOOTHNESS_ID = Shader.PropertyToID("_Smoothness");
		protected static readonly int RIM_POWER_ID = Shader.PropertyToID("_RimPower");
		protected const float FRAME = 1f / 24f;

		/// <summary> 연출 한 번의 상태. 같은 에셋이 겹쳐 재생돼도 섞이지 않게 에셋이 아니라 여기에 둠 </summary>
		protected sealed class Stage
		{
			public UltimateFxRun Run;
			public UltimateWorldLighting Lighting;
			public UltimateWorldGrade Grade;
			public Vector3 Target;
			public Vector3 ToCamera;

			// 시각 (초) -> 값. 기본은 변화 없음
			public Func<float, float> SunScale = _ => 1f;
			public Func<float, float> AmbientScale = _ => 1f;
			public Func<float, float> Bloom = _ => 0f;
			public Color BloomTint = Color.white;
			public float BloomThreshold = 1.1f;
			public Func<float, float> Exposure = _ => 0f;
			public Func<float, float> Chromatic = _ => 0f;
			public Func<float, float> Vignette = _ => 0f;
			public Func<float, float> Saturation = _ => 0f;
			public Func<float, float> Contrast = _ => 0f;
			public Func<float, Color> Filter = _ => Color.white;
		}

		[Header("3D 연결")]
		[SerializeField] private Shader meshGlowShader;
		[SerializeField] private Shader litParticleShader;

		[Header("인형 빛 반응")]
		[SerializeField, Min(0f), Tooltip("빛 안 받는 인형 그림이 연출 광원을 받는 배율")] private float spriteLightGain = 0.12f;
		[SerializeField, Min(1f)] private float spriteTintCap = 2.2f;
		[SerializeField, Min(0.05f)] private float spriteRefreshSeconds = 0.3f;

		[Header("배치")]
		[SerializeField, Tooltip("표적의 화면 위쪽 거리 (시전자 기준). 연출이 인물을 가리지 않게")] private float targetDistance = 2.8f;

		public override void Compose(UltimateFxRun run)
		{
			if (meshGlowShader == null || litParticleShader == null)
				throw new InvalidOperationException($"{name}: 3D 셰이더 미연결. 메뉴 WM/CombatFx/Build Ultimate 3D Batch Presets");
			Stage stage = new()
			{
				Run = run,
				Lighting = new UltimateWorldLighting(run.MainCamera, spriteLightGain, spriteTintCap, spriteRefreshSeconds),
				Grade = new UltimateWorldGrade(60f),
			};
			run.OnRelease(stage.Lighting.Dispose);
			run.OnRelease(stage.Grade.Dispose);
			Vector3 target = run.Center + run.ScreenForward * targetDistance;
			target.y = UltimateFxStage.GroundHeight(target);
			stage.Target = target;
			Vector3 toCamera = run.MainCamera != null ? run.MainCamera.transform.position - target : -run.ScreenForward;
			toCamera.y = 0f;
			stage.ToCamera = toCamera.sqrMagnitude > 0.0001f ? toCamera.normalized : -run.ScreenForward;

			ComposeWorld(stage);

			// 하늘 감독 Update 뒤에 매 프레임 덮음. 꼬리까지 한 트랙
			run.Track(0f, DurationSeconds + TailSeconds, _ =>
			{
				float now = run.Now;
				stage.Lighting.SetSkyDim(stage.SunScale(now), stage.AmbientScale(now));
				stage.Lighting.SetBloom(stage.Bloom(now), stage.BloomThreshold, stage.BloomTint);
				stage.Lighting.SetExposure(stage.Exposure(now));
				stage.Lighting.SetChromatic(stage.Chromatic(now));
				stage.Lighting.SetVignette(stage.Vignette(now), Color.black);
				stage.Grade.Set(stage.Saturation(now), stage.Contrast(now), stage.Filter(now));
				stage.Lighting.Apply(run.Delta);
			});
		}

		/// <summary> 시간선 구성. 표적, 광원 도구, 색 보정은 stage 에 준비됨 </summary>
		protected abstract void ComposeWorld(Stage stage);

		// ---- 곡선 ----

		/// <summary> start 부터 rise 초에 최대, 그 뒤 반감 halfLife </summary>
		protected static float Spike(float now, float start, float rise, float halfLife)
		{
			float age = now - start;
			if (age < 0f)
				return 0f;
			if (age < rise)
				return rise <= 0f ? 1f : age / rise;
			return Mathf.Pow(0.5f, (age - rise) / Mathf.Max(0.0001f, halfLife));
		}

		/// <summary> [start, start + seconds) 안이면 1 </summary>
		protected static float Window(float now, float start, float seconds) => now >= start && now < start + seconds ? 1f : 0f;

		/// <summary> from 에서 inSeconds 에 걸쳐 오르고 restoreStart 부터 outSeconds 에 걸쳐 내려옴 </summary>
		protected static float Plateau(float now, float from, float inSeconds, float restoreStart, float outSeconds)
		{
			float rise = UltimateFxRun.EaseInOut(UltimateFxRun.Span(now, from, from + inSeconds));
			float fall = UltimateFxRun.EaseInOut(UltimateFxRun.Span(now, restoreStart, restoreStart + outSeconds));
			return rise * (1f - fall);
		}

		protected static float OverShoot(float progress, float amount)
		{
			float clamped = Mathf.Clamp01(progress);
			return 1f + amount * Mathf.Sin(clamped * Mathf.PI) - Mathf.Pow(1f - clamped, 3f);
		}

		// ---- 재질과 메시 ----

		protected Material Glow(UltimateFxRun run, Color baseColor, Color emission, Color rim, bool useVertexColor)
		{
			Material material = new(meshGlowShader) { name = "UltimateFx_WorldGlow" };
			material.SetColor(BASE_COLOR_ID, baseColor);
			material.SetColor(EMISSION_ID, emission);
			material.SetColor(RIM_ID, rim);
			material.SetFloat(USE_VERTEX_COLOR_ID, useVertexColor ? 1f : 0f);
			run.OnRelease(() => UnityEngine.Object.Destroy(material));
			return material;
		}

		protected Material LitParticle(UltimateFxRun run, Texture texture, float softDistance)
		{
			Material material = new(litParticleShader) { name = "UltimateFx_WorldLitParticle", mainTexture = texture };
			material.SetFloat("_SoftDistance", softDistance);
			material.renderQueue = 3010;
			run.OnRelease(() => UnityEngine.Object.Destroy(material));
			return material;
		}

		protected static Mesh Keep(UltimateFxRun run, Mesh mesh)
		{
			run.OnRelease(() => UnityEngine.Object.Destroy(mesh));
			return mesh;
		}

		protected static MeshRenderer Solid(GameObject target, bool castShadows)
		{
			MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();
			meshRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
			meshRenderer.receiveShadows = true;
			return meshRenderer;
		}

		/// <summary> 메시 입자 (파편, 카드 눈). 월드 충돌 켜면 바닥에서 튀거나 멈춤 </summary>
		protected static ParticleSystem MeshParticles(UltimateFxRun run, string name, Mesh mesh, Material material, int maxParticles, float gravity, bool collide, float bounce, float dampen, bool castShadows)
		{
			ParticleSystem particles = run.Particles(name, material, maxParticles, gravity);
			ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
			particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
			particleRenderer.mesh = mesh;
			particleRenderer.alignment = ParticleSystemRenderSpace.Local;
			particleRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
			particleRenderer.receiveShadows = true;
			ParticleSystem.MainModule main = particles.main;
			main.startRotation3D = true;
			main.startSize3D = false;
			if (collide)
			{
				ParticleSystem.CollisionModule collision = particles.collision;
				collision.enabled = true;
				collision.type = ParticleSystemCollisionType.World;
				collision.mode = ParticleSystemCollisionMode.Collision3D;
				collision.bounce = bounce;
				collision.dampen = dampen;
				collision.lifetimeLoss = 0f;
				collision.radiusScale = 0.4f;
			}
			return particles;
		}

		protected static void EmitMesh(ParticleSystem particles, Vector3 position, Vector3 velocity, float size, float lifetime, Color color, Vector3 rotation, Vector3 spin)
		{
			ParticleSystem.EmitParams emitParams = new()
			{
				position = position,
				velocity = velocity,
				startSize = size,
				startLifetime = lifetime,
				startColor = color,
				rotation3D = rotation,
				angularVelocity3D = spin,
			};
			particles.Emit(emitParams, 1);
		}

		// ---- 광원 ----

		/// <summary> 연출 광원. 인형 물들임에 반영됨. 끌 때는 enabled = false </summary>
		protected static Light WorldLight(Stage stage, string name, LightType type, Color color, float range)
		{
			Light light = stage.Run.PointLight(name, color, range);
			light.type = type;
			stage.Lighting.Track(light);
			return light;
		}

		// ---- 바닥 반응 ----

		/// <summary> 바닥 판 데칼 (그을림, 균열, 구덩이). fadeIn 동안 나타나 lingerEnd 부터 fadeOut 동안 사라짐 </summary>
		protected static void GroundDecal(UltimateFxRun run, Texture2D texture, Vector3 position, float size, float appear, float fadeIn, float lingerEnd, float fadeOut, int order)
		{
			Material material = run.Alpha(texture, new Color(1f, 1f, 1f, 0f));
			GameObject plate = run.Plate("WorldDecal", material, position + Vector3.up * (0.025f + order * 0.004f), size, order);
			plate.transform.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
			run.Track(appear, fadeIn, progress => material.SetColor("_Color", new Color(1f, 1f, 1f, UltimateFxRun.EaseOut(progress))));
			run.Track(lingerEnd, fadeOut, progress => material.SetColor("_Color", new Color(1f, 1f, 1f, 1f - UltimateFxRun.EaseIn(progress))));
		}

		/// <summary> 바닥 판 위의 가산 빛 웅덩이 (데칼과 같은 판). 광원과 같이 써서 지면이 빛을 받는 순간을 강조 </summary>
		protected static void GroundGlow(UltimateFxRun run, Vector3 position, float size, Color color, float start, float rise, float halfLife, float seconds)
		{
			Material material = run.Additive(UltimateFxTextures.Soft, Color.clear);
			run.Plate("WorldGroundGlow", material, position + Vector3.up * 0.05f, size, 6);
			run.Track(start, seconds, _ => material.SetColor("_Color", color * Spike(run.Now, start, rise, halfLife)));
		}

		/// <summary> 균열 무늬를 그을음 원 위에 얹은 데칼 그림. 연출 끝에 지움 </summary>
		protected static Texture2D ScorchTexture(UltimateFxRun run, int seed, Color scorch, Color line, float coreRadius)
		{
			UltimateFxTextures.Crack(seed, out Texture2D core, out Texture2D _);
			Color[] crack = core.GetPixels();
			int size = core.width;
			Color[] pixels = new Color[crack.Length];
			float half = (size - 1) * 0.5f;
			for (int row = 0; row < size; row++)
			for (int column = 0; column < size; column++)
			{
				int index = row * size + column;
				float radius = new Vector2((column - half) / half, (row - half) / half).magnitude;
				float soot = Mathf.Pow(Mathf.Clamp01(1f - radius / Mathf.Max(0.05f, coreRadius)), 1.4f) * scorch.a;
				float crackLine = crack[index].a;
				Color color = Color.Lerp(scorch, line, crackLine);
				color.a = Mathf.Max(soot, crackLine * line.a);
				pixels[index] = color;
			}
			Texture2D texture = new(size, size, TextureFormat.RGBA32, true) { name = "UltimateFx_WorldScorch", wrapMode = TextureWrapMode.Clamp };
			texture.SetPixels(pixels);
			texture.Apply(true);
			run.OnRelease(() => UnityEngine.Object.Destroy(texture));
			return texture;
		}

		/// <summary> 바닥을 따라 퍼지는 먼지 고리. 빛 받는 입자라 연출 광원 쪽이 밝음 </summary>
		protected void DustRing(UltimateFxRun run, Vector3 origin, int count, float radius, Color color, float size)
		{
			ParticleSystem dust = run.Particles("WorldDust", LitParticle(run, UltimateFxTextures.Smoke, 0.6f), count + 8, -0.04f);
			UltimateFxStage.SetLifetimeLook(dust, UltimateFxStage.FadeGradient(Color.white, 0.08f, 0.45f), AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.8f));
			ParticleSystem.LimitVelocityOverLifetimeModule limit = dust.limitVelocityOverLifetime;
			limit.enabled = true;
			limit.dampen = 0.16f;
			limit.limit = 1.2f;
			for (int i = 0; i < count; i++)
			{
				float angle = (i + Random.value * 0.6f) / count * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				Color tone = color;
				tone.a = color.a * Random.Range(0.6f, 1f);
				UltimateFxStage.Emit(dust, origin + direction * Random.Range(0.3f, 0.9f) + Vector3.up * 0.25f, direction * Random.Range(radius * 1.2f, radius * 2.2f) + Vector3.up * Random.Range(0.3f, 1.4f), size * Random.Range(0.6f, 1.2f), Random.Range(1.2f, 2.2f), tone, Random.value * 360f, Random.Range(-50f, 50f));
			}
		}

		/// <summary> 빛 알갱이 (가산, 둥근 점). 위로 떠오르며 잡음 흐름 </summary>
		protected static ParticleSystem Motes(UltimateFxRun run, string name, int maxParticles, float gravity, float noise)
		{
			ParticleSystem motes = run.Particles(name, run.Additive(UltimateFxTextures.Soft, Color.white), maxParticles, gravity);
			UltimateFxStage.SetLifetimeLook(motes, UltimateFxStage.FadeGradient(Color.white, 0.06f, 0.55f), AnimationCurve.Linear(0f, 1f, 1f, 0.25f));
			if (noise > 0f)
			{
				ParticleSystem.NoiseModule noiseModule = motes.noise;
				noiseModule.enabled = true;
				noiseModule.strength = noise;
				noiseModule.frequency = 0.8f;
			}
			return motes;
		}

		/// <summary> 늘어진 빛 줄 (낙하 궤적, 검기) </summary>
		protected static ParticleSystem Streaks(UltimateFxRun run, string name, int maxParticles, float velocityScale)
		{
			ParticleSystem streaks = run.Particles(name, run.Additive(UltimateFxTextures.Spark, Color.white), maxParticles, 0f, true);
			UltimateFxStage.SetLifetimeLook(streaks, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.25f), AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
			ParticleSystemRenderer streakRenderer = streaks.GetComponent<ParticleSystemRenderer>();
			streakRenderer.velocityScale = velocityScale;
			streakRenderer.lengthScale = 1f;
			return streaks;
		}

		// ---- 인형 ----

		/// <summary>
		/// 범위 안 인형 그림 (빌보드) 을 잠깐 띄움. 연출만, 판정과 이동 없음. 시전자 근처는 제외
		/// - 그림의 지역 위치만 바꾸고 끝나면 (도중 중단 포함) 원래 값으로
		/// </summary>
		protected static void LiftSprites(UltimateFxRun run, Vector3 center, float radius, float height, float start, float seconds)
		{
			List<Transform> lifted = new();
			List<Vector3> origins = new();
			run.At(start, () =>
			{
				foreach (SpriteRenderer sprite in UnityEngine.Object.FindObjectsByType<SpriteRenderer>())
				{
					if (sprite == null || sprite.transform.parent == null || sprite.isVisible == false)
						continue;
					Material material = sprite.sharedMaterial;
					if (material == null || material.shader == null || material.shader.name != "WitchMendokusai/BillboardShader")
						continue;
					Vector3 position = sprite.transform.position;
					Vector3 flat = new(position.x - center.x, 0f, position.z - center.z);
					Vector3 fromCaster = new(position.x - run.Caster.x, 0f, position.z - run.Caster.z);
					if (flat.magnitude > radius || fromCaster.magnitude < 0.8f)
						continue;
					lifted.Add(sprite.transform);
					origins.Add(sprite.transform.localPosition);
				}
			});
			void Restore()
			{
				for (int i = 0; i < lifted.Count; i++)
				{
					if (lifted[i] != null)
						lifted[i].localPosition = origins[i];
				}
				lifted.Clear();
			}
			run.Track(start, seconds, progress =>
			{
				float rise = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
				for (int i = 0; i < lifted.Count; i++)
				{
					if (lifted[i] == null)
						continue;
					Vector3 offset = lifted[i].parent.InverseTransformVector(Vector3.up * height * rise);
					lifted[i].localPosition = origins[i] + offset;
				}
			}, Restore);
			run.OnRelease(Restore);
		}
	}
}
