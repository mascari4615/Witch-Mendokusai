using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	/// <summary>
	/// 데미지/메시지 floating 텍스트 — 풀 기반. World 좌표를 화면으로 변환해 유지.
	/// USS .wm-floating-text + --active 로 POP/Fade.
	/// </summary>
	public class FloatingTextView : MonoBehaviour
	{
		private const int INITIAL_POOL_SIZE = 10;
		private const float LIFETIME_SECONDS = 1.0f;

		private readonly Stack<FloatingTextItem> pool = new();

		private UIRoot uiRoot;
		private InputManager inputManager;

		[Inject]
		public void Construct(UIRoot uiRoot, InputManager inputManager)
		{
			this.uiRoot = uiRoot;
			this.inputManager = inputManager;
		}

		private void Start()
		{
			for (int i = 0; i < INITIAL_POOL_SIZE; i++)
				pool.Push(CreateItem());
		}

		private FloatingTextItem CreateItem()
		{
			FloatingTextItem item = new();
			item.style.display = DisplayStyle.None;
			uiRoot.OverlayLayer.Add(item);
			return item;
		}

		private FloatingTextItem Pop()
		{
			if (pool.Count == 0)
				return CreateItem();
			return pool.Pop();
		}

		public IEnumerator AniTextUI(TextType textType, string msg, Vector3 worldPos = default)
		{
			FloatingTextItem item = Pop();
			item.style.display = DisplayStyle.Flex;
			item.Show(textType, msg);

			Vector3 jitteredWorldPos = worldPos;
			if (worldPos != default)
				jitteredWorldPos += Random.insideUnitSphere * 0.3f;

			Vector3 GetScreenPos()
			{
				if (worldPos == default)
					return inputManager.MouseScreenPosition;
				if (Camera.main == null)
					return Vector3.zero;
				return Camera.main.WorldToScreenPoint(jitteredWorldPos);
			}

			for (float time = 0; time < LIFETIME_SECONDS; time += Time.deltaTime)
			{
				item.SetScreenPosition(GetScreenPos());
				yield return new WaitForEndOfFrame();
			}

			item.Deactivate();
			item.style.display = DisplayStyle.None;
			pool.Push(item);
		}
	}
}
