using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 적 정지 바닥 아이템. 주운 순간 화면 안 몬스터 각각에 얼음 상태이상. 나중에 생긴 몬스터는 영향 없음
	public class FreezeObject : GameItemObject
	{
		private static readonly Plane[] viewPlanes = new Plane[6];
		private static readonly List<GameObject> targets = new();

		protected override void OnEffect()
		{
			CombatSfx.Play(CombatSfxEvent.Freeze);
			FreezeVisibleMonsters(Camera.main, resourceManager.SurvivalRunRules.FreezeSeconds);
			if (SurvivalFeel.Instance != null)
				SurvivalFeel.Instance.OnFreezePicked(transform.position);
		}

		private void FreezeVisibleMonsters(Camera camera, float seconds)
		{
			CombatView.CalculatePlanes(camera, viewPlanes);
			targets.Clear();
			targets.AddRange(ObjectBufferManager.GetObjects(ObjectType.Monster));
			foreach (GameObject target in targets)
			{
				// 몬스터 목록은 MonsterObject 만 등록
				MonsterObject monster = target.GetComponent<MonsterObject>();
				if (monster.IsAlive == false)
					continue;
				if (GeometryUtility.TestPlanesAABB(viewPlanes, monster.SpriteRenderer.bounds) == false)
					continue;
				UnitStatus.Apply(monster, UnitStatusType.Freeze, playerProvider.CurrentObject, seconds);
			}
			targets.Clear();
		}
	}
}
