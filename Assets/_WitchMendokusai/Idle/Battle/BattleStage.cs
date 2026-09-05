using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	/// <summary>Idle 전투 스냅샷과 표시 계층을 조율한다.</summary>
	[ExecuteAlways]
	public sealed class BattleStage : MonoBehaviour
	{
		[SerializeField] private BattlePresentationSO presentationAsset;

		private readonly List<Transform> scenery = new List<Transform>();
		private readonly List<MeshFilter> sceneryMeshes = new List<MeshFilter>();
		private Geometry.Shape sceneryShape = (Geometry.Shape)(-1);
		private Transform holder;
		private Transform battleRoot;
		private Transform worldRoot;
		private AltScenePresenter altScene;
		private Material groundMaterial;
		private Color groundRest;
		private BattleEntityPresenter entities;
		private BattleFx fx;
		private BattleCameraDirector cameraDirector;
		private double originShown;
		private bool originReady;
		private float supplyGlowLeft;
		private bool built;
		private StageScene scene = StageScene.Battle;

		public void Build()
		{
			// 배치 빌드에서는 안 세운다 (실측 2026-09-01). 씬 검사가 씬을 여는 것만으로
			// [ExecuteAlways] 가 무대를 짓기 시작하면 -nographics 배치에서 빌드 사망
			if (Application.isBatchMode)
			{
				return;
			}
			if (presentationAsset == null)
			{
				Debug.LogError("[Idle] BattlePresentationSO is missing");
				return;
			}
			if (presentationAsset.TryValidate(out string error) == false)
			{
				Debug.LogError("[Idle] BattlePresentationSO is invalid: " + error);
				return;
			}
			if (built) { return; }
			built = true;
			ClearPreview();
			GameObject root = new GameObject("Preview");
			root.hideFlags = HideFlags.DontSave;
			root.transform.SetParent(transform, false);
			holder = root.transform;
			// 전투 뿌리 하나에 바닥, 세계, 볼트. 상점과 연구실 장면은 옆 뿌리 (layout.md 2)
			GameObject battle = new GameObject("Battle");
			battle.transform.SetParent(holder, false);
			battleRoot = battle.transform;
			BuildGround();
			GameObject world = new GameObject("World");
			world.transform.SetParent(battleRoot, false);
			worldRoot = world.transform;
			entities = new BattleEntityPresenter(worldRoot, presentationAsset.CreateEntitySettings());
			fx = new BattleFx(battleRoot, presentationAsset.CreateFxSettings());
			altScene = new AltScenePresenter(
				holder, presentationAsset.CreateEntitySettings(), presentationAsset.CreateAltSceneSettings());
			altScene.PlaceRooms(presentationAsset.ShopRoomPosition, presentationAsset.LabRoomPosition);
			cameraDirector = new BattleCameraDirector(presentationAsset.CreateCameraSettings());
			cameraDirector.Build(holder);
			BuildScenery();
		}

		/// <summary>세상의 시간 배율. 인형 애니메이터 속도에 반영 (조준 중 느려짐)</summary>
		public void SetTimeScale(float scale)
		{
			if (built)
			{
				entities.SetTimeScale(scale);
			}
		}

		/// <summary>
		/// 보여 줄 장면. 전투 마당과 방은 <b>같은 세상의 다른 자리</b>. 카메라가 옮겨 감
		///
		/// ★ 전에는 전투 뿌리를 끄고 방을 덮어씌움. 그래서 상점에서도 피해 숫자가 떴음
		///   (사용자 2026-09-05). 지금은 전투가 제자리에서 계속 돌고 카메라만 자리를 옮김
		/// </summary>
		internal void ShowScene(StageScene wanted)
		{
			if (built == false)
			{
				return;
			}

			scene = wanted;
			altScene.Show(wanted);
			cameraDirector.Show(wanted);
			fx?.SetTextShown(wanted == StageScene.Battle);
		}

		public void Render(IdleSnapshot snapshot, float delta)
		{
			if (built == false) { return; }
			ReshapeScenery(Geometry.ShapeOfStage(snapshot.Stage, presentationAsset.ShapeStagesPerStep));
			Follow(snapshot);
			entities.Render(snapshot, delta);
			fx.Consume(snapshot.Hits, entities);
			fx.Advance(delta, entities);
			AdvanceSupply(delta);
			altScene.Tick(delta);
		}

		public void SetFloatingTextRoot(VisualElement root)
		{
			if (fx != null)
			{
				fx.SetFloatingTextRoot(root);
			}
		}

		public void OnVolley(long target)
		{
			if (fx != null && entities != null)
			{
				fx.PlayVolley(target, entities);
			}
		}

		public bool TryPickFoe(IPanel panel, Vector2 panelPosition, out long foeIndex)
		{
			if (entities == null)
			{
				foeIndex = -1L;
				return false;
			}

			return entities.TryPickFoe(panel, panelPosition, out foeIndex);
		}

		/// <summary>조준 중인 적을 무대에 알린다. -1 이면 아무도 안 걸림</summary>
		public void SetAimTarget(long foeIndex)
		{
			entities?.SetAimTarget(foeIndex);
		}

		public void OnSupply(float seconds)
		{
			supplyGlowLeft = seconds;
			if (fx != null && entities != null)
			{
				fx.PlaySupply(entities);
			}
		}

		public void OnAppraise()
		{
			if (fx != null && entities != null)
			{
				fx.PlayAppraise(entities);
			}
		}
		public void OnTap() { }

		private void BuildGround()
		{
			if (presentationAsset.GroundPrefab != null)
			{
				GameObject made = Instantiate(presentationAsset.GroundPrefab, battleRoot, false);
				made.name = "Ground";
				MeshRenderer floor = made.GetComponentInChildren<MeshRenderer>();
				groundMaterial = floor != null ? floor.sharedMaterial : BattleVisualFactory.MakeMaterial(presentationAsset.GroundColor);
				groundRest = groundMaterial.color;
				return;
			}
			GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.SetParent(battleRoot, false);
			ground.transform.localScale = new Vector3(6f, 1f, 4f);
			groundMaterial = BattleVisualFactory.Paint(ground, presentationAsset.GroundColor);
			groundRest = presentationAsset.GroundColor;
		}

		/// <summary>
		/// 배경 소품. 적과 같은 기하 언어를 쓰되 저채도 (visual.md 6)
		///
		/// ★ 구역이 바뀌면 소품 도형도 따라감. 세계가 달라진 것이 배경에서 먼저 읽힘
		/// </summary>
		private void BuildScenery()
		{
			if (presentationAsset.GroundPrefab != null)
			{
				return;
			}

			for (int at = 0; at < presentationAsset.SceneryCount; at++)
			{
				GameObject prop = new GameObject("Scenery" + at);
				prop.transform.SetParent(worldRoot, false);

				MeshFilter mesh = prop.AddComponent<MeshFilter>();
				MeshRenderer renderer = prop.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = BattleVisualFactory.MakeMaterial(presentationAsset.SceneryColor);

				float side = at % 2 == 0 ? 1f : -1f;
				float size = presentationAsset.SceneryBaseSize + presentationAsset.SceneryStepSize * (at % 4);
				prop.transform.localPosition = new Vector3(
					at * presentationAsset.ScenerySpacing + presentationAsset.SceneryStartX,
					size * 0.5f,
					side * (presentationAsset.SceneryLaneOffset + presentationAsset.SceneryLaneStep * (at % 3)));
				prop.transform.localScale = new Vector3(size, size, size);
				prop.transform.localRotation = Quaternion.Euler(presentationAsset.SceneryEulerStep * at);

				sceneryMeshes.Add(mesh);
				scenery.Add(prop.transform);
			}

			ReshapeScenery(Geometry.Shape.Tetrahedron);
		}

		/// <summary>소품 도형을 구역에 맞춤. 같은 도형이면 그대로</summary>
		private void ReshapeScenery(Geometry.Shape shape)
		{
			if (sceneryShape == shape && sceneryMeshes.Count > 0 && sceneryMeshes[0].sharedMesh != null)
			{
				return;
			}

			sceneryShape = shape;
			Mesh made = Geometry.Build(shape, 1f);

			foreach (MeshFilter mesh in sceneryMeshes)
			{
				mesh.sharedMesh = made;
			}
		}

		/// <summary>
		/// 카메라가 볼 자리 (편성 한가운데) 와 판이 민 거리 (<see cref="IdleSnapshot.OriginX"/>) 맞추기
		///
		/// ★ 세상은 안 민다. 인형과 적은 판이 준 좌표 그대로 서고 카메라만 움직임
		///   판이 좌표를 다시 깎은 프레임에는 카메라를 같은 만큼 워프시켜 화면을 붙잡음
		/// </summary>
		private void Follow(IdleSnapshot snapshot)
		{
			if (originReady == false)
			{
				originShown = snapshot.OriginX;
				originReady = true;
			}
			else if (snapshot.OriginX != originShown)
			{
				cameraDirector.Warp((float)(snapshot.OriginX - originShown));
				originShown = snapshot.OriginX;
			}

			float sum = 0f;
			int count = 0;
			for (int seat = 0; seat < snapshot.Fighters.Length && seat < snapshot.Seats.Length; seat++)
			{
				if (snapshot.Seats[seat].Taken)
				{
					sum += (float)snapshot.Fighters[seat].X;
					count++;
				}
			}

			float middle = count > 0 ? sum / count : 0f;
			cameraDirector.Aim(worldRoot.TransformPoint(new Vector3(middle, 0f, 0f)));
			WrapScenery(middle);
		}

		/// <summary>배경 소품을 카메라 앞으로 돌려 놓는다. 끝없는 길처럼 보이게</summary>
		private void WrapScenery(float middle)
		{
			float span = scenery.Count * presentationAsset.ScenerySpacing;
			if (span <= 0f)
			{
				return;
			}

			float margin = presentationAsset.SceneryWrapMargin;
			foreach (Transform prop in scenery)
			{
				while (prop.localPosition.x < middle - margin) { prop.localPosition += new Vector3(span, 0f, 0f); }
				while (prop.localPosition.x > middle + span - margin) { prop.localPosition -= new Vector3(span, 0f, 0f); }
			}
		}

		private void AdvanceSupply(float delta)
		{
			if (supplyGlowLeft <= 0f) { return; }
			supplyGlowLeft -= delta;
			groundMaterial.color = Color.Lerp(groundRest, presentationAsset.BoltColor, Mathf.Clamp01(supplyGlowLeft) * presentationAsset.SupplyGlowShare);
		}

		private void ClearPreview()
		{
			for (int at = transform.childCount - 1; at >= 0; at--)
			{
				Transform child = transform.GetChild(at);
				if (child.name == "Preview") { BattleVisualFactory.Kill(child.gameObject); }
			}
		}

		private void OnDisable()
		{
			if (holder != null) { BattleVisualFactory.Kill(holder.gameObject); }
			holder = null;
			battleRoot = null;
			worldRoot = null;
			altScene = null;
			entities = null;
			fx = null;
			cameraDirector = null;
			scenery.Clear();
			sceneryMeshes.Clear();
			sceneryShape = (Geometry.Shape)(-1);
			originReady = false;
			built = false;
		}
	}
}
