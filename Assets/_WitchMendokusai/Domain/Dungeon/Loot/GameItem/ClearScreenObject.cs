using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	// 화면 청소 바닥 아이템. 화면 안 일반 적 처치, 보스와 정예 제외. 임시 이름
	public class ClearScreenObject : GameItemObject
	{
		private static readonly Plane[] viewPlanes = new Plane[6];
		private static readonly List<GameObject> targets = new();

		protected override void OnEffect()
		{
			RuntimeManager.PlayOneShot("event:/SFX/EXP", transform.position);
			Camera camera = Camera.main;
			if (camera == null)
				return;

			GeometryUtility.CalculateFrustumPlanes(camera, viewPlanes);
			targets.Clear();
			targets.AddRange(ObjectBufferManager.GetObjects(ObjectType.Monster));
			foreach (GameObject target in targets)
			{
				if (target == null || target.activeInHierarchy == false || target.TryGetComponent(out MonsterObject monster) == false || monster.UnitData == null)
					continue;
				if (FloorItemRules.ClearsTarget(monster.UnitData.Type == MonsterType.Boss, monster.IsElite) == false)
					continue;
				if (GeometryUtility.TestPlanesAABB(viewPlanes, monster.SpriteRenderer.bounds) == false)
					continue;

				monster.ReceiveDamage(new DamageInfo
				{
					type = DamageType.Normal,
					damage = monster.UnitStat[UnitStatType.HP_CUR],
					damageSource = playerProvider.CurrentObject,
					equipmentDataId = DamageInfo.NO_DATA_ID,
					skillDataId = DamageInfo.NO_DATA_ID,
					ignoreInvincible = true,
				});
			}
			targets.Clear();

			if (SurvivalFeel.Instance != null)
				SurvivalFeel.Instance.OnClearPicked(transform.position);
		}
	}
}
