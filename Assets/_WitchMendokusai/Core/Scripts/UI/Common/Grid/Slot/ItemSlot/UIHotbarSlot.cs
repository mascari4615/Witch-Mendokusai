using UnityEngine;
using UnityEngine.EventSystems;

namespace WitchMendokusai
{
	public class UIHotbarSlot : UIItemSlot
	{
		private static readonly int SelectedTrigger = Animator.StringToHash("Selected");
		private static readonly int NormalTrigger = Animator.StringToHash("Normal");

		private Animator _animator;

		public override void Init()
		{
			base.Init();
			_animator = GetComponent<Animator>();
		}

		public void SetSelected(bool selected)
		{
			if (_animator != null)
				_animator.SetTrigger(selected ? SelectedTrigger : NormalTrigger);
		}

		public override void OnPointerDown(PointerEventData eventData)
		{
			if (UIItemGrid is UIHotbar hotbar)
				hotbar.SelectHotbarSlot(Index);

			base.OnPointerDown(eventData);
		}
	}
}
