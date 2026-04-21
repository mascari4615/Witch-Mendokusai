using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIQuestEdge : MonoBehaviour
	{
		[SerializeField] private float lineWidth = 4f;
		[SerializeField] private Image lineImage;
		[SerializeField] private Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
		[SerializeField] private Color unlockedColor = Color.white;
		[SerializeField] private Color completedColor = new Color(1f, 0.82f, 0.2f, 1f);

		private UIQuestNode fromNode;
		public UIQuestNode FromNode => fromNode;

		public void SetEdge(RectTransform from, RectTransform to)
		{
			Vector2 fromPos = from.anchoredPosition;
			Vector2 toPos = to.anchoredPosition;
			Vector2 dir = toPos - fromPos;
			float dist = dir.magnitude;
			float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

			RectTransform rt = (RectTransform)transform;
			rt.anchoredPosition = (fromPos + toPos) * 0.5f;
			rt.sizeDelta = new Vector2(dist, lineWidth);
			rt.localRotation = Quaternion.Euler(0f, 0f, angle);
		}

		public void SetFromNode(UIQuestNode node)
		{
			fromNode = node;
		}

		public void SetState(QuestState state)
		{
			if (lineImage == null)
				return;

			lineImage.color = state switch
			{
				QuestState.Completed => completedColor,
				QuestState.Unlocked => unlockedColor,
				_ => lockedColor
			};
		}
	}
}
