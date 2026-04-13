using UnityEngine;

namespace WitchMendokusai
{
	public class UIQuestEdge : MonoBehaviour
	{
		[SerializeField] private float lineWidth = 4f;

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
	}
}
