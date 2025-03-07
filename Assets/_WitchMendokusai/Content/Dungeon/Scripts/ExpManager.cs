using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	public class ExpManager : MonoBehaviour
	{
		private const int REQUIRE_EXP_INCREASEMENT = 30;
		
		[SerializeField] private GameObject levelUpEffect;
		
		private UnitStat PlayerStat => Player.Instance.UnitStat;

		private void Start()
		{
			PlayerStat.AddListener(UnitStatType.EXP_CUR, UpdateLevel);
			Init();
		}

		public void Init()
		{
			PlayerStat[UnitStatType.EXP_MAX] = REQUIRE_EXP_INCREASEMENT;
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
				PlayerStat[UnitStatType.EXP_MAX] += REQUIRE_EXP_INCREASEMENT;
				PlayerStat[UnitStatType.LEVEL_CUR]++;
				
				GameEventManager.Instance.Raise(GameEventType.OnLevelUp);

				GameObject l = ObjectPoolManager.Instance.Spawn(levelUpEffect);
				l.transform.position = Player.Instance.transform.position;
				l.SetActive(true);
			}
		}
	}
}