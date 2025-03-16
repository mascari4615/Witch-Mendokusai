using UnityEngine;

namespace WitchMendokusai
{
	public abstract class UIBase : MonoBehaviour
	{
		public abstract void Init();
		public abstract void UpdateUI();
		protected virtual void OnOpen() { }
		protected virtual void OnClose() { }

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

		public void ToggleActive() => SetActive(gameObject.activeSelf);
	}
}