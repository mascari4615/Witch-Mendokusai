using UnityEngine;

namespace WitchMendokusai
{
	// 맵 오브젝트 상호작용의 비용과 효과. 효과를 전부 낼 수 있고 비용을 낼 수 있을 때만 소비
	// 머무름 오브젝트는 시작 효과 (OnActivate) 를 상호작용 때, 완료 효과 (OnHoldComplete) 를 머무름이 끝날 때
	public partial class MonsterSpawner : IMapObjectHost
	{
		bool IMapObjectHost.TryActivate(MapObjectSite site)
		{
			if (dungeonManager.IsDungeon == false || playerProvider.CurrentObject == null || playerProvider.CurrentObject.IsAlive == false)
				return false;
			MapObjectData data = site.Data;
			if (TryFindBlockedEffect(data, MapObjectEffectTiming.OnActivate, site.transform.position, out string blocked))
			{
				Debug.Log($"[MapObject] {data.name} 수락 보류: {blocked}");
				PopMapObjectText(site, blocked);
				return false;
			}
			if (CanAffordMapObject(site) == false)
			{
				PopMapObjectText(site, ShortCostText(data.CostType));
				return false;
			}
			PayMapObjectCost(site);
			ApplyMapObjectEffects(site, MapObjectEffectTiming.OnActivate);
			if (data.HasHold)
				PopMapObjectText(site, MapObjectSettings.Load().HoldStartText);
			Debug.Log($"[MapObject] {data.name} 사용 {site.UsedCount + 1}회");
			return true;
		}

		bool IMapObjectHost.CanAfford(MapObjectSite site) => CanAffordMapObject(site);

		string IMapObjectHost.DescribeCost(MapObjectSite site)
		{
			MapObjectSettings settings = MapObjectSettings.Load();
			if (site.IsHolding)
				return string.Format(settings.HoldProgressFormat, Mathf.FloorToInt(site.HoldProgress), Mathf.CeilToInt(site.Data.HoldSeconds));
			switch (site.Data.CostType)
			{
				case MapObjectCostType.None:
					return string.Empty;
				case MapObjectCostType.RiskAccept:
					return settings.RiskCostText;
				case MapObjectCostType.HealthPercent:
					return string.Format(settings.HealthCostFormat, MapObjectHealthCost(site));
				case MapObjectCostType.RunNyang:
					return string.Format(settings.NyangCostFormat, MapObjectNyangCost(site), dungeonManager.RunNyang);
				default:
					throw new System.InvalidOperationException($"{site.Data.name}: 비용 {site.Data.CostType} 미지원");
			}
		}

		// 상태, 진행 초, 판 안 냥, 체력이 바뀌면 달라짐
		int IMapObjectHost.PromptStamp(MapObjectSite site)
		{
			PlayerObject player = playerProvider.CurrentObject;
			return System.HashCode.Combine(site.Version, Mathf.FloorToInt(site.HoldProgress), dungeonManager.RunNyang, player.UnitStat[UnitStatType.HP_CUR], player.UnitStat[UnitStatType.HP_MAX]);
		}

		private bool CanAffordMapObject(MapObjectSite site)
		{
			switch (site.Data.CostType)
			{
				case MapObjectCostType.None:
				case MapObjectCostType.RiskAccept:
					return true;
				case MapObjectCostType.HealthPercent:
					return MapObjectRules.CanPayHealth(playerProvider.CurrentObject.UnitStat[UnitStatType.HP_CUR], MapObjectHealthCost(site));
				case MapObjectCostType.RunNyang:
					return dungeonManager.RunNyang >= MapObjectNyangCost(site);
				default:
					throw new System.InvalidOperationException($"{site.Data.name}: 비용 {site.Data.CostType} 미지원");
			}
		}

		private void PayMapObjectCost(MapObjectSite site)
		{
			switch (site.Data.CostType)
			{
				case MapObjectCostType.None:
				case MapObjectCostType.RiskAccept:
					return;
				case MapObjectCostType.HealthPercent:
					// 피격이 아닌 지불. 무적, 반격, 피격 소리와 증폭 없음
					playerProvider.CurrentObject.Health.SpendHealth(MapObjectHealthCost(site));
					return;
				case MapObjectCostType.RunNyang:
					if (dungeonManager.TrySpendRunNyang(MapObjectNyangCost(site)) == false)
						throw new System.InvalidOperationException($"{site.Data.name}: CanAfford 뒤 냥 지불 실패");
					return;
				default:
					throw new System.InvalidOperationException($"{site.Data.name}: 비용 {site.Data.CostType} 미지원");
			}
		}

		private int MapObjectHealthCost(MapObjectSite site) => MapObjectRules.HealthCost(playerProvider.CurrentObject.UnitStat[UnitStatType.HP_MAX], site.Data.CostValue, site.CostMultiplier);

		private static int MapObjectNyangCost(MapObjectSite site) => MapObjectRules.ScaledCost(site.Data.CostValue, site.CostMultiplier);

		private static string ShortCostText(MapObjectCostType costType)
		{
			MapObjectSettings settings = MapObjectSettings.Load();
			return costType == MapObjectCostType.HealthPercent ? settings.ShortHealthText : settings.ShortNyangText;
		}

		// 이 시점 효과 중 지금 못 내는 것. 있으면 true 와 반응 글자
		private bool TryFindBlockedEffect(MapObjectData data, MapObjectEffectTiming timing, Vector3 position, out string reason)
		{
			foreach (MapObjectEffect effect in data.Effects)
			{
				if (IsEffectDue(data, effect, timing) == false)
					continue;
				reason = BlockedReason(effect, position);
				if (reason != null)
					return true;
			}
			reason = null;
			return false;
		}

		// 머무름 없는 오브젝트는 모든 효과가 상호작용 때
		private static bool IsEffectDue(MapObjectData data, MapObjectEffect effect, MapObjectEffectTiming timing) => data.HasHold == false || effect.Timing == timing;

		private string BlockedReason(MapObjectEffect effect, Vector3 position)
		{
			MapObjectSettings settings = MapObjectSettings.Load();
			switch (effect.Type)
			{
				case MapObjectEffectType.SpawnElite:
					SurvivalRunRules rules = dungeonManager.RunRules;
					bool allowed = WaveDensityRules.AllowedCount(SpawnClass.Priority, CurrentMonsterCount, effect.Count, rules.EnemySoftCap, rules.EnemyHardCap) == effect.Count
						&& TryMapObjectGround(EliteSpawnPosition(position), settings, out _);
					return allowed ? null : settings.BusyText;
				case MapObjectEffectType.Heal:
					PlayerObject player = playerProvider.CurrentObject;
					return player.UnitStat[UnitStatType.HP_CUR] >= player.UnitStat[UnitStatType.HP_MAX] ? settings.HealthFullText : null;
				case MapObjectEffectType.SpawnChest:
				case MapObjectEffectType.RevealMapObjects:
				// 조각은 막대가 찼거나 확정한 뒤에도 스탯을 줌 (고유 효과). 막지 않음
				case MapObjectEffectType.MasteryShard:
					return null;
				default:
					throw new System.InvalidOperationException($"맵 오브젝트 효과 {effect.Type} 처리 없음");
			}
		}

		private void ApplyMapObjectEffects(MapObjectSite site, MapObjectEffectTiming timing)
		{
			MapObjectSettings settings = MapObjectSettings.Load();
			foreach (MapObjectEffect effect in site.Data.Effects)
			{
				if (IsEffectDue(site.Data, effect, timing) == false)
					continue;
				if (effect.FailPercent > 0f)
				{
					bool won = MapObjectRules.RollSucceeds(effect.FailPercent, mapObjectRollRandom.NextDouble());
					PopMapObjectText(site, won ? settings.LuckWinText : settings.LuckFailText);
					Debug.Log($"[MapObject] {site.Data.name} {effect.Type} 굴림 {(won ? "당첨" : "꽝")} (꽝 {effect.FailPercent}%)");
					if (won == false)
						continue;
				}
				ApplyMapObjectEffect(site, effect);
			}
		}

		private void ApplyMapObjectEffect(MapObjectSite site, MapObjectEffect effect)
		{
			Vector3 position = site.transform.position;
			switch (effect.Type)
			{
				case MapObjectEffectType.SpawnElite:
					SpawnMapObjectElite(effect, position);
					return;
				case MapObjectEffectType.SpawnChest:
					// 정예 상자와 같은 생성. 접촉하면 기존 상자 선택 흐름
					for (int i = 0; i < effect.Count; i++)
						gameLogic.SpawnChest(position);
					return;
				case MapObjectEffectType.Heal:
					PlayerObject player = playerProvider.CurrentObject;
					for (int i = 0; i < effect.Count; i++)
						player.ReceiveHeal(MapObjectRules.PercentOfMaxHealth(player.UnitStat[UnitStatType.HP_MAX], effect.Value));
					return;
				case MapObjectEffectType.RevealMapObjects:
					RevealMapObjects(site, effect.Value);
					return;
				case MapObjectEffectType.MasteryShard:
					AddMasteryShard(site, effect);
					return;
				default:
					throw new System.InvalidOperationException($"맵 오브젝트 효과 {effect.Type} 처리 없음");
			}
		}

		// 먹을 때마다 막대 스탯 (고유 효과), 그리고 막대 진행 (확정 변신 숙련). 숙련 없는 인물 판에는 조각이 안 놓임
		private void AddMasteryShard(MapObjectSite site, MapObjectEffect effect)
		{
			DollMasteryData data = DollMasteryRun.Data;
			DollMasteryTrackData track = data.Tracks[effect.MasteryTrack];
			int shards = Mathf.RoundToInt(effect.Value);
			if (track.PickupStat != UnitStatType.NONE)
				playerProvider.CurrentObject.UnitStat[track.PickupStat] += track.PickupAmount * shards;
			if (DollMasteryRun.AddShard(effect.MasteryTrack, shards))
				PopMapObjectText(site, string.Format(data.ReadyTextFormat, track.DisplayName));
		}

		private void RevealMapObjects(MapObjectSite source, float radius)
		{
			int revealed = RevealMapObjectSites(source, source.transform.position, radius);
			PopMapObjectText(source, string.Format(MapObjectSettings.Load().RevealFormat, revealed));
			Debug.Log($"[MapObject] {source.Data.name} 드러냄 {revealed}개");
		}

		// 표식 너머 (주인공 반대쪽) 상호작용 거리만큼. 처치 보상은 기존 정예 경로 (MakeElite, GameLogic.SpawnEliteReward)
		private void SpawnMapObjectElite(MapObjectEffect effect, Vector3 position)
		{
			MapObjectSettings settings = MapObjectSettings.Load();
			Monster monster = effect.Monster != null ? effect.Monster : FindGuaranteedEliteMonster(settings);
			EliteProfile profile = eventTable != null ? eventTable.Elite : settings.FallbackElite;
			if (TryMapObjectGround(EliteSpawnPosition(position), settings, out Vector3 ground) == false)
				throw new System.InvalidOperationException("정예 자리 지면 없음. BlockedReason 검사 뒤라 생기면 안 됨");
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

		// 오브젝트 머리 위 반응 글자 (수락 보류, 꽝, 완료). 기존 떠오르는 글자 경로
		private static void PopMapObjectText(MapObjectSite site, string text)
		{
			if (string.IsNullOrEmpty(text) || UIManager.TryGetExistingInstance(out UIManager uiManager) == false)
				return;
			uiManager.PopText(text, TextType.Warning, site.transform.position + Vector3.up * MapObjectSettings.Load().TextHeight);
		}
	}
}
