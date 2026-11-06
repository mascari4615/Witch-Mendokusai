using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 현재 보스 체력 표시. 보스 등장 사건으로 대상을 찾고 처치 뒤 숨김
	public class UIBossHpBar : MonoBehaviour
	{
		[SerializeField] private Image hpBar;
		[SerializeField] private CanvasGroup canvasGroup;
		[SerializeField] private TextMeshProUGUI hpBarText;
		[SerializeField] private TextMeshProUGUI nameText;

		private MonsterObject target;
		private int shownCurrent = -1;
		private int shownMax = -1;

		private void OnEnable()
		{
			EventBusBridge.Subscribe<BossSpawnedEvent>(OnBossSpawned);
			FindBoss();
		}

		private void OnDisable()
		{
			EventBusBridge.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
			target = null;
			canvasGroup.alpha = 0;
		}

		private void OnBossSpawned(BossSpawnedEvent evt) => FindBoss();

		private void FindBoss()
		{
			target = null;
			canvasGroup.alpha = 0;
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			foreach (GameObject monster in monsters)
			{
				if (monster.activeInHierarchy && monster.TryGetComponent(out MonsterObject monsterObject) && monsterObject.UnitData.Type == MonsterType.Boss)
				{
					target = monsterObject;
					nameText.text = target.UnitData.Name;
					shownCurrent = -1;
					return;
				}
			}
		}

		private void Update()
		{
			if (target == null)
				return;

			int current = target.UnitStat[UnitStatType.HP_CUR];
			int max = target.UnitStat[UnitStatType.HP_MAX];
			if (target.gameObject.activeInHierarchy == false || current <= 0 || max <= 0)
			{
				canvasGroup.alpha = 0;
				if (target.gameObject.activeInHierarchy == false || current <= 0)
					target = null;
				return;
			}

			canvasGroup.alpha = 1;
			if (current == shownCurrent && max == shownMax)
				return;

			shownCurrent = current;
			shownMax = max;
			hpBarText.text = $"{current} / {max}";
			hpBar.fillAmount = (float)current / max;
		}
	}
}
