using UnityEngine;
using UnityEngine.EventSystems;

namespace WitchMendokusai
{
	// 버튼 선택 표시. 키보드 선택과 마우스 호버 공용
	public class UIFocusFrame : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
	{
		[SerializeField] private GameObject frame;

		private bool isSelected;
		private bool isHovered;

		public void OnSelect(BaseEventData eventData)
		{
			isSelected = true;
			Apply();
		}

		public void OnDeselect(BaseEventData eventData)
		{
			isSelected = false;
			Apply();
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			isHovered = true;
			Apply();
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			isHovered = false;
			Apply();
		}

		private void OnDisable()
		{
			isSelected = false;
			isHovered = false;
			Apply();
		}

		private void Apply() => frame.SetActive(isSelected || isHovered);
	}
}
