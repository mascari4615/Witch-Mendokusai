using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 대화 패널 위 오른쪽 선택지 목록. 위아래 이동, 결정 시 번호 전달
	public class UIDialogueChoiceList : MonoBehaviour
	{
		[SerializeField] private UIChoiceRow rowPrefab;
		[SerializeField] private RectTransform rowsParent;

		private readonly List<UIChoiceRow> rows = new();
		private Action<int> onSubmit = delegate { };

		public void Show(IReadOnlyList<string> labels, Action<int> submit)
		{
			onSubmit = submit;
			gameObject.SetActive(true);
			while (rows.Count < labels.Count)
			{
				int index = rows.Count;
				UIChoiceRow row = Instantiate(rowPrefab, rowsParent);
				row.GetComponent<Button>().onClick.AddListener(() => Submit(index));
				rows.Add(row);
			}

			for (int i = 0; i < rows.Count; i++)
			{
				bool isVisible = i < labels.Count;
				rows[i].gameObject.SetActive(isVisible);
				if (isVisible)
				{
					rows[i].SetLabel(labels[i]);
					Navigation navigation = new()
					{
						mode = Navigation.Mode.Explicit,
						selectOnUp = i > 0 ? rows[i - 1].Selectable : null,
						selectOnDown = i < labels.Count - 1 ? rows[i + 1].Selectable : null
					};
					rows[i].Selectable.navigation = navigation;
				}
			}

			LayoutRebuilder.ForceRebuildLayoutImmediate(rowsParent);
			for (int i = 0; i < labels.Count; i++)
				rows[i].PlayAppear(i);
			rows[0].Selectable.Select();
		}

		public void Hide() => gameObject.SetActive(false);

		private void Submit(int index)
		{
			rows[index].PlayConfirm();
			Action<int> submit = onSubmit;
			onSubmit = delegate { };
			submit(index);
		}
	}
}
