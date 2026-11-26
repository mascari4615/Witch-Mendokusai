using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 건설. 위 시점 (CityView) 에 들어가면 켜짐. 집기, 돌리기, 놓기, 거두기, 되돌리기
	// 커서는 마우스가 가리킨 칸. 키만 쓸 때는 화면 가운데 칸이라 카메라를 움직여 고름
	// 손가락은 톡한 칸. 손가락을 따라다니면 두 손가락으로 시점을 미는 동안 미리보기가 튐
	public sealed partial class VillageStage
	{
		// 번호 키가 있는 칸. 그 뒤 칸은 눌러서 고름
		private const int SLOT_COUNT = 9;

		[Header("Build")]
		[SerializeField] private float buildHeight = 24f;
		[SerializeField] private float buildLimit = 30f;
		[Tooltip("휠로 오가는 높이와 한 칸의 변화")]
		[SerializeField] private Vector2 buildZoom = new(9f, 46f);
		[SerializeField] private float buildZoomStep = 4f;
		[SerializeField] private float pointerWake = 3f;
		[SerializeField] private float footprintHeight = 0.12f;
		[SerializeField] private Color footprintOk = new(0.45f, 1f, 0.55f, 0.42f);
		[SerializeField] private Color footprintNo = new(1f, 0.35f, 0.3f, 0.5f);
		[SerializeField] private Color footprintIdle = new(1f, 0.92f, 0.5f, 0.35f);

		private bool isBuilding;
		private VillageBuildHud buildHud;
		private readonly List<PlaceableDef> palette = new();
		private readonly Stack<Action> undo = new();
		private PlaceableDef held;
		private int heldUid;
		private int heldTurn;
		private GameObject heldBody;
		private string heldModel;
		private GameObject footprint;
		private Mesh footprintMesh;
		private bool usesPointer;
		private Vector2 lastPointer;
		private Vector2Int cursor;
		private readonly BuildTapRule tapRule = new();
		private bool hasTouchCursor;

		private void StepBuilding()
		{
			// 시점 순환이 자유비행으로 넘어가지 않고 바로 평소 시점으로
			if (isBuilding && cameraManager.CurrentContentMode == ContentCameraMode.FreeFly)
				LeaveBuildView();
			bool wanted = cameraManager != null && cameraManager.CurrentContentMode == ContentCameraMode.CityView;
			if (wanted != isBuilding)
			{
				if (wanted)
					StartBuilding();
				else
					StopBuilding();
			}
			if (isBuilding == false)
				return;
			MoveCursor();
			ShowHeld();
		}

		private void StartBuilding()
		{
			isBuilding = true;
			palette.Clear();
			foreach (PlaceableDef placeable in Catalog.Placeables)
			{
				if (placeable.Kind != PlaceableKind.Building)
					palette.Add(placeable);
			}
			Vector3 player = playerProvider.Current.transform.position;
			if (OverheadContentCameraController.TryGet(ContentCameraMode.CityView, out OverheadContentCameraController overhead))
			{
				overhead.SetFocusBounds(transform.TransformPoint(new Vector3(Catalog.VillageArea.X + Catalog.VillageArea.Width * 0.5f, 0f, Catalog.VillageArea.Z + Catalog.VillageArea.Depth * 0.5f)), buildLimit);
				overhead.ConfigureZoom(buildZoom.x, buildZoom.y, buildZoomStep);
				overhead.ResetView(player, cameraManager.OrbitAngles.x, buildHeight);
			}
			buildHud = manager.Hud.ShowBuild(palette, SLOT_COUNT, new VillageBuildActions(TakeNew, TurnHeld, RemoveAtCursor, DropHeld, Undo, LeaveBuildView));
			hasTouchCursor = false;
			tapRule.Forget();
			inputManager.RegisterInputEvent(InputEventType.Click0, InputEventResponseType.Started, ClickCell);
			inputManager.RegisterInputEvent(InputEventType.Submit, InputEventResponseType.Performed, UseCell);
			inputManager.RegisterInputEvent(InputEventType.Click1, InputEventResponseType.Started, DropHeld);
			inputManager.RegisterInputEvent(InputEventType.VillageTurn, InputEventResponseType.Performed, TurnHeld);
			inputManager.RegisterInputEvent(InputEventType.VillageRemove, InputEventResponseType.Performed, RemoveAtCursor);
			inputManager.RegisterInputEvent(InputEventType.VillageUndo, InputEventResponseType.Performed, Undo);
			for (int i = 0; i < SLOT_COUNT; i++)
				inputManager.RegisterInputEvent(InputEventType.HotbarSlot1 + i, InputEventResponseType.Performed, SlotAction(i));
			ShowGrid(true);
			RefreshBuildHud();
		}

		private void LeaveBuildView() => cameraManager.SetContentCameraMode(ContentCameraMode.Normal);

		// 손가락 꾸미기 버튼. F6 순환의 첫 칸과 같은 곳
		private void EnterBuildView() => cameraManager.SetContentCameraMode(ContentCameraMode.CityView);

		private void StopBuilding()
		{
			if (isBuilding == false)
				return;
			isBuilding = false;
			Release();
			undo.Clear();
			inputManager.UnregisterInputEvent(InputEventType.Click0, InputEventResponseType.Started, ClickCell);
			inputManager.UnregisterInputEvent(InputEventType.Submit, InputEventResponseType.Performed, UseCell);
			inputManager.UnregisterInputEvent(InputEventType.Click1, InputEventResponseType.Started, DropHeld);
			inputManager.UnregisterInputEvent(InputEventType.VillageTurn, InputEventResponseType.Performed, TurnHeld);
			inputManager.UnregisterInputEvent(InputEventType.VillageRemove, InputEventResponseType.Performed, RemoveAtCursor);
			inputManager.UnregisterInputEvent(InputEventType.VillageUndo, InputEventResponseType.Performed, Undo);
			for (int i = 0; i < SLOT_COUNT; i++)
				inputManager.UnregisterInputEvent(InputEventType.HotbarSlot1 + i, InputEventResponseType.Performed, SlotAction(i));
			if (footprint != null)
				Destroy(footprint);
			footprint = null;
			ShowGrid(false);
			buildHud = null;
			if (manager.Hud != null)
				manager.Hud.HideBuild();
			manager.Save();
		}

		private readonly Dictionary<int, Action> slotActions = new();

		// 해제할 때 같은 것을 넘기도록 칸마다 하나씩 둠
		private Action SlotAction(int slot)
		{
			if (slotActions.TryGetValue(slot, out Action action) == false)
			{
				action = () =>
				{
					if (slot < palette.Count)
						TakeNew(palette[slot]);
				};
				slotActions.Add(slot, action);
			}
			return action;
		}

		// ---- 커서

		private void MoveCursor()
		{
			if (inputManager.IsTouchMode)
			{
				MoveCursorByTap();
				return;
			}
			Vector2 pointer = inputManager.MouseScreenPosition;
			// 막대 버튼 위의 마우스는 칸을 고르지 않음. 안 그러면 거두기, 돌리기 버튼이 버튼 뒤에 가려진 칸에 일함
			if (UIPointer.IsOverInteractive(pointer))
				return;
			if ((pointer - lastPointer).magnitude > pointerWake)
			{
				usesPointer = true;
				lastPointer = pointer;
			}
			else if (inputManager.CameraMoveInput.sqrMagnitude > 0.01f)
			{
				usesPointer = false;
			}
			if (TryCellAt(usesPointer ? pointer : ScreenCenter, out Vector2Int cell))
				cursor = cell;
		}

		private static Vector2 ScreenCenter => new(Screen.width * 0.5f, Screen.height * 0.5f);

		private bool TryCellAt(Vector2 screenPosition, out Vector2Int cell)
		{
			cell = cursor;
			Camera view = Camera.main;
			if (view == null)
				return false;
			Ray ray = view.ScreenPointToRay(screenPosition);
			Plane plane = new(Vector3.up, transform.position);
			if (plane.Raycast(ray, out float distance) == false)
				return false;
			Vector3 local = transform.InverseTransformPoint(ray.GetPoint(distance));
			cell = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.z));
			return true;
		}

		// 손가락. 톡 전에는 화면 가운데 칸, 톡하면 그 칸에 머묾. 끌거나 두 손가락으로 민 것은 톡이 아님 (TouchGesture)
		// 판정 (집기, 미리보기, 놓기) 은 BuildTapRule. 던전 타워 배치와 같은 두 번 톡
		private void MoveCursorByTap()
		{
			if (hasTouchCursor == false && TryCellAt(ScreenCenter, out Vector2Int center))
				cursor = center;
			if (inputManager.PointerTappedThisFrame == false)
				return;
			Vector2 tap = inputManager.PointerTapPosition;
			if (UIPointer.IsOverInteractive(tap) || TryCellAt(tap, out Vector2Int cell) == false)
				return;
			hasTouchCursor = true;
			cursor = cell;
			switch (tapRule.Tap(cell.x, cell.y, held != null))
			{
				case BuildTap.Take:
					TakeAtCursor();
					break;
				case BuildTap.Preview:
					UiSfx.Play(UiSfxEvent.FocusMove);
					break;
				case BuildTap.Put:
					PutHeld();
					break;
			}
		}

		// 커서 칸이 가운데인 자리
		private Vector2Int HeldCorner() =>
			new(cursor.x - (VillageRules.WidthOf(held, heldTurn) - 1) / 2, cursor.y - (VillageRules.DepthOf(held, heldTurn) - 1) / 2);

		private HomesteadResult CheckHeld()
		{
			Vector2Int corner = HeldCorner();
			HomesteadResult result = Sim.CanPut(held.Id, corner.x, corner.y, heldTurn, heldUid);
			return result.Ok && heldUid == 0 ? Sim.CanMakeDecor(held.Id) : result;
		}

		// ---- 하는 일

		private void ClickCell()
		{
			// 손가락 모드는 톡 (MoveCursorByTap) 이 맡음. PC 에서 손가락 모드를 강제로 켜면 마우스 누름과 뗌이 둘 다 와서 한 번 클릭이 두 번 일함
			if (inputManager.IsTouchMode)
				return;
			if (inputManager.IsPointerOverUI() == false)
				UseCell();
		}

		private void UseCell()
		{
			if (held == null)
				TakeAtCursor();
			else
				PutHeld();
		}

		private void TakeNew(PlaceableDef placeable)
		{
			int turn = held == placeable ? heldTurn : 0;
			Release();
			Hold(placeable, 0, turn);
		}

		private void TakeAtCursor()
		{
			PlacedPiece piece = Sim.PieceAt(cursor.x, cursor.y);
			if (piece == null)
				return;
			if (Sim.PlaceableOf(piece).Fixed)
			{
				Refuse(HomesteadResult.Fail(HomesteadFailure.Locked, piece.Id));
				return;
			}
			Hold(Sim.PlaceableOf(piece), piece.Uid, piece.Turn);
			SetBodyVisible(piece, false);
		}

		private void Hold(PlaceableDef placeable, int uid, int turn)
		{
			held = placeable;
			heldUid = uid;
			heldTurn = turn;
			tapRule.Forget();
			UiSfx.Play(UiSfxEvent.FocusMove);
			RefreshBuildHud();
		}

		private void PutHeld()
		{
			Vector2Int corner = HeldCorner();
			if (heldUid == 0)
			{
				HomesteadResult made = Sim.Put(held.Id, corner.x, corner.y, heldTurn, out PlacedPiece placed);
				if (Refuse(made))
					return;
				undo.Push(() => Sim.RemovePiece(placed.Uid));
				UiSfx.Play(UiSfxEvent.Confirm);
				RefreshBuildHud();
				return;
			}
			PlacedPiece piece = Sim.State.Village.Find(candidate => candidate.Uid == heldUid);
			int fromX = piece.X;
			int fromZ = piece.Z;
			int fromTurn = piece.Turn;
			int uid = heldUid;
			int turn = heldTurn;
			HomesteadResult moved = Sim.CanPut(piece.Id, corner.x, corner.y, turn, uid);
			if (Refuse(moved))
				return;
			Release();
			Sim.MovePiece(uid, corner.x, corner.y, turn);
			undo.Push(() => Sim.MovePiece(uid, fromX, fromZ, fromTurn));
			UiSfx.Play(UiSfxEvent.Confirm);
			ShowGrid(true);
			RefreshBuildHud();
		}

		private bool Refuse(HomesteadResult result)
		{
			if (result.Ok)
				return false;
			UiSfx.Play(UiSfxEvent.Blocked);
			buildHud.ShowReason(HomesteadText.Reason(result, Catalog));
			return true;
		}

		private void DropHeld()
		{
			if (held == null)
				return;
			Release();
			UiSfx.Play(UiSfxEvent.Cancel);
			RefreshBuildHud();
		}

		private void Release()
		{
			if (heldUid != 0)
				SetBodyVisible(Sim.State.Village.Find(candidate => candidate.Uid == heldUid), true);
			held = null;
			heldUid = 0;
			tapRule.Forget();
			if (heldBody != null)
				Destroy(heldBody);
			heldBody = null;
			heldModel = null;
		}

		private void SetBodyVisible(PlacedPiece piece, bool visible)
		{
			if (piece == null)
				return;
			if (decorBodies.TryGetValue(piece.Uid, out (string Look, GameObject Body) decor))
				decor.Body.SetActive(visible);
			else if (siteBodies.TryGetValue(piece.Id, out (string Look, GameObject Body) site))
				site.Body.SetActive(visible);
		}

		private void TurnHeld()
		{
			if (held == null)
				TakeAtCursor();
			if (held == null)
				return;
			heldTurn = (heldTurn + 1) % VillageRules.TURNS;
			UiSfx.Play(UiSfxEvent.FocusMove);
		}

		// 든 것이 있으면 그것을, 없으면 커서 아래 것을 거둠
		private void RemoveAtCursor()
		{
			PlacedPiece piece = heldUid != 0 ? Sim.State.Village.Find(candidate => candidate.Uid == heldUid) : Sim.PieceAt(cursor.x, cursor.y);
			bool isNewHeld = held != null && heldUid == 0;
			Release();
			if (isNewHeld || piece == null)
			{
				RefreshBuildHud();
				return;
			}
			string id = piece.Id;
			int x = piece.X;
			int z = piece.Z;
			int turn = piece.Turn;
			if (Refuse(Sim.RemovePiece(piece.Uid)))
				return;
			undo.Push(() => Sim.Put(id, x, z, turn, out PlacedPiece _));
			UiSfx.Play(UiSfxEvent.Cancel);
			RefreshBuildHud();
		}

		private void Undo()
		{
			if (undo.Count == 0)
				return;
			Release();
			undo.Pop()();
			UiSfx.Play(UiSfxEvent.Cancel);
			ShowGrid(true);
			RefreshBuildHud();
		}

		private void RefreshBuildHud() => buildHud?.Refresh(Sim, held, heldUid != 0, undo.Count > 0);

		// ---- 든 것 보이기

		private string ModelOf(PlaceableDef placeable)
		{
			if (placeable.Models.Count > 0)
				return placeable.Models[0];
			return siteBodies.TryGetValue(placeable.Id, out (string Look, GameObject Body) site) ? site.Look.Split('@')[0] : null;
		}

		private void ShowHeld()
		{
			int width = 1;
			int depth = 1;
			Vector2Int corner = cursor;
			Color color = footprintIdle;
			if (held != null)
			{
				corner = HeldCorner();
				width = VillageRules.WidthOf(held, heldTurn);
				depth = VillageRules.DepthOf(held, heldTurn);
				color = CheckHeld().Ok ? footprintOk : footprintNo;
				ShowHeldBody(corner);
			}
			else
			{
				PlacedPiece piece = Sim.PieceAt(cursor.x, cursor.y);
				if (piece != null)
				{
					PlaceableDef placeable = Sim.PlaceableOf(piece);
					corner = new Vector2Int(piece.X, piece.Z);
					width = VillageRules.WidthOf(placeable, piece.Turn);
					depth = VillageRules.DepthOf(placeable, piece.Turn);
				}
			}
			ShowFootprint(corner, width, depth, color);
		}

		private void ShowHeldBody(Vector2Int corner)
		{
			string model = ModelOf(held);
			if (model != heldModel || heldBody == null)
			{
				if (heldBody != null)
					Destroy(heldBody);
				heldBody = model != null ? Models.Build(model, outdoor, Vector3.zero, 0f) : null;
				heldModel = model;
				if (heldBody != null)
				{
					foreach (Collider part in heldBody.GetComponentsInChildren<Collider>())
						part.enabled = false;
				}
			}
			if (heldBody != null)
				heldBody.transform.SetLocalPositionAndRotation(CenterOf(held, corner.x, corner.y, heldTurn), FacingOf(heldTurn));
		}

		private void ShowFootprint(Vector2Int corner, int width, int depth, Color color)
		{
			if (footprint == null)
			{
				footprint = new GameObject("VillageFootprint");
				footprint.transform.SetParent(outdoor, false);
				footprintMesh = new Mesh { name = "VillageFootprint", hideFlags = HideFlags.DontSave };
				footprintMesh.SetVertices(new[] { Vector3.zero, Vector3.forward, new Vector3(1f, 0f, 1f), Vector3.right });
				footprintMesh.SetUVs(0, new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
				footprintMesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
				footprint.AddComponent<MeshFilter>().sharedMesh = footprintMesh;
				MeshRenderer plate = footprint.AddComponent<MeshRenderer>();
				plate.sharedMaterial = LineMaterial;
				plate.shadowCastingMode = ShadowCastingMode.Off;
			}
			footprint.transform.localPosition = new Vector3(corner.x, footprintHeight, corner.y);
			footprint.transform.localScale = new Vector3(width, 1f, depth);
			footprintMesh.SetColors(new[] { color, color, color, color });
		}
	}
}
