using UnityEngine;
using TMPro;

namespace WitchMendokusai
{
	public class UIFPS : MonoBehaviour
	{
		private const string VISIBLE_PREF_KEY = "WM.Display.Fps";

		private static bool? isVisible;

		// 환경설정 시스템 항목과 개발자 창 표시 탭이 같은 값을 씀. 릴리스 빌드 포함, 재실행 뒤 유지
		public static bool IsVisible
		{
			get => isVisible ??= PlayerPrefs.GetInt(VISIBLE_PREF_KEY, 0) == 1;
			set
			{
				isVisible = value;
				PlayerPrefs.SetInt(VISIBLE_PREF_KEY, value ? 1 : 0);
				PlayerPrefs.Save();
			}
		}

		[SerializeField] private TextMeshProUGUI text;
		[SerializeField, Tooltip("프레임 시간 지수 평활 비율")] private float smoothing = 0.1f;
		private float deltaTime;

		private void Update()
		{
			bool visible = IsVisible;
			text.enabled = visible;
			if (visible == false)
				return;

			deltaTime += (Time.unscaledDeltaTime - deltaTime) * smoothing;
			text.text = Mathf.Ceil(1.0f / deltaTime).ToString();
		}
	}
}
