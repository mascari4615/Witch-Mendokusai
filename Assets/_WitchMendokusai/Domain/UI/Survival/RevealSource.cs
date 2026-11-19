using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 카드가 나오는 자리. 레벨업은 금박 꾸러미, 상자 보상은 뚜껑이 열리는 상자
	// - 상자는 같은 그림을 뚜껑 (위 43%) 과 몸통으로 나눠 그림
	public sealed class RevealSource
	{
		private const float LID_RATIO = 0.43f;
		private const float LID_HINGE_X = 0.12f;
		private const float LID_RISE_RATIO = 0.3f;
		private const float PACK_BORDER = 4f;

		private RectTransform lid;
		private RectTransform tear;
		private Image seam;

		public RectTransform Root { get; private set; }
		public CanvasGroup Group { get; private set; }
		public Vector2 Size { get; private set; }

		public static RevealSource Pack(CardRevealRun run, Vector2 size, Color gold, Color body)
		{
			RevealSource source = NewRoot(run, size, "[Reveal] Pack");
			Image frame = AddImage(source.Root, null, gold, size, Vector2.zero);
			AddImage(frame.rectTransform, null, body, size - Vector2.one * PACK_BORDER * 2f, Vector2.zero);
			float emblem = Mathf.Min(size.x, size.y) * 0.34f;
			AddImage(frame.rectTransform, null, gold, Vector2.one * emblem, Vector2.zero).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
			AddImage(frame.rectTransform, null, body, Vector2.one * emblem * 0.72f, Vector2.zero).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
			// 빛이 비스듬히 흐르는 금박
			Image foil = AddImage(frame.rectTransform, RevealTextures.Beam, new Color(1f, 0.95f, 0.75f, 0.45f), new Vector2(size.x * 0.5f, size.y * 1.4f), Vector2.zero);
			foil.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -28f);
			frame.gameObject.AddComponent<RectMask2D>();
			float tearHeight = size.y * run.Skin.PackTearRatio;
			source.tear = AddImage(source.Root, null, gold, new Vector2(size.x, tearHeight), new Vector2(0f, (size.y - tearHeight) * 0.5f)).rectTransform;
			return source;
		}

		public static RevealSource Chest(CardRevealRun run, Sprite chest, float size)
		{
			RevealSource source = NewRoot(run, Vector2.one * size, "[Reveal] Chest");
			source.seam = AddImage(source.Root, RevealTextures.Bloom, new Color(1f, 1f, 1f, 0f), new Vector2(size * 0.9f, size * 0.3f), new Vector2(0f, size * (0.5f - LID_RATIO)));
			Image body = AddImage(source.Root, chest, Color.white, Vector2.one * size, Vector2.zero);
			body.type = Image.Type.Filled;
			body.fillMethod = Image.FillMethod.Vertical;
			body.fillOrigin = (int)Image.OriginVertical.Bottom;
			body.fillAmount = 1f - LID_RATIO;
			Image lidImage = AddImage(source.Root, chest, Color.white, Vector2.one * size, Vector2.zero);
			lidImage.type = Image.Type.Filled;
			lidImage.fillMethod = Image.FillMethod.Vertical;
			lidImage.fillOrigin = (int)Image.OriginVertical.Top;
			lidImage.fillAmount = LID_RATIO;
			// 뚜껑 경첩을 왼쪽 아래로. 축을 옮겨도 그림 자리는 그대로
			RectTransform lidRect = lidImage.rectTransform;
			Vector2 pivot = new(LID_HINGE_X, 1f - LID_RATIO);
			lidRect.pivot = pivot;
			lidRect.anchoredPosition = (pivot - new Vector2(0.5f, 0.5f)) * size;
			source.lid = lidRect;
			return source;
		}

		// 터지기 직전. 꾸러미는 위 띠가 찢겨 날아가고, 상자는 뚜껑이 열리며 틈 빛
		public void Open(CardRevealRun run, float burstAt)
		{
			if (tear != null)
			{
				Image tearImage = tear.GetComponent<Image>();
				run.Play(burstAt - 0.06f, tear.DOAnchorPos(tear.anchoredPosition + new Vector2(Size.x * 0.6f, Size.y * 0.4f), 0.3f).SetEase(Ease.OutCubic));
				run.Play(burstAt - 0.06f, tear.DOLocalRotate(new Vector3(0f, 0f, -38f), 0.3f).SetEase(Ease.OutCubic));
				run.Play(burstAt - 0.06f, tearImage.DOFade(0f, 0.3f));
			}
			if (lid != null)
			{
				Vector2 lidHome = lid.anchoredPosition;
				run.Play(burstAt - 0.08f, lid.DOLocalRotate(new Vector3(0f, 0f, run.Skin.LidOpenAngle), 0.18f).SetEase(Ease.OutBack));
				run.Play(burstAt - 0.08f, lid.DOAnchorPos(lidHome + new Vector2(0f, Size.y * LID_RISE_RATIO), 0.18f).SetEase(Ease.OutBack));
				run.Play(burstAt - 0.08f, seam.DOFade(1f, 0.1f));
				run.Play(burstAt + 0.02f, seam.rectTransform.DOScaleY(3f, 0.3f).SetEase(Ease.OutQuad));
			}
		}

		// 상자가 튐 (기대). 빛기둥 스킨에서 씀
		public void Hop(CardRevealRun run, float at, float height, float duration)
		{
			Vector2 home = Root.anchoredPosition;
			run.Play(at, Root.DOScale(new Vector3(1.18f, 0.82f, 1f), duration * 0.25f).SetEase(Ease.OutQuad));
			run.Play(at + duration * 0.25f, Root.DOScale(new Vector3(0.9f, 1.12f, 1f), duration * 0.3f).SetEase(Ease.OutQuad));
			run.Play(at + duration * 0.25f, Root.DOAnchorPosY(home.y + height, duration * 0.3f).SetEase(Ease.OutQuad));
			run.Play(at + duration * 0.55f, Root.DOScale(Vector3.one, duration * 0.45f).SetEase(Ease.OutBounce));
			run.Play(at + duration * 0.55f, Root.DOAnchorPosY(home.y, duration * 0.45f).SetEase(Ease.InQuad));
		}

		private static RevealSource NewRoot(CardRevealRun run, Vector2 size, string objectName)
		{
			Image holder = run.Stage.NewImage(false, null, Color.clear, run.Origin, size, objectName);
			RevealSource source = new()
			{
				Root = holder.rectTransform,
				Group = holder.gameObject.AddComponent<CanvasGroup>(),
				Size = size,
			};
			return source;
		}

		private static Image AddImage(RectTransform parent, Sprite sprite, Color color, Vector2 size, Vector2 position)
		{
			GameObject child = new("[Image] Part", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			RectTransform rect = (RectTransform)child.transform;
			rect.SetParent(parent, false);
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = size;
			rect.anchoredPosition = position;
			Image image = child.GetComponent<Image>();
			image.sprite = sprite;
			image.color = color;
			image.raycastTarget = false;
			image.preserveAspect = sprite != null && sprite != RevealTextures.Beam && sprite != RevealTextures.Bloom;
			return image;
		}
	}
}
