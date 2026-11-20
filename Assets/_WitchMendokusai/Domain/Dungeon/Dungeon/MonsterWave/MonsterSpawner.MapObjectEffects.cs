using UnityEngine;

namespace WitchMendokusai
{
	// 맵 오브젝트 상호작용의 비용과 효과. 효과를 전부 낼 수 있을 때만 비용과 사용 횟수 소비
	public partial class MonsterSpawner
	{
		private bool TryActivateMapObject(MapObjectSite site)
		{
			if (dungeonManager.IsDungeon == false || playerProvider.CurrentObject == null || playerProvider.CurrentObject.IsAlive == false)
				return false;
			MapObjectData data = site.Data;
			foreach (MapObjectEffect effect in data.Effects)
			{
				if (CanApplyMapObjectEffect(effect, site.transform.position) == false)
				{
					Debug.Log($"[MapObject] {data.name} 수락 보류: {effect.Type} 불가 (상한 또는 지면)");
					return false;
				}
			}
			if (TryPayMapObjectCost(site) == false)
				return false;
			foreach (MapObjectEffect effect in data.Effects)
				ApplyMapObjectEffect(effect, site.transform.position);
			Debug.Log($"[MapObject] {data.name} 사용 {site.UsedCount + 1}회");
			return true;
		}

		// 위험 수락은 지불 없음. 그 외 비용 종류는 2단계 (MapObjectRules.IsCostSupported)
		private bool TryPayMapObjectCost(MapObjectSite site)
		{
			switch (site.Data.CostType)
			{
				case MapObjectCostType.None:
				case MapObjectCostType.RiskAccept:
					return true;
				default:
					throw new System.InvalidOperationException($"{site.Data.name}: 비용 {site.Data.CostType} 미지원");
			}
		}

		private bool CanApplyMapObjectEffect(MapObjectEffect effect, Vector3 position)
		{
			switch (effect.Type)
			{
				case MapObjectEffectType.SpawnElite:
					SurvivalRunRules rules = dungeonManager.RunRules;
					return WaveDensityRules.AllowedCount(SpawnClass.Priority, CurrentMonsterCount, effect.Count, rules.EnemySoftCap, rules.EnemyHardCap) == effect.Count
						&& TryMapObjectGround(EliteSpawnPosition(position), MapObjectSettings.Load(), out _);
				case MapObjectEffectType.SpawnChest:
					return true;
				default:
					throw new System.InvalidOperationException($"맵 오브젝트 효과 {effect.Type} 처리 없음");
			}
		}

		private void ApplyMapObjectEffect(MapObjectEffect effect, Vector3 position)
		{
			switch (effect.Type)
			{
				case MapObjectEffectType.SpawnElite:
					SpawnMapObjectElite(effect, position);
					return;
				case MapObjectEffectType.SpawnChest:
					SpawnMapObjectChest(effect, position);
					return;
				default:
					throw new System.InvalidOperationException($"맵 오브젝트 효과 {effect.Type} 처리 없음");
			}
		}

		// 표식 너머 (주인공 반대쪽) 상호작용 거리만큼. 처치 보상은 기존 정예 경로 (MakeElite, GameLogic.SpawnEliteReward)
		private void SpawnMapObjectElite(MapObjectEffect effect, Vector3 position)
		{
			MapObjectSettings settings = MapObjectSettings.Load();
			Monster monster = effect.Monster != null ? effect.Monster : FindGuaranteedEliteMonster(settings);
			EliteProfile profile = eventTable != null ? eventTable.Elite : settings.FallbackElite;
			if (TryMapObjectGround(EliteSpawnPosition(position), settings, out Vector3 ground) == false)
				throw new System.InvalidOperationException("정예 자리 지면 없음. CanApplyMapObjectEffect 검사 뒤라 생기면 안 됨");
			for (int i = 0; i < effect.Count; i++)
				StartCoroutine(SpawnMonsterAt(monster, ground, profile, effect.GuaranteedChest));
		}

		private Vector3 EliteSpawnPosition(Vector3 position)
		{
			Vector3 away = position - playerProvider.Current.transform.position;
			away.y = 0f;
			if (away.sqrMagnitude < Mathf.Epsilon)
				away = Vector3.forward;
			return position + away.normalized * PlayerInteraction.InteractionDistance;
		}

		// 사건표의 첫 확정 상자 정예. 없으면 설정의 대체 정예
		private Monster FindGuaranteedEliteMonster(MapObjectSettings settings)
		{
			if (eventTable != null)
				foreach (SurvivalEventEntry entry in eventTable.Events)
					if (entry.Type == SurvivalEventType.Elite && entry.GuaranteedChest && entry.Monster != null)
						return entry.Monster;
			return settings.FallbackEliteMonster;
		}

		// 정예 상자와 같은 생성 (GameLogic.SpawnChest). 접촉하면 기존 상자 선택 흐름
		private void SpawnMapObjectChest(MapObjectEffect effect, Vector3 position)
		{
			for (int i = 0; i < effect.Count; i++)
				gameLogic.SpawnChest(position);
		}
	}
}
