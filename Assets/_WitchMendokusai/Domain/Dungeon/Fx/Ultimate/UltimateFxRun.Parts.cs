using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 여러 연출이 같이 쓰는 월드 부품. 값은 부르는 연출이 자기 직렬화 필드에서 넘김
	/// - 얇은 고리, 굴절 충격파, 지면 균열, 파편, 먼지, 불꽃 터짐, 임시 풀과 나무, 풀 반응 (휘어짐과 뜯겨 나는 풀잎)
	/// </summary>
	public sealed partial class UltimateFxRun
	{
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
					Vector3 offset = UltimateFxStage.RandomRingPoint(Mathf.Max(1f, kind.MinDistance), fx.FoliageAreaRadius);
					Vector3 position = Center + offset;
					position.y = UltimateFxStage.GroundHeight(position);
					float scale = Mathf.Lerp(kind.ScaleRange.x, kind.ScaleRange.y, Random.value);
					GameObject instance = Object.Instantiate(kind.Prefab, position, Quaternion.Euler(0f, Random.value * 360f, 0f), root.transform);
					instance.transform.localScale = Vector3.one * scale;
					FxFoliage foliage = instance.AddComponent<FxFoliage>();
					foliage.Flexibility = kind.Flexibility;
				}
			}
		}

		/// <summary> 중심에서 speed 로 퍼지는 바람. 풀과 나무가 휘고, tearBlades 면 바람 앞에서 풀잎이 뜯겨 날아감 (지면 타격이 있는 연출만) </summary>
		public void React(Vector3 origin, float speed, float reach, bool tearBlades = true)
		{
			UltimateFx.FoliageResponse response = fx.Response;
			FxFoliage.React(origin, speed, reach, response.Bend, response.DecaySeconds, response.Hertz);
			if (tearBlades == false || response.BladeCount <= 0)
				return;
			ParticleSystem blades = Particles("Blades", Alpha(UltimateFxTextures.GrassBlade, Color.white), response.BladeCount + 8, 0.9f);
			UltimateFxStage.SetLifetimeLook(blades, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.75f), null);
			for (int i = 0; i < response.BladeCount; i++)
			{
				Vector3 offset = UltimateFxStage.RandomRingPoint(reach * 0.15f, reach * 0.6f);
				Vector3 away = offset.normalized;
				Vector3 velocity = away * response.BladeSpeed * Random.Range(0.6f, 1.2f) + Vector3.up * Random.Range(2f, 6f);
				Color color = response.BladeColor * Random.Range(0.8f, 1.15f);
				color.a = 1f;
				UltimateFxStage.Emit(blades, origin + offset + Vector3.up * 0.2f, velocity, Random.Range(0.35f, 0.7f), Random.Range(1.2f, 2.2f), color, Random.value * 360f, Random.Range(-500f, 500f));
			}
		}

		/// <summary> 바닥 위로 퍼지는 얇은 고리 하나. 크기 비교 기준선 </summary>
		public void ThinRing(float start, Vector3 origin, float fromRadius, float toRadius, float seconds, Color color, int order)
		{
			Material material = Additive(UltimateFxTextures.ThinRing, Color.clear);
			GameObject ring = null;
			Track(start, seconds, progress =>
			{
				if (ring == null)
					ring = Plate("ThinRing", material, origin + Vector3.up * 0.07f, 0.01f, order);
				float radius = Mathf.Lerp(fromRadius, toRadius, EaseOut(progress));
				float size = radius / UltimateFxTextures.RING_RADIUS * 2f;
				ring.transform.localScale = new Vector3(size, 1f, size);
				material.SetColor("_Color", new Color(color.r, color.g, color.b, color.a * Mathf.Pow(1f - progress, 1.4f)));
			}, () => Destroy(ring));
		}

		/// <summary> 굴절 띠와 빛 고리가 함께 퍼지는 충격파. 같은 속도로 풀이 휨. tearBlades 가 거짓이면 풀잎 뜯김 입자 없이 휘기만 </summary>
		public void Shockwave(float start, Vector3 origin, float maxRadius, float seconds, float distortion, float bandWidth, Color rimColor, bool tearBlades = true)
		{
			Material shock = Shockwave();
			Material glow = Additive(UltimateFxTextures.Ring, Color.clear);
			float diameter = maxRadius * 2.2f;
			GameObject shockObject = null;
			GameObject glowObject = null;
			At(start, () => React(origin, maxRadius / seconds * 0.8f, maxRadius, tearBlades));
			Track(start, seconds, progress =>
			{
				if (shockObject == null)
				{
					shockObject = UltimateFxStage.CreateMeshObject("Shock", root.transform, UltimateFxStage.GroundQuad, shock);
					shock.renderQueue = 2931;
					shockObject.transform.position = origin + Vector3.up * 0.1f;
					shockObject.transform.localScale = new Vector3(diameter, 1f, diameter);
					glowObject = Plate("ShockGlow", glow, origin + Vector3.up * 0.06f, 0.01f, 6);
				}
				float radius = maxRadius * EaseOut(progress);
				float fade = Mathf.Pow(1f - progress, 1.6f);
				shock.SetFloat("_Ring", Mathf.Clamp01(radius / (diameter * 0.5f)));
				shock.SetFloat("_Width", bandWidth * Mathf.Lerp(0.6f, 1.3f, progress));
				shock.SetFloat("_Strength", distortion);
				shock.SetFloat("_Fade", fade);
				shock.SetColor("_RimColor", rimColor);
				shock.SetFloat("_Aspect", Screen.height > 0 ? Screen.width / (float)Screen.height : 1.7778f);
				Vector2 viewport = Viewport(origin);
				shock.SetVector("_ScreenCenter", new Vector4(viewport.x, viewport.y, 0f, 0f));
				float glowSize = radius / UltimateFxTextures.RING_RADIUS * 2f;
				glowObject.transform.localScale = new Vector3(glowSize, 1f, glowSize);
				glow.SetColor("_Color", new Color(rimColor.r, rimColor.g, rimColor.b, fade));
			}, () =>
			{
				Destroy(shockObject);
				Destroy(glowObject);
			});
		}

		/// <summary>
		/// 지면 균열. 가운데에서 번져 나간 뒤 식음. 식을 때 색을 어둡게 바꾸지 않고 같은 색에서 투명해짐
		/// - 붉은 갈색으로 식히면 풀밭 위에서 나무뿌리로 읽혔음 (2026-11-19 검토)
		/// </summary>
		public void Crack(float start, Vector3 origin, float radius, int seed, Color coreColor, Color glowColor, float revealSeconds, float heatSeconds)
		{
			UltimateFxTextures.Crack(seed, out Texture2D coreTexture, out Texture2D glowTexture);
			Material glow = Additive(glowTexture, Color.clear);
			Material core = Additive(coreTexture, Color.clear);
			float diameter = radius * 2.3f;
			float yaw = Random.value * 360f;
			GameObject glowObject = null;
			GameObject coreObject = null;
			Track(start, revealSeconds + heatSeconds, progress =>
			{
				if (glowObject == null)
				{
					glowObject = Plate("CrackGlow", glow, origin + Vector3.up * 0.03f, 0.01f, 3);
					coreObject = Plate("CrackCore", core, origin + Vector3.up * 0.04f, 0.01f, 4);
					glowObject.transform.rotation = coreObject.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
				}
				float seconds = progress * (revealSeconds + heatSeconds);
				float reveal = EaseOut(seconds / revealSeconds);
				float size = diameter * Mathf.Lerp(0.15f, 1f, reveal);
				glowObject.transform.localScale = coreObject.transform.localScale = new Vector3(size, 1f, size);
				float heat = Span(seconds, revealSeconds, revealSeconds + heatSeconds);
				float flicker = 0.85f + 0.15f * Mathf.Sin(seconds * 23f) * Mathf.Sin(seconds * 9.7f + 1f);
				glow.SetColor("_Color", new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * Mathf.Pow(1f - heat, 1.8f) * flicker));
				core.SetColor("_Color", new Color(coreColor.r, coreColor.g, coreColor.b, coreColor.a * Mathf.Pow(1f - heat, 3f)));
			}, () =>
			{
				Destroy(glowObject);
				Destroy(coreObject);
			});
		}

		/// <summary> 돌 파편 (알파) 과 달아오른 파편 (더하기) 이 위로 튀어 떨어짐 </summary>
		public void Debris(Vector3 origin, float radius, int count, float speed, Color rockColor, Color hotColor, float hotChance)
		{
			Texture2D[] shards = UltimateFxTextures.Shards;
			ParticleSystem[] rock = new ParticleSystem[shards.Length];
			ParticleSystem[] hot = new ParticleSystem[shards.Length];
			for (int i = 0; i < shards.Length; i++)
			{
				rock[i] = Particles("Rock" + i, Alpha(shards[i], Color.white), count + 8, 2.6f);
				UltimateFxStage.SetLifetimeLook(rock[i], UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.82f), null);
				hot[i] = Particles("Hot" + i, Additive(shards[i], Color.white), count + 8, 2.2f);
				UltimateFxStage.SetLifetimeLook(hot[i], UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.4f), null);
			}
			for (int i = 0; i < count; i++)
			{
				float angle = Random.value * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				Vector3 velocity = direction * Random.Range(0.25f, 1f) * speed * 0.7f + Vector3.up * Random.Range(0.55f, 1f) * speed;
				Vector3 position = origin + direction * Random.Range(0.2f, radius * 0.5f) + Vector3.up * 0.3f;
				int kind = Random.Range(0, shards.Length);
				float size = Random.Range(0.25f, 0.85f);
				float lifetime = Random.Range(1.3f, 2.5f);
				float spin = Random.Range(-600f, 600f);
				bool isHot = Random.value < hotChance;
				Color color = isHot ? hotColor : rockColor * Random.Range(0.7f, 1.15f);
				color.a = 1f;
				UltimateFxStage.Emit(isHot ? hot[kind] : rock[kind], position, velocity, isHot ? size * 0.8f : size, lifetime, color, Random.value * 360f, spin);
			}
		}

		/// <summary> 바닥에서 바깥으로 퍼지는 먼지 고리 </summary>
		public void Dust(Vector3 origin, float radius, int count, Color color)
		{
			ParticleSystem dust = Particles("Dust", Alpha(UltimateFxTextures.Smoke, Color.white), count + 8, -0.05f);
			UltimateFxStage.SetLifetimeLook(dust, UltimateFxStage.FadeGradient(Color.white, 0.12f, 0.45f), AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.7f));
			for (int i = 0; i < count; i++)
			{
				float angle = Random.value * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				Vector3 position = origin + direction * Random.Range(0.3f, radius * 0.4f) + Vector3.up * 0.2f;
				Vector3 velocity = direction * Random.Range(radius * 0.9f, radius * 2.1f) + Vector3.up * Random.Range(0.4f, 3.2f);
				Color tone = color;
				tone.a = color.a * Random.Range(0.55f, 1f);
				UltimateFxStage.Emit(dust, position, velocity, Random.Range(1.4f, 3.4f), Random.Range(1.4f, 2.8f), tone, Random.value * 360f, Random.Range(-40f, 40f));
			}
			ParticleSystem.LimitVelocityOverLifetimeModule limit = dust.limitVelocityOverLifetime;
			limit.enabled = true;
			limit.dampen = 0.14f;
			limit.limit = 1.5f;
		}

		/// <summary> 길게 늘어난 빛 알갱이가 사방으로 </summary>
		public void SparkBurst(Vector3 origin, int count, Vector2 speedRange, Vector2 lifeRange, Vector2 sizeRange, Color colorFrom, Color colorTo, float gravity)
		{
			ParticleSystem sparks = Particles("Sparks", Additive(UltimateFxTextures.Spark, Color.white), count + 8, gravity, true);
			UltimateFxStage.SetLifetimeLook(sparks, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.45f), AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
			for (int i = 0; i < count; i++)
			{
				Vector3 direction = Random.onUnitSphere;
				direction.y = Mathf.Abs(direction.y) * 1.2f + 0.2f;
				Color color = Color.Lerp(colorFrom, colorTo, Random.value);
				color.a = 1f;
				UltimateFxStage.Emit(sparks, origin, direction.normalized * Random.Range(speedRange.x, speedRange.y), Random.Range(sizeRange.x, sizeRange.y), Random.Range(lifeRange.x, lifeRange.y), color);
			}
		}
	}
}
