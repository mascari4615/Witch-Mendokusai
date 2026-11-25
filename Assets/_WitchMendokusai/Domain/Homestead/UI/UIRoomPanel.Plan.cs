using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// UIRoomPanel.cs 의 Plan 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일에. 방 평면. 가구 들기, 옮기기, 놓기, 되돌리기
	public sealed partial class UIRoomPanel
	{
		// 방 비율 그대로 칸에 가득
		private float PixelsPerMeter => plan.resolvedStyle.width / Sim.HouseTier.Width;

		private void FitPlan()
		{
			VisualElement box = plan.parent;
			if (box.layout.width <= 0 || box.layout.height <= 0)
				return;
			float scale = Mathf.Min(box.layout.width / Sim.HouseTier.Width, box.layout.height / Sim.HouseTier.Depth);
			plan.style.width = Sim.HouseTier.Width * scale;
			plan.style.height = Sim.HouseTier.Depth * scale;
			plan.schedule.Execute(RebuildPieces);
		}

		private void RebuildPieces()
		{
			foreach (VisualElement element in pieceElements.Values)
				element.RemoveFromHierarchy();
			pieceElements.Clear();
			foreach (PlacedFurniture piece in Sim.State.Placed)
			{
				FurnitureDef furniture = Sim.Catalog.Get<FurnitureDef>(piece.Id);
				VisualElement element = MakePiece(furniture);
				PlaceElement(element, furniture, piece.X, piece.Z, piece.Yaw);
				int uid = piece.Uid;
				element.RegisterCallback<PointerDownEvent>(evt =>
				{
					if (held != null)
						return;
					TakePiece(uid);
					justPicked = true;
					evt.StopPropagation();
				});
				element.style.display = uid == heldUid ? DisplayStyle.None : DisplayStyle.Flex;
				pieceElements[uid] = element;
			}
			// 깔개는 아래로
			foreach (PlacedFurniture piece in Sim.State.Placed)
			{
				if (Sim.Catalog.Get<FurnitureDef>(piece.Id).Layer == FurnitureLayer.Rug)
					pieceElements[piece.Uid].SendToBack();
			}
			heldElement?.BringToFront();
			MarkPalette();
		}

		private VisualElement MakePiece(FurnitureDef furniture)
		{
			VisualElement element = new();
			element.AddToClassList(BLOCK + "piece");
			element.AddToClassList(BLOCK + "piece--" + furniture.Layer.ToString().ToLowerInvariant());
			element.AddToClassList(BLOCK + "shape--" + furniture.Shape.Replace('_', '-'));
			Make(BLOCK + "piece-icon", element).style.backgroundImage = new StyleBackground(Art.Icon(furniture.Id));
			Make(BLOCK + "piece-front", element);
			plan.Add(element);
			return element;
		}

		private void PlaceElement(VisualElement element, FurnitureDef furniture, float x, float z, float yaw)
		{
			float scale = PixelsPerMeter;
			float width = furniture.Width * scale;
			float height = furniture.Depth * scale;
			element.style.width = width;
			element.style.height = height;
			element.style.left = (x + Sim.HouseTier.Width / 2f) * scale - width / 2f;
			element.style.top = (Sim.HouseTier.Depth / 2f - z) * scale - height / 2f;
			element.style.rotate = new UnityEngine.UIElements.Rotate(Angle.Degrees(yaw));
			// 그림은 늘 똑바로
			element[0].style.rotate = new UnityEngine.UIElements.Rotate(Angle.Degrees(-yaw));
		}

		private void TakeFromPalette()
		{
			if (spare.Count == 0)
				return;
			Drop();
			held = spare[paletteIndex];
			heldUid = 0;
			heldX = 0f;
			heldZ = 0f;
			heldYaw = 0f;
			BeginHold();
		}

		private void TakePiece(int uid)
		{
			PlacedFurniture piece = Sim.State.Placed.Find(candidate => candidate.Uid == uid);
			held = Sim.Catalog.Get<FurnitureDef>(piece.Id);
			heldUid = uid;
			heldX = piece.X;
			heldZ = piece.Z;
			heldYaw = piece.Yaw;
			pieceUid = uid;
			pieceElements[uid].style.display = DisplayStyle.None;
			BeginHold();
		}

		private void BeginHold()
		{
			focus = Focus.Plan;
			heldElement = MakePiece(held);
			heldElement.AddToClassList(BLOCK + "piece--held");
			heldElement.pickingMode = PickingMode.Ignore;
			UiSfx.Play(UiSfxEvent.FocusMove);
			MoveHeld(heldX, heldZ);
			MarkPalette();
		}

		private void MoveHeld(float x, float z)
		{
			HouseTierDef room = Sim.HouseTier;
			if (held.Layer == FurnitureLayer.Wall)
			{
				RoomRules.SnapToWall(held, room, ref x, ref z, out heldYaw);
				heldX = x;
				heldZ = z;
			}
			else
			{
				RoomRules.Extent(held, heldYaw, out float halfX, out float halfZ);
				float limitX = Mathf.Max(0f, room.Width / 2f - halfX);
				float limitZ = Mathf.Max(0f, room.Depth / 2f - halfZ);
				heldX = Mathf.Clamp(Mathf.Round(x / moveStep) * moveStep, -limitX, limitX);
				heldZ = Mathf.Clamp(Mathf.Round(z / moveStep) * moveStep, -limitZ, limitZ);
			}
			PlaceElement(heldElement, held, heldX, heldZ, heldYaw);
			heldElement.EnableInClassList(BLOCK + "piece--no", Sim.CanPlace(held.Id, heldX, heldZ, heldYaw, heldUid).Ok == false);
			RefreshHint();
		}

		private void Turn(int direction)
		{
			if (held == null || held.Layer == FurnitureLayer.Wall)
				return;
			heldYaw = Mathf.Repeat(heldYaw + turnStep * direction, 360f);
			UiSfx.Play(UiSfxEvent.FocusMove);
			MoveHeld(heldX, heldZ);
		}

		private void PutDown()
		{
			List<PlacedFurniture> before = Snapshot();
			HomesteadResult result = heldUid == 0 ? Sim.Place(held.Id, heldX, heldZ, heldYaw, out PlacedFurniture _) : Sim.Move(heldUid, heldX, heldZ, heldYaw);
			if (result.Ok == false)
			{
				UiSfx.Play(UiSfxEvent.PurchaseFail);
				return;
			}
			undo.Push(before);
			FurnitureDef placed = held;
			bool wasNew = heldUid == 0;
			UiSfx.Play(UiSfxEvent.Confirm);
			Drop();
			UpdateUI();
			// 같은 것이 더 있으면 이어서 놓기
			if (wasNew && Sim.SpareCount(placed.Id) > 0)
			{
				paletteIndex = spare.IndexOf(placed);
				TakeFromPalette();
				return;
			}
			focus = wasNew ? Focus.Palette : Focus.Plan;
			MarkPalette();
		}

		private void PickUpHeld()
		{
			if (held == null)
				return;
			if (heldUid != 0)
			{
				undo.Push(Snapshot());
				Sim.PickUp(heldUid);
			}
			UiSfx.Play(UiSfxEvent.Cancel);
			Drop();
			focus = Focus.Palette;
			UpdateUI();
		}

		private void PickUpAll()
		{
			undo.Push(Snapshot());
			Drop();
			Sim.PickUpAll();
			UiSfx.Play(UiSfxEvent.Cancel);
			focus = Focus.Palette;
			UpdateUI();
		}

		private List<PlacedFurniture> Snapshot() =>
			Sim.State.Placed.ConvertAll(piece => new PlacedFurniture { Uid = piece.Uid, Id = piece.Id, X = piece.X, Z = piece.Z, Yaw = piece.Yaw });

		private void Undo()
		{
			if (undo.Count == 0)
				return;
			Drop();
			Sim.RestorePlacement(undo.Pop());
			UiSfx.Play(UiSfxEvent.Cancel);
			UpdateUI();
		}

		private void Drop()
		{
			heldElement?.RemoveFromHierarchy();
			heldElement = null;
			held = null;
			heldUid = 0;
		}

		// 그 방향에 있는 가장 가까운 가구로. 왼쪽에 없으면 목록으로
		private void MovePlan(Vector2 direction)
		{
			PlacedFurniture current = Sim.State.Placed.Find(piece => piece.Uid == pieceUid);
			PlacedFurniture best = null;
			float bestScore = float.MaxValue;
			foreach (PlacedFurniture piece in Sim.State.Placed)
			{
				if (current == null || piece.Uid == current.Uid)
					continue;
				Vector2 offset = new(piece.X - current.X, piece.Z - current.Z);
				float along = Vector2.Dot(offset, direction);
				if (along <= 0.01f)
					continue;
				float score = along + (offset - direction * along).magnitude * 2f;
				if (score < bestScore)
				{
					bestScore = score;
					best = piece;
				}
			}
			if (best != null)
				pieceUid = best.Uid;
			else if (direction.x < 0f)
				focus = Focus.Palette;
			UiSfx.Play(UiSfxEvent.FocusMove);
			MarkPalette();
		}

		private void OnPlanPointerMove(PointerMoveEvent evt)
		{
			if (held == null || evt.deltaPosition.sqrMagnitude <= 0f)
				return;
			justPicked = false;
			float scale = PixelsPerMeter;
			Vector2 local = plan.WorldToLocal(evt.position);
			MoveHeld(local.x / scale - Sim.HouseTier.Width / 2f, Sim.HouseTier.Depth / 2f - local.y / scale);
		}

		private void OnPlanPointerUp(PointerUpEvent evt)
		{
			if (held == null)
				return;
			if (justPicked)
			{
				// 누르기만 한 것. 든 채로 두어 돌리기와 거두기를 쓰게
				justPicked = false;
				return;
			}
			PutDown();
		}
	}
}

