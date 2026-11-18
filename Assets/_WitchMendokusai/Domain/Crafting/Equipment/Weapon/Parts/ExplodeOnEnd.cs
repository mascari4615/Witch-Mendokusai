using System.Collections;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class ExplodeOnEnd : SkillComponent
	{
		[SerializeField] private GameObject explosionPrefab;
		[SerializeField, Min(0f)] private float fuseSeconds = 0f;
		[SerializeField] private bool explodeOnContact = false;
		[SerializeField, Tooltip("터질 때 공용 물보라 효과음 (CombatSfx WeaponSplash)")] private bool playSplashSfx = true;

		private int damageBonus;
		private SkillObject owner;
		private bool exploded;
		private Coroutine fuse;

		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(ObjectPoolManager objectPoolManager)
		{
			this.objectPoolManager = objectPoolManager;
		}

		public override void InitContext(SkillObject skillObject)
		{
			owner = skillObject;
			exploded = false;
			damageBonus = 0;
		}

		public void SetDamageBonus(int damageBonus)
		{
			this.damageBonus = damageBonus;
		}

		private void OnEnable()
		{
			if (fuseSeconds > 0f)
				fuse = StartCoroutine(RunFuse());
		}

		private void OnDisable()
		{
			if (fuse != null)
				StopCoroutine(fuse);
		}

		private IEnumerator RunFuse()
		{
			yield return new WaitForSeconds(fuseSeconds);
			Explode();
		}

		private void OnTriggerEnter(Collider other)
		{
			if (explodeOnContact == false)
				return;
			if (other.TryGetComponent(out UnitHealth unitHealth) && unitHealth.Unit is MonsterObject)
				Explode();
		}

		public void Explode()
		{
			if (exploded)
				return;
			exploded = true;

			if (playSplashSfx)
				CombatSfx.Play(CombatSfxEvent.WeaponSplash, transform.position);
			GameObject g = objectPoolManager.Spawn(explosionPrefab, transform.position, Quaternion.identity);
			g.transform.localScale = explosionPrefab.transform.localScale * transform.lossyScale.x;
			if (g.TryGetComponent(out SkillObject skillObject) && owner != null)
				skillObject.InitContext(owner.Context);
			DamagingObject damagingObject = g.GetComponentInChildren<DamagingObject>(true);
			if (damagingObject != null)
				damagingObject.SetDamageBonus(damageBonus);
			g.SetActive(true);

			gameObject.SetActive(false);
		}
	}
}
