using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	// 설정창 크레딧 탭 데이터. 항목 추가와 제거는 Entries 줄만, 코드 수정 없음
	// 라이선스 원문: 빌드 포함용 TextAsset 참조
	[CreateAssetMenu(fileName = nameof(CreditsInfo), menuName = "WM/UI/" + nameof(CreditsInfo))]
	public class CreditsInfo : ScriptableObject
	{
		// 옛 이름 Fonts (2026-11-20 이전 자산 호환)
		[field: SerializeField]
		[field: FormerlySerializedAs("<Fonts>k__BackingField")]
		public List<CreditEntry> Entries { get; private set; } = new();

		public CreditEntry FindById(string id)
		{
			foreach (CreditEntry entry in Entries)
			{
				if (entry.Id == id)
					return entry;
			}
			return null;
		}

		// 분류 순서대로, 분류 안은 자산 순서 그대로
		public List<CreditEntry> InCategory(CreditCategory category)
		{
			List<CreditEntry> result = new();
			foreach (CreditEntry entry in Entries)
			{
				if (entry.Category == category)
					result.Add(entry);
			}
			return result;
		}

		// 빈 필드, 겹친 Id. 빈 목록이면 통과
		public List<string> Validate()
		{
			List<string> problems = new();
			HashSet<string> seenIds = new();
			foreach (CreditEntry entry in Entries)
			{
				problems.AddRange(entry.Validate());
				if (string.IsNullOrWhiteSpace(entry.Id) == false && seenIds.Add(entry.Id) == false)
					problems.Add($"[{entry.Id}] Id 중복");
			}
			return problems;
		}
	}

	// 값은 직렬화 번호. 순서 바꾸기와 중간 삽입 금지, 새 분류는 끝에
	public enum CreditCategory
	{
		Font = 0,
		Icon = 1,
		Sprite = 2,
		Model3D = 3,
		Animation = 4,
		Texture = 5,
		Effect = 6,
		Sfx = 7,
		Music = 8,
		Library = 9,
	}

	public static class CreditCategoryText
	{
		// 화면 표시 순서
		public static readonly CreditCategory[] Order =
		{
			CreditCategory.Font,
			CreditCategory.Icon,
			CreditCategory.Sprite,
			CreditCategory.Model3D,
			CreditCategory.Animation,
			CreditCategory.Texture,
			CreditCategory.Effect,
			CreditCategory.Sfx,
			CreditCategory.Music,
			CreditCategory.Library,
		};

		public static string Label(CreditCategory category) => category switch
		{
			CreditCategory.Font => "글꼴",
			CreditCategory.Icon => "아이콘",
			CreditCategory.Sprite => "2D 그림",
			CreditCategory.Model3D => "3D 모델",
			CreditCategory.Animation => "애니메이션",
			CreditCategory.Texture => "텍스처",
			CreditCategory.Effect => "연출",
			CreditCategory.Sfx => "효과음",
			CreditCategory.Music => "음악",
			CreditCategory.Library => "라이브러리",
			_ => category.ToString(),
		};
	}

	[Serializable]
	public class CreditEntry
	{
		[field: SerializeField, Tooltip("영문 소문자와 하이픈. .origin 파일의 credit: 값")] public string Id { get; private set; }
		[field: SerializeField] public CreditCategory Category { get; private set; }
		[field: SerializeField, Tooltip("작품 제목")] public string Name { get; private set; }
		[field: SerializeField, Tooltip("제작자")] public string Author { get; private set; }
		[field: SerializeField, Tooltip("원본 주소")] public string SourceUrl { get; private set; }
		[field: SerializeField] public string LicenseName { get; private set; }
		[field: SerializeField] public string LicenseUrl { get; private set; }
		[field: SerializeField, Tooltip("라이선스 원문. 없으면 LicenseUrl 만 표시")] public TextAsset License { get; private set; }
		[field: SerializeField, Tooltip("원문에 저작권 줄이 없는 공용 원문 (MIT 등) 의 저작권 줄")] public string CopyrightNotice { get; private set; }
		[field: SerializeField, Tooltip("CC BY, OFL, MIT 처럼 표기나 고지가 조건인 것")] public bool AttributionRequired { get; private set; }
		[field: SerializeField] public bool Modified { get; private set; }
		[field: SerializeField, Tooltip("수정 내용. Modified 면 필수")] public string ModificationNote { get; private set; }
		[field: SerializeField, Tooltip("쓰는 곳 (게임 안 위치)")] public string UsedIn { get; private set; }
		[field: SerializeField, Tooltip("프로젝트 루트 기준 파일이나 폴더. origin 검사의 대상 범위")] public List<string> AssetPaths { get; private set; } = new();

		// 저작권 표기: 직접 적은 줄, 라이선스 원문 첫 줄, 제작자 순
		public string CopyrightLine
		{
			get
			{
				if (string.IsNullOrWhiteSpace(CopyrightNotice) == false)
					return CopyrightNotice;
				if (License != null)
					return FirstLine(License.text);
				return Author;
			}
		}

		public List<string> Validate()
		{
			List<string> problems = new();
			string label = string.IsNullOrWhiteSpace(Id) ? Name : Id;
			if (string.IsNullOrWhiteSpace(Id))
				problems.Add($"[{label}] Id 비었음");
			if (string.IsNullOrWhiteSpace(Name))
				problems.Add($"[{label}] 제목 비었음");
			if (string.IsNullOrWhiteSpace(Author))
				problems.Add($"[{label}] 제작자 비었음");
			if (string.IsNullOrWhiteSpace(SourceUrl))
				problems.Add($"[{label}] 원본 주소 비었음");
			if (string.IsNullOrWhiteSpace(LicenseName))
				problems.Add($"[{label}] 라이선스 이름 비었음");
			if (string.IsNullOrWhiteSpace(LicenseUrl) && License == null)
				problems.Add($"[{label}] 라이선스 주소와 원문 둘 다 비었음");
			if (Modified && string.IsNullOrWhiteSpace(ModificationNote))
				problems.Add($"[{label}] 수정했는데 수정 내용 비었음");
			if (AttributionRequired && string.IsNullOrWhiteSpace(CopyrightLine))
				problems.Add($"[{label}] 표기 필수인데 저작권 줄 비었음");
			return problems;
		}

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

	// 에셋 옆 .origin 파일 한 장. 형식: "credit: <Id>" 와 "license: <LicenseName>" 줄, 나머지 줄은 메모
	public readonly struct CreditOrigin
	{
		public const string FILE_EXTENSION = ".origin";

		public readonly string CreditId;
		public readonly string LicenseName;

		public CreditOrigin(string creditId, string licenseName)
		{
			CreditId = creditId;
			LicenseName = licenseName;
		}

		public static CreditOrigin Parse(string text)
		{
			string creditId = string.Empty;
			string licenseName = string.Empty;
			foreach (string rawLine in text.Split('\n'))
			{
				// 윈도 편집기의 BOM 제거
				string line = rawLine.Trim().TrimStart('﻿');
				if (TryValue(line, "credit:", out string creditValue))
					creditId = creditValue;
				else if (TryValue(line, "license:", out string licenseValue))
					licenseName = licenseValue;
			}
			return new CreditOrigin(creditId, licenseName);
		}

		// 크레딧 자산과 어긋난 점. 빈 목록이면 통과
		public List<string> Check(CreditsInfo credits, string originPath)
		{
			List<string> problems = new();
			if (string.IsNullOrWhiteSpace(CreditId))
			{
				problems.Add($"{originPath}: credit: 줄 없음");
				return problems;
			}
			CreditEntry entry = credits.FindById(CreditId);
			if (entry == null)
			{
				problems.Add($"{originPath}: 크레딧에 없는 Id '{CreditId}'");
				return problems;
			}
			if (string.IsNullOrWhiteSpace(LicenseName) == false && string.Equals(LicenseName, entry.LicenseName, StringComparison.OrdinalIgnoreCase) == false)
				problems.Add($"{originPath}: 라이선스 '{LicenseName}' 가 크레딧 '{entry.LicenseName}' 와 다름");
			return problems;
		}

		private static bool TryValue(string line, string key, out string value)
		{
			if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
			{
				value = line.Substring(key.Length).Trim();
				return true;
			}
			value = string.Empty;
			return false;
		}
	}
}
