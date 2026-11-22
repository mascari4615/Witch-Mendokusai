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
		[SerializeField, Min(0f), Tooltip("바닥 잔해를 날리는 반경, 미터 (크기 배율이 곱해짐). 0 이면 안 날림")] private float debrisPushRadius = 2f;
		[SerializeField, Tooltip("바닥 기름 웅덩이에 불을 붙임")] private bool igniteGoop;
		[SerializeField, Tooltip("바닥 물 웅덩이에 감전을 퍼뜨림")] private bool shockGoop;

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
			AffectRemains();

			gameObject.SetActive(false);
		}

		private void AffectRemains()
		{
			RemainsWorld remains = RemainsWorld.Current;
			if (remains == null)
				return;
			float radius = debrisPushRadius * transform.lossyScale.x;
			if (radius > 0f)
				remains.Blast(transform.position, radius, remains.Settings.BlastPushSpeed, remains.Settings.BlastPushLift, igniteGoop);
			if (shockGoop)
				remains.Goop.Shock(transform.position, radius);
		}
	}
}
