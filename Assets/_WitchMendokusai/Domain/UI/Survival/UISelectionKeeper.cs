using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 패널이 열린 동안 선택을 패널 안에 유지. 마우스 클릭이나 비활성화로 선택을 잃으면 복원
	public class UISelectionKeeper : MonoBehaviour
	{
		private Transform scope;
		private GameObject last;

		public Selectable Fallback { get; set; }

		public void SetScope(Transform newScope)
		{
			scope = newScope;
			last = null;
		}

		public void ResetScope() => SetScope(transform);

		private void OnEnable() => ResetScope();

		private void OnDisable()
		{
			EventSystem eventSystem = EventSystem.current;
			if (eventSystem == null)
				return;
			GameObject current = eventSystem.currentSelectedGameObject;
			if (current != null && current.transform.IsChildOf(transform))
				eventSystem.SetSelectedGameObject(null);
		}

		private void LateUpdate()
		{
			EventSystem eventSystem = EventSystem.current;
			if (eventSystem == null)
				return;
			GameObject current = eventSystem.currentSelectedGameObject;
			if (IsValid(current))
			{
				last = current;
				return;
			}
			GameObject target = FindTarget();
			if (target != null)
				eventSystem.SetSelectedGameObject(target);
		}

		private GameObject FindTarget()
		{
			if (IsValid(last))
				return last;
			if (Fallback != null && IsValid(Fallback.gameObject))
				return Fallback.gameObject;
			foreach (Selectable selectable in scope.GetComponentsInChildren<Selectable>())
				if (IsValid(selectable.gameObject))
					return selectable.gameObject;
			return null;
		}

		private bool IsValid(GameObject target)
		{
			if (target == null || target.activeInHierarchy == false || target.transform.IsChildOf(scope) == false)
				return false;
			return target.TryGetComponent(out Selectable selectable) && selectable.IsInteractable();
		}
	}
}
