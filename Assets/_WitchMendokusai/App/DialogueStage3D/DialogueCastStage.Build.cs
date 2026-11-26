using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.DialogueStage3D
{
	// DialogueCastStage 의 별도 무대 (`@무대 3D`). 게임 월드와 떨어진 자리에 바닥, 조명, 카메라, 배우를 세움
	public sealed partial class DialogueCastStage
	{
		private static readonly DialogueStageSlot[] SLOT_ORDER =
		{
			DialogueStageSlot.FarLeft, DialogueStageSlot.Left, DialogueStageSlot.Center, DialogueStageSlot.Right, DialogueStageSlot.FarRight,
		};

		private readonly Dictionary<string, DialogueStageSlot> slots = new Dictionary<string, DialogueStageSlot>();
		private GameObject stageRoot;

		private void ShowStage()
		{
			takeover.EnsureFree();
			BuildStage();
			takeover.Take();
		}

		private void HideStage()
		{
			if (stageRoot != null)
				Destroy(stageRoot);
			stageRoot = null;
			stageCamera = null;
			takeover.Release();
		}

		private void BuildStage()
		{
			DialogueCastStageSettings stage = Settings;
			if (stage.IdleClip == null || stage.TalkClip == null)
				throw new System.InvalidOperationException($"{DialogueCastStageSettings.RESOURCE_NAME} 에 대기나 말하기 클립이 비었다");

			stageRoot = new GameObject("Dialogue cast stage root");
			stageRoot.transform.SetParent(transform, false);
			stageRoot.transform.position = stage.StageOrigin;

			GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			floor.name = "Stage floor";
			Destroy(floor.GetComponent<Collider>());
			floor.transform.SetParent(stageRoot.transform, false);
			floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
			floor.transform.localScale = new Vector3(stage.FloorSize * 2f, 0.05f, stage.FloorSize * 2f);
			floor.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = stage.FloorColor };
			SetLayer(floor, stage.ContentLayer);

			AddLight("Key light", Quaternion.Euler(stage.KeyLightAngles), stage.KeyLightIntensity, stage.KeyLightColor, LightShadows.Soft);
			AddLight("Fill light", Quaternion.Euler(stage.KeyLightAngles.x * 0.5f, stage.KeyLightAngles.y + 180f, 0f), stage.FillLightIntensity, Color.white, LightShadows.None);

			GameObject cameraHost = new GameObject("Dialogue cast camera");
			cameraHost.transform.SetParent(stageRoot.transform, false);
			stageCamera = cameraHost.AddComponent<DialogueCastCamera>();
			stageCamera.CreateOwnCamera(stage);
			stageCamera.Cut(DialogueShotMath.Wide(new List<Vector3>(), stage.ActorHeight, stage.StageOrigin, Vector3.back, stage.Wide));
		}

		private void AddLight(string lightName, Quaternion rotation, float intensity, Color color, LightShadows shadows)
		{
			Light light = new GameObject(lightName).AddComponent<Light>();
			light.transform.SetParent(stageRoot.transform, false);
			light.transform.rotation = rotation;
			light.type = LightType.Directional;
			light.intensity = intensity;
			light.color = color;
			light.shadows = shadows;
			light.cullingMask = 1 << Settings.ContentLayer;
		}

		// 변경분의 현재 출연진에 맞춤. 바뀌었으면 true
		private bool SyncStageActors(DialogueStageDelta delta)
		{
			bool changed = false;
			HashSet<string> present = new HashSet<string>();
			foreach (DialogueStageActor stageActor in delta.Actors)
			{
				present.Add(stageActor.Name);
				if (actors.ContainsKey(stageActor.Name) == false)
				{
					actors[stageActor.Name] = SpawnActor(stageActor.Name);
					changed = true;
				}
				if (slots.TryGetValue(stageActor.Name, out DialogueStageSlot slot) == false || slot != stageActor.Slot)
					changed = true;
				slots[stageActor.Name] = stageActor.Slot;
			}
			changed |= RemoveAbsent(present);
			if (changed)
				PlaceActors(delta.Entered);
			return changed;
		}

		private bool RemoveAbsent(HashSet<string> present)
		{
			List<string> gone = new List<string>();
			foreach (string actorName in actors.Keys)
			{
				if (present.Contains(actorName) == false)
					gone.Add(actorName);
			}
			foreach (string actorName in gone)
			{
				actors[actorName].Release();
				actors.Remove(actorName);
				slots.Remove(actorName);
			}
			return gone.Count > 0;
		}

		// 자리 위치에 세우고 상대를 보되 카메라 쪽으로 조금 돌림. 이번 줄에 들어온 배우는 바로, 나머지는 걸어감
		private void PlaceActors(List<DialogueStageActorChange> entered)
		{
			foreach (KeyValuePair<string, IDialogueCastPerformer> pair in actors)
			{
				DialogueCastActor actor = (DialogueCastActor)pair.Value;
				Vector3 position = SlotPosition(slots[pair.Key]);
				Quaternion rotation = FacingFor(pair.Key, position);
				bool justEntered = entered.Exists(change => change.Actor == pair.Key);
				if (justEntered) actor.Place(position, rotation);
				else actor.MoveTo(position, rotation);
			}
		}

		private Vector3 SlotPosition(DialogueStageSlot slot)
		{
			int index = System.Array.IndexOf(SLOT_ORDER, slot);
			return Settings.StageOrigin + Vector3.right * ((index - 2) * Settings.SlotSpacing);
		}

		private Quaternion FacingFor(string actorName, Vector3 position)
		{
			Vector3 toCamera = Vector3.back;
			Vector3 others = Vector3.zero;
			int count = 0;
			foreach (KeyValuePair<string, DialogueStageSlot> pair in slots)
			{
				if (pair.Key == actorName)
					continue;
				others += SlotPosition(pair.Value);
				count++;
			}
			if (count == 0)
				return Quaternion.LookRotation(toCamera, Vector3.up);
			Vector3 toOthers = others / count - position;
			toOthers.y = 0f;
			if (toOthers.sqrMagnitude < 0.0001f)
				return Quaternion.LookRotation(toCamera, Vector3.up);
			Vector3 facing = Vector3.Slerp(toOthers.normalized, toCamera, Settings.FacingTowardCamera);
			return Quaternion.LookRotation(facing, Vector3.up);
		}

		private DialogueCastActor SpawnActor(string actorName)
		{
			DialogueCastStageSettings.CastEntry entry = Settings.FindCast(actorName);
			GameObject prefab = entry != null && entry.Prefab != null ? entry.Prefab : Settings.FallbackPrefab;
			if (prefab == null)
				throw new System.InvalidOperationException($"배우 {actorName} 의 프리팹이 없다 ({DialogueCastStageSettings.RESOURCE_NAME} 의 Cast 나 FallbackPrefab)");
			GameObject instance = Instantiate(prefab, stageRoot.transform);
			instance.name = "Cast " + actorName;
			SetLayer(instance, Settings.ContentLayer);
			FitHeight(instance, Settings.ActorHeight * (entry != null ? entry.HeightScale : 1f));
			if (entry != null && entry.Materials != null && entry.Materials.Length > 0)
			{
				foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
					renderer.sharedMaterials = entry.Materials;
			}
			DialogueCastActor actor = instance.AddComponent<DialogueCastActor>();
			actor.Initialize(actorName, Settings);
			return actor;
		}

		// 모델마다 원래 크기가 달라서 (작은 인형, 성인 비율) 그린 높이를 재어 같은 키로 맞춤
		private static void FitHeight(GameObject instance, float targetHeight)
		{
			Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
			if (renderers.Length == 0)
				return;
			Bounds bounds = renderers[0].bounds;
			foreach (Renderer renderer in renderers)
				bounds.Encapsulate(renderer.bounds);
			if (bounds.size.y <= 0.0001f)
				return;
			instance.transform.localScale *= targetHeight / bounds.size.y;
		}

		private static void SetLayer(GameObject target, int layer)
		{
			foreach (Transform part in target.GetComponentsInChildren<Transform>(true))
				part.gameObject.layer = layer;
		}
	}
}
