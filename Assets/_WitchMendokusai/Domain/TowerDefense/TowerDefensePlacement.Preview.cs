using UnityEngine;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using VContainer;

namespace WitchMendokusai
{
	// TowerDefensePlacement 의 미리보기와 유령 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlacement.cs 를 본다.
	public partial class TowerDefensePlacement : MonoBehaviour
	{
		// 설치 전에 「여기 지으면 어디까지 닿는지」를 보여주는 원. 설치 후에야 알 수 있으면 그건 판단이 아니라 도박이다.
		private TowerDefenseRing previewRing;

		/// <summary>
		/// 미리보기 마커가 없으면 만든다.
		///
		/// ★ 이 마커는 인스펙터에서 끼워 넣는 것이었다 — 비어 있으면 *아무 말도 없이* 미리보기가
		///   통째로 사라진다(사용자 실증: "설치 미리보기도 지금 동작 안하는 것 같은데"). 화면에 꼭
		///   있어야 하는 것을 씬 배선에 기대면, 그 배선이 끊긴 날 조용히 기능이 없어진다.
		///   코드가 스스로 세우면 어떤 씬에서도 미리보기는 늘 있다.
		/// </summary>
		private void EnsurePreviewMarker()
		{
			if (previewMarker != null)
			{
				// ★ 이미 있어도 *그리는 층은 매번 다시 못 박는다* (사용자 실측: "여전히 마커 가려짐").
				//   두 가지 이유로 한 번만 설정하면 흘러내린다:
				//   ① 씬에 미리 박아둔 마커면 이 함수가 그냥 돌아가서 설정이 아예 안 걸린다.
				//   ② 색칠하는 쪽이 재질 *사본*을 만들어 쓴다 — 원본에만 걸어둔 설정은 사본을 안 따라간다.
				//   보이는 것이 목적인 물건은, 보이게 하는 설정도 매번 확인하는 편이 싸다.
				ApplyPreviewMarkerLayer();
				return;
			}

			previewMarker = TowerDefenseVisuals.Primitive(PrimitiveType.Quad, unlit: true);
			previewMarker.name = "PlacementPreviewMarker";
			Destroy(previewMarker.GetComponent<Collider>()); // 표시용 — 레이캐스트를 가로채면 안 된다.
			previewMarker.transform.SetParent(transform, false);
			// 바닥에 눕힌 한 칸짜리 판. 살짝 띄워 바닥과 겹쳐 깜빡이지 않게.
			// 길 안내와 같은 병을 앓는다 — 불투명하면 깊이를 남겨 인형 몸을 자른다(MakeFloorDecal ★ 주석).
			ApplyPreviewMarkerLayer();
			previewMarker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			previewMarker.transform.localScale = (Vector3.one * cellSize * 0.92f).ToUnity();
			previewMarker.SetActive(false);
		}

		/// <summary>
		/// 커서 표시를 「무엇에도 안 가리는 층」에 둔다 — 안개보다 나중에 그리고, 깊이는 안 남긴다.
		/// *색칠에 쓰는 그 사본*에 건다(GetComponentInChildren + .material) — 색칠 쪽과 다른 재질을
		/// 만지면 설정이 화면에 안 나타난다. 그게 「고쳤는데 여전히 가려지는」 상태의 정체였다.
		/// </summary>
		private void ApplyPreviewMarkerLayer()
		{
			if (previewMarker == null)
				return;

			Renderer markerRenderer = previewMarker.GetComponentInChildren<Renderer>();
			if (markerRenderer == null)
				return;

			TowerDefenseVisuals.MakeFloorDecal(markerRenderer.material, aboveFog: true);
		}

		// ── 유령 건물(설치 미리보기) ──────────────────────────────────────────────
		// ★ 왜 필요한가 (사용자 지시: "설치 미리보기에서 건물 모습이랑 사거리가 보여야겠죠"):
		//   네모 마커 하나로는 *무엇을* 짓는지 알 수 없다. 종류가 일곱이 넘는데 커서에 뜨는 그림이 늘 같으면
		//   핫바를 잘못 고른 것을 설치한 뒤에야 안다. 실제로 세울 그 프리팹을 반투명으로 미리 세워 보여준다.
		private GameObject ghostBuilding;
		private GameObject ghostSourcePrefab;
		private readonly System.Collections.Generic.List<Renderer> ghostRenderers = new();

		private void UpdateGhostBuilding(Vector3 snappedWorldPosition)
		{
			GameObject wanted = GhostPrefabForSelection();
			if (wanted == null)
			{
				if (ghostBuilding != null)
					ghostBuilding.SetActive(false);
				return;
			}

			// 고른 종류가 바뀌면 유령도 갈아끼운다.
			if (ghostSourcePrefab != wanted)
			{
				if (ghostBuilding != null)
					Destroy(ghostBuilding);

				ghostSourcePrefab = wanted;
				ghostBuilding = Instantiate(wanted, transform);
				ghostBuilding.name = "PlacementGhost";
				StripGhost(ghostBuilding);

				ghostRenderers.Clear();
				ghostBuilding.GetComponentsInChildren(true, ghostRenderers);
			}

			ghostBuilding.SetActive(true);
			ghostBuilding.transform.position = snappedWorldPosition.ToUnity();
		}

		/// <summary> 유령은 *보이기만* 한다 — 충돌·전투·이동이 살아 있으면 미리보기가 게임에 개입한다. </summary>
		private static void StripGhost(GameObject ghost)
		{
			foreach (Collider collider in ghost.GetComponentsInChildren<Collider>(true))
				collider.enabled = false;
			foreach (Rigidbody body in ghost.GetComponentsInChildren<Rigidbody>(true))
				body.isKinematic = true;
			foreach (MonoBehaviour behaviour in ghost.GetComponentsInChildren<MonoBehaviour>(true))
				behaviour.enabled = false;

			// ★ 애니메이터는 MonoBehaviour 가 아니라 위 줄이 못 껐다 (사용자 실측: "마커가 계속
			//   애니메이션 재생"). 유령이 살아 있는 것처럼 꿈틀대면 「지금 지어진 것」과 구분이 안 된다 —
			//   미리보기는 *가만히 서 있는 그림*이어야 「아직 아니다」가 읽힌다.
			//   끄기만 하면 마지막 자세로 굳으므로 별도 자세 지정이 필요 없다.
			foreach (Animator animator in ghost.GetComponentsInChildren<Animator>(true))
				animator.enabled = false;
			foreach (Animation animation in ghost.GetComponentsInChildren<Animation>(true))
				animation.enabled = false;
		}

		private void TintGhost(bool canBuild)
		{
			if (ghostBuilding == null)
				return;

			Color tint = canBuild ? new Color(0.5f, 1f, 0.6f, 0.55f) : new Color(1f, 0.45f, 0.45f, 0.55f);
			foreach (Renderer renderer in ghostRenderers)
			{
				if (renderer == null)
					continue;
				if (renderer is SpriteRenderer sprite)
					sprite.color = tint;
				else if (renderer.material.HasProperty("_BaseColor"))
					renderer.material.SetColor("_BaseColor", tint);
				else
					renderer.material.color = tint;
			}
		}

		private GameObject GhostPrefabForSelection()
		{
			if (stage == null)
				return null;

			return SelectedKind switch
			{
				TowerDefensePlaceableKind.Harvester => stage.HarvesterUnit != null ? stage.HarvesterUnit.Prefab : null,
				TowerDefensePlaceableKind.Generator => stage.HarvesterUnit != null ? stage.HarvesterUnit.Prefab : null,
				TowerDefensePlaceableKind.Tower => stage.TowerUnit != null ? stage.TowerUnit.Prefab : null,
				// 벽·함정·전초기지는 프리팹이 아니라 코드가 그리는 도형이라 유령이 없다(마커가 그 자리를 대신한다).
				_ => null,
			};
		}

		/// <summary>
		/// 미리보기 원 — 포탑이면 사거리, 채집이면 노드를 잡을 수 있는 거리. 둘 다 「이 자리의 의미」를 말한다.
		/// 반지름은 매치 정본(전술 사거리 / 노드 점유 반경)에서 읽는다 — 여기 숫자를 따로 박으면 거짓말이 된다.
		/// </summary>
		private void UpdatePreviewRing(Vector3 snappedWorldPosition)
		{
			if (match == null || stage == null)
				return;

			bool isHarvester = SelectedKind == TowerDefensePlaceableKind.Harvester;
			bool isGenerator = SelectedKind == TowerDefensePlaceableKind.Generator;
			if (SelectedKind == TowerDefensePlaceableKind.Trap)
			{
				if (previewRing == null)
					previewRing = TowerDefenseRing.Create(transform, "PlacementPreviewRing", Color.white, 0.12f, 0.06f);
				previewRing.transform.position = (snappedWorldPosition + new Vector3(0f, 0.06f, 0f)).ToUnity();
				previewRing.SetRadius(stage.TrapRadius);
				previewRing.SetColor(new Color(1f, 0.45f, 0.32f, 0.9f));
				previewRing.SetVisible(true);
				return;
			}

			if (SelectedKind == TowerDefensePlaceableKind.Wall)
			{
				if (previewRing != null)
					previewRing.SetVisible(false);
				return;
			}

			// 영웅은 짓는 게 아니라 보내는 것 — 원은 「거기 서면 어디까지 닿나」를 말한다.
			if (SelectedKind == TowerDefensePlaceableKind.Hero)
			{
				if (stage.HeroArchetype == null || match.HasHero == false)
				{
					if (previewRing != null)
						previewRing.SetVisible(false);
					return;
				}

				if (previewRing == null)
					previewRing = TowerDefenseRing.Create(transform, "PlacementPreviewRing", Color.white, 0.12f, 0.06f);
				previewRing.transform.position = (snappedWorldPosition + new Vector3(0f, 0.06f, 0f)).ToUnity();
				previewRing.SetRadius(stage.HeroArchetype.Range);
				previewRing.SetColor(new Color(1f, 0.62f, 0.9f, 0.9f));
				previewRing.SetVisible(true);
				return;
			}
			// ★ 발전 인형은 *전기가 닿는 거리*를 그려야 한다. 여기서 안 갈라내면 아래 포탑 사거리로 흘러들어가
			//   「이만큼 쏜다」는 원을 발전기 자리에 그린다 — 발전기는 쏘지 않는다. 설치 위치를 정하는
			//   유일한 근거가 그 반경인데, 화면이 다른 원을 보여주면 판단할 근거가 아예 없어진다.
			float radius = isGenerator ? stage.GeneratorRadius
				: isHarvester ? stage.NodeCaptureRadius
				: match.TowerRange(SelectedTowerIndex);
			if (radius <= 0f)
			{
				if (previewRing != null)
					previewRing.SetVisible(false);
				return;
			}

			if (previewRing == null)
				previewRing = TowerDefenseRing.Create(transform, "PlacementPreviewRing", Color.white, 0.12f, 0.06f);

			previewRing.transform.position = (snappedWorldPosition + new Vector3(0f, 0.06f, 0f)).ToUnity();
			previewRing.SetRadius(radius);
			previewRing.SetColor(isGenerator
				? new Color(1f, 0.82f, 0.3f, 0.9f)
				: isHarvester
					? new Color(0.42f, 0.92f, 0.68f, 0.9f)
					: new Color(0.45f, 0.78f, 1f, 0.9f));
			previewRing.SetVisible(true);
		}
	}
}
