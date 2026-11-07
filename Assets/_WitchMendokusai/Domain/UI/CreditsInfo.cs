using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 설정창 크레딧 탭. 라이선스 원문을 빌드에 싣기 위해 TextAsset으로 참조
	[CreateAssetMenu(fileName = nameof(CreditsInfo), menuName = "WM/UI/" + nameof(CreditsInfo))]
	public class CreditsInfo : ScriptableObject
	{
		[field: SerializeField] public List<CreditEntry> Fonts { get; private set; } = new();
	}

	[Serializable]
	public class CreditEntry
	{
		[field: SerializeField] public string Name { get; private set; }
		[field: SerializeField] public string LicenseName { get; private set; }
		[field: SerializeField] public TextAsset License { get; private set; }

		// 라이선스 파일 첫 줄의 저작권 표기
		public string CopyrightLine => FirstLine(License.text);

		public static string FirstLine(string text)
		{
			foreach (string line in text.Split('\n'))
			{
				string trimmed = line.Trim();
				if (trimmed.Length > 0)
					return trimmed;
			}
			return string.Empty;
		}
	}
}
