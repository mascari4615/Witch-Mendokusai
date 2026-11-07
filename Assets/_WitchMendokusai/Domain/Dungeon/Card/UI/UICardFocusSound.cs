using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WitchMendokusai
{
	// UGUI 패널의 선택 변경을 포커스 이동음 한 지점으로. 패널이 직접 고른 선택은 무음
	public sealed class UICardFocusSound
	{
		private GameObject lastSelected;
		private int silentUntilFrame = -1;

		public void Silence() => silentUntilFrame = Time.frameCount + 1;

		public void Tick(Transform scope, Func<GameObject, bool> isIgnored = null)
		{
			GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
			if (selected == lastSelected)
				return;

			bool hadSelection = lastSelected != null;
			lastSelected = selected;
			if (hadSelection == false || selected == null || Time.frameCount <= silentUntilFrame)
				return;
			if (selected.transform.IsChildOf(scope) == false || (isIgnored != null && isIgnored(selected)))
				return;
			UiSfx.Play(UiSfxEvent.FocusMove);
		}
	}
}
