using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	[RequireComponent(typeof(DamageReaction))]
	public class DamagePopupFeedback : MonoBehaviour, IDamageReaction
	{
		private UIManager uiManager;

		[Inject]
		public void Construct(UIManager uiManager)
		{
			this.uiManager = uiManager;
		}

		public void OnDamaged(DamageInfo damageInfo)
		{
			uiManager?.PopDamage(damageInfo, transform.position + Vector3.forward * 1);
		}
	}
}
