using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 번개 궁극기 (옛 번개, 불, 얼음 세 벌을 흡수한 자리). 발밑 노란 고리 예고 -> 한 장에 튀는 섬광과 함께 월드 정지
	/// -> 정지 동안 대각 검은 띠 둘과 노란 선 (컷인 틀), 폭풍처럼 어두운 배경 -> 시전자 둘레에 번개 기둥이 차례로 내리꽂힘 -> 바닥 고리가 퍼지고 오래 남음
	/// - 실루엣: 하늘에서 바닥까지 세로로 꺾인 선 여러 개와 바닥 고리. 배경 장악: 정지 동안 탈색과 어둡게, 번개마다 노란 섬광
	/// - 시간 구조는 참고 영상 30fps 실측 (예고 0.53초, 정지 1.47초, 고리 반지름 화면폭 17% -> 25% 약 0.5초, 고리 잔류 3초 이상)
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_10_Thunder", menuName = "WM/CombatFx/Ultimate/" + nameof(ThunderUltimateFx))]
	public sealed class ThunderUltimateFx : UltimateFx
	{
		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color boltColor = new(1.9f, 1.8f, 1.1f);
		[SerializeField, ColorUsage(false, true)] private Color ringColor = new(1.6f, 1.3f, 0.3f);
		[SerializeField] private Color bandColor = new(0.02f, 0.02f, 0.03f, 1f);
		[SerializeField] private Color lineColor = new(1f, 0.85f, 0.2f, 1f);
		[SerializeField] private Color stormTint = new(0.55f, 0.55f, 0.7f, 1f);

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("실측 0.53초")] private float telegraphSeconds = 0.53f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.47초")] private float freezeSeconds = 1.47f;
		[SerializeField, Range(0.001f, 1f), Tooltip("정지 동안 게임 시간 배율")] private float freezeScale = 0.02f;
		[SerializeField, Min(0.05f), Tooltip("번개 기둥이 다 꽂힐 때까지")] private float strikeSpanSeconds = 0.7f;
		[SerializeField, Min(0.05f), Tooltip("번개 한 줄기가 보이는 시간")] private float boltSeconds = 0.26f;
		[SerializeField, Min(0.1f), Tooltip("실측 약 0.5초")] private float ringSeconds = 0.5f;
		[SerializeField, Min(0.5f), Tooltip("실측 3초 이상")] private float lingerSeconds = 3f;

		[Header("크기 (월드 단위)")]
		[SerializeField, Min(1)] private int boltCount = 7;
		[SerializeField, Min(1f)] private float strikeRadius = 3.5f;
		[SerializeField, Min(2f), Tooltip("번개 높이. 캐릭터 키의 약 10배 이상")] private float boltHeight = 16f;
		[SerializeField, Min(0.05f)] private float boltWidth = 1.3f;
		[SerializeField, Min(1f), Tooltip("고리 처음 반지름 (화면폭 약 17%)")] private float ringStartRadius = 4.5f;
		[SerializeField, Min(1f), Tooltip("고리 끝 반지름 (화면폭 약 25%)")] private float ringEndRadius = 6.5f;

		[Header("화면과 카메라")]
		[SerializeField, Range(0f, 1f)] private float stormDarken = 0.45f;
		[SerializeField, Range(0f, 1f)] private float stormDesaturate = 0.6f;
		[SerializeField, Range(0f, 1f)] private float flashPeak = 0.75f;
		[SerializeField, Range(0f, 30f), Tooltip("대각 띠 각 (도)")] private float bandAngle = 18f;
		[SerializeField, Min(0f)] private float strikeShake = 1.2f;
		[SerializeField, Range(0f, 1f), Tooltip("번개 한 줄기마다 섬광")] private float strikeFlash = 0.15f;

		private float FreezeStart => telegraphSeconds;
		private float StrikeStart => FreezeStart + freezeSeconds;
		private float LingerStart => StrikeStart + strikeSpanSeconds;

		public override float DurationSeconds => LingerStart + lingerSeconds;

		public override void Compose(UltimateFxRun run)
		{
			Vector3 center = run.Caster;

			// 예고: 발밑 노란 고리와 룬
			Material runeMaterial = run.Additive(UltimateFxTextures.Rune, Color.clear);
			GameObject rune = run.Plate("Rune", runeMaterial, center + Vector3.up * 0.05f, ringStartRadius * 2.2f, 2);
			run.Track(0f, StrikeStart + ringSeconds, progress =>
			{
				float appear = UltimateFxRun.EaseOut(run.Now / telegraphSeconds);
				float fade = 1f - UltimateFxRun.Span(run.Now, StrikeStart, StrikeStart + ringSeconds);
				runeMaterial.SetColor("_Color", new Color(ringColor.r, ringColor.g, ringColor.b, appear * fade * 0.8f));
				rune.transform.rotation = Quaternion.Euler(0f, run.Now * 20f, 0f);
			}, () => run.Destroy(rune));

			// 정지: 한 장에 튀는 섬광, 월드 정지, 대각 띠 둘과 노란 선, 폭풍 배경
			run.At(FreezeStart, () => run.HitStop(freezeSeconds, freezeScale));
			run.Track(FreezeStart, 0.4f, progress => run.Flash(flashPeak * Mathf.Pow(1f - progress, 3f), Color.Lerp(Color.white, lineColor, 0.4f)), () => run.Flash(0f, Color.white));
			run.Track(FreezeStart, freezeSeconds, progress =>
			{
				float slide = UltimateFxRun.EaseOut(progress * 5f);
				float leave = UltimateFxRun.EaseIn(UltimateFxRun.Span(progress, 0.85f, 1f));
				float thickness = 0.24f * slide * (1f - leave);
				run.Layer.SetBand(0, 1f, bandColor, bandAngle, 0.36f, thickness, false);
				run.Layer.SetBand(1, 1f, bandColor, bandAngle, -0.36f, thickness, false);
				run.Layer.SetBand(2, 1f - leave, lineColor, bandAngle, 0.36f - thickness * 0.5f, 0.008f * slide, true);
				float storm = Mathf.Min(UltimateFxRun.EaseOut(progress * 4f), 1f);
				run.Grade.Darken = stormDarken * storm;
				run.Grade.Desaturate = stormDesaturate * storm;
				run.Grade.SetTint(0.3f * storm, stormTint);
			}, () =>
			{
				for (int i = 0; i < UltimateScreenLayer.BAND_COUNT; i++)
					run.Layer.SetBand(i, 0f, bandColor, bandAngle, 0f, 0f, false);
			});

			// 번개 기둥: 둘레를 돌며 차례로. 꽂힐 때마다 노란 섬광과 흔들림
			Material boltMaterial = run.Additive(UltimateFxTextures.Line, boltColor);
			boltMaterial.renderQueue = 3020;
			for (int i = 0; i < boltCount; i++)
			{
				float angle = i * Mathf.PI * 2f / boltCount + 0.4f;
				Vector3 ground = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * strikeRadius * (i == 0 ? 0f : 1f);
				ground.y = UltimateFxStage.GroundHeight(ground);
				float strike = StrikeStart + strikeSpanSeconds * i / boltCount;
				LineRenderer bolt = null;
				run.Track(strike, boltSeconds, progress =>
				{
					if (bolt == null)
					{
						bolt = CreateBolt(run, boltMaterial);
						run.Shake(strikeShake);
					}
					if (Mathf.FloorToInt(run.Now * 40f) % 2 == 0)
						Jag(bolt, ground + Vector3.up * boltHeight, ground);
					bolt.widthMultiplier = boltWidth * (1f - progress * 0.7f);
					run.Flash(strikeFlash * (1f - progress), Color.Lerp(Color.white, lineColor, 0.4f));
				}, () =>
				{
					run.Destroy(bolt.gameObject);
					run.Flash(0f, Color.white);
				});
				run.At(strike, () => run.SparkBurst(ground + Vector3.up * 0.3f, 24, new Vector2(6f, 14f), new Vector2(0.3f, 0.7f), new Vector2(0.12f, 0.3f), boltColor, ringColor, 0.8f));
			}
			run.Track(StrikeStart, strikeSpanSeconds + 0.6f, progress =>
			{
				float clear = UltimateFxRun.Span(progress, 0.5f, 1f);
				run.Grade.Darken = stormDarken * (1f - clear);
				run.Grade.Desaturate = stormDesaturate * (1f - clear);
				run.Grade.SetTint(0.3f * (1f - clear), stormTint);
			}, () =>
			{
				run.Grade.Darken = 0f;
				run.Grade.Desaturate = 0f;
				run.Grade.SetTint(0f, stormTint);
			});

			// 바닥 고리: 퍼진 뒤 오래 남음 (실측에 균열 없음, 고리 잔류만). 충격파
			run.Shockwave(StrikeStart, center, ringEndRadius, ringSeconds * 1.6f, 0.05f, 0.14f, ringColor);
			Material ringMaterial = run.Additive(UltimateFxTextures.ThinRing, Color.clear);
			GameObject ring = null;
			run.Track(StrikeStart, LingerStart + lingerSeconds - StrikeStart, progress =>
			{
				if (ring == null)
					ring = run.Plate("LingerRing", ringMaterial, center + Vector3.up * 0.07f, 1f, 8);
				float seconds = run.Now - StrikeStart;
				float radius = Mathf.Lerp(ringStartRadius, ringEndRadius, UltimateFxRun.EaseOut(seconds / ringSeconds));
				float size = radius / UltimateFxTextures.RING_RADIUS * 2f;
				ring.transform.localScale = new Vector3(size, 1f, size);
				float fade = 1f - UltimateFxRun.Span(progress, 0.7f, 1f);
				float shimmer = 0.85f + 0.15f * Mathf.Sin(run.Now * 18f);
				ringMaterial.SetColor("_Color", new Color(ringColor.r, ringColor.g, ringColor.b, fade * shimmer));
			}, () => run.Destroy(ring));
		}

		private static LineRenderer CreateBolt(UltimateFxRun run, Material material)
		{
			GameObject boltObject = new("Bolt");
			boltObject.transform.SetParent(run.Root, false);
			LineRenderer line = boltObject.AddComponent<LineRenderer>();
			line.sharedMaterial = material;
			line.useWorldSpace = true;
			line.alignment = LineAlignment.View;
			line.numCornerVertices = 2;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
			return line;
		}

		private static void Jag(LineRenderer bolt, Vector3 from, Vector3 to)
		{
			const int POINTS = 16;
			bolt.positionCount = POINTS;
			for (int i = 0; i < POINTS; i++)
			{
				float along = i / (POINTS - 1f);
				Vector3 wiggle = (i == 0 || i == POINTS - 1) ? Vector3.zero : new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.7f;
				bolt.SetPosition(i, Vector3.Lerp(from, to, along) + wiggle);
			}
		}
	}
}
