using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class ExpManager : MonoBehaviour
	{
		[SerializeField] private GameObject levelUpEffect;

		private PlayerProvider playerProvider;
		private GameEventManager gameEventManager;
		private ObjectPoolManager objectPoolManager;
		private ResourceManager resourceManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, GameEventManager gameEventManager, ObjectPoolManager objectPoolManager, ResourceManager resourceManager)
		{
			this.playerProvider = playerProvider;
			this.gameEventManager = gameEventManager;
			this.objectPoolManager = objectPoolManager;
			this.resourceManager = resourceManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;
		private SurvivalRunRules RunRules => resourceManager.SurvivalRunRules;

		private void Start()
		{
			PlayerStat.AddListener(UnitStatType.EXP_CUR, UpdateLevel);
			Init();
		}

		public void Init()
		{
			PlayerStat[UnitStatType.EXP_MAX] = ExpCurveRules.Required(RunRules.ExpCurveBands, RunRules.ExpBaseRequired, 0);
			PlayerStat[UnitStatType.EXP_CUR] = 0;
			PlayerStat[UnitStatType.LEVEL_CUR] = 0;
			// Debug.Log(nameof(Init) + PlayerStat[StatType.EXP_CUR] + " / " + PlayerStat[StatType.EXP_MAX]);
		}

		private bool isUpdatingLevel;

		public void UpdateLevel()
		{
			if (isUpdatingLevel)
				return;

			isUpdatingLevel = true;
			try
			{
				while (PlayerStat[UnitStatType.EXP_CUR] >= PlayerStat[UnitStatType.EXP_MAX])
				{
					int requiredExp = PlayerStat[UnitStatType.EXP_MAX];
					int nextLevel = PlayerStat[UnitStatType.LEVEL_CUR] + 1;
					PlayerStat[UnitStatType.EXP_MAX] = ExpCurveRules.Required(RunRules.ExpCurveBands, RunRules.ExpBaseRequired, nextLevel);
					PlayerStat[UnitStatType.LEVEL_CUR] = nextLevel;
					PlayerStat[UnitStatType.EXP_CUR] -= requiredExp;

					UiSfx.Play(UiSfxEvent.LevelUp);
					gameEventManager.Raise(GameEventType.OnLevelUp);

					GameObject effect = objectPoolManager.Spawn(levelUpEffect);
					effect.transform.position = playerProvider.Current.transform.position;
					effect.SetActive(true);
				}
			}
			finally
			{
				isUpdatingLevel = false;
			}
		}
	}
}
