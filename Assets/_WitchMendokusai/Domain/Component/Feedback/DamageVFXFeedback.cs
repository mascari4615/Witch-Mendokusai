using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	[RequireComponent(typeof(DamageReaction))]
	public class DamageVFXFeedback : MonoBehaviour, IDamageReaction, IDeathReaction
	{
		[SerializeField] private GameObject hitEffectPrefab;
		[SerializeField] private GameObject dieEffectPrefab;

		private ObjectPoolManager objectPoolManager;
		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(ObjectPoolManager objectPoolManager, PlayerProvider playerProvider)
		{
			this.objectPoolManager = objectPoolManager;
			this.playerProvider = playerProvider;
		}

		public void OnDamaged(DamageInfo damageInfo)
		{
			if (hitEffectPrefab == null) return;

			GameObject hitEffect = objectPoolManager.Spawn(hitEffectPrefab);

			// 플레이어를 향하는 방향으로 약간 이동해서 이펙트 생성 (기존 MonsterObject 로직)
			Vector3 offset = Vector3.zero;
			if (playerProvider.Current != null)
			{
				offset = Vector3.Normalize(playerProvider.Current.transform.position - transform.position) * 0.5f;
			}

			hitEffect.transform.position = transform.position + offset;
			hitEffect.SetActive(true);
		}

		public void OnDeath()
		{
			if (dieEffectPrefab == null) return;

			GameObject dieEffect = objectPoolManager.Spawn(dieEffectPrefab);

			Vector3 offset = Vector3.zero;
			if (playerProvider.Current != null)
			{
				offset = Vector3.Normalize(playerProvider.Current.transform.position - transform.position) * 0.5f;
			}

			dieEffect.transform.position = transform.position + offset;
			dieEffect.SetActive(true);
		}
	}
}
