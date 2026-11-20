using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 카드 약한 판 (일반 스킬용). 손에서 카드 몇 장을 부채꼴로 던져 표적 앞에 비스듬히 꽂고, 잠시 뒤 터짐
	/// - 0.9초. 광원 1개 (카드 묶음을 따르는 점광, 터질 때 0.3초), 장면 빛 변경 없음, 후처리는 빛 번짐만
	/// - 공통 부품은 CardWorldFx. 강한 판은 CardHeavyUltimateFx (별개 에셋)
	/// - 재생: 개발 창 fx ultimate CardLight
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_CardLight", menuName = "WM/CombatFx/Ultimate/" + nameof(CardLightUltimateFx))]
	public sealed class CardLightUltimateFx : CardWorldFx
	{
		[Header("투척")]
		[SerializeField, Min(1)] private int cardCount = 4;
		[SerializeField, Range(0f, 90f), Tooltip("부채꼴 전체 각도")] private float fanDegrees = 34f;
		[SerializeField, Min(0f)] private float handHeight = 1f;
		[SerializeField, Min(0.05f)] private float flightSeconds = 0.24f;
		[SerializeField, Min(0f), Tooltip("카드 사이 출발 간격")] private float throwInterval = 0.035f;
		[SerializeField, Tooltip("표적 앞 꽂히는 거리 범위 (시전자 기준)")] private Vector2 landingDistance = new(2.1f, 2.7f);
		[SerializeField, Min(0f)] private float flightArc = 0.6f;
		[SerializeField, Tooltip("날며 도는 바퀴 수")] private float flightSpin = 2f;
		[SerializeField, Range(0f, 80f), Tooltip("꽂힌 카드가 진행 방향으로 눕는 각도")] private float stuckPitch = 38f;
		[SerializeField, Range(0f, 0.6f), Tooltip("땅에 묻히는 깊이 (카드 높이 비율)")] private float sinkRatio = 0.25f;

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("터지는 시각")] private float burstTime = 0.62f;
		[SerializeField, Min(0.05f)] private float burstSeconds = 0.2f;

		[Header("빛과 입자")]
		[SerializeField, Min(0f)] private float lightIntensity = 6f;
		[SerializeField, Min(1f)] private float lightRange = 5f;
		[SerializeField, Min(0f)] private float burstLightBoost = 1.8f;
		[SerializeField, Min(0f)] private float bloom = 0.6f;
		[SerializeField, Min(0)] private int sparksPerCard = 16;
		[SerializeField, Min(0)] private int bitsPerCard = 6;
		[SerializeField, Min(0f)] private float bitSpeed = 4f;
		[SerializeField, Min(0f)] private float shakeStrength = 0.35f;
		[SerializeField, Min(0.2f)] private float crackLinger = 0.9f;

		public override float DurationSeconds => burstTime + burstSeconds + 0.1f;

		protected override void ComposeWorld(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			Mesh mesh = CardMesh(run);
			Material material = CardMaterial(run);
			Vector3 forward = stage.Target - run.Caster;
			forward.y = 0f;
			forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : run.ScreenForward;
			Vector3 hand = run.Caster + Vector3.up * handHeight + forward * 0.3f;
			float cardHeight = 0.88f * CardScale;

			Texture2D crack = CrackTexture(run, 0);
			List<Transform> cards = new();
			List<Vector3> landings = new();
			ParticleSystem trail = Motes(run, "CardTrail", cardCount * 20 + 8, 0f, 0f);
			for (int i = 0; i < cardCount; i++)
			{
				float ratio = cardCount <= 1 ? 0.5f : i / (float)(cardCount - 1);
				float yaw = Mathf.Lerp(-fanDegrees * 0.5f, fanDegrees * 0.5f, ratio);
				Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * forward;
				Vector3 landing = run.Caster + direction * Random.Range(landingDistance.x, landingDistance.y);
				landing.y = UltimateFxStage.GroundHeight(landing);
				Quaternion stuck = StuckRotation(direction, stuckPitch, Random.Range(-8f, 8f));
				// 꽂힌 자세에서 카드 아래 끝이 sinkRatio 만큼 땅속
				Vector3 planted = landing + stuck * Vector3.up * cardHeight * (0.5f - sinkRatio);
				Transform card = CardObject(run, mesh, material, 1f, true);
				card.gameObject.SetActive(false);
				cards.Add(card);
				landings.Add(landing);
				float start = i * throwInterval;
				Quaternion flat = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
				run.At(start, () => card.gameObject.SetActive(true));
				run.Track(start, flightSeconds, progress =>
				{
					// 수평으로 누운 채 돌며 날다 꽂히기 직전 세워짐
					Vector3 position = Vector3.Lerp(hand, planted, progress) + Vector3.up * flightArc * Mathf.Sin(progress * Mathf.PI);
					card.position = position;
					Quaternion spin = flat * Quaternion.Euler(0f, 0f, flightSpin * 360f * progress);
					card.rotation = Quaternion.Slerp(spin, stuck, UltimateFxRun.EaseIn(Mathf.Clamp01((progress - 0.6f) / 0.4f)));
					if (Random.value < 0.6f)
						UltimateFxStage.Emit(trail, position, Vector3.zero, Random.Range(0.05f, 0.1f), 0.18f, SparkColor);
				});
				run.At(start + flightSeconds, () =>
				{
					GoldSparks(run, landing + Vector3.up * 0.1f, 6, 2.5f);
					Puff(run, landing, 4, 0.5f);
				});
				Crack(run, crack, landing, 0.9f, start + flightSeconds, burstTime + crackLinger, 0.4f);
			}

			// 묶음을 따르는 점광 하나
			Light bundle = WorldLight(stage, "CardLight", LightType.Point, LightColor, lightRange);
			run.Track(0f, DurationSeconds, _ =>
			{
				Vector3 sum = Vector3.zero;
				foreach (Transform card in cards)
					sum += card.position;
				bundle.transform.position = sum / Mathf.Max(1, cards.Count) + Vector3.up * 0.5f;
				float now = run.Now;
				float glow = Mathf.Clamp01(now / 0.06f) * 0.6f + burstLightBoost * Spike(now, burstTime, 0.03f, 0.1f);
				bundle.intensity = lightIntensity * glow * (1f - UltimateFxRun.Span(now, burstTime + 0.15f, DurationSeconds));
			}, () => bundle.enabled = false);

			// 터짐: 카드가 살짝 부풀었다 녹고 금 불꽃과 작은 카드 조각이 튐
			ParticleSystem bits = CardBits(run, mesh, CardMaterial(run), cardCount * bitsPerCard, 0.8f, true);
			run.At(burstTime, () =>
			{
				run.Shake(shakeStrength);
				for (int i = 0; i < cards.Count; i++)
				{
					Vector3 origin = cards[i].position;
					GoldSparks(run, origin, sparksPerCard, 4f);
					for (int bit = 0; bit < bitsPerCard; bit++)
						EmitMesh(bits, origin + Random.insideUnitSphere * 0.2f, (Random.onUnitSphere + Vector3.up) * bitSpeed * Random.Range(0.4f, 1f), CardScale * Random.Range(0.18f, 0.3f), Random.Range(0.7f, 1f), Color.white, Random.insideUnitSphere * 180f, Random.insideUnitSphere * 720f);
				}
			});
			Vector3[] baseScales = new Vector3[cards.Count];
			run.At(burstTime, () =>
			{
				for (int i = 0; i < cards.Count; i++)
					baseScales[i] = cards[i].localScale;
			});
			run.Track(burstTime, burstSeconds, progress =>
			{
				for (int i = 0; i < cards.Count; i++)
					cards[i].localScale = baseScales[i] * (1f + 0.25f * Mathf.Sin(Mathf.Clamp01(progress * 2f) * Mathf.PI * 0.5f));
				material.SetFloat(DISSOLVE_DIRECTION_ID, 1f);
				material.SetFloat(DISSOLVE_ID, UltimateFxRun.EaseIn(progress));
			}, () =>
			{
				foreach (Transform card in cards)
					run.Destroy(card.gameObject);
			});

			stage.BloomTint = LightColor;
			stage.Bloom = now => bloom * Mathf.Clamp01(now / 0.1f) * (1f + Spike(now, burstTime, 0.03f, 0.15f)) * (1f - UltimateFxRun.Span(now, burstTime + 0.2f, DurationSeconds + 0.5f));
		}
	}
}
