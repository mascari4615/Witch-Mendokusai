using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 달 가르기 강한 판 (강력 스킬용). 표적 뒤 공중에 거대한 달이 떠올라 사선으로 두 동강 나고, 갈라진 틈에서 흰 빛이 땅까지 쏟아짐
	/// - 흐름: 달이 떠오름 (1.4초, 장면 빛이 연청으로) -> 검기 (번쩍 1장, 흑백 2장) -> 절단 (단면 발광, 단면 점광, 흔들림, 2차 번쩍)
	///   -> 두 반쪽이 단면을 따라 미끄러지며 벌어짐 (1.2초), 빛줄기와 빛 알갱이가 땅까지, 큰 조각 셋이 떨어져 구덩이 -> 반쪽이 녹아 사라짐 -> 장면 빛 복귀
	/// - 약한 판과 같은 공통 부품 (MoonWorldFx) 위에 고유 단계 셋: 달 위상 (옆에서 비추는 달빛 점광으로 명암 경계), 단면 빛줄기, 달 조각 구덩이. 떠오름 예고는 시간 확대
	/// - 원 레퍼런스 m04 (15fps): 등장과 성장 1.4s, 배경 전환 2.2s, 번쩍 1장, 흑백 2장, 절단 0.67s, 2차 번쩍, 분리 1.2s, 소멸 0.6s
	/// - 광원 2개 (달빛 점광, 단면 점광). 밤 전환은 하지 않음 (밤은 밤의 날 전용)
	/// - 재생: 개발 창 fx ultimate MoonHeavy
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_MoonHeavy", menuName = "WM/CombatFx/Ultimate/" + nameof(MoonHeavyUltimateFx))]
	public sealed class MoonHeavyUltimateFx : MoonWorldFx
	{
		[Header("달")]
		[SerializeField, Min(1f), Tooltip("설계 지름 6. 쿼터뷰 화면 위 끝 (높이 약 7.5) 안에 들게 5")] private float diameter = 4.4f;
		[SerializeField, Min(0f), Tooltip("표적에서 화면 위쪽으로 달까지. 멀면 화면 위로 잘림 (v8 촬영)")] private float behindDistance = 1f;
		[SerializeField, Tooltip("떠오르는 중심 높이 (시작, 끝). 꼭대기가 시야 위쪽 7.5 안")] private Vector2 riseHeight = new(0.8f, 2.3f);
		[SerializeField, Tooltip("떠오르며 커지는 배율 (시작, 끝)")] private Vector2 riseScale = new(0.6f, 1f);
		[SerializeField, Tooltip("천천히 도는 속도 (도/초). 구덩이 음영이 움직여 구로 읽힘")] private float idleSpin = 7f;
		[SerializeField, Min(0f), Tooltip("두 반쪽이 단면을 따라 미끄러지는 거리 (반지름 1 기준)")] private float slideDistance = 0.18f;
		[SerializeField, Min(0f), Tooltip("두 반쪽이 단면 바깥으로 벌어지는 거리 (반지름 1 기준)")] private float splitDistance = 0.32f;
		[SerializeField, Tooltip("벌어지며 도는 각도")] private float splitRoll = 4f;

		[Header("시간 (초)")]
		[SerializeField, Min(0.2f), Tooltip("m04 등장과 성장 1.4s")] private float riseSeconds = 1.4f;
		[SerializeField, Min(0f)] private float holdSeconds = 0.2f;
		[SerializeField, Min(0f), Tooltip("검기 뒤 흑백까지")] private float monoDelay = 0.08f;
		[SerializeField, Min(0f), Tooltip("검기 뒤 절단까지")] private float cutDelay = 0.16f;
		[SerializeField, Min(0.1f), Tooltip("m04 분리 1.2s")] private float splitSeconds = 1.2f;
		[SerializeField, Min(0f)] private float driftSeconds = 1.1f;
		[SerializeField, Min(0.1f), Tooltip("m04 소멸 0.6s")] private float vanishSeconds = 0.7f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 1.4f;

		[Header("빛")]
		[SerializeField, Min(0f)] private float moonLightIntensity = 6f;
		[SerializeField, Min(0f), Tooltip("단면 점광이 켜진 뒤 달빛 세기")] private float moonLightAfterCut = 4f;
		[SerializeField, Min(1f)] private float moonLightRange = 24f;
		[SerializeField, Tooltip("달빛 점광 자리 (달 기준: 화면 오른쪽, 위, 카메라 쪽). 옆에서 비춰 위상 경계가 생기게")] private Vector3 moonLightOffset = new(-5f, 3.5f, 2.5f);
		[SerializeField, Min(0f)] private float sectionIntensity = 30f;
		[SerializeField, Min(1f)] private float sectionRange = 16f;
		[SerializeField, Min(0.05f), Tooltip("실측 반감 0.5~1.8s")] private float sectionHalfLife = 0.7f;
		[SerializeField, Range(0f, 1f)] private float sunDim = 0.4f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.55f;
		[SerializeField, Tooltip("연청 장면 빛 (색 필터)")] private Color paleFilter = new(0.84f, 0.92f, 1.08f, 1f);

		[Header("후처리와 카메라")]
		[SerializeField, Min(0f)] private float flashExposure = 1.2f;
		[SerializeField, Min(0f)] private float secondFlashExposure = 0.8f;
		[SerializeField, Range(-100f, 100f)] private float monoContrast = 50f;
		[SerializeField, Min(0f)] private float bloomRise = 0.5f;
		[SerializeField, Min(0f)] private float bloomCut = 1.8f;
		[SerializeField, Range(0f, 1f)] private float vignette = 0.3f;
		[SerializeField, Range(1f, 1.6f), Tooltip("달 전체가 들어오게 물러나는 배율")] private float pullBack = 1.25f;
		[SerializeField, Min(0f)] private float shakeStrength = 1.3f;
		[SerializeField, Min(1)] private int shakeCount = 4;

		[Header("빛줄기와 조각")]
		[SerializeField, Min(0.1f)] private float shaftRadius = 1.4f;
		[SerializeField, ColorUsage(true, true)] private Color shaftColor = new(0.8f, 0.9f, 1.25f, 1f);
		[SerializeField, Min(0.05f)] private float shaftGrowSeconds = 0.15f;
		[SerializeField, Min(0)] private int grainCount = 120;
		[SerializeField, Min(0)] private int shaftDustCount = 50;
		[SerializeField, Min(0)] private int slashCount = 60;
		[SerializeField, Min(0)] private int smallFragmentCount = 12;
		[SerializeField, Min(0)] private int bigFragmentCount = 3;
		[SerializeField, Tooltip("큰 조각 크기 범위")] private Vector2 bigFragmentSize = new(1.3f, 1.9f);
		[SerializeField, Min(0.1f)] private float bigFragmentFallSeconds = 0.55f;
		[SerializeField, Min(0.2f), Tooltip("큰 조각이 떨어지는 표적 둘레 반지름")] private float bigFragmentSpread = 2.6f;
		[SerializeField, Min(0.2f)] private float craterSize = 3.4f;
		[SerializeField, Min(1f)] private float windSpeed = 12f;
		[SerializeField, Min(0), Tooltip("절단 순간 단면에서 사방으로 튀는 빛 줄 수 (정점)")] private int cutBurstCount = 90;

		private float SlashTime => riseSeconds + holdSeconds;
		private float CutTime => SlashTime + cutDelay;
		private float VanishStart => CutTime + splitSeconds + driftSeconds;
		private float RestoreStart => VanishStart - restoreSeconds * 0.3f;

		public override float DurationSeconds => VanishStart + vanishSeconds + 0.3f;

		protected override void ComposeWorld(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			MoonBody moon = CreateMoon(stage);
			float radius = diameter * 0.5f;
			Vector3 ground = stage.Target + run.ScreenForward * behindDistance;
			ground.y = UltimateFxStage.GroundHeight(ground);
			Vector3 center = ground + Vector3.up * riseHeight.y;
			Quaternion facing = Quaternion.LookRotation(-stage.ToCamera);

			ComposeRise(stage, moon, ground, radius, facing);
			ComposeMoonLight(stage, center);
			ComposeCut(stage, moon, center, radius);
			ComposeShaft(stage, moon, center, radius);
			ComposeFragments(stage, moon, center, radius);
			ComposeGrade(stage);
		}

		// 등장: 땅 위에서 떠오르며 커지고 아래부터 녹아 나타남. 천천히 돌아 구덩이 음영이 움직임
		private void ComposeRise(Stage stage, MoonBody moon, Vector3 ground, float radius, Quaternion facing)
		{
			UltimateFxRun run = stage.Run;
			SetDissolve(moon, 1f, -1f);
			run.At(0f, () => moon.Pivot.gameObject.SetActive(true));
			run.Track(0f, VanishStart + vanishSeconds, _ =>
			{
				float now = run.Now;
				float rise = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, 0f, riseSeconds));
				moon.Pivot.position = ground + Vector3.up * Mathf.Lerp(riseHeight.x, riseHeight.y, rise);
				moon.Pivot.localScale = Vector3.one * radius * Mathf.Lerp(riseScale.x, riseScale.y, rise);
				moon.Pivot.rotation = facing * Quaternion.Euler(0f, idleSpin * now - 20f, 0f);
				if (now < riseSeconds)
					SetDissolve(moon, 1f - UltimateFxRun.EaseOut(UltimateFxRun.Span(now, 0f, riseSeconds * 0.7f)), -1f);
			});
			run.Track(VanishStart, vanishSeconds, progress => SetDissolve(moon, UltimateFxRun.EaseIn(progress), 1f), () => run.Destroy(moon.Pivot.gameObject));

			ParticleSystem motes = Motes(run, "MoonMotes", 70, -0.08f, 0.6f);
			for (int i = 0; i < 60; i++)
			{
				float time = i / 60f * riseSeconds;
				run.At(time, () =>
				{
					Vector3 offset = UltimateFxStage.RandomRingPoint(radius * 0.4f, radius * 1.3f);
					UltimateFxStage.Emit(motes, ground + offset + Vector3.up * 0.1f, Vector3.up * Random.Range(1f, 2.4f), Random.Range(0.06f, 0.13f), Random.Range(1f, 1.6f), GrainColor);
				});
			}
		}

		// 달 위상: 달 옆 (카메라에서 보아 왼쪽 앞) 의 달빛 점광. 달 표면에 밝은 쪽과 어두운 쪽 경계, 땅과 인물은 연청으로
		private void ComposeMoonLight(Stage stage, Vector3 center)
		{
			UltimateFxRun run = stage.Run;
			Light moonLight = WorldLight(stage, "MoonLight", LightType.Point, MoonLightColor, moonLightRange);
			moonLight.transform.position = center + run.ScreenRight * moonLightOffset.x + Vector3.up * moonLightOffset.y + stage.ToCamera * moonLightOffset.z;
			run.Track(0f, VanishStart + vanishSeconds, _ =>
			{
				float now = run.Now;
				float build = UltimateFxRun.EaseInOut(UltimateFxRun.Span(now, 0.1f, riseSeconds));
				float afterCut = Mathf.Lerp(1f, moonLightAfterCut / Mathf.Max(0.01f, moonLightIntensity), UltimateFxRun.Span(now, CutTime, CutTime + 0.1f));
				float fade = 1f - UltimateFxRun.Span(now, VanishStart, VanishStart + vanishSeconds);
				moonLight.intensity = moonLightIntensity * build * afterCut * fade;
			}, () => moonLight.enabled = false);
		}

		// 검기와 절단: 시전자에서 달까지 사선 검기, 단면 발광과 단면 점광, 두 반쪽이 단면을 따라 미끄러지며 벌어짐
		private void ComposeCut(Stage stage, MoonBody moon, Vector3 center, float radius)
		{
			UltimateFxRun run = stage.Run;
			run.At(SlashTime, () =>
			{
				Vector3 lineWorld = moon.Pivot.rotation * moon.CutLine;
				Vector3 from = center - lineWorld * radius * 2.2f;
				Slash(run, from, center + lineWorld * radius * 0.6f, slashCount, 120f, 0.12f);
			});
			Light section = WorldLight(stage, "SectionLight", LightType.Point, SectionLightColor, sectionRange);
			run.Track(CutTime, VanishStart + vanishSeconds - CutTime, _ =>
			{
				float now = run.Now;
				// 단면 아래 (달과 땅 사이) 에서 지면을 흰색으로
				section.transform.position = Vector3.Lerp(center, stage.Target, 0.55f) + Vector3.up * 0.6f;
				float fade = 1f - UltimateFxRun.Span(now, VanishStart, VanishStart + vanishSeconds);
				section.intensity = sectionIntensity * Mathf.Max(Spike(now, CutTime, 0.08f, sectionHalfLife), 0.18f) * fade;
			}, () => section.enabled = false);

			run.Track(CutTime, VanishStart + vanishSeconds - CutTime, _ =>
			{
				float now = run.Now;
				float glow = Mathf.Max(Spike(now, CutTime, 0.05f, 0.5f), 0.35f) * (1f + 0.1f * Mathf.Sin(now * 23f));
				moon.Cap.SetColor(EMISSION_ID, CapGlow * glow);
			});
			run.Track(CutTime, splitSeconds, progress =>
			{
				float ease = UltimateFxRun.EaseOut(progress);
				moon.Upper.localPosition = moon.CutLine * slideDistance * ease + moon.CutNormal * splitDistance * ease;
				moon.Lower.localPosition = -moon.CutLine * slideDistance * ease - moon.CutNormal * splitDistance * ease;
				moon.Upper.localRotation = Quaternion.Euler(0f, 0f, splitRoll * ease);
				moon.Lower.localRotation = Quaternion.Euler(0f, 0f, -splitRoll * ease);
			});
			run.Track(CutTime + splitSeconds, driftSeconds + vanishSeconds, progress =>
			{
				float drift = 1f + 0.25f * progress;
				moon.Upper.localPosition = (moon.CutLine * slideDistance + moon.CutNormal * splitDistance) * drift;
				moon.Lower.localPosition = -(moon.CutLine * slideDistance + moon.CutNormal * splitDistance) * drift;
			});
			run.At(CutTime, () =>
			{
				run.SparkBurst(center, cutBurstCount, new Vector2(8f, 22f), new Vector2(0.25f, 0.6f), new Vector2(0.12f, 0.3f), GrainColor, Color.white, 0.6f);
				run.ShakeSeries(run.Now, shakeCount, 0.08f, shakeStrength, 0.7f);
				run.React(stage.Target, windSpeed, 10f, false);
			});
		}

		// 단면 빛줄기: 갈라진 틈 아래로 겹친 원기둥 셋 (가운데가 밝게) 과 빛줄기 안 먼지, 떨어지는 빛 알갱이
		private void ComposeShaft(Stage stage, MoonBody moon, Vector3 center, float radius)
		{
			UltimateFxRun run = stage.Run;
			Mesh cylinder = Keep(run, UltimateMeshFactory.OpenCylinder(32, 1f, 0.25f));
			Vector3 top = center - Vector3.up * radius * 0.15f;
			Vector3 bottom = new(center.x, UltimateFxStage.GroundHeight(center), center.z);
			float length = Mathf.Max(0.5f, top.y - bottom.y);
			float[] radii = { 1f, 0.68f, 0.38f };
			Material[] materials = new Material[radii.Length];
			Transform[] shafts = new Transform[radii.Length];
			for (int i = 0; i < radii.Length; i++)
			{
				materials[i] = run.Additive(UltimateFxTextures.White, Color.clear);
				materials[i].renderQueue = 3020 + i;
				GameObject shaft = UltimateFxStage.CreateMeshObject("MoonShaft", run.Root, cylinder, materials[i]);
				shaft.transform.position = top;
				shaft.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
				shaft.transform.localScale = new Vector3(shaftRadius * 2f * radii[i], 0f, shaftRadius * 2f * radii[i]);
				shafts[i] = shaft.transform;
			}
			run.Track(CutTime, VanishStart + vanishSeconds - CutTime, _ =>
			{
				float now = run.Now;
				float grow = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, CutTime, CutTime + shaftGrowSeconds));
				float strength = Mathf.Max(Spike(now, CutTime, shaftGrowSeconds, 0.9f), 0.3f) * (1f - UltimateFxRun.Span(now, VanishStart, VanishStart + vanishSeconds));
				for (int i = 0; i < shafts.Length; i++)
				{
					Vector3 scale = shafts[i].localScale;
					shafts[i].localScale = new Vector3(scale.x, length * grow, scale.z);
					materials[i].SetColor("_Color", shaftColor * strength * (0.5f + 0.25f * i));
				}
			});

			ParticleSystem dust = run.Particles("ShaftDust", LitParticle(run, UltimateFxTextures.Soft, 0f), shaftDustCount + 8, -0.01f);
			UltimateFxStage.SetLifetimeLook(dust, UltimateFxStage.FadeGradient(Color.white, 0.2f, 0.6f), null);
			ParticleSystem grains = Grains(run, grainCount);
			for (int i = 0; i < shaftDustCount; i++)
			{
				float time = CutTime + i / (float)Mathf.Max(1, shaftDustCount) * 1.2f;
				run.At(time, () =>
				{
					Vector3 position = Vector3.Lerp(bottom, top, Random.value) + UltimateFxStage.RandomRingPoint(0f, shaftRadius * 0.9f);
					UltimateFxStage.Emit(dust, position, Vector3.down * Random.Range(0.1f, 0.3f), Random.Range(0.06f, 0.12f), Random.Range(1.5f, 2.4f), new Color(0.95f, 0.97f, 1f, 0.9f));
				});
			}
			for (int i = 0; i < grainCount; i++)
			{
				float time = CutTime + i / (float)Mathf.Max(1, grainCount) * 1.4f;
				run.At(time, () =>
				{
					Vector3 lineWorld = moon.Pivot.rotation * moon.CutLine;
					Vector3 origin = center + lineWorld * radius * Random.Range(-0.85f, 0.85f) - Vector3.up * radius * 0.1f;
					UltimateFxStage.Emit(grains, origin, Vector3.down * Random.Range(1.5f, 4f) + Random.insideUnitSphere * 0.4f, Random.Range(0.05f, 0.13f), Random.Range(0.8f, 1.4f), GrainColor);
				});
			}
			GroundGlow(run, bottom, shaftRadius * 5f, new Color(0.7f, 0.8f, 1f, 1f), CutTime, 0.1f, 0.9f, VanishStart + vanishSeconds - CutTime);
		}

		// 달 조각: 단면 테두리에서 작은 조각이 흩어지고 큰 조각 셋이 표적 둘레 땅에 박혀 구덩이와 연청 잔광
		private void ComposeFragments(Stage stage, MoonBody moon, Vector3 center, float radius)
		{
			UltimateFxRun run = stage.Run;
			ParticleSystem fragments = Fragments(run, smallFragmentCount, out Mesh _);
			run.At(CutTime, () =>
			{
				Vector3 lineWorld = moon.Pivot.rotation * moon.CutLine;
				for (int i = 0; i < smallFragmentCount; i++)
				{
					Vector3 origin = center + lineWorld * radius * Random.Range(-0.95f, 0.95f);
					EmitMesh(fragments, origin, (Random.onUnitSphere + Vector3.down * 0.3f) * Random.Range(1f, 3f), Random.Range(0.2f, 0.45f), Random.Range(2f, 2.6f), Color.white, Random.insideUnitSphere * 180f, Random.insideUnitSphere * 300f);
				}
			});

			Material material = RockMaterial(run);
			Texture2D crater = CraterTexture(run, 0);
			for (int i = 0; i < bigFragmentCount; i++)
			{
				int index = i;
				GameObject fragment = UltimateFxStage.CreateMeshObject("MoonChunk", run.Root, RockMesh(run, index), material);
				Solid(fragment, true);
				float size = Random.Range(bigFragmentSize.x, bigFragmentSize.y);
				fragment.transform.localScale = new Vector3(size, size * 0.8f, size);
				fragment.SetActive(false);
				float angle = (index / (float)Mathf.Max(1, bigFragmentCount) + Random.Range(-0.08f, 0.08f)) * Mathf.PI * 2f;
				Vector3 landing = stage.Target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * bigFragmentSpread * Random.Range(0.6f, 1f);
				landing.y = UltimateFxStage.GroundHeight(landing);
				Vector3 planted = landing + Vector3.up * size * 0.15f;
				float start = CutTime + 0.08f + index * 0.12f;
				float land = start + bigFragmentFallSeconds;
				Vector3 from = center;
				Vector3 spin = Random.onUnitSphere * 420f;
				Quaternion startRotation = Random.rotation;
				run.At(start, () =>
				{
					Vector3 lineWorld = moon.Pivot.rotation * moon.CutLine;
					from = center + lineWorld * radius * Random.Range(-0.7f, 0.7f) - Vector3.up * radius * 0.2f;
					fragment.SetActive(true);
				});
				run.Track(start, bigFragmentFallSeconds, progress =>
				{
					Vector3 position = Vector3.Lerp(from, planted, progress);
					position.y = Mathf.Lerp(from.y, planted.y, UltimateFxRun.EaseIn(progress));
					fragment.transform.position = position;
					fragment.transform.rotation = startRotation * Quaternion.Euler(spin * progress * bigFragmentFallSeconds);
				});
				run.At(land, () =>
				{
					run.Shake(shakeStrength * 0.4f);
					Puff(run, landing, 14, 1.4f);
					run.SparkBurst(landing + Vector3.up * 0.2f, 14, new Vector2(4f, 9f), new Vector2(0.2f, 0.4f), new Vector2(0.06f, 0.14f), GrainColor, Color.white, 1.4f);
				});
				Crater(run, crater, landing, craterSize * size, land, DurationSeconds, TailSeconds * 0.8f);
				GroundGlow(run, landing, craterSize * size * 1.4f, new Color(0.45f, 0.65f, 1f, 1f), land, 0.05f, 0.5f, 1.4f);
				run.Track(VanishStart, vanishSeconds, progress => fragment.transform.position = planted - Vector3.up * size * 0.7f * UltimateFxRun.EaseIn(progress));
			}
		}

		private void ComposeGrade(Stage stage)
		{
			float slash = SlashTime;
			float mono = SlashTime + monoDelay;
			float cut = CutTime;
			float restore = RestoreStart;
			stage.BloomTint = MoonLightColor;
			stage.SunScale = now => Mathf.Lerp(1f, sunDim, Plateau(now, 0f, riseSeconds, restore, restoreSeconds));
			stage.AmbientScale = now => Mathf.Lerp(1f, ambientDim, Plateau(now, 0f, riseSeconds, restore, restoreSeconds));
			stage.Filter = now => Color.Lerp(Color.white, paleFilter, Plateau(now, 0f, riseSeconds, restore, restoreSeconds));
			stage.Vignette = now => vignette * Plateau(now, 0f, riseSeconds, restore, restoreSeconds);
			// 번쩍 1장 (검기), 흑백 2장, 2차 번쩍 (m04 #107, #111~112, #125)
			stage.Exposure = now => flashExposure * Window(now, slash, FRAME) + secondFlashExposure * Spike(now, cut + FRAME * 2f, 0.02f, 0.06f);
			stage.Saturation = now => -100f * Window(now, mono, FRAME * 2f);
			stage.Contrast = now => monoContrast * Window(now, mono, FRAME * 2f);
			stage.Chromatic = now => 0.3f * Spike(now, cut, 0.02f, 0.15f);
			stage.Bloom = now => Mathf.Max(bloomRise * Plateau(now, 0f, riseSeconds, restore, restoreSeconds), bloomCut * Spike(now, cut, 0.06f, 0.6f) * (1f - UltimateFxRun.Span(now, restore, restore + restoreSeconds)));
			stage.Run.At(0f, () =>
			{
				System.Action release = stage.Run.Zoom(pullBack, 0f, riseSeconds * 0.9f, float.PositiveInfinity, 1.2f);
				stage.Run.At(VanishStart, () => release());
			});
		}
	}
}
