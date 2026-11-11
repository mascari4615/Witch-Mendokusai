using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(DamageReaction))]
	public class DamageFlashFeedback : MonoBehaviour, IDamageReaction
	{
		[SerializeField] private SpriteRenderer spriteRenderer;
		[SerializeField, Min(0f)] private float flashDuration = 0.1f;
		[SerializeField, Range(0f, 1f), Tooltip("피격 흰 점멸 세기. 큰 몸은 낮춰 형태 유지 (보스 0.5, 사용자 2026-11-12)")]
		private float flashStrength = 1f;
		private UnitHealth health;
		private Coroutine flashRoutine;
		private MaterialPropertyBlock properties;
		private MaterialPropertyBlock originalProperties;
		private bool hasOriginalProperties;
		private static readonly int EMISSION_ID = Shader.PropertyToID("_Emission");

		private void Awake()
		{
			health = GetComponent<UnitHealth>();
			properties = new MaterialPropertyBlock();
			originalProperties = new MaterialPropertyBlock();
		}

		private void OnDisable()
		{
			StopAllCoroutines();
			flashRoutine = null;
			RestoreProperties();
		}

		private void RestoreProperties()
		{
			if (spriteRenderer == null || hasOriginalProperties == false)
				return;
			// 피격 뒤 기존 속성 블록 복원. 빈 블록은 해제
			spriteRenderer.SetPropertyBlock(originalProperties.isEmpty ? null : originalProperties);
			hasOriginalProperties = false;
		}

		private void SetEmission(float value)
		{
			if (spriteRenderer == null || properties == null)
				return;
			spriteRenderer.GetPropertyBlock(properties);
			properties.SetFloat(EMISSION_ID, value);
			spriteRenderer.SetPropertyBlock(properties);
		}

		public void OnDamaged(DamageInfo damageInfo)
		{
			if (health.IsAlive == false) return;
			if (CombatEffectSettings.Flash == false) return;

			if (flashRoutine != null)
			{
				StopCoroutine(flashRoutine);
			}

			if (gameObject.activeInHierarchy)
			{
				flashRoutine = StartCoroutine(FlashRoutine());
			}
		}

		private IEnumerator FlashRoutine()
		{
			if (hasOriginalProperties == false)
			{
				spriteRenderer.GetPropertyBlock(originalProperties);
				hasOriginalProperties = true;
			}
			SetEmission(flashStrength);
			yield return new WaitForSeconds(flashDuration);
			RestoreProperties();
			flashRoutine = null;
		}
	}
}
