using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class ExpManager : MonoBehaviour
	{
		private const int REQUIRE_EXP_INCREMENT = 30;

		[SerializeField] private GameObject levelUpEffect;

		private PlayerProvider playerProvider;
		private GameEventManager gameEventManager;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, GameEventManager gameEventManager, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.gameEventManager = gameEventManager;
			this.objectPoolManager = objectPoolManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		private void Start()
		{
			PlayerStat.AddListener(UnitStatType.EXP_CUR, UpdateLevel);
			Init();
		}

		public void Init()
		{
			PlayerStat[UnitStatType.EXP_MAX] = REQUIRE_EXP_INCREMENT;
			PlayerStat[UnitStatType.EXP_CUR] = 0;
			PlayerStat[UnitStatType.LEVEL_CUR] = 0;
			// Debug.Log(nameof(Init) + PlayerStat[StatType.EXP_CUR] + " / " + PlayerStat[StatType.EXP_MAX]);
		}

		public void UpdateLevel()
		{
			// Debug.Log("Try Level Up" + PlayerStat[StatType.EXP_CUR] + " / " + PlayerStat[StatType.EXP_MAX]);
			if (PlayerStat[UnitStatType.EXP_CUR] >= PlayerStat[UnitStatType.EXP_MAX])
			{
				// Debug.Log("Level Up");
				RuntimeManager.PlayOneShot("event:/SFX/LevelUp", transform.position);

				PlayerStat[UnitStatType.EXP_CUR] -= PlayerStat[UnitStatType.EXP_MAX];
				PlayerStat[UnitStatType.EXP_MAX] += REQUIRE_EXP_INCREMENT;
				PlayerStat[UnitStatType.LEVEL_CUR]++;
				
				gameEventManager.Raise(GameEventType.OnLevelUp);

				GameObject l = objectPoolManager.Spawn(levelUpEffect);
				l.transform.position = playerProvider.Current.transform.position;
				l.SetActive(true);
			}
		}
	}
}