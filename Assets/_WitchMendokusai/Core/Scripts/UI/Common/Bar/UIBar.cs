using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIBar : MonoBehaviour
	{
		[SerializeField] private Image bar;
		[SerializeField] private TextMeshProUGUI text;

		[SerializeField] private UnitStatType curType;
		[SerializeField] private UnitStatType maxType;
		[SerializeField] private UnitStatType textType;

		[SerializeField] private float lerpSpeed = 5f;
		[SerializeField] private bool isExpBar;
		private Coroutine routine;

		private UnitStat PlayerStat => Player.Instance.UnitStat;
		private int Text => PlayerStat[textType];
		private int Cur => PlayerStat[curType];
		private int Max => PlayerStat[maxType];

		private void Start()
		{
			PlayerStat.AddListener(textType, UpdateUI);
			PlayerStat.AddListener(curType, UpdateUI);
			PlayerStat.AddListener(maxType, UpdateUI);

			GameEventManager.Instance.RegisterCallback(GameEventType.OnDungeonStart, UpdateUI);

			UpdateUI();
		}

		public void UpdateUI()
		{
			// Debug.Log(gameObject.name + "UpdateUI");
			if (routine != null)
				StopCoroutine(routine);
			routine = StartCoroutine(UpdateBarLerp());

			text.text = Text.ToString();
		}

		private IEnumerator UpdateBarLerp()
		{
			// Debug.Log(gameObject.name + "UpdateBarLerp");
			float t = 0;
			float origin = bar.fillAmount;
			float target = (float)Cur / Max;

			if (isExpBar)
				if (origin > target)
					origin = 0;

			while (true)
			{
				// Debug.Log(gameObject.name + "UpdateBarLerp Tick");
				bar.fillAmount = Mathf.Lerp(origin, target, t);
				t += Time.deltaTime * lerpSpeed;
				yield return null;

				if (t >= 1)
				{
					bar.fillAmount = target;
					break;
				}
			}
		}
	}
}