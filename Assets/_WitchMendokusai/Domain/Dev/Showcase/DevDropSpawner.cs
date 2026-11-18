using UnityEngine;

namespace WitchMendokusai
{
	// 바닥 아이템 하나를 정확한 자리에. 진열장과 drop 명령이 같이 씀
	public sealed class DevDropSpawner
	{
		private readonly ObjectPoolManager objectPoolManager;
		private readonly ResourceManager resourceManager;
		private readonly GameLogic gameLogic;
		private BreakableSettings breakableSettings;

		public DevDropSpawner(ObjectPoolManager objectPoolManager, ResourceManager resourceManager, GameLogic gameLogic)
		{
			this.objectPoolManager = objectPoolManager;
			this.resourceManager = resourceManager;
			this.gameLogic = gameLogic;
		}

		// 냥 동전 프리팹 정본은 부서지는 물건 설정
		private BreakableSettings BreakableSettings
		{
			get
			{
				if (breakableSettings == null)
					breakableSettings = Resources.Load<BreakableSettings>(nameof(BreakableSettings));
				if (breakableSettings == null)
					throw new System.InvalidOperationException($"Resources/{nameof(BreakableSettings)} 없음");
				return breakableSettings;
			}
		}

		// 경험치는 보석 상한에 닿으면 기존 보석에 합쳐져 null
		public GameObject Spawn(DevShowcaseDropType type, Vector3 position, int nyangAmount)
		{
			switch (type)
			{
				case DevShowcaseDropType.Heal:
					return Place(resourceManager.HealObjectPrefab.gameObject, position);
				case DevShowcaseDropType.Magnet:
					return Place(resourceManager.MagnetObjectPrefab.gameObject, position);
				case DevShowcaseDropType.Freeze:
					return Place(resourceManager.FreezeObjectPrefab.gameObject, position);
				case DevShowcaseDropType.ClearScreen:
					return Place(resourceManager.ClearScreenObjectPrefab.gameObject, position);
				case DevShowcaseDropType.Chest:
					return Place(resourceManager.SurvivalRunRules.ChestPrefab.gameObject, position);
				case DevShowcaseDropType.Nyang:
				{
					GameObject coin = objectPoolManager.Spawn(BreakableSettings.NyangPrefab.gameObject, position);
					coin.GetComponent<NyangObject>().Init(nyangAmount);
					coin.SetActive(true);
					return coin;
				}
				case DevShowcaseDropType.Exp:
				{
					ExpObject expObject = gameLogic.SpawnExpOrb(position);
					if (expObject == null)
						return null;
					expObject.transform.position = position;
					return expObject.gameObject;
				}
				default:
					throw new System.ArgumentOutOfRangeException(nameof(type), type, null);
			}
		}

		private GameObject Place(GameObject prefab, Vector3 position)
		{
			GameObject placed = objectPoolManager.Spawn(prefab, position);
			placed.SetActive(true);
			return placed;
		}
	}
}
