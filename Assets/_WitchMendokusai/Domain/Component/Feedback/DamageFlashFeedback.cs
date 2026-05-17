using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(DamageReaction))]
	public class DamageFlashFeedback : MonoBehaviour, IDamageReaction
	{
		[SerializeField] private SpriteRenderer spriteRenderer;
		private UnitHealth health;
		private Coroutine flashRoutine;

		private void Awake()
		{
			health = GetComponent<UnitHealth>();
		}

		public void OnDamaged(DamageInfo damageInfo)
		{
			if (health.IsAlive == false) return;

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
			if (spriteRenderer != null && spriteRenderer.material != null)
			{
				spriteRenderer.material.SetFloat("_Emission", 1);
				yield return new WaitForSeconds(0.1f);
				spriteRenderer.material.SetFloat("_Emission", 0);
			}
			flashRoutine = null;
		}
	}
}
