using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 디테일 베이스. 좌 큰 일러스트 + 우 정보 패널.
	///
	/// 책임 분리(하이브리드):
	/// - 베이스(이 클래스)가 *공통* 표시 — 큰 일러스트(entry.Icon, fallback 색 박스), 이름, grade 라벨, 카테고리 라벨, id
	/// - 카테고리 `BuildDetail(entry)` = *카테고리 특화 정보 영역*만 — body 컨테이너에 attach
	/// </summary>
	public class DiscoveryDetailPanel : VisualElement
	{
		public const string USS_CLASS = "wm-discovery-detail";
		public const string USS_ILLUSTRATION = "wm-discovery-detail__illustration";
		public const string USS_ILLUSTRATION_IMAGE = "wm-discovery-detail__illustration-image";
		public const string USS_ILLUSTRATION_FALLBACK = "wm-discovery-detail__illustration--fallback";
		public const string USS_INFO = "wm-discovery-detail__info";
		public const string USS_NAME = "wm-discovery-detail__name";
		public const string USS_META = "wm-discovery-detail__meta";
		public const string USS_META_GRADE_PREFIX = "wm-discovery-detail__meta--grade-";
		public const string USS_BODY = "wm-discovery-detail__body";

		public DiscoveryDetailPanel(EntryDescriptor entry, IEntryProvider category)
		{
			AddToClassList(USS_CLASS);
			style.flexDirection = FlexDirection.Row;
			style.flexGrow = 1;

			VisualElement illustration = new();
			illustration.AddToClassList(USS_ILLUSTRATION);
			bool usingPreview = entry.PreviewPrefab != null;
			if (usingPreview)
			{
				Image rtImage = new()
				{
					scaleMode = ScaleMode.ScaleToFit,
				};
				rtImage.AddToClassList(USS_ILLUSTRATION_IMAGE);
				rtImage.pickingMode = PickingMode.Position;
				illustration.Add(rtImage);

				// TASK-WM-133 — ctor 시점엔 panel == null(아직 미부착) 이라
				// panel-context 미해결. AttachToPanelEvent 에서 panel-root
				// owner-push 된 DiscoveryPreviewController 를 1회 해결·캐싱 →
				// 드래그/detach 콜백은 캡처된 ref 사용(detach 시 조상 walk
				// 불가 타이밍 안전). static Instance reach 제거.
				DiscoveryPreviewController previewController = null;

				RegisterCallback<AttachToPanelEvent>(_ =>
				{
					previewController = this.GetUIServices()?.DiscoveryPreview;
					if (previewController == null)
						return;
					previewController.Show(entry.PreviewPrefab);
					previewController.Activate();
					rtImage.image = previewController.RenderTexture;
				});

				rtImage.RegisterCallback<PointerDownEvent>(evt =>
				{
					rtImage.CapturePointer(evt.pointerId);
					previewController?.BeginDrag();
				});
				rtImage.RegisterCallback<PointerMoveEvent>(evt =>
				{
					if (rtImage.HasPointerCapture(evt.pointerId))
						previewController?.DragYawDelta(evt.deltaPosition.x);
				});
				rtImage.RegisterCallback<PointerUpEvent>(evt =>
				{
					if (rtImage.HasPointerCapture(evt.pointerId))
						rtImage.ReleasePointer(evt.pointerId);
					previewController?.EndDrag();
				});

				// Detail 패널이 detach (다른 entry / Category 뒤로 / 윈도우 닫기) 될 때 카메라 비활성.
				RegisterCallback<DetachFromPanelEvent>(_ =>
				{
					if (previewController == null)
						return;
					previewController.EndDrag();
					previewController.Deactivate();
					previewController = null;
				});
			}
			else if (entry.Icon != null)
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

private static string BuildMetaText(EntryDescriptor entry, IEntryProvider category)
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
