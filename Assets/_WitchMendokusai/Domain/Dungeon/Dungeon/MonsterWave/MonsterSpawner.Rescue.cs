using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	// 갇힌 사람. 마을이 기다리는 사람이 있으면 (DungeonManager.RescueWanted) 예산과 따로 판에 하나 둠
	// 전투 규칙과 예산 뽑기는 그대로. 머무름이 차면 열쇠를 이벤트로 알리고 끝 (누구인지는 마을이 앎)
	public partial class MonsterSpawner
	{
		[SerializeField, Tooltip("갇힌 사람 등장 시각 굴림의 판 시드 갈래. 예산 자리 (+1), 운 굴림 (+2) 과 겹치지 않게")]
		private int rescueSeedOffset = 3;

		private string rescueKey = string.Empty;
		// 등장 시각과 자리 굴림. 예산 오브젝트의 굴림 (mapObjectRandom) 과 따로
		private System.Random rescueRandom;

		private void PlanRescue(int seedSalt)
		{
			rescueKey = dungeonManager.RescueWanted;
			if (rescueKey.Length == 0)
				return;
			MapObjectData captive = MapObjectSettings.LoadAllData().First(data => data.Category == MapObjectCategory.Rescue);
			rescueRandom = new System.Random(RunSeed.Derive(seedSalt + rescueSeedOffset));
			float spawnTime = Mathf.Lerp(captive.SpawnTime.x, captive.SpawnTime.y, (float)rescueRandom.NextDouble());
			// 계획은 등장 시각 순. 같은 자리를 지켜 끼움
			int index = mapObjectPlans.FindIndex(plan => plan.SpawnTime > spawnTime);
			MapObjectPlan rescuePlan = new(captive, spawnTime, mapObjectPlans.Count);
			if (index < 0)
				mapObjectPlans.Add(rescuePlan);
			else
				mapObjectPlans.Insert(index, rescuePlan);
			Debug.Log($"[MapObject] 갇힌 사람 {rescueKey} {spawnTime:0}초");
		}

		private void RescueCaptive(MapObjectSite site)
		{
			EventBusBridge.Publish(new CaptiveRescuedEvent { Key = rescueKey });
			PopMapObjectText(site, MapObjectSettings.Load().RescueText);
			Debug.Log($"[MapObject] 갇힌 사람 {rescueKey} 구함");
		}
	}
}
