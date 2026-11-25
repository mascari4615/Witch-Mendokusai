using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 분신의 몸. 주인공의 보이는 그림 조각 (머리, 몸, 손) 을 어두운 색으로 그대로 따라 그림
	public class ShadowCloneBody : MonoBehaviour
	{
		[SerializeField] private Color tint = new(0.06f, 0.07f, 0.16f, 0.72f);
		[SerializeField, Min(0f), Tooltip("나타나고 사라지는 시간")] private float fadeSeconds = 0.18f;

		private readonly List<SpriteRenderer> sourceParts = new();
		private readonly List<SpriteRenderer> parts = new();
		private Transform owner;
		private float shown;
		private bool targetShown = true;

		public void Init(UnitObject unit)
		{
			owner = unit.transform;
			unit.MeshParent.GetComponentsInChildren(true, sourceParts);
			sourceParts.RemoveAll(part => part.enabled == false);
			foreach (SpriteRenderer source in sourceParts)
			{
				GameObject child = new(source.name);
				child.transform.SetParent(transform, false);
				SpriteRenderer part = child.AddComponent<SpriteRenderer>();
				part.sharedMaterial = source.sharedMaterial;
				parts.Add(part);
			}
		}

		public void SetShown(bool value) => targetShown = value;

		private void LateUpdate()
		{
			shown = Mathf.MoveTowards(shown, targetShown ? 1f : 0f, fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f);
			for (int i = 0; i < parts.Count; i++)
			{
				SpriteRenderer source = sourceParts[i];
				SpriteRenderer part = parts[i];
				part.enabled = shown > 0f && source != null && source.enabled && source.gameObject.activeInHierarchy;
				if (part.enabled == false)
					continue;
				part.sprite = source.sprite;
				part.flipX = source.flipX;
				part.flipY = source.flipY;
				part.sortingLayerID = source.sortingLayerID;
				part.sortingOrder = source.sortingOrder - 1;
				part.color = new Color(tint.r, tint.g, tint.b, tint.a * shown);
				Transform sourceTransform = source.transform;
				part.transform.SetPositionAndRotation(transform.position + (sourceTransform.position - owner.position), sourceTransform.rotation);
				part.transform.localScale = sourceTransform.lossyScale;
			}
		}
	}
}
