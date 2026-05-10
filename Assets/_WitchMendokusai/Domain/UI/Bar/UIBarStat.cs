using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public abstract class UIBarStat<T> : MonoBehaviour where T : System.Enum
	{
		[SerializeField] private Image bar;
		[SerializeField] private TextMeshProUGUI text;

		[SerializeField] private T curType;
		[SerializeField] private T maxType;
		[SerializeField] private T textType;

		[SerializeField] private float lerpSpeed = 5f;
		[SerializeField] private bool isExpType;
		private Coroutine routine;

		private Stat<T> currentStat;

		private int Text => currentStat[textType];
		private int Cur => currentStat[curType];
		private int Max => currentStat[maxType];

		protected void BindStat(Stat<T> stat)
		{
			if (currentStat != null)
			{
				currentStat.RemoveListener(textType, UpdateUI);
				currentStat.RemoveListener(curType, UpdateUI);
				currentStat.RemoveListener(maxType, UpdateUI);
			}

			currentStat = stat;

			if (stat == null)
				return;

			stat.AddListener(textType, UpdateUI);
			stat.AddListener(curType, UpdateUI);
			stat.AddListener(maxType, UpdateUI);

			UpdateUI();
		}

		public void UpdateUI()
		{
			if (gameObject.activeSelf == false)
			{
				float target = (Max == 0) ? 0 : ((float)Cur / Max);
				bar.fillAmount = target;
				text.text = Text.ToString();
			}
			else
			{
				if (routine != null)
					StopCoroutine(routine);
				routine = StartCoroutine(UpdateBarLerp());

				text.text = Text.ToString();
			}
		}

		private IEnumerator UpdateBarLerp()
		{
			float t = 0;
			float origin = bar.fillAmount;
			float target = (Max == 0) ? 0 : ((float)Cur / Max);

			if (isExpType)
				if (origin > target)
					origin = 0;

			if (target > 1)
				target = 1;
			else if (target < 0)
				target = 0;

			if (origin == target)
			{
				bar.fillAmount = target;
				yield break;
			}

			while (true)
			{
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

		private void OnDisable()
		{
			StopAllCoroutines();
		}
	}
}
