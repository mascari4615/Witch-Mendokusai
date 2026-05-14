using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UnitHealth))]
	public class DamagePopupFeedback : MonoBehaviour
	{
		private UnitHealth health;
		private UIManager uiManager;

		[Inject]
		public void Construct(UIManager uiManager)
		{
			this.uiManager = uiManager;
		}

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
			uiManager?.PopDamage(damageInfo, transform.position + Vector3.forward * 1);
		}
	}
}
