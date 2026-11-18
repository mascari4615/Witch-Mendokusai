using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	// 피격 흰 점멸과 몬스터 그림 좌우 흔들림
	// - 둘 다 그림 재질 속성 블록. 흔들림은 셰이더 정점 이동이라 위치와 충돌체는 그대로
	// - 블록은 상태이상 물들임과 같이 씀. 자기 값만 덧쓰고 셋 다 꺼지면 해제 (UnitBodyProperties)
	[RequireComponent(typeof(DamageReaction))]
	public class DamageFlashFeedback : MonoBehaviour, IDamageReaction
	{
		[SerializeField] private SpriteRenderer spriteRenderer;
		[SerializeField, Min(0f)] private float flashDuration = 0.1f;
		[SerializeField, Range(0f, 1f), Tooltip("피격 흰 점멸 세기. 큰 몸은 낮춰 형태 유지 (보스 0.5, 사용자 2026-11-12)")]
		private float flashStrength = 1f;
		private UnitHealth health;
		private UnitObject unitObject;
		private Coroutine flashRoutine;
		private MaterialPropertyBlock properties;
		// 피격마다 대기 객체를 새로 만들지 않게 값이 같으면 재사용
		private WaitForSeconds flashWait;
		private float flashWaitSeconds = -1f;
		private WaitForSeconds shakeWait;
		private float shakeWaitSeconds = -1f;

		private void Awake()
		{
			health = GetComponent<UnitHealth>();
			unitObject = GetComponent<UnitObject>();
			properties = new MaterialPropertyBlock();
		}

		private void OnDisable()
		{
			StopAllCoroutines();
			flashRoutine = null;
			ClearFeedback();
		}

		private void ClearFeedback()
		{
			// Awake 전에 꺼지는 경우
			if (properties == null)
				return;
			spriteRenderer.GetPropertyBlock(properties);
			properties.SetFloat(UnitBodyProperties.EMISSION_ID, 0f);
			properties.SetVector(UnitBodyProperties.HIT_SHAKE_ID, Vector4.zero);
			UnitBodyProperties.Commit(spriteRenderer, properties);
		}

		public void OnDamaged(DamageInfo damageInfo)
		{
			// 상태이상 틱은 몸 물들임이 이미 보임
			if (health.IsAlive == false || damageInfo.type == DamageType.StatusTick)
				return;
			bool flash = CombatEffectSettings.Flash;
			bool shake = unitObject is MonsterObject;
			if (flash == false && shake == false)
				return;
			if (gameObject.activeInHierarchy == false)
				return;

			if (flashRoutine != null)
				StopCoroutine(flashRoutine);
			flashRoutine = StartCoroutine(FeedbackRoutine(flash, shake));
		}

		private IEnumerator FeedbackRoutine(bool flash, bool shake)
		{
			float shakeSeconds = 0f;
			spriteRenderer.GetPropertyBlock(properties);
			if (flash)
				properties.SetFloat(UnitBodyProperties.EMISSION_ID, flashStrength);
			if (shake)
				shakeSeconds = StartShake();
			spriteRenderer.SetPropertyBlock(properties);

			float flashSeconds = flash ? flashDuration : 0f;
			yield return CachedWait(ref flashWait, ref flashWaitSeconds, flashSeconds);
			spriteRenderer.GetPropertyBlock(properties);
			properties.SetFloat(UnitBodyProperties.EMISSION_ID, 0f);
			UnitBodyProperties.Commit(spriteRenderer, properties);

			// 흔들림이 끝난 뒤 한 번 더 정리해 블록 해제
			if (shakeSeconds > flashSeconds)
			{
				yield return CachedWait(ref shakeWait, ref shakeWaitSeconds, shakeSeconds - flashSeconds);
				spriteRenderer.GetPropertyBlock(properties);
				UnitBodyProperties.Commit(spriteRenderer, properties);
			}
			flashRoutine = null;
		}

		private static WaitForSeconds CachedWait(ref WaitForSeconds cache, ref float cachedSeconds, float seconds)
		{
			if (cache == null || Mathf.Approximately(cachedSeconds, seconds) == false)
			{
				cache = new WaitForSeconds(seconds);
				cachedSeconds = seconds;
			}
			return cache;
		}

		// 연속 피격은 처음부터 다시, 남은 폭에 더하되 상한. 얼음, 기절은 작고 빠르게
		private float StartShake()
		{
			UnitStatusConfig config = ResourceManager.Instance.UnitStatus;
			UnitStatusDriver.HitShakeScale(unitObject, out float amplitudeScale, out float frequencyScale);
			float amplitude = Mathf.Min(config.HitShakeMaxAmplitude * amplitudeScale, UnitBodyProperties.RemainingShake(properties) + config.HitShakeAmplitude * amplitudeScale);
			if (amplitude <= 0f)
				return 0f;
			properties.SetVector(UnitBodyProperties.HIT_SHAKE_ID, new Vector4(UnitBodyProperties.ShaderTime, amplitude, config.HitShakeSeconds, config.HitShakeFrequency * frequencyScale));
			return config.HitShakeSeconds;
		}
	}
}
