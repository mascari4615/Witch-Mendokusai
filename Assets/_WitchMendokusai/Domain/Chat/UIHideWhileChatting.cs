using UnityEngine;

namespace WitchMendokusai
{
	// 대화 패널과 겹치는 HUD를 대화 동안 숨김
	[RequireComponent(typeof(CanvasGroup))]
	public class UIHideWhileChatting : MonoBehaviour
	{
		[SerializeField] private CanvasGroup group;

		private void LateUpdate()
		{
			float alpha = UIChat.IsChatting ? 0f : 1f;
			if (Mathf.Approximately(group.alpha, alpha) == false)
				group.alpha = alpha;
		}
	}
}
