using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 공개 연출이 그리는 층. 패널 맨 뒤 (어둡게, 빛기둥, 뒤 입자) 와 맨 앞 (번짐, 앞 입자)
	public sealed class UICardRevealStage : MonoBehaviour
	{
		private static readonly Color DIM_COLOR = new(0.024f, 0.02f, 0.04f, 1f);

		private RectTransform root;
		private RectTransform backLayer;
		private RectTransform frontLayer;
		private Image dim;
		private readonly List<GameObject> spawned = new();

		public UIRevealParticles BackParticles { get; private set; }
		public UIRevealParticles FrontParticles { get; private set; }
		public Image Dim => dim;
		public RectTransform Root => root;

		public static UICardRevealStage Ensure(RectTransform stageRoot)
		{
			if (stageRoot.TryGetComponent(out UICardRevealStage stage))
				return stage;
			stage = stageRoot.gameObject.AddComponent<UICardRevealStage>();
			stage.Build(stageRoot);
			return stage;
		}

		private void Build(RectTransform stageRoot)
		{
			root = stageRoot;
			backLayer = NewLayer("[Reveal] Back");
			frontLayer = NewLayer("[Reveal] Front");
			GameObject dimObject = new("[Image] Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			RectTransform dimRect = (RectTransform)dimObject.transform;
			dimRect.SetParent(backLayer, false);
			Stretch(dimRect);
			dim = dimObject.GetComponent<Image>();
			dim.sprite = RevealTextures.Vignette;
			dim.raycastTarget = false;
			dim.color = new Color(DIM_COLOR.r, DIM_COLOR.g, DIM_COLOR.b, 0f);
			BackParticles = UIRevealParticles.Create(backLayer, "[Reveal] Back particles");
			FrontParticles = UIRevealParticles.Create(frontLayer, "[Reveal] Front particles");
		}

		// 비네트 그림 한 장. 가장자리가 alpha 만큼, 가운데는 거의 그대로
		public Color DimColor(float alpha) => new(DIM_COLOR.r, DIM_COLOR.g, DIM_COLOR.b, alpha);

		// 패널 내용이 바뀌어도 맨 뒤와 맨 앞 유지
		public void Arrange()
		{
			backLayer.SetAsFirstSibling();
			frontLayer.SetAsLastSibling();
			BackParticles.transform.SetAsLastSibling();
			FrontParticles.transform.SetAsLastSibling();
		}

		// target 가운데를 이 층 좌표로. 가운데 기준점 이미지의 anchoredPosition 과 같은 기준
		public Vector2 ToLocal(RectTransform target)
		{
			Vector3 world = target.TransformPoint(target.rect.center);
			return (Vector2)root.InverseTransformPoint(world) - root.rect.center;
		}

		// 이 층 좌표를 다른 부모의 local 좌표로
		public Vector3 ToParentLocal(Vector2 stagePosition, Transform parent) => parent.InverseTransformPoint(root.TransformPoint(stagePosition + root.rect.center));

		public Vector2 Size => root.rect.size;

		public Image NewImage(bool front, Sprite sprite, Color color, Vector2 position, Vector2 size, string objectName)
		{
			GameObject imageObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			RectTransform rect = (RectTransform)imageObject.transform;
			rect.SetParent(front ? frontLayer : backLayer, false);
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = position;
			rect.sizeDelta = size;
			Image image = imageObject.GetComponent<Image>();
			image.sprite = sprite;
			image.color = color;
			image.raycastTarget = false;
			// 입자보다 뒤
			rect.SetSiblingIndex(Mathf.Max(0, (front ? FrontParticles : BackParticles).transform.GetSiblingIndex()));
			spawned.Add(imageObject);
			return image;
		}

		public void Clear()
		{
			ClearImages();
			BackParticles.Clear();
			FrontParticles.Clear();
		}

		public void ClearImages()
		{
			foreach (GameObject item in spawned)
				if (item != null)
					Destroy(item);
			spawned.Clear();
		}

		private RectTransform NewLayer(string objectName)
		{
			GameObject layer = new(objectName, typeof(RectTransform));
			RectTransform rect = (RectTransform)layer.transform;
			rect.SetParent(root, false);
			Stretch(rect);
			// 레이아웃 그룹 안이어도 자리 차지 안 함
			layer.AddComponent<LayoutElement>().ignoreLayout = true;
			return rect;
		}

		private static void Stretch(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = rect.offsetMax = Vector2.zero;
		}
	}
}
