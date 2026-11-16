using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class UIPanel : MonoBehaviour, IUIPanel
	{
		[field: Header("_" + nameof(UIPanel))]
		[field: SerializeField] public string Name { get; private set; } = "UIPanel";
		[field: SerializeField] public Sprite PanelIcon { get; private set; } = null;

		public IUIPanelGroup PanelGroup { get; private set; }
		public abstract bool IsFullscreen { get; }

		public void Init(IUIPanelGroup group)
		{
			PanelGroup = group;
			OnInit();
		}
		protected abstract void OnInit();

		public abstract void UpdateUI();

		public void SetActive(bool newActive)
		{
			// Debug.Log($"{name} {nameof(SetActive)}({newActive})");

			if (newActive)
				OnBeforeShow();

			gameObject.SetActive(newActive);

			if (newActive)
			{
				// Animation 트랜지션 버튼의 animator.hasBoundPlayables가
				// SetActive(true) 직후 프레임에서는 false라 Select() 트리거가 무시됨.
				// 한 프레임 뒤에 OnOpen()을 호출해 Animator 초기화를 보장한다.
				StartCoroutine(OpenNextFrame());
			}
			else
			{
				OnClose();
			}
		}

		private IEnumerator OpenNextFrame()
		{
			yield return null;
			if (gameObject.activeInHierarchy)
				OnOpen();
		}

		// 켜기 직전, 같은 프레임. OnOpen 이 한 프레임 늦게 돌아 그 사이 옛 내용이 보이는 것을 막을 때
		protected virtual void OnBeforeShow() { }
		protected virtual void OnOpen() { }
		protected virtual void OnClose() { }

		public virtual void SetNPC(NPCObject npc) { }
	}
}