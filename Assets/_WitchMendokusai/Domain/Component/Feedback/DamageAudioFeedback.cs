using UnityEngine;
using FMODUnity;

namespace WitchMendokusai
{
	[RequireComponent(typeof(DamageReaction))]
	public class DamageAudioFeedback : MonoBehaviour, IDamageReaction, IDeathReaction
	{
		[SerializeField] private string hitEventPath = "event:/SFX/Monster/Hit";
		[SerializeField] private string dieEventPath = "event:/SFX/Monster/Die";

		public void OnDamaged(DamageInfo damageInfo)
		{
			if (string.IsNullOrEmpty(hitEventPath) == false)
			{
				RuntimeManager.PlayOneShot(hitEventPath, transform.position);
			}
		}

		public void OnDeath()
		{
			if (string.IsNullOrEmpty(dieEventPath) == false)
			{
				RuntimeManager.PlayOneShot(dieEventPath, transform.position);
			}
		}
	}
}
