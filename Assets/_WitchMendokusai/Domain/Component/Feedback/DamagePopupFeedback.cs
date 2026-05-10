using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UnitHealth))]
	public class DamagePopupFeedback : MonoBehaviour
	{
		private UnitHealth health;

		private void Awake()
		{
			health = GetComponent<UnitHealth>();
		}

		private void OnEnable()
		{
			health.OnTakeDamage += PopDamage;
		}

		private void OnDisable()
		{
			health.OnTakeDamage -= PopDamage;
		}

		private void PopDamage(DamageInfo damageInfo)
		{
			if (UIManager.Instance != null)
			{
				UIManager.Instance.PopDamage(damageInfo, transform.position + Vector3.forward * 1);
			}
		}
	}
}