using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WitchMendokusai
{
	// UGUI 패널의 선택 변경을 포커스 이동음 한 지점으로. 패널이 직접 고른 선택은 무음
	public sealed class UICardFocusSound : MonoBehaviour
	{
		private Func<GameObject, bool> isIgnored;
		private GameObject lastSelected;
		private int silentUntilFrame = -1;

		public static UICardFocusSound AttachTo(GameObject panel, Func<GameObject, bool> isIgnored = null)
		{
			UICardFocusSound focusSound = panel.AddComponent<UICardFocusSound>();
			focusSound.isIgnored = isIgnored;
			return focusSound;
		}

		public void Silence() => silentUntilFrame = Time.frameCount + 1;

		private void Update()
		{
			GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
			if (selected == lastSelected)
				return;

			bool hadSelection = lastSelected != null;
			lastSelected = selected;
			if (hadSelection == false || selected == null || Time.frameCount <= silentUntilFrame)
				return;
			if (selected.transform.IsChildOf(transform) == false || (isIgnored != null && isIgnored(selected)))
				return;
			UiSfx.Play(UiSfxEvent.FocusMove);
		}
	}
}
