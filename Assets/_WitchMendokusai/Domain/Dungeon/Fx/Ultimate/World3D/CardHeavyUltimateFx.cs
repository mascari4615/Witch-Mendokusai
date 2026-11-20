using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 카드 강한 판 (강력 스킬용). 시전자 뒤 공중에 카드 원진이 돌다가 풀려 쏟아지고, 거대 카드 한 장이 무대 스포트 아래 세로로 꽂힌 뒤 카드 눈으로 터져 바닥에 쌓임
	/// - 흐름: 원진 형성 (1.8초, 한 장씩 손에서 날아올라 합류, 공전과 자전, 해를 반사하는 카드가 번쩍) -> 연사 (0.4초, 원진 카드가 표적 둘레에 꽂힘)
	///   -> 무대 스포트 예고 (크기 고정 0.5초, 원뿔 안 먼지) -> 거대 카드가 돌며 떨어져 앞면을 보이며 섬 -> 떨림 -> 카드 눈 폭산, 바닥에 남음
	/// - 약한 판과 같은 공통 부품 (CardWorldFx) 위에 고유 단계 셋: 무대 스포트, 카드 눈 쌓임, 원진 반사 깜빡임. 거대 카드는 값 확대
	/// - 원 레퍼런스 m03 (15fps): 고리 형성 1.8s, 연사 0.4s, 폭산 0.4s, 종이눈 1.8s, 번쩍임 없음, 카메라 확대
	/// - 광원 2개 (원진 점광, 무대 스포트), 겹치는 구간 0.5초 동안만 둘
	/// - 재생: 개발 창 fx ultimate CardHeavy
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_CardHeavy", menuName = "WM/CombatFx/Ultimate/" + nameof(CardHeavyUltimateFx))]
	public sealed class CardHeavyUltimateFx : CardWorldFx
	{
		private sealed class RingCard
		{
			public Transform Transform;
			public MaterialPropertyBlock Block;
			public MeshRenderer Renderer;
			public float Angle;
			public float SpinPhase;
			public Vector3 Landing;
			public Quaternion Stuck;
			public bool Landed;
		}

		[Header("원진")]
		[SerializeField, Min(1)] private int ringCount = 36;
		[SerializeField, Min(0.5f)] private float ringRadius = 2.6f;
		[SerializeField, Min(0f), Tooltip("원진 중심 높이 (시전자 발 기준)")] private float ringHeight = 3.4f;
		[SerializeField, Tooltip("원진 중심의 화면 위쪽 거리 (시전자 기준)")] private float ringForward = 0.8f;
		[SerializeField, Range(0f, 90f), Tooltip("원 면이 수평에서 카메라 쪽으로 기우는 각도")] private float ringTilt = 38f;
		[SerializeField, Tooltip("공전 속도 (도/초). m03 실측 140")] private float orbitSpeed = 140f;
		[SerializeField, Tooltip("카드 자전 속도 (도/초)")] private float spinSpeed = 260f;
		[SerializeField, Min(0.05f)] private float joinSeconds = 0.3f;
		[SerializeField, Min(0f), Tooltip("해 반사 각도에서 번쩍이는 세기")] private float glintGain = 5f;
		[SerializeField, Range(1f, 256f), Tooltip("번쩍임이 맞는 각도 폭 (클수록 좁음)")] private float glintSharpness = 40f;

		[Header("연사와 거대 카드")]
		[SerializeField, Tooltip("표적 둘레 꽂히는 반지름 범위")] private Vector2 volleyRadius = new(0.9f, 3.4f);
		[SerializeField, Min(0.05f)] private float volleyFlightSeconds = 0.17f;
		[SerializeField, Range(0f, 80f)] private float volleyPitch = 48f;
		[SerializeField, Min(1f), Tooltip("거대 카드 배율. 높이 약 7")] private float giantScale = 7f;
		[SerializeField, Min(2f)] private float giantDropHeight = 12f;
		[SerializeField, Tooltip("떨어지며 도는 바퀴 수")] private float giantSpinTurns = 2.25f;
		[SerializeField, Range(0f, 0.5f), Tooltip("박히는 깊이 (높이 비율)")] private float giantSink = 0.2f;
		[SerializeField, Range(-60f, 60f), Tooltip("선 거대 카드가 카메라 정면에서 돌아간 각도. 0 이면 얇은 판으로만 읽힘")] private float giantYaw = 14f;
		[SerializeField, Min(0)] private int snowCount = 120;
		[SerializeField, Min(0f)] private float snowSpeed = 5f;
		[SerializeField, Range(0f, 1f)] private float snowGravity = 0.28f;
		[SerializeField, Min(0.1f)] private float snowScale = 0.95f;

		[Header("시간 (초)")]
		[SerializeField, Min(0.2f), Tooltip("m03 고리 형성 1.8s")] private float ringSeconds = 1.8f;
		[SerializeField, Min(0.1f), Tooltip("m03 연사 0.4s")] private float volleySeconds = 0.4f;
		[SerializeField, Min(0.1f), Tooltip("실측 450~500ms. 스포트가 크기 그대로 밝아짐")] private float telegraphSeconds = 0.5f;
		[SerializeField, Min(0.05f), Tooltip("거대 카드가 화면 위에서 땅까지")] private float giantFallSeconds = 0.26f;
		[SerializeField, Min(0.05f)] private float trembleSeconds = 0.4f;
		[SerializeField, Min(0.1f), Tooltip("m03 종이눈 1.8s")] private float snowSeconds = 1.8f;
		[SerializeField, Min(0f), Tooltip("눈이 쌓여 남는 시간")] private float restSeconds = 1.2f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 1.2f;

		[Header("빛")]
		[SerializeField] private Color ringLightColor = new(0.75f, 0.45f, 1f, 1f);
		[SerializeField, Min(0f)] private float ringLightIntensity = 9f;
		[SerializeField, Min(1f)] private float ringLightRange = 7f;
		[SerializeField, Min(0f)] private float spotIntensity = 34f;
		[SerializeField, Range(5f, 90f), Tooltip("설계 34도")] private float spotAngle = 34f;
		[SerializeField, Min(2f)] private float spotHeight = 11f;
		[SerializeField, Min(0.05f)] private float spotHalfLife = 0.9f;
		[SerializeField, Range(0f, 1f)] private float sunDim = 0.45f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.55f;

		[Header("후처리와 카메라")]
		[SerializeField, Range(-100f, 0f), Tooltip("지속 탈색 상한 -40 (UI 도 같이 옅어짐)")] private float saturation = -40f;
		[SerializeField] private Color shadeFilter = new(0.93f, 0.86f, 1f, 1f);
		[SerializeField, Min(0f)] private float bloom = 1.2f;
		[SerializeField, Range(0f, 1f)] private float vignette = 0.3f;
		[SerializeField, Range(1f, 1.3f), Tooltip("원진 동안 당김 배율 (m03 카메라 확대)")] private float zoomIn = 1.08f;
		[SerializeField, Min(0f)] private float shakeStrength = 1f;
		[SerializeField, Min(1)] private int shakeCount = 4;

		[Header("반응")]
		[SerializeField, Min(0)] private int coneDustCount = 60;
		[SerializeField, Min(0)] private int impactSparks = 50;
		[SerializeField, Min(0)] private int impactDust = 44;
		[SerializeField, Min(1f)] private float crackSize = 8f;
		[SerializeField, Min(1f)] private float windSpeed = 14f;
		[SerializeField, Min(1f)] private float windReach = 8f;
		[SerializeField, Min(0f), Tooltip("거대 카드가 꽂힐 때 둘레 카드가 쓰러지는 반지름")] private float knockRadius = 4.2f;

		private float VolleyStart => ringSeconds;
		private float LandTime => VolleyStart + volleySeconds + telegraphSeconds;
		private float BurstTime => LandTime + trembleSeconds;
		private float RestoreStart => BurstTime + snowSeconds * 0.5f;

		public override float DurationSeconds => BurstTime + snowSeconds + restSeconds;

		protected override void ComposeWorld(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			Mesh mesh = CardMesh(run);
			Material ringMaterial = CardMaterial(run);
			List<RingCard> cards = ComposeRing(stage, mesh, ringMaterial);
			ComposeVolley(stage, cards);
			Transform giant = ComposeGiant(stage, mesh, cards);
			ComposeSnow(stage, mesh, giant, cards, ringMaterial);
			ComposeGrade(stage);
		}

		// 원진: 손에서 한 장씩 날아올라 합류, 공전하며 자전. 해 반사 각도의 카드가 번쩍
		private List<RingCard> ComposeRing(Stage stage, Mesh mesh, Material material)
		{
			UltimateFxRun run = stage.Run;
			Vector3 center = run.Caster + run.ScreenForward * ringForward + Vector3.up * ringHeight;
			Vector3 normal = (Vector3.up * Mathf.Cos(ringTilt * Mathf.Deg2Rad) + stage.ToCamera * Mathf.Sin(ringTilt * Mathf.Deg2Rad)).normalized;
			Vector3 axisU = Vector3.Cross(normal, run.ScreenForward).normalized;
			Vector3 axisV = Vector3.Cross(normal, axisU);
			Vector3 hand = run.Caster + Vector3.up * 1.1f;
			Vector3 sunDirection = RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : Vector3.up;

			List<RingCard> cards = new();
			for (int i = 0; i < ringCount; i++)
			{
				Transform card = CardObject(run, mesh, material, 1f, false);
				card.gameObject.SetActive(false);
				RingCard ringCard = new()
				{
					Transform = card,
					Renderer = card.GetComponent<MeshRenderer>(),
					Block = new MaterialPropertyBlock(),
					Angle = i / (float)ringCount * 360f,
					SpinPhase = Random.value * 360f,
				};
				cards.Add(ringCard);
				float join = i / (float)ringCount * (ringSeconds - joinSeconds);
				run.At(join, () => card.gameObject.SetActive(true));
				run.Track(join, VolleyStart + i * (volleySeconds / ringCount) - join, progress =>
				{
					float now = run.Now;
					float orbit = ringCard.Angle + orbitSpeed * now;
					float radians = orbit * Mathf.Deg2Rad;
					Vector3 radial = axisU * Mathf.Cos(radians) + axisV * Mathf.Sin(radians);
					Vector3 slot = center + radial * ringRadius;
					float fly = UltimateFxRun.EaseOut(Mathf.Clamp01((now - join) / joinSeconds));
					card.position = Vector3.Lerp(hand, slot, fly) + Vector3.up * Mathf.Sin(fly * Mathf.PI) * 0.6f;
					// 카드 세로축이 바깥 (방사) 을 보고 앞면이 원 면 바깥 (카메라 쪽). 세로축을 따라 자전해 앞뒷면이 번갈아 보임
					Quaternion facing = Quaternion.LookRotation(-normal, radial);
					card.rotation = facing * Quaternion.Euler(0f, ringCard.SpinPhase + spinSpeed * now, 0f);
					// 반사 깜빡임: 카드 면 법선이 해와 시선의 중간 방향에 맞으면 번쩍 (면 반사를 키워 쿼터뷰 거리에서도 보이게)
					Vector3 view = run.MainCamera != null ? (run.MainCamera.transform.position - card.position).normalized : stage.ToCamera;
					Vector3 halfway = (sunDirection + view).normalized;
					float alignment = Mathf.Abs(Vector3.Dot(card.forward, halfway));
					float glint = Mathf.Pow(alignment, glintSharpness) * glintGain;
					ringCard.Renderer.GetPropertyBlock(ringCard.Block);
					ringCard.Block.SetColor(EMISSION_ID, GoldGlow * (1f + glint) + Color.white * glint * 0.6f);
					ringCard.Renderer.SetPropertyBlock(ringCard.Block);
				});
			}

			Light ringLight = WorldLight(stage, "RingLight", LightType.Point, ringLightColor, ringLightRange);
			ringLight.transform.position = center;
			run.Track(0f, LandTime, _ =>
			{
				float now = run.Now;
				float build = UltimateFxRun.EaseInOut(UltimateFxRun.Span(now, 0f, ringSeconds));
				float release = 1f - UltimateFxRun.Span(now, VolleyStart, LandTime);
				ringLight.intensity = ringLightIntensity * build * Mathf.Lerp(0.25f, 1f, release);
			}, () => ringLight.enabled = false);

			ParticleSystem motes = Motes(run, "RingMotes", 80, -0.05f, 0.5f);
			for (int i = 0; i < 70; i++)
			{
				float time = i / 70f * ringSeconds;
				run.At(time, () =>
				{
					float radians = Random.value * Mathf.PI * 2f;
					Vector3 position = center + (axisU * Mathf.Cos(radians) + axisV * Mathf.Sin(radians)) * ringRadius * Random.Range(0.85f, 1.15f);
					UltimateFxStage.Emit(motes, position, Random.insideUnitSphere * 0.4f, Random.Range(0.05f, 0.12f), Random.Range(0.5f, 1f), Random.value < 0.5f ? SparkColor : new Color(ringLightColor.r * 2f, ringLightColor.g * 2f, ringLightColor.b * 2f, 1f));
				});
			}
			run.At(0f, () =>
			{
				System.Action release = run.Zoom(1f / zoomIn, 0f, ringSeconds, float.PositiveInfinity, 0.6f);
				run.At(LandTime - 0.15f, () => release());
			});
			return cards;
		}

		// 연사: 원진 카드가 차례로 표적 둘레에 날아 비스듬히 꽂힘
		private void ComposeVolley(Stage stage, List<RingCard> cards)
		{
			UltimateFxRun run = stage.Run;
			float cardHeight = 0.88f * CardScale;
			ParticleSystem hits = Motes(run, "VolleyHits", cards.Count * 5 + 8, 0.8f, 0f);
			for (int i = 0; i < cards.Count; i++)
			{
				RingCard card = cards[i];
				float leave = VolleyStart + i * (volleySeconds / cards.Count);
				Vector3 landing = stage.Target + UltimateFxStage.RandomRingPoint(volleyRadius.x, volleyRadius.y);
				landing.y = UltimateFxStage.GroundHeight(landing);
				card.Landing = landing;
				Vector3 from = Vector3.zero;
				Quaternion fromRotation = Quaternion.identity;
				run.At(leave, () =>
				{
					from = card.Transform.position;
					fromRotation = card.Transform.rotation;
					card.Stuck = StuckRotation(landing - from, volleyPitch + Random.Range(-10f, 10f), Random.Range(-12f, 12f));
				});
				run.Track(leave, volleyFlightSeconds, progress =>
				{
					Vector3 planted = landing + card.Stuck * Vector3.up * cardHeight * 0.3f;
					card.Transform.position = Vector3.Lerp(from, planted, UltimateFxRun.EaseIn(progress));
					card.Transform.rotation = Quaternion.Slerp(fromRotation, card.Stuck, progress);
				}, () =>
				{
					card.Landed = true;
					for (int spark = 0; spark < 5; spark++)
						UltimateFxStage.Emit(hits, landing + Vector3.up * 0.1f, (Random.onUnitSphere + Vector3.up * 1.3f) * Random.Range(1.2f, 3f), Random.Range(0.05f, 0.11f), Random.Range(0.25f, 0.5f), SparkColor);
				});
			}
		}

		// 무대 스포트 예고 뒤 거대 카드가 돌며 떨어져 앞면을 보이며 섬. 둘레 카드가 바깥으로 쓰러짐
		private Transform ComposeGiant(Stage stage, Mesh mesh, List<RingCard> cards)
		{
			UltimateFxRun run = stage.Run;
			Vector3 target = stage.Target;
			float height = 0.88f * CardScale * giantScale;
			Material material = CardMaterial(run);
			Transform giant = CardObject(run, mesh, material, giantScale, true);
			giant.gameObject.SetActive(false);
			Quaternion facing = Quaternion.LookRotation(-stage.ToCamera) * Quaternion.Euler(0f, giantYaw, 0f);
			Vector3 planted = target + Vector3.up * height * (0.5f - giantSink);
			Vector3 sky = planted + Vector3.up * giantDropHeight;
			float fallStart = LandTime - giantFallSeconds;
			run.At(fallStart, () => giant.gameObject.SetActive(true));
			run.Track(fallStart, giantFallSeconds, progress =>
			{
				giant.position = Vector3.Lerp(sky, planted, UltimateFxRun.EaseIn(progress));
				// 감속하며 돌다 앞면을 보이며 섬 (하늘 검의 회전 없는 직선 낙하와 구분)
				float spin = giantSpinTurns * 360f * (1f - UltimateFxRun.EaseOut(progress));
				giant.rotation = facing * Quaternion.Euler(0f, spin, 0f);
			});
			run.Track(LandTime, trembleSeconds, progress =>
			{
				float shake = (1f - progress) * Mathf.Sin(progress * 60f) * 2.2f;
				giant.position = planted;
				giant.rotation = facing * Quaternion.Euler(shake * 0.4f, 0f, shake);
			});

			// 무대 스포트: 위에서 아래로 원뿔 빛 (크기 고정), 원뿔 안 먼지가 빛남
			Light spot = WorldLight(stage, "StageSpot", LightType.Spot, LightColor, spotHeight + 4f);
			spot.spotAngle = spotAngle;
			spot.innerSpotAngle = spotAngle * 0.7f;
			spot.transform.position = target + Vector3.up * spotHeight;
			spot.transform.rotation = Quaternion.LookRotation(Vector3.down);
			float spotStart = LandTime - telegraphSeconds;
			run.Track(spotStart, RestoreStart + restoreSeconds - spotStart, _ =>
			{
				float now = run.Now;
				float ramp = UltimateFxRun.EaseIn(UltimateFxRun.Span(now, spotStart, LandTime));
				float decay = now < LandTime ? 1f : Mathf.Lerp(0.35f, 1f, Mathf.Pow(0.5f, (now - LandTime) / spotHalfLife));
				spot.intensity = spotIntensity * ramp * decay * (1f - UltimateFxRun.Span(now, RestoreStart, RestoreStart + restoreSeconds));
			}, () => spot.enabled = false);
			ParticleSystem coneDust = run.Particles("ConeDust", LitParticle(run, UltimateFxTextures.Soft, 0f), coneDustCount + 8, -0.01f);
			UltimateFxStage.SetLifetimeLook(coneDust, UltimateFxStage.FadeGradient(Color.white, 0.25f, 0.6f), null);
			ParticleSystem.NoiseModule noise = coneDust.noise;
			noise.enabled = true;
			noise.strength = 0.25f;
			noise.frequency = 0.5f;
			float coneSlope = Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad);
			for (int i = 0; i < coneDustCount; i++)
			{
				float time = spotStart + i / (float)coneDustCount * telegraphSeconds * 2f;
				run.At(time, () =>
				{
					float drop = Random.Range(0.5f, spotHeight - 1f);
					Vector3 offset = UltimateFxStage.RandomRingPoint(0f, drop * coneSlope * 0.85f);
					Vector3 position = target + Vector3.up * (spotHeight - drop) + offset;
					UltimateFxStage.Emit(coneDust, position, Vector3.down * Random.Range(0.05f, 0.25f), Random.Range(0.05f, 0.12f), Random.Range(2.2f, 3.2f), new Color(1f, 0.95f, 0.85f, 0.9f));
				});
			}

			run.At(LandTime, () =>
			{
				run.ShakeSeries(run.Now, shakeCount, 0.07f, shakeStrength, 0.7f);
				run.React(target, windSpeed, windReach, false);
				GoldSparks(run, target + Vector3.up * 0.2f, impactSparks, 7f);
				DustRing(run, target, impactDust, 3.5f, DustColor, 1.5f);
				// 둘레에 꽂혀 있던 카드가 바깥으로 쓰러지며 튐 (지면 고리 대신)
				foreach (RingCard card in cards)
				{
					Vector3 away = card.Landing - target;
					away.y = 0f;
					if (card.Landed == false || away.magnitude > knockRadius)
						continue;
					KnockOver(run, card, away, BurstTime - LandTime);
				}
			});
			Crack(run, CrackTexture(run, 0), target, crackSize, LandTime, DurationSeconds, TailSeconds * 0.8f);
			GroundGlow(run, target, 6f, new Color(LightColor.r, LightColor.g, LightColor.b, 1f) * 0.6f, LandTime, 0.08f, 0.6f, BurstTime + 0.6f - LandTime);
			return giant;
		}

		private static void KnockOver(UltimateFxRun run, RingCard card, Vector3 away, float seconds)
		{
			Vector3 direction = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
			Vector3 start = card.Transform.position;
			Quaternion startRotation = card.Transform.rotation;
			// 바깥으로 누운 자세 (앞면이나 뒷면이 위)
			Quaternion lying = Quaternion.LookRotation(Vector3.down, direction) * Quaternion.Euler(0f, 0f, Random.Range(-30f, 30f));
			Vector3 end = card.Landing + direction * Random.Range(0.4f, 0.9f) + Vector3.up * 0.03f;
			float duration = Mathf.Min(seconds, 0.32f);
			run.Track(run.Now, duration, progress =>
			{
				card.Transform.position = Vector3.Lerp(start, end, progress) + Vector3.up * Mathf.Sin(progress * Mathf.PI) * 0.5f;
				card.Transform.rotation = Quaternion.Slerp(startRotation, lying, UltimateFxRun.EaseOut(progress));
			});
		}

		// 폭산: 거대 카드가 부풀었다 사라지며 카드 눈 (메시 입자) 이 흩날려 바닥에 쌓임. 꽂혀 있던 카드도 금 불꽃으로 녹음
		private void ComposeSnow(Stage stage, Mesh mesh, Transform giant, List<RingCard> cards, Material ringMaterial)
		{
			UltimateFxRun run = stage.Run;
			float height = 0.88f * CardScale * giantScale;
			float width = 0.63f * CardScale * giantScale;
			ParticleSystem snow = CardBits(run, mesh, CardMaterial(run), snowCount, snowGravity, true);
			UltimateFxStage.SetLifetimeLook(snow, null, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0f)));
			ParticleSystem.CollisionModule collision = snow.collision;
			collision.bounce = 0f;
			collision.dampen = 1f;
			float snowLife = DurationSeconds - BurstTime + 0.2f;
			Vector3 baseScale = Vector3.one;
			run.At(BurstTime, () => baseScale = giant.localScale);
			run.Track(BurstTime, 0.08f, progress => giant.localScale = baseScale * (1f + 0.12f * progress), () => run.Destroy(giant.gameObject));
			run.At(BurstTime, () =>
			{
				for (int i = 0; i < snowCount; i++)
				{
					Vector3 local = new(Random.Range(-0.5f, 0.5f) * width, Random.Range(-0.5f, 0.5f) * height, 0f);
					Vector3 origin = giant.position + giant.rotation * local;
					Vector3 velocity = (origin - (giant.position - Vector3.up * height * 0.3f)).normalized * snowSpeed * Random.Range(0.3f, 1f) + Vector3.up * Random.Range(1f, 3f);
					EmitMesh(snow, origin, velocity, CardScale * snowScale * Random.Range(0.8f, 1.1f), snowLife * Random.Range(0.92f, 1f), Color.white, Random.insideUnitSphere * 180f, Random.insideUnitSphere * 420f);
				}
				GoldSparks(run, giant.position, impactSparks, 6f);
				foreach (RingCard card in cards)
				{
					if (card.Transform != null)
						GoldSparks(run, card.Transform.position, 3, 2f);
				}
			});
			run.Track(BurstTime, 0.25f, progress =>
			{
				ringMaterial.SetFloat(DISSOLVE_DIRECTION_ID, 1f);
				ringMaterial.SetFloat(DISSOLVE_ID, UltimateFxRun.EaseIn(progress));
			}, () =>
			{
				foreach (RingCard card in cards)
					run.Destroy(card.Transform.gameObject);
			});
		}

		private void ComposeGrade(Stage stage)
		{
			float restore = RestoreStart;
			stage.BloomTint = LightColor;
			stage.SunScale = now => Mathf.Lerp(1f, sunDim, Plateau(now, 0f, 0.6f, restore, restoreSeconds));
			stage.AmbientScale = now => Mathf.Lerp(1f, ambientDim, Plateau(now, 0f, 0.6f, restore, restoreSeconds));
			stage.Saturation = now => saturation * Plateau(now, 0f, 0.6f, restore, restoreSeconds);
			stage.Filter = now => Color.Lerp(Color.white, shadeFilter, Plateau(now, 0f, 0.6f, restore, restoreSeconds));
			stage.Vignette = now => vignette * Plateau(now, 0f, 0.6f, restore, restoreSeconds);
			stage.Bloom = now => bloom * Plateau(now, 0f, 0.6f, restore, restoreSeconds) * (1f + 0.6f * Spike(now, LandTime, 0.05f, 0.3f));
		}
	}
}
