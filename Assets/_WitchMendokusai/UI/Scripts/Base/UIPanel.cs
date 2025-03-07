using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class UIPanel : MonoBehaviour, IUI
	{
		[field: Header("_" + nameof(UIPanel))]
		[field: SerializeField] public string Name { get; private set; } = "UIPanel";
		[field: SerializeField] public Sprite PanelIcon { get; private set; }

		public virtual void Init() {}
		public abstract void UpdateUI();
		public virtual void OnOpen() {}
		public virtual void OnClose() {}

		public void SetActive(bool newActive)
		{
			// Debug.Log($"{name} SetActive {newActive}");

			gameObject.SetActive(newActive);

			if (newActive)
				OnOpen();
			else
				OnClose();
		}

		public void ToggleActive() => SetActive(gameObject.activeSelf);
	}
}