using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using WitchMendokusai.Presentation;
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

		private readonly Stack<FloatingTextElement> pool = new();

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

		private FloatingTextElement CreateItem()
		{
			FloatingTextElement item = new();
			item.style.display = DisplayStyle.None;
			uiRoot.OverlayLayer.Add(item);
			return item;
		}

		private FloatingTextElement Pop()
		{
			if (pool.Count == 0)
				return CreateItem();
			return pool.Pop();
		}

		public IEnumerator AniTextUI(TextType textType, string msg, Vector3 worldPos = default)
		{
			FloatingTextElement item = Pop();
			item.style.display = DisplayStyle.Flex;
			item.Show(KindOf(textType), msg);

			// 월드 좌표를 준 호출인지 여부는 인자 유무로 판단해야 한다 — 좌표가 원점(0,0,0)인 대상
			// (개척의 코어가 정확히 원점에 선다)을 "좌표 없음"으로 오인해 마우스 위치에 띄우면
			// 데미지 숫자가 대상이 아니라 커서를 따라다닌다.
			bool hasWorldPosition = worldPos != default;

			Vector3 jitteredWorldPos = worldPos;
			if (hasWorldPosition)
				jitteredWorldPos += Random.insideUnitSphere * 0.3f;

			Vector3 GetScreenPos()
			{
				if (hasWorldPosition == false)
					return inputManager.MouseScreenPosition;

				// ★ Camera.main 이 아니라 *지금 보이는* 카메라. 모드 카메라를 본편 카메라 위에 겹쳐
				//   렌더하는 화면(개척)에서 둘이 갈라져 숫자가 엉뚱한 자리에 뜬다(WM-194 실측).
				Camera viewCamera = ViewCameraResolver.Current;
				if (viewCamera == null)
					return Vector3.zero;

				Vector3 screenPos = viewCamera.WorldToScreenPoint(jitteredWorldPos);
				if (screenPos.z < 0f)
					return new Vector3(-9999f, -9999f, 0f); // 카메라 뒤 = 화면 밖으로 밀어 반대편 유령 숫자 방지.

				return screenPos;
			}

			for (float time = 0; time < LIFETIME_SECONDS; time += Time.deltaTime)
			{
				item.SetScreenPosition(GetScreenPos());
				yield return new WaitForEndOfFrame();
			}

			item.Hide();
			pool.Push(item);
		}

		private static FloatingTextKind KindOf(TextType textType)
		{
			switch (textType)
			{
				case TextType.Critical: return FloatingTextKind.Critical;
				case TextType.Heal: return FloatingTextKind.Heal;
				case TextType.Exp: return FloatingTextKind.Experience;
				case TextType.Warning: return FloatingTextKind.Warning;
				default: return FloatingTextKind.Normal;
			}
		}
	}
}
