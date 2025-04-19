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

		private int Text => GetStat()[textType];
		private int Cur => GetStat()[curType];
		private int Max => GetStat()[maxType];

		protected abstract Stat<T> GetStat();

		private void Start()
		{
			GetStat().AddListener(textType, UpdateUI);
			GetStat().AddListener(curType, UpdateUI);
			GetStat().AddListener(maxType, UpdateUI);

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
				// Debug.Log(gameObject.name + "UpdateUI");
				if (routine != null)
					StopCoroutine(routine);
				routine = StartCoroutine(UpdateBarLerp());

				text.text = Text.ToString();
			}
		}

		private IEnumerator UpdateBarLerp()
		{
			// Debug.Log(gameObject.name + "UpdateBarLerp");
			float t = 0;
			float origin = bar.fillAmount;
			float target = (Max == 0) ? 0 : ((float)Cur / Max);
			// Debug.Log(gameObject.name + " UpdateBarLerp Start: " + $"{Cur} / {Max} = [{target}]");

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
				// Debug.Log(gameObject.name + " UpdateBarLerp Tick: " + $"{origin} / {target} ({t}) = [{Mathf.Lerp(origin, target, t)}]");
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