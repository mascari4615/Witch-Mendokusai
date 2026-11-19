using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 블랙홀 궁극기. 화면을 가르는 청록 빛줄 -> 띠로 열림 -> 흑백 반전 두 장과 선화 두 장 -> 별 혜성이 꼬리를 끌며 표적 둘레에 원을 그림
	/// -> 마름모 마법진 -> 위에서 블랙홀이 내려옴 -> 백색 (상한) 과 청록 구 폭발 -> 유리 파편으로 흩어짐
	/// - 실루엣: 검은 구와 가로 원반 고리, 바닥의 마름모. 배경 장악: 차갑게 탈색하고 청록 물들임, 반전 두 장
	/// - 얼굴 컷인은 뺌 (생존 게임은 수십 번 발동, 원작도 건너뛰기 요청을 받음)
	/// - 시간 구조는 참고 영상 30fps 실측 (빛줄 0.3초, 띠 0.3초, 반전 4장, 원 그리기 1.3초, 마법진 0.5초, 낙하 0.6초, 백색 감쇠 0.4초, 파편 0.9초)
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_09_BlackHole", menuName = "WM/CombatFx/Ultimate/" + nameof(BlackHoleUltimateFx))]
	public sealed class BlackHoleUltimateFx : UltimateFx
	{
		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color cyan = new(0.4f, 1.6f, 1.7f);
		[SerializeField, ColorUsage(false, true)] private Color cyanCore = new(1.6f, 2.2f, 2.2f);
		[SerializeField] private Color coldTint = new(0.4f, 0.75f, 0.85f, 1f);

		[Header("시간 (초)")]
		[SerializeField, Min(0.05f), Tooltip("실측 0.3초")] private float slashSeconds = 0.3f;
		[SerializeField, Min(0.05f), Tooltip("실측 0.3초")] private float bandSeconds = 0.3f;
		[SerializeField, Min(0.01f), Tooltip("실측 2장")] private float invertSeconds = 0.067f;
		[SerializeField, Min(0.01f), Tooltip("실측 2장")] private float lineArtSeconds = 0.067f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.3초")] private float drawSeconds = 1.3f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.5초")] private float circleSeconds = 0.5f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.6초")] private float fallSeconds = 0.6f;
		[SerializeField, Min(0.1f), Tooltip("실측 12장 감쇠")] private float burstSeconds = 0.4f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.9초")] private float shardSeconds = 0.9f;

		[Header("크기와 배치 (월드 단위)")]
		[SerializeField, Tooltip("표적 위치 (화면 가로, 화면 위쪽)")] private Vector2 targetOffset = new(-3f, 0.5f);
		[SerializeField, Min(1f), Tooltip("마법진 반지름")] private float circleRadius = 4.5f;
		[SerializeField, Min(1f), Tooltip("블랙홀 지름. 캐릭터 키의 약 6배")] private float holeSize = 5.5f;
		[SerializeField, Min(1f)] private float holeHeight = 1.8f;
		[SerializeField, Min(2f)] private float fallHeight = 8f;
		[SerializeField, Min(1)] private int cometCount = 10;
		[SerializeField, Min(0)] private int shardCount = 70;
		[SerializeField, Min(1f)] private float burstRadius = 9f;
		[SerializeField, Range(-80f, 80f)] private float slashAngle = 22f;

		[Header("화면과 카메라")]
		[SerializeField, Range(0f, 1f)] private float desaturate = 0.55f;
		[SerializeField, Range(0f, 1f)] private float tintAmount = 0.35f;
		[SerializeField, Range(0f, 1f)] private float bandAlpha = 0.75f;
		[SerializeField, Range(0f, 0.5f)] private float bandThickness = 0.2f;
		[SerializeField, Range(0f, 1f)] private float burstFlash = 0.7f;
		[SerializeField, Min(0f)] private float burstShake = 2.2f;

		private float BandStart => slashSeconds;
		private float InvertStart => BandStart + bandSeconds;
		private float DrawStart => InvertStart + invertSeconds + lineArtSeconds + 0.1f;
		private float CircleStart => DrawStart + drawSeconds;
		private float FallStart => CircleStart + circleSeconds;
		private float BurstStart => FallStart + fallSeconds + 0.1f;
		private float ShardStart => BurstStart + burstSeconds * 0.3f;

		public override float DurationSeconds => ShardStart + shardSeconds + 0.4f;

		public override void Compose(UltimateFxRun run)
		{
			Vector3 target = run.Center + run.ScreenRight * targetOffset.x + run.ScreenForward * targetOffset.y;
			target.y = UltimateFxStage.GroundHeight(target);

			// 빛줄과 띠: 화면을 사선으로 가르는 청록 빛이 넓어짐, 가운데 반짝
			Material glintMaterial = run.Additive(UltimateFxTextures.CrossGlint, Color.clear);
			GameObject glint = run.Billboard("SlashGlint", glintMaterial, run.Caster + Vector3.up * 2f, 5f, 5f, false, true, 12);
			run.Track(0f, slashSeconds + bandSeconds, progress =>
			{
				float seconds = progress * (slashSeconds + bandSeconds);
				float open = UltimateFxRun.EaseIn(UltimateFxRun.Span(seconds, slashSeconds, slashSeconds + bandSeconds));
				run.Layer.SetBand(0, bandAlpha, cyan, slashAngle, 0f, Mathf.Lerp(0.008f, bandThickness, open), true);
				run.Layer.SetBand(1, open * bandAlpha, Color.white, slashAngle, 0f, bandThickness * 0.35f * open, true);
				glintMaterial.SetColor("_Color", new Color(cyanCore.r, cyanCore.g, cyanCore.b, UltimateFxRun.Bell(progress, 0.5f)));
				glint.GetComponent<UltimateBillboard>().Roll = seconds * 90f;
			}, () =>
			{
				run.Layer.SetBand(0, 0f, cyan, slashAngle, 0f, 0f, true);
				run.Layer.SetBand(1, 0f, cyan, slashAngle, 0f, 0f, true);
				run.Destroy(glint);
			});

			// 반전 두 장, 선화 두 장, 이어서 차가운 탈색과 청록 물들임
			run.Grade.SetToneColors(Color.white, Color.black, 0.3f);
			run.Grade.SetLineColors(new Color(0.15f, 0.15f, 0.18f, 1f), new Color(0.85f, 0.85f, 0.88f, 1f));
			run.Track(InvertStart, invertSeconds, _ => run.Grade.TwoTone = 1f, () => run.Grade.TwoTone = 0f);
			run.Track(InvertStart + invertSeconds, lineArtSeconds, _ => run.Grade.LineArt = 1f, () => run.Grade.LineArt = 0f);
			run.Track(InvertStart + invertSeconds + lineArtSeconds, 0.3f, progress => Cold(run, progress));

			// 원 그리기: 혜성이 시전자에서 날아가 표적 둘레를 돌며 원을 그림
			List<TrailRenderer> comets = new();
			Material cometMaterial = run.Additive(UltimateFxTextures.Line, cyan);
			cometMaterial.renderQueue = 3016;
			run.At(DrawStart, () =>
			{
				for (int i = 0; i < cometCount; i++)
					comets.Add(CreateComet(run, cometMaterial, run.Caster + Vector3.up * 1.5f));
			});
			run.Track(DrawStart, drawSeconds, progress =>
			{
				for (int i = 0; i < comets.Count; i++)
				{
					float startAngle = i * Mathf.PI * 2f / comets.Count;
					float fly = UltimateFxRun.EaseInOut(UltimateFxRun.Span(progress, 0f, 0.45f));
					float orbit = UltimateFxRun.Span(progress, 0.35f, 1f);
					float angle = startAngle + orbit * Mathf.PI * 2f / comets.Count * 1.2f;
					Vector3 onCircle = target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * circleRadius + Vector3.up * 0.3f;
					Vector3 arc = Vector3.Lerp(run.Caster + Vector3.up * 1.5f, onCircle, fly) + Vector3.up * Mathf.Sin(fly * Mathf.PI) * 4f;
					comets[i].transform.position = arc;
				}
			}, () =>
			{
				foreach (TrailRenderer comet in comets)
					run.Destroy(comet.gameObject);
			});
			run.ThinRing(DrawStart + drawSeconds * 0.6f, target, circleRadius * 0.95f, circleRadius, drawSeconds * 0.4f + circleSeconds + fallSeconds, cyan, 7);

			// 마름모 마법진
			Material rhombusMaterial = run.Additive(UltimateFxTextures.Rhombus, Color.clear);
			GameObject rhombus = null;
			run.Track(CircleStart, circleSeconds + fallSeconds + 0.1f, progress =>
			{
				if (rhombus == null)
					rhombus = run.Plate("Rhombus", rhombusMaterial, target + Vector3.up * 0.05f, circleRadius * 2.4f, 5);
				float appear = UltimateFxRun.EaseOut(progress * 3f);
				rhombus.transform.localScale = Vector3.one * circleRadius * 2.4f * Mathf.Lerp(0.6f, 1f, appear);
				rhombus.transform.rotation = Quaternion.Euler(0f, 45f + progress * 30f, 0f);
				rhombusMaterial.SetColor("_Color", new Color(cyan.r, cyan.g, cyan.b, appear));
			});

			// 블랙홀 낙하
			Material holeMaterial = run.Alpha(UltimateFxTextures.BlackHole, Color.white);
			Material discMaterial = run.Additive(UltimateFxTextures.DiscRing, cyanCore);
			GameObject hole = null;
			GameObject disc = null;
			run.Track(FallStart, fallSeconds + 0.1f, progress =>
			{
				if (hole == null)
				{
					hole = run.Billboard("BlackHole", holeMaterial, target, holeSize, holeSize, false, true, 13);
					disc = run.Billboard("Disc", discMaterial, target, holeSize * 2f, holeSize * 0.5f, false, true, 14);
				}
				float height = Mathf.Lerp(fallHeight, holeHeight, UltimateFxRun.EaseOut(progress));
				hole.transform.position = disc.transform.position = target + Vector3.up * height;
				disc.GetComponent<UltimateBillboard>().Roll = -8f;
				float pulse = 1f + 0.05f * Mathf.Sin(run.Now * 30f);
				hole.transform.localScale = Vector3.one * holeSize * pulse;
			});

			// 폭발: 백색은 배경 상한까지, 청록 구가 부풀며 사라짐
			Material burstMaterial = run.Additive(UltimateFxTextures.Soft, Color.clear);
			GameObject burst = null;
			run.Track(BurstStart, burstSeconds * 2f, progress =>
			{
				if (burst == null)
				{
					burst = run.Billboard("Burst", burstMaterial, target + Vector3.up * holeHeight, 1f, 1f, false, true, 15);
					run.Destroy(hole);
					run.Destroy(disc);
					run.Destroy(rhombus);
				}
				float decay = Mathf.Pow(1f - UltimateFxRun.Span(progress, 0f, 0.5f), 2f);
				run.Flash(burstFlash * decay, Color.white);
				burst.transform.localScale = Vector3.one * burstRadius * 2f * UltimateFxRun.EaseOut(progress * 1.5f);
				burstMaterial.SetColor("_Color", new Color(cyan.r, cyan.g, cyan.b, 1f - progress));
				Cold(run, 1f - UltimateFxRun.Span(progress, 0.4f, 1f));
			}, () =>
			{
				run.Destroy(burst);
				run.Flash(0f, Color.white);
				Cold(run, 0f);
			});
			run.At(BurstStart, () =>
			{
				run.ShakeSeries(BurstStart, 7, 0.07f, burstShake, 0.75f);
				run.React(target, 12f, burstRadius, false);
			});

			// 유리 파편
			run.At(ShardStart, () => EmitShards(run, target + Vector3.up * holeHeight));
		}

		private void Cold(UltimateFxRun run, float amount)
		{
			run.Grade.Desaturate = desaturate * amount;
			run.Grade.SetTint(tintAmount * amount, coldTint);
		}

		private static TrailRenderer CreateComet(UltimateFxRun run, Material material, Vector3 position)
		{
			GameObject comet = new("Comet");
			comet.transform.SetParent(run.Root, false);
			comet.transform.position = position;
			TrailRenderer trail = comet.AddComponent<TrailRenderer>();
			trail.sharedMaterial = material;
			trail.time = 0.45f;
			trail.widthMultiplier = 0.35f;
			trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
			trail.alignment = LineAlignment.View;
			trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			trail.receiveShadows = false;
			trail.minVertexDistance = 0.05f;
			return trail;
		}

		private void EmitShards(UltimateFxRun run, Vector3 origin)
		{
			Texture2D[] shards = UltimateFxTextures.Shards;
			for (int kind = 0; kind < shards.Length; kind++)
			{
				ParticleSystem pieces = run.Particles("Glass" + kind, run.Additive(shards[kind], Color.white), shardCount + 8, 0.5f);
				UltimateFxStage.SetLifetimeLook(pieces, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.6f), null);
				for (int i = 0; i < shardCount / shards.Length; i++)
				{
					Vector3 direction = Random.onUnitSphere;
					Color color = Color.Lerp(cyan, cyanCore, Random.value);
					color.a = 1f;
					UltimateFxStage.Emit(pieces, origin + direction * Random.Range(0.5f, holeSize * 0.4f), direction * Random.Range(3f, 10f), Random.Range(0.4f, 1.2f), Random.Range(shardSeconds * 0.6f, shardSeconds), color, Random.value * 360f, Random.Range(-400f, 400f));
				}
			}
		}
	}
}
