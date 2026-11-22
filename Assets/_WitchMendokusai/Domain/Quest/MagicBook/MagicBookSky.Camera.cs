using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 하늘의 카메라. 빈 하늘 끌기, 휠 확대 (커서 기준), 고른 별이 가장자리로 가면 따라가기, 장으로 날아가기.
	/// 시간은 멈춘 채로 열리므로 실제 시간 (unscaled) 으로 움직임
	/// </summary>
	public sealed partial class MagicBookSky
	{
		private const long TICK_MILLISECONDS = 16;

		private Rect skyBounds;
		private Vector2 cameraCenter;
		private float cameraZoom = 1f;
		private Vector2 targetCenter;
		private float targetZoom = 1f;
		private bool isDragging;
		private Vector2 dragStartPointer;
		private Vector2 dragStartCenter;
		private IVisualElementScheduledItem tick;

		/// <summary>카메라가 움직인 뒤. 팝업이 고른 별을 따라 붙도록</summary>
		public event Action CameraMoved = delegate { };

		public float Zoom => cameraZoom;

		private void RegisterCameraCallbacks()
		{
			RegisterCallback<WheelEvent>(OnWheel);
			RegisterCallback<PointerDownEvent>(OnDragDown);
			RegisterCallback<PointerMoveEvent>(OnDragMove);
			RegisterCallback<PointerUpEvent>(OnDragUp);
			RegisterCallback<PointerCaptureOutEvent>(_ => isDragging = false);
			RegisterCallback<GeometryChangedEvent>(_ => ApplyCamera());
		}

		/// <summary>하늘 좌표를 이 요소 안 좌표로 (팝업 자리 계산용)</summary>
		public Vector2 SkyToLocal(Vector2 sky) => ViewCenter + (sky - cameraCenter) * cameraZoom;

		private Vector2 ViewCenter => new(contentRect.width * 0.5f, contentRect.height * 0.5f);

		/// <summary>하늘 전체가 화면에 들어오게. 열 때. 아직 크기가 없으면 false</summary>
		public bool FitAll(bool instant)
		{
			Vector2 view = contentRect.size;
			if (view.x <= 0f || view.y <= 0f)
			{
				return false;
			}
			float zoom = skyBounds.width > 0f && skyBounds.height > 0f ? Mathf.Min(view.x / skyBounds.width, view.y / skyBounds.height) * settings.FitMargin : 1f;
			MoveTo(skyBounds.center, Mathf.Clamp(zoom, settings.ZoomMin, settings.ZoomMax), instant);
			return true;
		}

		/// <summary>장 하나로 다가감 (위 줄 장 이름을 골랐을 때)</summary>
		public void FlyToChapter(ChapterSO chapter)
		{
			if (chapterBounds.TryGetValue(chapter, out Rect bounds))
			{
				MoveTo(bounds.center, Mathf.Clamp(settings.ChapterZoom, settings.ZoomMin, settings.ZoomMax), false);
			}
		}

		/// <summary>고른 별이 화면 안쪽 칸을 벗어났으면 그만큼만 옮김. 안쪽이면 그대로 (흔들리지 않게)</summary>
		public void Follow(Vector2 sky)
		{
			Vector2 view = contentRect.size;
			if (view.x <= 0f || view.y <= 0f)
			{
				return;
			}
			Vector2 half = view * (0.5f - settings.FollowInset) / targetZoom;
			Vector2 offset = sky - targetCenter;
			Vector2 shift = new(Overflow(offset.x, half.x), Overflow(offset.y, half.y));
			if (shift != Vector2.zero)
			{
				MoveTo(targetCenter + shift, targetZoom, false);
			}
		}

		private static float Overflow(float offset, float half)
		{
			if (offset > half)
			{
				return offset - half;
			}
			return offset < -half ? offset + half : 0f;
		}

		private void MoveTo(Vector2 center, float zoom, bool instant)
		{
			targetCenter = center;
			targetZoom = zoom;
			if (instant)
			{
				cameraCenter = center;
				cameraZoom = zoom;
				ApplyCamera();
				return;
			}
			tick ??= schedule.Execute(Tick).Every(TICK_MILLISECONDS);
			tick.Resume();
		}

		private void Tick()
		{
			float blend = 1f - Mathf.Exp(-settings.FollowSharpness * Time.unscaledDeltaTime);
			cameraCenter = Vector2.Lerp(cameraCenter, targetCenter, blend);
			cameraZoom = Mathf.Lerp(cameraZoom, targetZoom, blend);
			bool settled = Vector2.Distance(cameraCenter, targetCenter) * cameraZoom < settings.SettleDistance && Mathf.Abs(cameraZoom - targetZoom) < settings.SettleZoom;
			if (settled)
			{
				cameraCenter = targetCenter;
				cameraZoom = targetZoom;
				tick.Pause();
			}
			ApplyCamera();
		}

		private void ApplyCamera()
		{
			Vector2 translate = ViewCenter - cameraCenter * cameraZoom;
			world.style.translate = new Translate(translate.x, translate.y);
			world.style.scale = new Scale(new Vector3(cameraZoom, cameraZoom, 1f));
			CameraMoved();
		}

		private void OnWheel(WheelEvent evt)
		{
			float zoom = Mathf.Clamp(targetZoom * (evt.delta.y < 0f ? settings.ZoomStep : 1f / settings.ZoomStep), settings.ZoomMin, settings.ZoomMax);
			// 커서 아래 하늘 자리가 그대로 남게
			Vector2 cursor = evt.localMousePosition;
			Vector2 skyUnderCursor = targetCenter + (cursor - ViewCenter) / targetZoom;
			MoveTo(skyUnderCursor - (cursor - ViewCenter) / zoom, zoom, false);
			evt.StopPropagation();
		}

		// 별 위에서 누른 것은 별 버튼 몫. 빈 하늘에서만 끌기
		private void OnDragDown(PointerDownEvent evt)
		{
			if (evt.target is VisualElement target && FindStar(target) != null)
			{
				return;
			}
			isDragging = true;
			dragStartPointer = evt.position;
			dragStartCenter = targetCenter;
			this.CapturePointer(evt.pointerId);
		}

		private void OnDragMove(PointerMoveEvent evt)
		{
			if (isDragging == false)
			{
				return;
			}
			Vector2 delta = (Vector2)evt.position - dragStartPointer;
			// 무대가 화면 배율로 줄어 있으므로 화면 px 를 이 요소 px 로
			float panelScale = worldBound.width > 0f && contentRect.width > 0f ? worldBound.width / contentRect.width : 1f;
			MoveTo(dragStartCenter - delta / (panelScale * cameraZoom), targetZoom, true);
		}

		private void OnDragUp(PointerUpEvent evt)
		{
			if (isDragging == false)
			{
				return;
			}
			isDragging = false;
			this.ReleasePointer(evt.pointerId);
		}

		private static MagicBookStar FindStar(VisualElement element)
		{
			for (VisualElement current = element; current != null; current = current.parent)
			{
				if (current is MagicBookStar star)
				{
					return star;
				}
			}
			return null;
		}
	}
}
