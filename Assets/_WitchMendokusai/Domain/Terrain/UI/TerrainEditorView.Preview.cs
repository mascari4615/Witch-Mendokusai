using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// TerrainEditorView 의 미리보기 조작 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TerrainEditorView.cs 를 본다.
	public partial class TerrainEditorView : VisualElement
	{
		private Vector2 mesh3dRotation = new(30f, 45f);
		private float mesh3dZoom = 1f;
		private bool isDragging;
		private Vector2 lastPointerPosition;

		private void SetPreviewMode(TerrainPreviewMode mode)
		{
			if (previewMode == mode)
				return;
			previewMode = mode;
			ApplyPreviewModeButtonStyle();
			Regenerate();
		}

		private void ApplyPreviewModeButtonStyle()
		{
			Color active = new(0.32f, 0.55f, 0.78f, 1f);
			Color inactive = new(0.22f, 0.22f, 0.22f, 1f);
			previewHeightmapButton.style.backgroundColor = previewMode == TerrainPreviewMode.Heightmap ? active : inactive;
			previewSlopeButton.style.backgroundColor = previewMode == TerrainPreviewMode.Slope ? active : inactive;
			previewBiomeButton.style.backgroundColor = previewMode == TerrainPreviewMode.Biome ? active : inactive;
			previewMesh3DButton.style.backgroundColor = previewMode == TerrainPreviewMode.Mesh3D ? active : inactive;
		}

		private void OnPreviewGeometryChanged(GeometryChangedEvent evt)
		{
			int newWidth = Mathf.Clamp((int)evt.newRect.width, PREVIEW_MIN_SIZE, PREVIEW_MAX_SIZE);
			int newHeight = Mathf.Clamp((int)evt.newRect.height, PREVIEW_MIN_SIZE, PREVIEW_MAX_SIZE);
			if (newWidth == previewPixelWidth && newHeight == previewPixelHeight)
				return;
			previewPixelWidth = newWidth;
			previewPixelHeight = newHeight;

			// splitter 드래그 등 GeometryChangedEvent 가 매 frame fire — Regenerate 폭주 방지.
			// 마지막 변경 후 150ms 무 변동 시 1회만 Regenerate.
			geometryRegenerateSchedule?.Pause();
			geometryRegenerateSchedule = schedule.Execute(Regenerate).StartingIn(150);
		}

		private void OnPreviewPointerDown(PointerDownEvent evt)
		{
			if (previewMode != TerrainPreviewMode.Mesh3D)
				return;
			isDragging = true;
			lastPointerPosition = evt.position;
			previewImage.CapturePointer(evt.pointerId);
			evt.StopPropagation();
		}

		private void OnPreviewPointerMove(PointerMoveEvent evt)
		{
			if (isDragging == false)
				return;
			if (previewMode != TerrainPreviewMode.Mesh3D)
				return;

			Vector2 cur = (Vector2)evt.position;
			Vector2 delta = cur - lastPointerPosition;
			lastPointerPosition = cur;

			mesh3dRotation.y += delta.x * 0.4f;
			mesh3dRotation.x = Mathf.Clamp(mesh3dRotation.x + delta.y * 0.4f, -85f, 85f);

			Regenerate();
			evt.StopPropagation();
		}

		private void OnPreviewPointerUp(PointerUpEvent evt)
		{
			if (isDragging == false)
				return;
			isDragging = false;
			if (previewImage.HasPointerCapture(evt.pointerId))
				previewImage.ReleasePointer(evt.pointerId);
			evt.StopPropagation();
		}

		private void OnPreviewWheel(WheelEvent evt)
		{
			if (previewMode != TerrainPreviewMode.Mesh3D)
				return;
			float zoomDelta = -evt.delta.y * 0.1f;
			mesh3dZoom = Mathf.Clamp(mesh3dZoom + zoomDelta, 0.2f, 4f);
			Regenerate();
			evt.StopPropagation();
		}
	}
}
