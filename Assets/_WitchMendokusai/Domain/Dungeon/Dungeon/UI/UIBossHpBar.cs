using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 현재 보스 체력 표시. 보스 등장 사건으로 대상을 찾고 처치 뒤 숨김
	// 등장과 퇴장은 실제 시간으로 흐려지며 살짝 미끄러짐 (히트스톱 중에도 진행)
	public class UIBossHpBar : MonoBehaviour
	{
		[SerializeField] private Image hpBar;
		[SerializeField] private CanvasGroup canvasGroup;
		[SerializeField] private TextMeshProUGUI hpBarText;
		[SerializeField] private TextMeshProUGUI nameText;
		[SerializeField, Min(0f), Tooltip("나타나는 데 걸리는 시간 (초, 실제 시간)")] private float showSeconds = 0.35f;
		[SerializeField, Min(0f), Tooltip("사라지는 데 걸리는 시간 (초, 실제 시간)")] private float hideSeconds = 0.5f;
		[SerializeField, Tooltip("숨은 상태의 자리. 제자리에서 이만큼 떨어진 곳에서 미끄러져 들어옴 (캔버스 단위)")] private Vector2 hiddenOffset = new(0f, 24f);

		private MonsterObject target;
		private int shownCurrent = -1;
		private int shownMax = -1;
		private RectTransform slideTarget;
		private Vector2 restPosition;
		// 0 숨음, 1 다 보임
		private float visibility;

		private void Awake()
		{
			slideTarget = (RectTransform)canvasGroup.transform;
			restPosition = slideTarget.anchoredPosition;
		}

		private void OnEnable()
		{
			EventBusBridge.Subscribe<BossSpawnedEvent>(OnBossSpawned);
			ApplyVisibility(0f);
			FindBoss();
		}

		private void OnDisable()
		{
			EventBusBridge.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
			target = null;
			ApplyVisibility(0f);
		}

		private void OnBossSpawned(BossSpawnedEvent evt) => FindBoss();

		private void FindBoss()
		{
			target = null;
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
			bool show = UpdateTarget();
			float seconds = show ? showSeconds : hideSeconds;
			float goal = show ? 1f : 0f;
			if (Mathf.Approximately(visibility, goal))
				return;
			ApplyVisibility(seconds > 0f ? Mathf.MoveTowards(visibility, goal, Time.unscaledDeltaTime / seconds) : goal);
		}

		// 대상의 체력을 읽어 막대를 갱신. 보여야 하면 true
		private bool UpdateTarget()
		{
			if (target == null)
				return false;

			int current = target.UnitStat[UnitStatType.HP_CUR];
			int max = target.UnitStat[UnitStatType.HP_MAX];
			bool gone = target.gameObject.activeInHierarchy == false || current <= 0;
			if (gone)
			{
				// 빈 막대로 사라짐
				target = null;
				hpBar.fillAmount = 0f;
				if (shownMax > 0)
					hpBarText.text = $"0 / {shownMax}";
				return false;
			}
			if (max <= 0)
				return false;

			if (current != shownCurrent || max != shownMax)
			{
				shownCurrent = current;
				shownMax = max;
				hpBarText.text = $"{current} / {max}";
				hpBar.fillAmount = (float)current / max;
			}
			return true;
		}

		private void ApplyVisibility(float value)
		{
			visibility = value;
			float eased = Mathf.SmoothStep(0f, 1f, value);
			canvasGroup.alpha = eased;
			slideTarget.anchoredPosition = restPosition + hiddenOffset * (1f - eased);
		}
	}
}
