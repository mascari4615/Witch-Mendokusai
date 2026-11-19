using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 카드 궁극기. 카드 낱장이 하나씩 원을 이룸 -> 원이 풀리며 표적으로 연사 -> 하늘에서 큰 카드 낙하 -> 충격 고리
	/// -> 큰 카드가 낱장으로 터짐 -> 종이눈처럼 팔랑이며 떨어져 바닥에 쌓여 남음
	/// - 번쩍임 없음. 실루엣: 직사각형 원 -> 큰 직사각형. 배경 장악: 탈색과 어둡게 (무대 조명), 큰 카드 둘레만 밝음
	/// - 시간 구조는 참고 영상 15fps 실측 (원 1.8초, 연사 0.4초, 낙하 1초, 폭산 0.4초, 종이눈 1.8초)
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_03_Card", menuName = "WM/CombatFx/Ultimate/" + nameof(CardUltimateFx))]
	public sealed class CardUltimateFx : UltimateFx
	{
		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color ringGlow = new(1.4f, 0.4f, 0.55f);
		[SerializeField] private Color stageShade = new(0.12f, 0.04f, 0.14f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color impactRing = new(1.3f, 1.2f, 1.25f);

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("실측 1.8초")] private float formSeconds = 1.8f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.4초")] private float volleySeconds = 0.4f;
		[SerializeField, Min(0.1f), Tooltip("실측 1초")] private float fallSeconds = 1f;
		[SerializeField, Min(0.05f), Tooltip("착지 뒤 터질 때까지. 실측 0.4초")] private float holdSeconds = 0.4f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.4초")] private float burstSeconds = 0.4f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.8초")] private float confettiSeconds = 1.8f;
		[SerializeField, Min(0f), Tooltip("바닥에 쌓인 카드가 남아 있는 시간")] private float restSeconds = 1.6f;

		[Header("크기와 배치 (월드 단위)")]
		[SerializeField, Min(4)] private int ringCards = 36;
		[SerializeField, Min(1f)] private float ringRadius = 2.6f;
		[SerializeField] private Vector2 ringOffset = new(2.2f, 1.4f);
		[SerializeField, Min(0.2f)] private float cardHeight = 1.1f;
		[SerializeField, Tooltip("표적까지 화면 가로 거리. 음수면 왼쪽 (다른 궁극기와 방향이 겹치지 않게 왼쪽)")] private float targetDistance = -3.8f;
		[SerializeField, Min(2f), Tooltip("큰 카드 높이. 캐릭터 키의 약 8배")] private float bigCardHeight = 6.5f;
		[SerializeField, Min(2f)] private float fallHeight = 12f;
		[SerializeField, Min(1f)] private float impactRadius = 9f;
		[SerializeField, Min(0)] private int burstCards = 90;
		[SerializeField, Min(0f)] private float burstSpeed = 9f;

		[Header("화면과 카메라")]
		[SerializeField, Range(0f, 1f)] private float stageDesaturate = 0.7f;
		[SerializeField, Range(0f, 1f)] private float stageDarken = 0.35f;
		[SerializeField, Range(0f, 1f)] private float stageVignette = 0.55f;
		[SerializeField, Range(0.5f, 1f)] private float fallZoom = 0.86f;
		[SerializeField, Min(0f)] private float landingShake = 1.4f;
		[SerializeField, Min(0f)] private float distortion = 0.04f;

		private float VolleyStart => formSeconds;
		private float FallStart => VolleyStart + volleySeconds;
		private float LandTime => FallStart + fallSeconds;
		private float BurstStart => LandTime + holdSeconds;
		private float ConfettiStart => BurstStart + burstSeconds;

		public override float DurationSeconds => ConfettiStart + confettiSeconds + restSeconds;

		public override void Compose(UltimateFxRun run)
		{
			Vector3 target = run.Center + run.ScreenRight * targetDistance;
			target.y = UltimateFxStage.GroundHeight(target);
			Camera mainCamera = run.MainCamera;
			Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;
			Vector3 cameraUp = mainCamera != null ? mainCamera.transform.up : Vector3.up;
			Vector3 ringCenter = run.Caster + run.ScreenRight * ringOffset.x + Vector3.up * ringOffset.y;
			float cardWidth = cardHeight * UltimateFxTextures.Card.width / UltimateFxTextures.Card.height;

			// 무대 조명: 탈색, 어둡게, 가장자리 보라 그늘. 처음부터 종이눈 끝까지
			run.Track(0f, formSeconds * 0.4f, progress => Stage(run, progress));
			run.Track(ConfettiStart + confettiSeconds * 0.5f, confettiSeconds * 0.5f + restSeconds * 0.5f, progress => Stage(run, 1f - progress), () => Stage(run, 0f));

			// 원 형성: 한 장씩 원 둘레에 놓이며 원 전체가 돎
			List<GameObject> cards = new();
			Material cardMaterial = run.Alpha(UltimateFxTextures.Card, Color.white);
			cardMaterial.renderQueue = 3010;
			for (int i = 0; i < ringCards; i++)
			{
				int index = i;
				float appear = formSeconds * 0.75f * index / ringCards;
				run.At(appear, () =>
				{
					GameObject card = run.Billboard("Card" + index, cardMaterial, ringCenter, cardWidth, cardHeight, false, true, 10);
					cards.Add(card);
				});
			}
			run.Track(0f, VolleyStart + volleySeconds * 1.6f, progress =>
			{
				float spin = run.Now * 70f;
				for (int i = 0; i < cards.Count; i++)
				{
					float released = UltimateFxRun.Span(run.Now, VolleyStart + volleySeconds * i / ringCards, VolleyStart + volleySeconds * i / ringCards + volleySeconds * 0.5f);
					float angle = (i * 360f / ringCards + spin) * Mathf.Deg2Rad;
					Vector3 onRing = ringCenter + (cameraRight * Mathf.Cos(angle) + cameraUp * Mathf.Sin(angle)) * ringRadius;
					cards[i].transform.position = Vector3.Lerp(onRing, target + Vector3.up * 1f, UltimateFxRun.EaseIn(released));
					cards[i].GetComponent<UltimateBillboard>().Roll = angle * Mathf.Rad2Deg - 90f;
					cards[i].SetActive(released < 1f);
				}
			}, () =>
			{
				foreach (GameObject card in cards)
					run.Destroy(card);
			});
			run.Track(VolleyStart, volleySeconds, progress => run.Layer.SetStreaks(0.5f * (1f - progress), ringGlow, 0f, 1.6f), () => run.Layer.SetStreaks(0f, Color.white, 0f));

			// 큰 카드 낙하: 하늘에서 표적 위로 곧게 떨어짐, 떨어지는 길에 흰 줄 둘
			Material bigMaterial = run.Alpha(UltimateFxTextures.BigCard, Color.white);
			float bigWidth = bigCardHeight * UltimateFxTextures.BigCard.width / UltimateFxTextures.BigCard.height;
			GameObject bigCard = null;
			Material trailMaterial = run.Additive(UltimateFxTextures.Line, new Color(1f, 1f, 1f, 0.8f));
			GameObject[] trails = new GameObject[2];
			System.Action releaseZoom = delegate { };
			run.At(FallStart, () =>
			{
				bigCard = run.Billboard("BigCard", bigMaterial, target + Vector3.up * fallHeight, bigWidth, bigCardHeight, false, true, 12);
				for (int i = 0; i < trails.Length; i++)
					trails[i] = run.Billboard("FallLine" + i, trailMaterial, target, 0.12f, fallHeight, true, true, 11);
				releaseZoom = run.Zoom(fallZoom, -3f, fallSeconds, float.PositiveInfinity, 0.8f);
			});
			run.Track(FallStart, fallSeconds, progress =>
			{
				float height = Mathf.Lerp(fallHeight, bigCardHeight * 0.5f, UltimateFxRun.EaseIn(progress));
				bigCard.transform.position = target + Vector3.up * height;
				bigCard.GetComponent<UltimateBillboard>().Roll = Mathf.Lerp(-12f, 0f, progress);
				for (int i = 0; i < trails.Length; i++)
				{
					trails[i].transform.position = target + run.ScreenRight * (i == 0 ? -bigWidth * 0.35f : bigWidth * 0.35f) + Vector3.up * height;
					trails[i].transform.localScale = new Vector3(0.12f, fallHeight * (1f - progress) + 1f, 1f);
				}
			}, () =>
			{
				foreach (GameObject trail in trails)
					run.Destroy(trail);
			});

			// 착지: 충격 고리, 굴절, 흔들림, 풀 반응. 번쩍임 없음
			run.At(LandTime, () =>
			{
				run.Shake(landingShake);
				run.Dust(target, impactRadius * 0.4f, 50, new Color(0.7f, 0.65f, 0.7f, 0.5f));
			});
			run.Shockwave(LandTime, target, impactRadius, 0.8f, distortion, 0.14f, impactRing);
			run.ThinRing(LandTime, target, 1f, impactRadius * 1.3f, 0.9f, impactRing, 8);

			// 폭산: 큰 카드가 낱장으로 흩어짐
			run.At(BurstStart, () =>
			{
				releaseZoom();
				run.Destroy(bigCard);
				EmitConfetti(run, target, cardWidth, cardHeight);
			});
		}

		private void Stage(UltimateFxRun run, float amount)
		{
			run.Grade.Desaturate = stageDesaturate * amount;
			run.Grade.Darken = stageDarken * amount;
			run.Layer.SetVignette(stageVignette * amount, stageShade);
		}

		private void EmitConfetti(UltimateFxRun run, Vector3 target, float cardWidth, float cardHeight)
		{
			Material material = run.Alpha(UltimateFxTextures.Card, Color.white);
			ParticleSystem confetti = run.Particles("Confetti", material, burstCards + 8, 0.35f);
			ParticleSystem.MainModule main = confetti.main;
			main.startSize3D = true;
			ParticleSystemRenderer particleRenderer = confetti.GetComponent<ParticleSystemRenderer>();
			particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
			ParticleSystem.CollisionModule collision = confetti.collision;
			collision.enabled = true;
			collision.type = ParticleSystemCollisionType.Planes;
			collision.dampen = 1f;
			collision.bounce = 0f;
			collision.lifetimeLoss = 0f;
			GameObject plane = new("ConfettiFloor");
			plane.transform.SetParent(run.Root, false);
			plane.transform.position = target + Vector3.up * 0.05f;
			collision.SetPlane(0, plane.transform);
			ParticleSystem.VelocityOverLifetimeModule flutter = confetti.velocityOverLifetime;
			flutter.enabled = true;
			flutter.space = ParticleSystemSimulationSpace.World;
			flutter.x = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
			flutter.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
			ParticleSystem.LimitVelocityOverLifetimeModule drag = confetti.limitVelocityOverLifetime;
			drag.enabled = true;
			drag.limit = 3f;
			drag.dampen = 0.08f;
			Gradient life = UltimateFxStage.FadeGradient(Color.white, 0.01f, 0.92f);
			UltimateFxStage.SetLifetimeLook(confetti, life, null);
			float lifetime = burstSeconds + confettiSeconds + restSeconds;
			for (int i = 0; i < burstCards; i++)
			{
				Vector3 direction = Random.onUnitSphere;
				direction.y = Mathf.Abs(direction.y) * 1.4f + 0.3f;
				Vector3 position = target + Vector3.up * Random.Range(0.5f, bigCardHeight) + Random.insideUnitSphere;
				ParticleSystem.EmitParams emitParams = new()
				{
					position = position,
					velocity = direction.normalized * burstSpeed * Random.Range(0.5f, 1.1f),
					startSize3D = new Vector3(cardWidth, cardHeight, 1f) * Random.Range(0.8f, 1.15f),
					startLifetime = lifetime * Random.Range(0.85f, 1f),
					startColor = Color.white,
					rotation = Random.value * 360f,
					angularVelocity = Random.Range(-360f, 360f),
				};
				confetti.Emit(emitParams, 1);
			}
		}

	}
}
