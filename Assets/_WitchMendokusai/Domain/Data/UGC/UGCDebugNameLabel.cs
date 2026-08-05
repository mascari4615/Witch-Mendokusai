using UnityEngine;

namespace WitchMendokusai
{
	[DisallowMultipleComponent]
	public sealed class UGCDebugNameLabel : MonoBehaviour
	{
		private const string LabelChildName = "__UGC_DEBUG_LABEL";

		// TextMesh 가 읽을 수 있는 폰트 크기 범위 — 디자인 조절값이 아니라 유효 범위라 const.
		private const int FONT_SIZE_MIN = 20;
		private const int FONT_SIZE_MAX = 120;

		[SerializeField] private string labelText;
		[SerializeField] private Color labelColor = Color.white;
		[SerializeField] private float yOffset = 1.6f;
		[SerializeField] private int fontSize = 56;
		// 라벨이 오브젝트에 파묻히지 않게 하는 최소 높이.
		[SerializeField] private float minYOffset = 0.4f;
		[SerializeField] private float characterSize = 0.06f;

		private Transform labelTransform;
		private TextMesh labelMesh;

		private void Awake()
		{
			EnsureLabelObject();
			ApplyVisual();
		}

		private void LateUpdate()
		{
			if (labelTransform == null || labelMesh == null)
				EnsureLabelObject();

			if (labelTransform == null)
				return;

			labelTransform.position = transform.position + Vector3.up * Mathf.Max(minYOffset, yOffset);

			Camera cam = Camera.main;
			if (cam == null)
				return;

			labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - cam.transform.position);
		}

		public void Setup(string text, Color color, float offset)
		{
			labelText = text;
			labelColor = color;
			yOffset = offset;
			EnsureLabelObject();
			ApplyVisual();
		}

		private void EnsureLabelObject()
		{
			if (labelTransform == null)
			{
				Transform existing = transform.Find(LabelChildName);
				if (existing != null)
					labelTransform = existing;
			}

			if (labelTransform == null)
			{
				GameObject labelObject = new GameObject(LabelChildName);
				labelObject.transform.SetParent(transform, false);
				labelObject.hideFlags = HideFlags.DontSave;
				labelTransform = labelObject.transform;
			}

			if (labelMesh == null && labelTransform != null)
				labelMesh = labelTransform.GetComponent<TextMesh>();

			if (labelMesh == null && labelTransform != null)
				labelMesh = labelTransform.gameObject.AddComponent<TextMesh>();
		}

		private void ApplyVisual()
		{
			if (labelMesh == null)
				return;

			labelMesh.text = labelText;
			labelMesh.anchor = TextAnchor.MiddleCenter;
			labelMesh.alignment = TextAlignment.Center;
			labelMesh.color = labelColor;
			labelMesh.characterSize = characterSize;
			labelMesh.fontSize = Mathf.Clamp(fontSize, FONT_SIZE_MIN, FONT_SIZE_MAX);
			labelMesh.richText = false;
		}
	}
}
