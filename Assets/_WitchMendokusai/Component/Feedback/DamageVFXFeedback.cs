using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(UnitHealth))]
	public class DamageVFXFeedback : MonoBehaviour
	{
		[SerializeField] private GameObject hitEffectPrefab;
		[SerializeField] private GameObject dieEffectPrefab;
		
		private UnitHealth health;

		private void Awake()
		{
			health = GetComponent<UnitHealth>();
		}

		private void OnEnable()
		{
			health.OnTakeDamage += PlayHitEffect;
			health.OnDied += PlayDieEffect;
		}

		private void OnDisable()
		{
			health.OnTakeDamage -= PlayHitEffect;
			health.OnDied -= PlayDieEffect;
		}

		private void PlayHitEffect(DamageInfo damageInfo)
		{
			if (hitEffectPrefab == null) return;
			
			GameObject hitEffect = ObjectPoolManager.Instance.Spawn(hitEffectPrefab);
			
			// 플레이어를 향하는 방향으로 약간 이동해서 이펙트 생성 (기존 MonsterObject 로직)
			Vector3 offset = Vector3.zero;
			if (Player.Instance != null)
			{
				offset = Vector3.Normalize(Player.Instance.transform.position - transform.position) * 0.5f;
			}
			
			hitEffect.transform.position = transform.position + offset;
			hitEffect.SetActive(true);
		}

		private void PlayDieEffect()
		{
			if (dieEffectPrefab == null) return;
			
			GameObject dieEffect = ObjectPoolManager.Instance.Spawn(dieEffectPrefab);
			
			Vector3 offset = Vector3.zero;
			if (Player.Instance != null)
			{
				offset = Vector3.Normalize(Player.Instance.transform.position - transform.position) * 0.5f;
			}
			
			dieEffect.transform.position = transform.position + offset;
			dieEffect.SetActive(true);
		}
	}
}