using UnityEngine;
using FMODUnity;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UnitHealth))]
	public class DamageAudioFeedback : MonoBehaviour
	{
		[SerializeField] private string hitEventPath = "event:/SFX/Monster/Hit";
		[SerializeField] private string dieEventPath = "event:/SFX/Monster/Die";
		
		private UnitHealth health;

		private void Awake()
		{
			health = GetComponent<UnitHealth>();
		}

		private void OnEnable()
		{
			health.OnTakeDamage += PlayHitSound;
			health.OnDied += PlayDieSound;
		}

		private void OnDisable()
		{
			health.OnTakeDamage -= PlayHitSound;
			health.OnDied -= PlayDieSound;
		}

		private void PlayHitSound(DamageInfo damageInfo)
		{
			if (string.IsNullOrEmpty(hitEventPath) == false)
			{
				RuntimeManager.PlayOneShot(hitEventPath, transform.position);
			}
		}

		private void PlayDieSound()
		{
			if (string.IsNullOrEmpty(dieEventPath) == false)
			{
				RuntimeManager.PlayOneShot(dieEventPath, transform.position);
			}
		}
	}
}