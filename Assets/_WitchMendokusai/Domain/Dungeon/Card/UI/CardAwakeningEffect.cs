using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 기존 카드 그림과 강화 검 휘두르기를 사용하는 공격 판정 없는 연출
	public class CardAwakeningEffect : MonoBehaviour
	{
		[SerializeField] private Sprite bladeSprite;
		[SerializeField] private SwordSwing slashPrefab;
		[SerializeField, Min(0.01f)] private float gatherDuration = 0.55f;
		[SerializeField, Min(0.01f)] private float releaseDuration = 0.16f;
		[SerializeField, Min(0.01f)] private float fadeDuration = 0.65f;
		[SerializeField, Min(1)] private int bladeCount = 6;
		[SerializeField, Min(1)] private int slashCount = 3;
		[SerializeField] private float gatherRadius = 2.5f;
		[SerializeField] private float focusRadius = 1.2f;
		[SerializeField] private float releaseRadius = 3.4f;
		[SerializeField] private float bladeScale = 1.25f;
		[SerializeField] private float bladeSpriteAngle = 45f;
		[SerializeField] private float orbitDegrees = 60f;
		[SerializeField] private float heightOffset = 0.7f;
		[SerializeField] private float bladeLift = 1.3f;
		[SerializeField, Min(0)] private float slashDelay = 0.07f;
		// 휘두르기 칼끝과 궤적이 닿는 반경, 첫 방향 각도
		[SerializeField, Min(0.1f)] private float slashReach = 2.2f;
		[SerializeField] private float slashAngleOffset = 30f;
		[SerializeField, Range(0, 1)] private float dimOpacity = 0.12f;

		private SpriteRenderer[] blades;
		private SwordSwing[] slashes;
		private Transform actor;
		private Camera worldCamera;
		private TimeManager timeManager;
		private Image dimImage;
		private float elapsed;
		private bool playing;

		public void Play(Transform target, TimeManager clock)
		{
			if (playing) return;
			actor = target;
			worldCamera = Camera.main;
			if (actor == null || worldCamera == null || clock == null || bladeSprite == null || slashPrefab == null)
			{
				Destroy(gameObject);
				return;
			}
			timeManager = clock;
			elapsed = 0;
			blades = CreateSprites("Resonating blade", bladeCount, bladeSprite);
			slashes = new SwordSwing[slashCount];
			transform.rotation = Quaternion.identity;
			for (int i = 0; i < slashes.Length; i++)
			{
				slashes[i] = Instantiate(slashPrefab, transform);
				// 연출 중심은 캐릭터 위로 올라가 있으므로 휘두르기 높이는 지면 기준으로 되돌림
				slashes[i].transform.SetLocalPositionAndRotation(Vector3.down * heightOffset, Quaternion.Euler(0, slashAngleOffset + i * 360f / slashes.Length, 0));
				slashes[i].Prepare(new SwordSwingArea(0, slashReach, 0));
			}
			GameObject shade = new("Awakening shade", typeof(RectTransform), typeof(Canvas));
			shade.transform.SetParent(transform, false);
			Canvas canvas = shade.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = -100;
			GameObject imageObject = new("Shade", typeof(RectTransform), typeof(Image));
			imageObject.transform.SetParent(shade.transform, false);
			dimImage = imageObject.GetComponent<Image>();
			dimImage.raycastTarget = false;
			dimImage.rectTransform.anchorMin = Vector2.zero;
			dimImage.rectTransform.anchorMax = Vector2.one;
			dimImage.rectTransform.offsetMin = Vector2.zero;
			dimImage.rectTransform.offsetMax = Vector2.zero;
			playing = true;
			timeManager.Pause(gameObject);
			Draw();
		}

		private SpriteRenderer[] CreateSprites(string objectName, int count, Sprite sprite)
		{
			SpriteRenderer[] renderers = new SpriteRenderer[count];
			for (int i = 0; i < count; i++)
			{
				GameObject visual = new(objectName, typeof(SpriteRenderer));
				visual.transform.SetParent(transform, false);
				SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
				renderer.sprite = sprite;
				renderer.color = Color.clear;
				renderers[i] = renderer;
			}
			return renderers;
		}

		private void Update()
		{
			if (playing == false) return;
			elapsed += Time.unscaledDeltaTime;
			if (actor == null || worldCamera == null || elapsed >= gatherDuration + releaseDuration + fadeDuration)
			{
				Destroy(gameObject);
				return;
			}
			Draw();
		}

		private void Draw()
		{
			transform.position = actor.position + Vector3.up * heightOffset;
			transform.rotation = Quaternion.identity;
			float gather = Mathf.Clamp01(elapsed / gatherDuration);
			float release = Mathf.Clamp01((elapsed - gatherDuration) / releaseDuration);
			float fade = Mathf.Clamp01((elapsed - gatherDuration - releaseDuration) / fadeDuration);
			float intensity = Mathf.SmoothStep(0, 1, gather) * (1 - fade);
			float radius = Mathf.Lerp(gatherRadius, focusRadius, gather * gather);
			radius = Mathf.Lerp(radius, releaseRadius, 1 - (1 - release) * (1 - release));
			dimImage.color = new Color(0, 0, 0, dimOpacity * intensity);
			float rotation = orbitDegrees * gather * gather * Mathf.Deg2Rad;
			for (int i = 0; i < blades.Length; i++)
			{
				float angle = rotation + i * Mathf.PI * 2 / blades.Length;
				Vector3 direction = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
				SpriteRenderer blade = blades[i];
				blade.transform.localPosition = direction * radius + Vector3.up * bladeLift * gather;
				float outwardAngle = Mathf.Atan2(Vector3.Dot(direction, worldCamera.transform.up), Vector3.Dot(direction, worldCamera.transform.right)) * Mathf.Rad2Deg - 45f;
				blade.transform.rotation = worldCamera.transform.rotation * Quaternion.Euler(0, 0, Mathf.LerpAngle(bladeSpriteAngle, outwardAngle, release));
				blade.transform.localScale = Vector3.one * bladeScale;
				blade.color = new Color(1, 1, 1, intensity * (1 - Mathf.Clamp01(fade * 3)));
			}
			for (int i = 0; i < slashes.Length; i++)
			{
				float age = elapsed - gatherDuration - i * slashDelay;
				slashes[i].Evaluate(age);
			}
		}

		private void OnDisable()
		{
			playing = false;
			if (timeManager != null) timeManager.Resume(gameObject);
		}
	}
}
