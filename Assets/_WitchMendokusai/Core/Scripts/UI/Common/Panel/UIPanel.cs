using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class UIPanel : MonoBehaviour
	{
		[field: Header("_" + nameof(UIPanel))]
		[field: SerializeField] public string Name { get; private set; } = "UIPanel";
		[field: SerializeField] public Sprite PanelIcon { get; private set; } = null;

		public IUIContentBase Content { get; private set; }

		public void Init(IUIContentBase content)
		{
			Content = content;
			OnInit();
		}
		protected abstract void OnInit();

		public abstract void UpdateUI();

		public void SetActive(bool newActive)
		{
			// Debug.Log($"{name} {nameof(SetActive)}({newActive})");

			gameObject.SetActive(newActive);

			if (newActive)
			{
				OnOpen();
			}
			else
			{
				OnClose();
			}
		}
		protected virtual void OnOpen() { }
		protected virtual void OnClose() { }

		public virtual void SetNPC(NPCObject npc) { }
	}
}