using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public enum TextType
	{
		Normal = 0,
		Critical = 1,

		Heal = 100,

		Exp = 150,

		Warning = 1000
	}

	public class UIFloatingText : MonoBehaviour
	{
		[SerializeField] private Transform textsRoot;
		[SerializeField] private GameObject textPrefab;
		private readonly Stack<(Animator animator, TextMeshProUGUI text)> texts = new();

		private void Start()
		{
			for (int i = 0; i < textsRoot.childCount; i++)
			{
				Animator animator = textsRoot.GetChild(i).GetComponent<Animator>();
				animator.keepAnimatorStateOnDisable = true;
				animator.gameObject.SetActive(false);
				texts.Push((animator, animator.transform.GetChild(0).GetComponent<TextMeshProUGUI>()));
			}
		}

		private (Animator, TextMeshProUGUI) Pop()
		{
			if (texts.Count == 0)
			{
				for (int i = 0; i < 10; i++)
				{
					GameObject newObject = Instantiate(textPrefab, textsRoot);
					Animator animator = newObject.GetComponent<Animator>();
					animator.keepAnimatorStateOnDisable = true;
					animator.gameObject.SetActive(false);
					texts.Push((animator, animator.transform.GetChild(0).GetComponent<TextMeshProUGUI>()));
				}
			}

			return texts.Pop();
		}

		public IEnumerator AniTextUI(TextType textType, string msg, Vector3 worldPos = default)
		{
			(Animator animator, TextMeshProUGUI text) = Pop();

			animator.SetTrigger("POP");
			animator.gameObject.SetActive(true);

			text.text = msg;
			UpdateTextStyle(ref text, textType);

			if (worldPos != default)
				worldPos += Random.insideUnitSphere * .3f;

			Vector3 GetScreenPos()
			{
				if (worldPos == default)
					return Input.mousePosition;
				else
					return Camera.main.WorldToScreenPoint(worldPos);
			}

			for (float time = 0; time < 1; time += Time.deltaTime)
			{
				animator.transform.position = GetScreenPos();
				yield return null;
			}

			animator.gameObject.SetActive(false);
			texts.Push((animator, text));
		}

		public Vector3 GetHorrorPos(Vector3 worldPos)
		{
			Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos + Random.insideUnitSphere * .3f);
			return screenPos;
		}

		private void UpdateTextStyle(ref TextMeshProUGUI text, TextType textType)
		{
			// TODO: 스탯 효과 (ex. DEF Down, ATK Up)

			text.color = Color.white;
		
			switch (textType)
			{
				case TextType.Normal:
					break;
				case TextType.Critical:
					text.text = $"크리티컬!\n{text.text}";
					text.color = new Color(1, 110f / 255f, 86f / 255f);
					break;
				case TextType.Heal:
					break;
				case TextType.Exp:
					break;
				case TextType.Warning:
					text.color = Color.yellow;
					break;
				default:
					break;
			}
		}
	}
}