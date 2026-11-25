using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 다가가 Z 로 쓰는 자리 하나. 이름과 할 일은 놓은 쪽이 줌
	// 기존 상호작용 (InteractiveObject, 1.5m) 과 표시 (InteractPromptView) 를 그대로 탐
	public sealed class HomesteadSpot : MonoBehaviour, IInteractable, IInteractPromptSource
	{
		private Func<string> nameSource;
		private Func<string> roleSource;
		private Action onInteract;

		public string PromptName => nameSource();
		public string PromptRole => roleSource();
		public int PromptStamp => (PromptName, PromptRole).GetHashCode();

		public static HomesteadSpot Create(Transform parent, Vector3 localPosition, string objectName, Func<string> name, Func<string> role, Action interact)
		{
			GameObject spotObject = new(objectName);
			spotObject.transform.SetParent(parent, false);
			spotObject.transform.localPosition = localPosition;
			return Attach(spotObject, name, role, interact);
		}

		// 자리를 먼저 붙이고 InteractiveObject 를 뒤에. InteractiveObject 가 Awake 에서 IInteractable 을 모음
		public static HomesteadSpot Attach(GameObject target, Func<string> name, Func<string> role, Action interact)
		{
			HomesteadSpot spot = target.AddComponent<HomesteadSpot>();
			spot.nameSource = name;
			spot.roleSource = role ?? (() => string.Empty);
			spot.onInteract = interact;
			target.AddComponent<InteractiveObject>();
			return spot;
		}

		public void OnInteract() => onInteract();
	}

	// 카메라를 보는 그림 하나. 작물, 주민, 인형, 전시품
	public sealed class HomesteadSprite : MonoBehaviour
	{
		[SerializeField] private float bobHeight;
		[SerializeField] private float bobSeconds = 2f;

		public SpriteRenderer Renderer { get; private set; }
		private Vector3 home;
		private float phase;

		public static HomesteadSprite Create(Transform parent, Vector3 localPosition, Sprite sprite, float height, float bob = 0f)
		{
			GameObject spriteObject = new("Sprite");
			spriteObject.transform.SetParent(parent, false);
			spriteObject.transform.localPosition = localPosition;
			HomesteadSprite made = spriteObject.AddComponent<HomesteadSprite>();
			made.Renderer = spriteObject.AddComponent<SpriteRenderer>();
			made.Renderer.sharedMaterial = HomesteadArt.Instance.SpriteMaterial;
			made.bobHeight = bob;
			made.home = localPosition;
			made.phase = (localPosition.x * 0.37f + localPosition.z * 0.61f) % 1f;
			made.SetSprite(sprite, height);
			return made;
		}

		public void SetSprite(Sprite sprite, float height)
		{
			Renderer.sprite = sprite;
			if (sprite != null)
				transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, sprite.bounds.size.y));
		}

		public void SetHome(Vector3 localPosition) => home = localPosition;

		private void LateUpdate()
		{
			Camera view = Camera.main;
			if (view == null)
				return;
			Vector3 angles = view.transform.eulerAngles;
			transform.rotation = Quaternion.Euler(angles.x, angles.y, 0f);
			if (bobHeight > 0f)
				transform.localPosition = home + Vector3.up * (Mathf.Sin((Time.time / bobSeconds + phase) * Mathf.PI * 2f) * bobHeight);
		}
	}
}
