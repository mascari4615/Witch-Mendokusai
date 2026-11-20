using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 달 가르기 약한 판 (일반 스킬용). 표적 바로 위에 작은 달이 나타나 사선으로 갈리고, 두 반쪽이 표적 양옆 땅에 떨어져 조각으로 부서짐
	/// - 1.2초 (등장 0.3, 절단 0.15, 낙하 0.5, 부서짐 0.25). 광원 1개 (단면 점광), 장면 빛 변경 없음, 후처리는 빛 번짐만
	/// - 공통 부품은 MoonWorldFx. 강한 판은 MoonHeavyUltimateFx (별개 에셋)
	/// - 재생: 개발 창 fx ultimate MoonLight
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_MoonLight", menuName = "WM/CombatFx/Ultimate/" + nameof(MoonLightUltimateFx))]
	public sealed class MoonLightUltimateFx : MoonWorldFx
	{
		[Header("달")]
		[SerializeField, Min(0.2f), Tooltip("설계 지름 1.6")] private float diameter = 2.4f;
		[SerializeField, Min(0.5f)] private float hoverHeight = 2.2f;
		[SerializeField, Min(0f), Tooltip("두 반쪽이 떨어지는 옆 거리")] private float fallSpread = 1.1f;
		[SerializeField, Tooltip("떨어지며 도는 각도")] private float fallSpin = 160f;

		[Header("시간 (초)")]
		[SerializeField, Min(0.05f)] private float appearSeconds = 0.3f;
		[SerializeField, Min(0.02f)] private float cutSeconds = 0.15f;
		[SerializeField, Min(0.1f)] private float fallSeconds = 0.5f;
		[SerializeField, Min(0.05f)] private float shatterSeconds = 0.25f;

		[Header("빛과 입자")]
		[SerializeField, Min(0f)] private float sectionIntensity = 6f;
		[SerializeField, Min(1f)] private float sectionRange = 6f;
		[SerializeField, Min(0.05f)] private float sectionHalfLife = 0.25f;
		[SerializeField, Min(0f)] private float bloom = 0.8f;
		[SerializeField, Range(0f, 1f), Tooltip("단면 발광 배율. 작은 달이라 강한 판 값 그대로면 흰 덩어리로 읽힘")] private float capGlowScale = 0.4f;
		[SerializeField, Min(0)] private int slashCount = 18;
		[SerializeField, Min(0)] private int grainCount = 30;
		[SerializeField, Min(0)] private int fragmentsPerHalf = 8;
		[SerializeField, Min(0f)] private float fragmentSpeed = 3.5f;
		[SerializeField, Min(0f)] private float shakeStrength = 0.45f;
		[SerializeField, Min(0.2f)] private float craterLinger = 0.9f;

		private float CutTime => appearSeconds;
		private float FallStart => CutTime + cutSeconds;
		private float LandTime => FallStart + fallSeconds;

		public override float DurationSeconds => LandTime + shatterSeconds;

		protected override void ComposeWorld(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			MoonBody moon = CreateMoon(stage);
			Vector3 center = stage.Target + Vector3.up * hoverHeight;
			float radius = diameter * 0.5f;
			moon.Pivot.position = center;
			moon.Pivot.localScale = Vector3.zero;
			SetDissolve(moon, 1f, -1f);

			// 등장: 넘쳤다 줄어드는 크기와 아래부터 녹아 나타남
			run.At(0f, () => moon.Pivot.gameObject.SetActive(true));
			run.Track(0f, appearSeconds, progress =>
			{
				moon.Pivot.localScale = Vector3.one * radius * OverShoot(progress, 0.12f);
				moon.Pivot.rotation = Quaternion.LookRotation(-stage.ToCamera) * Quaternion.Euler(0f, 40f * (1f - progress), 0f);
				SetDissolve(moon, 1f - UltimateFxRun.EaseOut(progress), -1f);
			});

			// 절단: 검기가 사선으로 지나가고 단면이 빛남
			run.At(CutTime - 0.04f, () => Slash(run, run.Caster + Vector3.up * 0.8f, center, slashCount, 70f, 0.08f));
			Light section = WorldLight(stage, "SectionLight", LightType.Point, SectionLightColor, sectionRange);
			section.transform.position = center - Vector3.up * 0.4f;
			run.Track(CutTime, DurationSeconds - CutTime, _ =>
			{
				section.intensity = sectionIntensity * (Spike(run.Now, CutTime, 0.04f, sectionHalfLife) + 0.7f * Spike(run.Now, LandTime, 0.03f, 0.12f));
				section.transform.position = Vector3.Lerp(center - Vector3.up * 0.4f, stage.Target + Vector3.up * 0.5f, UltimateFxRun.Span(run.Now, FallStart, LandTime));
			}, () => section.enabled = false);
			run.Track(CutTime, DurationSeconds - CutTime, _ => moon.Cap.SetColor(EMISSION_ID, CapGlow * capGlowScale * (0.3f + 0.7f * Spike(run.Now, CutTime, 0.03f, 0.2f))));
			run.At(CutTime, () => run.Shake(shakeStrength * 0.6f));

			// 두 반쪽이 단면 양쪽으로 벌어지며 표적 양옆 땅에 떨어짐
			Vector3 worldNormal = moon.Pivot.rotation * moon.CutNormal;
			Vector3 side = stage.Run.ScreenRight;
			Vector3 upperLanding = stage.Target + side * fallSpread + Vector3.up * radius * 0.3f;
			Vector3 lowerLanding = stage.Target - side * fallSpread + Vector3.up * radius * 0.3f;
			upperLanding.y = UltimateFxStage.GroundHeight(upperLanding) + radius * 0.3f;
			lowerLanding.y = UltimateFxStage.GroundHeight(lowerLanding) + radius * 0.3f;
			run.Track(CutTime, cutSeconds, progress =>
			{
				float split = UltimateFxRun.EaseOut(progress) * 0.12f;
				moon.Upper.localPosition = moon.CutNormal * split;
				moon.Lower.localPosition = -moon.CutNormal * split;
			});
			Vector3 upperStart = Vector3.zero;
			Vector3 lowerStart = Vector3.zero;
			run.At(FallStart, () =>
			{
				upperStart = moon.Upper.position;
				lowerStart = moon.Lower.position;
				moon.Upper.SetParent(run.Root, true);
				moon.Lower.SetParent(run.Root, true);
			});
			Quaternion upperRotation = Quaternion.identity;
			Quaternion lowerRotation = Quaternion.identity;
			run.At(FallStart, () =>
			{
				upperRotation = moon.Upper.rotation;
				lowerRotation = moon.Lower.rotation;
			});
			run.Track(FallStart, fallSeconds, progress =>
			{
				// 가로는 고르게, 세로는 가속 (떨어짐). 단면 바깥으로 살짝 벌어짐
				float drop = UltimateFxRun.EaseIn(progress);
				float bulge = 0.25f * Mathf.Sin(progress * Mathf.PI);
				moon.Upper.position = Fall(upperStart, upperLanding, progress, drop) + worldNormal * bulge;
				moon.Lower.position = Fall(lowerStart, lowerLanding, progress, drop) - worldNormal * bulge;
				moon.Upper.rotation = Quaternion.AngleAxis(-fallSpin * progress, stage.ToCamera) * upperRotation;
				moon.Lower.rotation = Quaternion.AngleAxis(fallSpin * progress, stage.ToCamera) * lowerRotation;
			});

			ParticleSystem grains = Grains(run, grainCount);
			run.Track(CutTime, fallSeconds, _ =>
			{
				if (Random.value < 0.5f)
					return;
				Vector3 origin = Vector3.Lerp(moon.Upper.position, moon.Lower.position, 0.5f);
				UltimateFxStage.Emit(grains, origin + Random.insideUnitSphere * radius * 0.5f, Vector3.down * Random.Range(0.5f, 1.5f), Random.Range(0.05f, 0.11f), Random.Range(0.4f, 0.7f), GrainColor);
			});

			// 부서짐: 반쪽이 사라지며 달 조각이 튀고 먼지와 구덩이
			ParticleSystem fragments = Fragments(run, fragmentsPerHalf * 2, out Mesh _);
			Texture2D crater = CraterTexture(run, 0);
			run.At(LandTime, () =>
			{
				run.Shake(shakeStrength);
				foreach (Vector3 landing in new[] { upperLanding, lowerLanding })
				{
					Vector3 ground = new(landing.x, UltimateFxStage.GroundHeight(landing), landing.z);
					for (int i = 0; i < fragmentsPerHalf; i++)
						EmitMesh(fragments, landing + Random.insideUnitSphere * radius * 0.4f, (Random.onUnitSphere + Vector3.up * 1.2f) * fragmentSpeed * Random.Range(0.4f, 1f), radius * Random.Range(0.25f, 0.5f), Random.Range(0.8f, 1.1f), Color.white, Random.insideUnitSphere * 180f, Random.insideUnitSphere * 500f);
					Puff(run, ground, 8, 0.8f);
					Crater(run, crater, ground, radius * 1.8f, LandTime, DurationSeconds + craterLinger, 0.4f);
				}
			});
			run.Track(LandTime, shatterSeconds, progress => SetDissolve(moon, UltimateFxRun.EaseOut(progress), 1f), () => run.Destroy(moon.Pivot.gameObject));
			run.OnRelease(() =>
			{
				run.Destroy(moon.Upper != null ? moon.Upper.gameObject : null);
				run.Destroy(moon.Lower != null ? moon.Lower.gameObject : null);
			});

			stage.BloomTint = MoonLightColor;
			stage.Bloom = now => bloom * Mathf.Clamp01(now / 0.15f) * (1f + 0.8f * Spike(now, CutTime, 0.03f, 0.2f)) * (1f - UltimateFxRun.Span(now, LandTime, DurationSeconds + 0.4f));
		}

		private static Vector3 Fall(Vector3 from, Vector3 to, float progress, float drop)
		{
			Vector3 position = Vector3.Lerp(from, to, progress);
			position.y = Mathf.Lerp(from.y, to.y, drop);
			return position;
		}
	}
}
