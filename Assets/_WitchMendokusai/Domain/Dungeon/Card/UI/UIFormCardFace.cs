using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 형태 카드 (갈래 확정, 최종) 의 얼굴. 일반 카드 칸 위에 틀, 전용 그림, 흐르는 빛을 얹음. 값은 FormCardLook
	// - 공개: 글자가 보이기 전에 빛으로 덮여 이 한 장만 다르다는 것을 먼저 알림
	// - 고른 뒤: 카드에서 조각이 튀고 빛기둥이 섬 (이어서 변신)
	// 카드 칸은 다시 쓰이므로 일반 카드가 오면 Hide
	public sealed class UIFormCardFace : MonoBehaviour
	{
		private static readonly int MAIN_ID = Shader.PropertyToID("_Main");
		private static readonly int BRIGHT_ID = Shader.PropertyToID("_Bright");
		private static readonly int DARK_ID = Shader.PropertyToID("_Dark");
		private static readonly int CELLS_ID = Shader.PropertyToID("_Cells");
		private static readonly int COVER_ID = Shader.PropertyToID("_Cover");
		private static readonly int PILLAR_ID = Shader.PropertyToID("_Pillar");
		private const float CELL_PIXELS = 4f;

		private readonly List<Object> owned = new();
		private FormCardLook look;
		private RectTransform back;
		private RectTransform front;
		private RawImage art;
		private Image cover;
		private Material coverMaterial;
		private FormCardStage stage;
		private bool coverPending;
		private Tween coverTween;

		private bool IsShown => look != null && front.gameObject.activeSelf;

		public void Show(FormCardLook newLook, RectTransform artArea)
		{
			if (look != newLook)
			{
				Clear();
				look = newLook;
				Build(artArea);
			}
			SetParts(true);
			coverTween?.Kill();
			coverMaterial.SetFloat(COVER_ID, 1f);
			cover.gameObject.SetActive(UIEffects.Enabled);
			coverPending = UIEffects.Enabled;
		}

		public void Hide()
		{
			coverTween?.Kill();
			coverPending = false;
			if (look != null)
				SetParts(false);
		}

		// 공개 연출이 끝난 뒤 (연출이 없으면 바로) 덮은 빛을 걷음
		public void Uncover()
		{
			if (coverPending == false)
				return;
			coverPending = false;
			coverTween = DOVirtual.Float(1f, 0f, look.CoverSeconds, value => coverMaterial.SetFloat(COVER_ID, value))
				.SetDelay(look.CoverHold).SetUpdate(true).SetLink(gameObject).OnComplete(() => cover.gameObject.SetActive(false));
		}

		// 고른 순간. 카드는 덱으로 날아가므로 조각과 기둥은 canvas 에 둠
		public void PlayPicked(Canvas canvas)
		{
			if (IsShown == false)
				return;
			RectTransform card = (RectTransform)transform;
			Material pillarMaterial = Flow(look.PillarSize, true);
			Image pillar = NewImage("[FormCard] Pillar", canvas.transform, pillarMaterial);
			RectTransform pillarRect = pillar.rectTransform;
			pillarRect.sizeDelta = look.PillarSize;
			pillarRect.position = card.TransformPoint(card.rect.center);
			pillarRect.localScale = new Vector3(0.1f, 1f, 1f);
			DOTween.Sequence().SetUpdate(true).SetLink(pillar.gameObject)
				.Append(pillarRect.DOScaleX(1f, look.PillarSeconds * 0.2f).SetEase(Ease.OutBack))
				.Append(pillarRect.DOScaleX(0f, look.PillarSeconds * 0.8f).SetEase(Ease.InQuad))
				.OnComplete(() =>
				{
					Destroy(pillarMaterial);
					Destroy(pillar.gameObject);
				});
			for (int i = 0; i < look.ShardCount; i++)
				ThrowShard(card, canvas, i);
		}

		private void ThrowShard(RectTransform card, Canvas canvas, int index)
		{
			Image shard = NewImage("[FormCard] Shard", canvas.transform, null);
			shard.color = index % 3 == 0 ? look.Bright : index % 3 == 1 ? look.Main : look.Dark;
			RectTransform rect = shard.rectTransform;
			float size = Random.Range(10f, 34f);
			rect.sizeDelta = new Vector2(size, size * Random.Range(0.4f, 1.2f));
			Vector2 from = new(Random.Range(card.rect.xMin, card.rect.xMax), Random.Range(card.rect.yMin, card.rect.yMax));
			rect.position = card.TransformPoint(from);
			rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
			Vector2 away = (from - card.rect.center).normalized * (look.ShardDistance * Random.Range(0.4f, 1f)) + Vector2.up * (look.ShardDistance * 0.25f);
			DOTween.Sequence().SetUpdate(true).SetLink(shard.gameObject)
				.Join(rect.DOLocalMove(rect.localPosition + (Vector3)away, look.ShardSeconds).SetEase(Ease.OutCubic))
				.Join(rect.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-260f, 260f)), look.ShardSeconds, RotateMode.LocalAxisAdd))
				.Join(rect.DOScale(0f, look.ShardSeconds).SetEase(Ease.InCubic))
				.OnComplete(() => Destroy(shard.gameObject));
		}

		private void Update()
		{
			if (IsShown)
				UIEffects.TickShaderTime();
		}

		private void OnDisable()
		{
			coverTween?.Kill();
			stage?.SetVisible(false);
		}

		private void OnEnable() => stage?.SetVisible(IsShown);

		private void OnDestroy() => Clear();

		private void SetParts(bool visible)
		{
			back.gameObject.SetActive(visible);
			front.gameObject.SetActive(visible);
			art.gameObject.SetActive(visible);
			stage.SetVisible(visible);
		}

		private void Build(RectTransform artArea)
		{
			RectTransform card = (RectTransform)transform;
			Vector2 size = card.rect.size;
			back = NewRect("[FormCard] Back", artArea.parent);
			// 그림 칸 바로 뒤 (바탕 위, 글자 아래)
			back.SetSiblingIndex(artArea.GetSiblingIndex());
			Image flow = NewImage("Flow", back, Own(Flow(size, false)));
			flow.color = new Color(1f, 1f, 1f, look.BackFlow);
			Stretch(flow.rectTransform);

			stage = new FormCardStage(look, artArea.rect.width / artArea.rect.height);
			art = new GameObject("[FormCard] Art", typeof(RectTransform)).AddComponent<RawImage>();
			art.transform.SetParent(artArea, false);
			art.raycastTarget = false;
			art.texture = stage.Texture;
			Stretch(art.rectTransform);

			front = NewRect("[FormCard] Front", card);
			front.SetAsLastSibling();
			BuildFrame(size);
			coverMaterial = Own(Flow(size, false));
			cover = NewImage("Cover", front, coverMaterial);
			Stretch(cover.rectTransform);
		}

		// 네 변 띠와 모서리 마름모. 카드 밖으로 나가 윤곽부터 다름
		private void BuildFrame(Vector2 size)
		{
			Vector2 half = size * 0.5f + Vector2.one * look.FrameOutset;
			float thick = look.FrameThickness;
			Edge(new Vector2(0f, half.y), new Vector2(half.x * 2f, thick));
			Edge(new Vector2(0f, -half.y), new Vector2(half.x * 2f, thick));
			Edge(new Vector2(half.x, 0f), new Vector2(thick, half.y * 2f));
			Edge(new Vector2(-half.x, 0f), new Vector2(thick, half.y * 2f));
			for (int side = -1; side <= 1; side += 2)
			for (int top = -1; top <= 1; top += 2)
				Gem(new Vector2(side * half.x, top * half.y), look.CornerSize);
			Gem(new Vector2(0f, half.y), look.CornerSize * 1.5f);
		}

		private void Edge(Vector2 position, Vector2 size)
		{
			bool flat = size.x > size.y;
			Place(look.Dark, position, size + Vector2.one * 4f, 0f);
			Place(look.Main, position, size, 0f);
			Place(look.Bright, position + (flat ? Vector2.up : Vector2.left) * (look.FrameThickness * 0.2f), flat ? new Vector2(size.x, size.y * 0.25f) : new Vector2(size.x * 0.25f, size.y), 0f);
		}

		private void Gem(Vector2 position, float size)
		{
			Place(look.Dark, position, Vector2.one * (size + 6f), 45f);
			Place(look.Main, position, Vector2.one * size, 45f);
			Place(look.Bright, position, Vector2.one * (size * 0.45f), 45f);
		}

		private void Place(Color color, Vector2 position, Vector2 size, float angle)
		{
			Image image = NewImage("Frame", front, null);
			image.color = color;
			image.rectTransform.anchoredPosition = position;
			image.rectTransform.sizeDelta = size;
			image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
		}

		private Material Flow(Vector2 size, bool pillar)
		{
			Material material = new(look.FlowShader) { name = "FormCardFlow" };
			material.SetColor(MAIN_ID, look.Main);
			material.SetColor(BRIGHT_ID, look.Bright);
			material.SetColor(DARK_ID, look.Dark);
			material.SetVector(CELLS_ID, new Vector4(Mathf.Round(size.x / CELL_PIXELS), Mathf.Round(size.y / CELL_PIXELS), 0f, 0f));
			material.SetFloat(PILLAR_ID, pillar ? 1f : 0f);
			material.SetFloat(COVER_ID, 1f);
			return material;
		}

		private T Own<T>(T target) where T : Object
		{
			owned.Add(target);
			return target;
		}

		private void Clear()
		{
			coverTween?.Kill();
			foreach (Object target in owned)
				Destroy(target);
			owned.Clear();
			stage?.Dispose();
			stage = null;
			if (look == null)
				return;
			Destroy(back.gameObject);
			Destroy(front.gameObject);
			Destroy(art.gameObject);
			look = null;
		}

		private static RectTransform NewRect(string name, Transform parent)
		{
			RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
			rect.SetParent(parent, false);
			Stretch(rect);
			return rect;
		}

		private static Image NewImage(string name, Transform parent, Material material)
		{
			Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
			image.transform.SetParent(parent, false);
			image.raycastTarget = false;
			if (material != null)
				image.material = material;
			return image;
		}

		private static void Stretch(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}
	}
}
