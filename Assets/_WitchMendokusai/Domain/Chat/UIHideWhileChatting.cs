using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 아래 HUD와 겹치는 대화, 카드 선택, 상자, 결과 동안 HUD 숨김
	[RequireComponent(typeof(CanvasGroup))]
	public class UIHideWhileChatting : MonoBehaviour
	{
		private static readonly HashSet<Object> requesters = new();

		[SerializeField] private CanvasGroup group;

		public static void Request(Object owner, bool isHidden)
		{
			if (isHidden)
				requesters.Add(owner);
			else
				requesters.Remove(owner);
		}

		private void LateUpdate()
		{
			requesters.RemoveWhere(owner => owner == null);
			float alpha = UIChat.IsChatting || requesters.Count > 0 ? 0f : 1f;
			if (Mathf.Approximately(group.alpha, alpha) == false)
				group.alpha = alpha;
		}
	}
}
