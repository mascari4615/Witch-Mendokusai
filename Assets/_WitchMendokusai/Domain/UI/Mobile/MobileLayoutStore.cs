using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 손가락 조작 장치를 사용자가 옮긴 자리를 **그 기기에만** 기억한다 (TASK-WM-200).
	///
	/// ★ 왜 저장 파일이 아닌가: 「엄지가 닿는 자리」는 세이브가 아니라 *그 기기의 손*에 딸린 값이다.
	///   폰에서 옮긴 자리가 PC 세이브를 따라다니면, 화면도 손도 다른 곳에서 엉뚱한 배치가 된다.
	/// ★ 왜 비율(0~1)로 저장하나: 픽셀로 적으면 화면이 바뀌는 순간(회전·다른 기기·창 크기)
	///   버튼이 화면 밖으로 나가 **영영 못 누르는** 상태가 된다. 비율이면 어디서든 화면 안이다.
	/// </summary>
	public static class MobileLayoutStore
	{
		private const string KEY_PREFIX = "WM.Mobile.Layout.";

		/// <summary>옮긴 양(화면 크기 대비 비율). 없으면 (0,0) = 원래 자리.</summary>
		public static Vector2 Load(string elementName)
		{
			return new Vector2(
				PlayerPrefs.GetFloat($"{KEY_PREFIX}{elementName}.x", 0f),
				PlayerPrefs.GetFloat($"{KEY_PREFIX}{elementName}.y", 0f));
		}

		public static void Save(string elementName, Vector2 normalizedOffset)
		{
			PlayerPrefs.SetFloat($"{KEY_PREFIX}{elementName}.x", normalizedOffset.x);
			PlayerPrefs.SetFloat($"{KEY_PREFIX}{elementName}.y", normalizedOffset.y);
			PlayerPrefs.Save();
		}

		public static void Clear(string elementName)
		{
			PlayerPrefs.DeleteKey($"{KEY_PREFIX}{elementName}.x");
			PlayerPrefs.DeleteKey($"{KEY_PREFIX}{elementName}.y");
			PlayerPrefs.Save();
		}

		/// <summary>
		/// 옮긴 결과가 화면 밖으로 못 나가게 자른다.
		///
		/// ★ 이 함수가 이 기능의 안전장치다 — 사용자가 버튼을 화면 밖으로 끌어 놓으면
		///   그 버튼을 다시 누를 방법이 없어서 *되돌릴 수도 없다*. 그래서 항상 일부는 화면 안에 남긴다.
		/// 순수 계산이라 시험으로 못 박는다 (Unity 없이 돈다).
		/// </summary>
		public static Vector2 ClampToScreen(
			Vector2 desiredOffset, Rect elementRect, Vector2 screenSize, float minVisible)
		{
			float minX = -elementRect.xMax + minVisible;
			float maxX = screenSize.x - elementRect.xMin - minVisible;
			float minY = -elementRect.yMax + minVisible;
			float maxY = screenSize.y - elementRect.yMin - minVisible;

			// 요소가 화면보다 크면 위 범위가 뒤집힌다 — 그때는 안 움직이는 편이 낫다(0 이 원래 자리).
			return new Vector2(
				minX <= maxX ? Mathf.Clamp(desiredOffset.x, minX, maxX) : 0f,
				minY <= maxY ? Mathf.Clamp(desiredOffset.y, minY, maxY) : 0f);
		}
	}
}
