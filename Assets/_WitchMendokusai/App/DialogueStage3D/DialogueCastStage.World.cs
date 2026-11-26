using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace WitchMendokusai.DialogueStage3D
{
	// DialogueCastStage 의 월드 대화 (`@무대 월드`). 플레이하던 자리의 인물을 그대로 쓰고,
	// 게임 메인 카메라를 잠깐 빌려 그 자리에서 대화 샷으로 블렌드해 들어가고 끝나면 게임 카메라 자리로 블렌드해 돌아옴.
	// 빌리는 동안 Cinemachine 은 멈추고, 돌아온 뒤 다시 켬
	public sealed partial class DialogueCastStage
	{
		private readonly HashSet<string> warnedMissing = new HashSet<string>();
		private Camera worldCamera;
		private CinemachineBrain worldBrain;
		private bool wasBrainEnabled;
		private DialogueShotPose gameplayPose;
		private GameObject worldCameraHost;
		private bool isReturning;
		private Vector3 worldFront = Vector3.back;

		private void ShowWorld()
		{
			if (isReturning)
				FinishReturn();
			worldCamera = Camera.main;
			if (worldCamera == null)
				throw new System.InvalidOperationException("월드 대화에 쓸 메인 카메라가 없다");
			// CameraManager 가 Cinemachine 을 손으로 매 프레임 돌리므로 그쪽 임대를 먼저 잡음. 없는 씬이면 Cinemachine 만 끔
			if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager) && cameraManager.TryBeginExternalControl(this) == false)
				throw new System.InvalidOperationException("다른 연출이 게임 카메라를 쓰는 중이다");
			worldBrain = worldCamera.GetComponent<CinemachineBrain>();
			wasBrainEnabled = worldBrain != null && worldBrain.enabled;
			if (worldBrain != null)
				worldBrain.enabled = false;
			gameplayPose = new DialogueShotPose(worldCamera.transform.position, worldCamera.transform.rotation, worldCamera.fieldOfView);
			worldCameraHost = new GameObject("Dialogue world camera driver");
			worldCameraHost.transform.SetParent(transform, false);
			stageCamera = worldCameraHost.AddComponent<DialogueCastCamera>();
			stageCamera.UseCamera(worldCamera, Settings);
			stageCamera.Cut(gameplayPose);
			worldFront = Vector3.back;
		}

		private void HideWorld()
		{
			if (worldCameraHost == null || isReturning)
				return;
			isReturning = true;
			stageCamera.Blend(gameplayPose, Settings.WorldReturnSeconds, FinishReturn);
		}

		// 게임 카메라 자리로 돌아온 뒤 Cinemachine 을 다시 켬
		private void FinishReturn()
		{
			if (worldCamera != null)
			{
				worldCamera.transform.SetPositionAndRotation(gameplayPose.Position, gameplayPose.Rotation);
				worldCamera.fieldOfView = gameplayPose.FieldOfView;
			}
			if (worldBrain != null)
				worldBrain.enabled = wasBrainEnabled;
			if (CameraManager.TryGetExistingInstance(out CameraManager cameraManager))
				cameraManager.EndExternalControl(this);
			if (worldCameraHost != null)
				Destroy(worldCameraHost);
			worldCameraHost = null;
			worldBrain = null;
			worldCamera = null;
			stageCamera = null;
			isReturning = false;
		}

		// 대본의 인물 이름을 월드의 캐릭터로 풀어 붙임 (대화 화면 층이 아는 플레이어, NPC, 등록된 화자). 없는 인물은 한 번 알림
		private bool SyncWorldActors(DialogueStageDelta delta)
		{
			bool changed = false;
			HashSet<string> present = new HashSet<string>();
			IDialogueStageHost host = DialogueStageBridge.Current;
			foreach (DialogueStageActor stageActor in delta.Actors)
			{
				if (actors.ContainsKey(stageActor.Name))
				{
					present.Add(stageActor.Name);
					continue;
				}
				if (host == null || host.TryResolveWorldAnchor(stageActor.Name, out Transform anchor) == false || anchor == null)
				{
					if (warnedMissing.Add(stageActor.Name))
						Debug.LogWarning($"[{nameof(DialogueCastStage)}] 월드에서 찾지 못한 인물: {stageActor.Name}. 이 인물은 2D 대사만");
					continue;
				}
				DialogueWorldActor actor = anchor.gameObject.AddComponent<DialogueWorldActor>();
				actor.Initialize(stageActor.Name, Settings);
				actors[stageActor.Name] = actor;
				present.Add(stageActor.Name);
				changed = true;
			}
			changed |= RemoveAbsent(present);
			if (changed)
				FaceEachOther();
			return changed;
		}

		// 서로 마주 보게 돌리고, 지금 게임 카메라가 있던 쪽을 대화 카메라 쪽으로 정함
		private void FaceEachOther()
		{
			List<Vector3> feet = new List<Vector3>();
			foreach (IDialogueCastPerformer actor in actors.Values)
				feet.Add(actor.Feet);
			worldFront = DialogueShotMath.FrontFromCamera(feet, gameplayPose.Position);
			foreach (IDialogueCastPerformer actor in actors.Values)
			{
				Vector3 others = Vector3.zero;
				int count = 0;
				foreach (IDialogueCastPerformer other in actors.Values)
				{
					if (other == actor)
						continue;
					others += other.Feet;
					count++;
				}
				if (count > 0)
					actor.TurnToward(others / count);
			}
		}

		// 월드는 벽과 물건이 있어 카메라와 시선 사이가 막히면 막힌 곳 바로 앞으로 당김. 인물 자신의 충돌체는 무시
		private DialogueShotPose Deocclude(DialogueShotPose pose)
		{
			Vector3 toCamera = pose.Position - pose.LookPoint;
			float distance = toCamera.magnitude;
			if (distance < 0.001f)
				return pose;
			Vector3 direction = toCamera / distance;
			float nearest = distance;
			foreach (RaycastHit hit in Physics.RaycastAll(pose.LookPoint, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
			{
				if (IsActorPart(hit.transform) || hit.distance >= nearest)
					continue;
				nearest = hit.distance;
			}
			if (nearest >= distance)
				return pose;
			Vector3 position = pose.LookPoint + direction * Mathf.Max(Settings.CameraNearClip * 4f, nearest - Settings.OcclusionPadding);
			return new DialogueShotPose(position, pose.LookPoint, pose.FieldOfView);
		}

		private bool IsActorPart(Transform part)
		{
			foreach (IDialogueCastPerformer actor in actors.Values)
			{
				if (actor.Root != null && part.IsChildOf(actor.Root))
					return true;
			}
			return false;
		}
	}
}
