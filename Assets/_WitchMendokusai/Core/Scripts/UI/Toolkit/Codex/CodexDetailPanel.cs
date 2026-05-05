using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 디테일 베이스 — 좌 큰 일러스트 + 우 정보 패널. 엔드필드 디테일 톤.
	///
	/// 책임 분리(하이브리드):
	/// - 베이스(이 클래스)가 *공통* 표시 — 큰 일러스트(entry.Icon, fallback 색 박스), 이름, grade 라벨, 카테고리 라벨, id
	/// - 카테고리 `BuildDetail(entry)` = *카테고리 특화 정보 영역*만 — body 컨테이너에 attach
	/// </summary>
	public class CodexDetailPanel : VisualElement
	{
		public const string USS_CLASS = "wm-codex-detail";
		public const string USS_ILLUSTRATION = "wm-codex-detail__illustration";
		public const string USS_ILLUSTRATION_IMAGE = "wm-codex-detail__illustration-image";
		public const string USS_ILLUSTRATION_FALLBACK = "wm-codex-detail__illustration--fallback";
		public const string USS_INFO = "wm-codex-detail__info";
		public const string USS_NAME = "wm-codex-detail__name";
		public const string USS_META = "wm-codex-detail__meta";
		public const string USS_META_GRADE_PREFIX = "wm-codex-detail__meta--grade-";
		public const string USS_BODY = "wm-codex-detail__body";

		public CodexDetailPanel(CodexEntry entry, ICodexCategory category)
		{
			AddToClassList(USS_CLASS);
			style.flexDirection = FlexDirection.Row;
			style.flexGrow = 1;

			VisualElement illustration = new();
			illustration.AddToClassList(USS_ILLUSTRATION);
			if (entry.Icon != null)
			{
				Image image = new()
				{
					sprite = entry.Icon,
					scaleMode = ScaleMode.ScaleToFit,
				};
				image.AddToClassList(USS_ILLUSTRATION_IMAGE);
				image.pickingMode = PickingMode.Ignore;
				illustration.Add(image);
			}
			else
			{
				illustration.AddToClassList(USS_ILLUSTRATION_FALLBACK);
			}
			Add(illustration);

			VisualElement info = new();
			info.AddToClassList(USS_INFO);
			info.style.flexGrow = 1;
			info.style.flexDirection = FlexDirection.Column;
			Add(info);

			Label nameLabel = new(entry.DisplayName);
			nameLabel.AddToClassList(USS_NAME);
			info.Add(nameLabel);

			Label metaLabel = new(BuildMetaText(entry, category));
			metaLabel.AddToClassList(USS_META);
			if (string.IsNullOrEmpty(entry.GradeKey) == false)
				metaLabel.AddToClassList(USS_META_GRADE_PREFIX + entry.GradeKey);
			info.Add(metaLabel);

			VisualElement body = new();
			body.AddToClassList(USS_BODY);
			body.style.flexGrow = 1;
			info.Add(body);

			VisualElement categoryDetail = category.BuildDetail(entry);
			if (categoryDetail != null)
				body.Add(categoryDetail);
		}

		private static string BuildMetaText(CodexEntry entry, ICodexCategory category)
		{
			string categoryLabel = category.DisplayName;
			string subGroup = string.IsNullOrEmpty(entry.SubGroup) ? null : entry.SubGroup;
			string gradeLabel = string.IsNullOrEmpty(entry.GradeKey) ? null : entry.GradeKey;
			string idLabel = entry.Id;

			System.Text.StringBuilder builder = new();
			builder.Append(categoryLabel);
			if (subGroup != null)
			{
				builder.Append(" · ");
				builder.Append(subGroup);
			}
			if (gradeLabel != null)
			{
				builder.Append(" · ");
				builder.Append(gradeLabel);
			}
			builder.Append("    [");
			builder.Append(idLabel);
			builder.Append("]");
			return builder.ToString();
		}
	}
}
