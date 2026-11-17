using System;
using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>UI 연출 에셋이 프레임 끝 화면 캡처가 필요할 때 쓰는 숨은 실행기. 처음 쓸 때 만들어 씬과 무관하게 유지</summary>
	public sealed class UIFxRunner : MonoBehaviour
	{
		private static UIFxRunner instance;

		private static UIFxRunner Instance
		{
			get
			{
				if (instance == null)
				{
					GameObject host = new(nameof(UIFxRunner)) { hideFlags = HideFlags.HideAndDontSave };
					DontDestroyOnLoad(host);
					instance = host.AddComponent<UIFxRunner>();
				}
				return instance;
			}
		}

		/// <summary>이번 프레임 끝의 화면을 텍스처로. 받은 쪽이 Destroy</summary>
		public static void CaptureScreen(Action<Texture2D> onCaptured) => Instance.StartCoroutine(Instance.Capture(onCaptured));

		private IEnumerator Capture(Action<Texture2D> onCaptured)
		{
			yield return new WaitForEndOfFrame();
			onCaptured(ScreenCapture.CaptureScreenshotAsTexture());
		}
	}
}
