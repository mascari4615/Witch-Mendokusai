using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WitchMendokusai
{
	// 선택 표시 틀 하나가 현재 선택 칸을 따라 이동. 범위 밖 선택이면 숨김
	public class UIMovingFrame : MonoBehaviour
	{
		[SerializeField] private RectTransform frame;
		[SerializeField] private RectTransform scope;
		[SerializeField] private float moveDuration = 0.11f;

		private GameObject current;
		private readonly Vector3[] corners = new Vector3[4];

		private void OnDisable()
		{
			frame.DOKill();
			current = null;
			frame.gameObject.SetActive(false);
		}

		private void LateUpdate()
		{
			EventSystem eventSystem = EventSystem.current;
			GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
			if (selected == null || selected.activeInHierarchy == false || selected.transform.IsChildOf(scope) == false)
			{
				if (current != null)
				{
					frame.DOKill();
					current = null;
					frame.gameObject.SetActive(false);
				}
				return;
			}

			RectTransform target = (RectTransform)selected.transform;
			target.GetWorldCorners(corners);
			RectTransform parent = (RectTransform)frame.parent;
			Vector2 min = parent.InverseTransformPoint(corners[0]);
			Vector2 max = parent.InverseTransformPoint(corners[2]);
			Vector2 size = max - min;
			Vector2 position = (min + max) * 0.5f;

			if (selected == current)
			{
				if (DOTween.IsTweening(frame) == false)
				{
					frame.localPosition = position;
					frame.sizeDelta = size;
				}
				return;
			}

			bool isFirst = current == null;
			current = selected;
			frame.DOKill();
			frame.gameObject.SetActive(true);
			if (isFirst || UIEffects.Enabled == false)
			{
				frame.localPosition = position;
				frame.sizeDelta = size;
				return;
			}
			frame.DOLocalMove(position, moveDuration).SetUpdate(true).SetTarget(frame);
			frame.DOSizeDelta(size, moveDuration).SetUpdate(true).SetTarget(frame);
		}
	}
}
