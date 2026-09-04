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
		private Transform worldRoot;
		private Material groundMaterial;
		private Color groundRest;
		private BattleEntityPresenter entities;
		private BattleFx fx;
		private float scroll;
		private bool scrollReady;
		private float supplyGlowLeft;
		private bool built;

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
			BuildGround();
			GameObject world = new GameObject("World");
			world.transform.SetParent(holder, false);
			worldRoot = world.transform;
			entities = new BattleEntityPresenter(worldRoot, presentationAsset.CreateEntitySettings());
			fx = new BattleFx(holder, presentationAsset.CreateFxSettings());
			BuildScenery();
		}

		public void Render(IdleSnapshot snapshot, float delta)
		{
			if (built == false) { return; }
			ReshapeScenery(Geometry.ShapeOfStage(snapshot.Stage, presentationAsset.ShapeStagesPerStep));
			Follow(snapshot, delta);
			entities.Render(snapshot, delta);
			fx.Consume(snapshot.Hits, entities);
			fx.Advance(delta, entities);
			AdvanceSupply(delta);
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

		public bool TryPickFoe(Vector2 panelPosition, out long foeIndex)
		{
			if (entities == null)
			{
				foeIndex = -1L;
				return false;
			}

			return entities.TryPickFoe(panelPosition, out foeIndex);
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
				GameObject made = Instantiate(presentationAsset.GroundPrefab, holder, false);
				made.name = "Ground";
				MeshRenderer floor = made.GetComponentInChildren<MeshRenderer>();
				groundMaterial = floor != null ? floor.sharedMaterial : BattleVisualFactory.MakeMaterial(presentationAsset.GroundColor);
				groundRest = groundMaterial.color;
				return;
			}
			GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Ground";
			ground.transform.SetParent(holder, false);
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

		private void Follow(IdleSnapshot snapshot, float delta)
		{
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
			float wanted = presentationAsset.PartyAnchorX - (count > 0 ? sum / count : 0f);
			if (scrollReady == false || Mathf.Abs(wanted - scroll) > presentationAsset.SnapJump)
			{
				scroll = wanted;
				scrollReady = true;
			}
			else { scroll = Mathf.Lerp(scroll, wanted, BattleMotion.CatchUp(presentationAsset.FollowCatchUp, delta)); }
			worldRoot.localPosition = new Vector3(scroll, 0f, 0f);
			float span = scenery.Count * presentationAsset.ScenerySpacing;
			float margin = presentationAsset.SceneryWrapMargin;
			foreach (Transform prop in scenery)
			{
				while (prop.localPosition.x + scroll < -margin) { prop.localPosition += new Vector3(span, 0f, 0f); }
				while (prop.localPosition.x + scroll > span - margin) { prop.localPosition -= new Vector3(span, 0f, 0f); }
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
				if (child.name == "Preview") { Kill(child.gameObject); }
			}
		}

		private void OnDisable()
		{
			if (holder != null) { Kill(holder.gameObject); }
			holder = null;
			worldRoot = null;
			entities = null;
			fx = null;
			scenery.Clear();
			sceneryMeshes.Clear();
			sceneryShape = (Geometry.Shape)(-1);
			scrollReady = false;
			built = false;
		}

		private static void Kill(GameObject piece)
		{
			if (Application.isPlaying) { Destroy(piece); }
			else { DestroyImmediate(piece); }
		}
	}
}
