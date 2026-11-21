using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 화면 밖 정예, 보스, 발견한 맵 오브젝트, 특수 아이템의 방향 표시. 화면 가장자리 안쪽에 화살촉과 그 뒤 점 (아이템은 마름모, 맵 오브젝트는 네모)
	// 맵 오브젝트 색은 데이터의 표식 색 (MapObjectData.MarkerColor), 크기 배율은 MapObjectSettings.OffscreenScale
	// 표식은 처음 한 번 짓고 재사용. 대상은 SurvivalFeel 의 정예 목록, 보스 등장 사건으로 모은 보스 목록, 바닥 전리품 버퍼의 GameItemObject
	// 모양과 색, 등장 튐은 SurvivalOffscreenMarkers.uss. 거리, 상한, 여백, 튐 대기는 SurvivalFeelConfig 의 화면 밖 표시 묶음
	public class SurvivalOffscreenMarkers : MonoBehaviour
	{
		private const string USS_ROOT = "wm-offscreen";
		private const string USS_MARKER = "wm-offscreen__marker";
		private const string USS_MARKER_ELITE = "wm-offscreen__marker--elite";
		private const string USS_MARKER_BOSS = "wm-offscreen__marker--boss";
		private const string USS_MARKER_ITEM = "wm-offscreen__marker--item";
		private const string USS_MARKER_MAP_OBJECT = "wm-offscreen__marker--mapobject";
		private const string USS_POP = "wm-offscreen__pop";
		private const string USS_POP_SHOWN = "wm-offscreen__pop--shown";
		private const string USS_POP_INSTANT = "wm-offscreen__pop--instant";
		private const string USS_PIVOT = "wm-offscreen__pivot";
		private const string USS_DOT = "wm-offscreen__dot";
		private const string USS_ARROW_SHADE = "wm-offscreen__arrow-shade";
		private const string USS_ARROW = "wm-offscreen__arrow";

		// 정렬 순서 겸용. 앞일수록 먼저 표식을 받음
		private enum MarkerKind
		{
			Boss = 0,
			Elite = 1,
			Item = 2,
			MapObject = 3,
		}

		private class Marker
		{
			public VisualElement Element;
			public VisualElement Pop;
			public VisualElement Pivot;
			public VisualElement Dot;
			public VisualElement Arrow;
			public Component Target;
			public int AssignedFrame;
		}

		private struct Candidate
		{
			public Component Target;
			public MarkerKind Kind;
			public Color Color;
			public float Distance;
			public Vector2 Position;
			public float Angle;
		}

		private ResourceManager resourceManager;
		private PlayerProvider playerProvider;
		private SurvivalFeel feel;

		private VisualElement root;
		private Marker[] markers = Array.Empty<Marker>();
		private bool[] markerUsed = Array.Empty<bool>();
		private bool[] candidateMatched = Array.Empty<bool>();
		private readonly List<MonsterObject> bosses = new();
		private readonly List<Candidate> candidates = new();
		private readonly ComponentLookup<GameItemObject> itemLookup = new();
		private readonly Comparison<Candidate> candidateOrder = CompareCandidates;

		private SurvivalFeelConfig Config => resourceManager.SurvivalFeel;

		public void Init(ResourceManager resourceManager, PlayerProvider playerProvider, SurvivalFeel feel)
		{
			this.resourceManager = resourceManager;
			this.playerProvider = playerProvider;
			this.feel = feel;
			EventBusBridge.Subscribe<BossSpawnedEvent>(OnBossSpawned);
			FindBosses();
		}

		private void OnDestroy()
		{
			EventBusBridge.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
			if (root != null)
				root.RemoveFromHierarchy();
			itemLookup.Clear();
		}

		private void OnBossSpawned(BossSpawnedEvent spawned) => FindBosses();

		// 보스 등장 때 한 번만 훑음. 매 프레임 찾지 않음
		private void FindBosses()
		{
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			foreach (GameObject monsterObject in monsters)
			{
				if (monsterObject == null || monsterObject.activeInHierarchy == false || monsterObject.TryGetComponent(out MonsterObject monster) == false)
					continue;
				if (monster.UnitData.Type == MonsterType.Boss && bosses.Contains(monster) == false)
					bosses.Add(monster);
			}
		}

		// UIRoot 는 이 컴포넌트보다 늦게 생길 수 있어 쓸 때 지음
		private bool EnsureBuilt(SurvivalFeelConfig config)
		{
			if (root != null && markers.Length == config.OffscreenMaxMarkers)
				return true;
			if (UIRoot.Instance == null || UIRoot.Instance.HudLayer == null)
				return false;
			if (config.OffscreenStyleSheet == null)
				throw new InvalidOperationException($"{config.name}: OffscreenStyleSheet 가 비어 있음. SurvivalOffscreenMarkers.uss 를 연결해야 함");
			if (root != null)
				root.RemoveFromHierarchy();

			root = Make(USS_ROOT);
			root.styleSheets.Add(config.OffscreenStyleSheet);
			markers = new Marker[config.OffscreenMaxMarkers];
			markerUsed = new bool[markers.Length];
			candidateMatched = new bool[markers.Length];
			for (int i = 0; i < markers.Length; i++)
				markers[i] = BuildMarker();
			// HUD 층 맨 아래. 띠는 위 층 (OverlayLayer)
			UIRoot.Instance.HudLayer.Insert(0, root);
			return true;
		}

		private Marker BuildMarker()
		{
			VisualElement element = Make(USS_MARKER);
			VisualElement pop = Make(USS_POP);
			VisualElement pivot = Make(USS_PIVOT);
			VisualElement dot = Make(USS_DOT);
			VisualElement arrow = Make(USS_ARROW);
			pivot.Add(dot);
			pivot.Add(Make(USS_ARROW_SHADE));
			pivot.Add(arrow);
			pop.Add(pivot);
			element.Add(pop);
			element.style.visibility = Visibility.Hidden;
			root.Add(element);
			return new Marker { Element = element, Pop = pop, Pivot = pivot, Dot = dot, Arrow = arrow };
		}

		private static VisualElement Make(string className)
		{
			VisualElement element = new() { pickingMode = PickingMode.Ignore };
			element.AddToClassList(className);
			return element;
		}

		private void Update()
		{
			if (resourceManager == null)
				return;
			SurvivalFeelConfig config = Config;
			if (EnsureBuilt(config) == false)
				return;

			candidates.Clear();
			Camera camera = Camera.main;
			PlayerObject player = playerProvider.CurrentObject;
			// 시간이 멈춘 동안 (카드 고르기, 상자, 일시정지) 숨김
			bool inRun = DungeonManager.Instance != null && DungeonManager.Instance.IsDungeon && Time.timeScale > 0f
				&& (TimeManager.TryGetExistingInstance(out TimeManager timeManager) == false || timeManager.IsPaused == false);
			float width = root.layout.width;
			float height = root.layout.height;
			bool hasLayout = float.IsNaN(width) == false && float.IsNaN(height) == false && width > 0f && height > 0f;
			if (inRun && camera != null && player != null && root.panel != null && hasLayout)
			{
				Vector3 playerPosition = player.transform.position;
				CollectBosses(config, camera, playerPosition, width, height);
				CollectElites(config, camera, playerPosition, width, height);
				CollectMapObjects(config, camera, playerPosition, width, height);
				if (config.OffscreenShowItems)
					CollectItems(config, camera, playerPosition, width, height);
				candidates.Sort(candidateOrder);
			}
			Assign(config);
		}

		private void CollectBosses(SurvivalFeelConfig config, Camera camera, Vector3 playerPosition, float width, float height)
		{
			for (int i = bosses.Count - 1; i >= 0; i--)
			{
				MonsterObject boss = bosses[i];
				if (boss == null || boss.isActiveAndEnabled == false || boss.IsAlive == false || boss.UnitData.Type != MonsterType.Boss)
				{
					bosses.RemoveAt(i);
					continue;
				}
				TryAddCandidate(config, camera, boss, MarkerKind.Boss, playerPosition, width, height);
			}
		}

		private void CollectElites(SurvivalFeelConfig config, Camera camera, Vector3 playerPosition, float width, float height)
		{
			IReadOnlyList<MonsterObject> elites = feel.Elites;
			for (int i = 0; i < elites.Count; i++)
			{
				MonsterObject elite = elites[i];
				if (elite == null || elite.isActiveAndEnabled == false || elite.IsAlive == false || elite.UnitData.Type == MonsterType.Boss)
					continue;
				TryAddCandidate(config, camera, elite, MarkerKind.Elite, playerPosition, width, height);
			}
		}

		private void CollectMapObjects(SurvivalFeelConfig config, Camera camera, Vector3 playerPosition, float width, float height)
		{
			foreach (MapObjectSite site in DungeonManager.Instance.MonsterSpawner.MapObjectSites)
			{
				if (site == null || site.IsSignaled == false)
					continue;
				TryAddCandidate(config, camera, site, MarkerKind.MapObject, playerPosition, width, height, site.Data.MarkerColor);
			}
		}

		// 바닥 전리품 버퍼에서 GameItemObject (상자, 적 정지, 화면 청소, 회복, 자석) 만. 경험치 구슬과 재료는 제외
		private void CollectItems(SurvivalFeelConfig config, Camera camera, Vector3 playerPosition, float width, float height)
		{
			List<GameObject> drops = ObjectBufferManager.GetObjects(ObjectType.Drop);
			for (int i = 0; i < drops.Count; i++)
			{
				GameObject drop = drops[i];
				if (drop == null || drop.activeInHierarchy == false || itemLookup.TryGet(drop, out GameItemObject item) == false)
					continue;
				TryAddCandidate(config, camera, item, MarkerKind.Item, playerPosition, width, height);
			}
		}

		private void TryAddCandidate(SurvivalFeelConfig config, Camera camera, Component target, MarkerKind kind, Vector3 playerPosition, float width, float height, Color color = default)
		{
			Vector3 worldPosition = target.transform.position;
			if (ProjectToEdge(config, camera, worldPosition, width, height, out Vector2 position, out float angle) == false)
				return;
			candidates.Add(new Candidate
			{
				Target = target,
				Kind = kind,
				Color = color,
				Distance = Vector3.Distance(playerPosition, worldPosition),
				Position = position,
				Angle = angle
			});
		}

		// 화면 밖이면 true 와 가장자리 안쪽 자리 (root 좌표), 방향 각도 (오른쪽 0, 시계 방향 +)
		private bool ProjectToEdge(SurvivalFeelConfig config, Camera camera, Vector3 worldPosition, float width, float height, out Vector2 position, out float angle)
		{
			position = Vector2.zero;
			angle = 0f;
			Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
			// 카메라 뒤는 투영이 뒤집힘. 되돌려 방향만 살림
			bool behind = viewport.z < 0f;
			if (behind)
			{
				viewport.x = 1f - viewport.x;
				viewport.y = 1f - viewport.y;
			}
			Vector2 screenPoint = new(viewport.x * Screen.width, (1f - viewport.y) * Screen.height);
			Vector2 local = root.WorldToLocal(RuntimePanelUtils.ScreenToPanel(root.panel, screenPoint));

			// 아래 HUD 띠에 가려진 대상은 화면 밖으로 봄
			float inset = config.OffscreenOnScreenInset;
			bool inside = local.x >= inset && local.x <= width - inset && local.y >= inset && local.y <= height - inset - height * config.OffscreenBottomReserve;
			if (behind == false && inside)
				return false;

			Vector2 center = new(width * 0.5f, height * 0.5f);
			Vector2 direction = local - center;
			if (direction.sqrMagnitude < 0.0001f)
				direction = Vector2.up;
			// 화면 가운데에서 대상 쪽으로 뻗어 표식 테두리에 닿는 자리. 아래쪽은 HUD 띠 위까지만 (체력 막대와 스킬 칸에 안 겹치게)
			float margin = config.OffscreenEdgeMargin;
			float halfWidth = Mathf.Max(1f, center.x - margin);
			float up = Mathf.Max(1f, center.y - margin);
			float down = Mathf.Max(1f, center.y - margin - height * config.OffscreenBottomReserve);
			float reachX = halfWidth / Mathf.Max(Mathf.Abs(direction.x), 0.0001f);
			float reachY = (direction.y > 0f ? down : up) / Mathf.Max(Mathf.Abs(direction.y), 0.0001f);
			float reach = Mathf.Min(reachX, reachY);
			position = center + direction * reach;
			angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
			return true;
		}

		// 보스, 정예, 아이템, 맵 오브젝트 순. 맵 오브젝트는 쓸 때까지 남아 있어 아이템 화살표를 밀어내지 않게 맨 뒤. 같은 종류 안에서는 가까운 순
		private static int CompareCandidates(Candidate left, Candidate right)
		{
			if (left.Kind != right.Kind)
				return ((int)left.Kind).CompareTo((int)right.Kind);
			return left.Distance.CompareTo(right.Distance);
		}

		// 지난 프레임과 같은 대상은 같은 표식에 남겨 튐을 다시 하지 않음
		private void Assign(SurvivalFeelConfig config)
		{
			int count = Mathf.Min(candidates.Count, markers.Length);
			for (int i = 0; i < markers.Length; i++)
			{
				markerUsed[i] = false;
				candidateMatched[i] = false;
			}

			for (int candidateIndex = 0; candidateIndex < count; candidateIndex++)
			{
				for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
				{
					if (markerUsed[markerIndex] || markers[markerIndex].Target != candidates[candidateIndex].Target)
						continue;
					Apply(config, markers[markerIndex], candidates[candidateIndex], false);
					markerUsed[markerIndex] = true;
					candidateMatched[candidateIndex] = true;
					break;
				}
			}

			for (int candidateIndex = 0; candidateIndex < count; candidateIndex++)
			{
				if (candidateMatched[candidateIndex])
					continue;
				for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
				{
					if (markerUsed[markerIndex])
						continue;
					Apply(config, markers[markerIndex], candidates[candidateIndex], true);
					markerUsed[markerIndex] = true;
					break;
				}
			}

			for (int i = 0; i < markers.Length; i++)
			{
				if (markerUsed[i])
					continue;
				Marker marker = markers[i];
				marker.Target = null;
				marker.Element.style.visibility = Visibility.Hidden;
				marker.Pop.AddToClassList(USS_POP_INSTANT);
				marker.Pop.RemoveFromClassList(USS_POP_SHOWN);
			}
		}

		private void Apply(SurvivalFeelConfig config, Marker marker, Candidate candidate, bool fresh)
		{
			// 새 배정: 전환 없이 작게 복귀 (튐 역전 방지). 다음 프레임 전환 켬, 대기 프레임 뒤 튐
			if (fresh)
			{
				marker.Target = candidate.Target;
				marker.AssignedFrame = Time.frameCount;
				marker.Pop.AddToClassList(USS_POP_INSTANT);
				marker.Pop.RemoveFromClassList(USS_POP_SHOWN);
				marker.Element.EnableInClassList(USS_MARKER_BOSS, candidate.Kind == MarkerKind.Boss);
				marker.Element.EnableInClassList(USS_MARKER_ELITE, candidate.Kind == MarkerKind.Elite);
				marker.Element.EnableInClassList(USS_MARKER_ITEM, candidate.Kind == MarkerKind.Item);
				bool isMapObject = candidate.Kind == MarkerKind.MapObject;
				marker.Element.EnableInClassList(USS_MARKER_MAP_OBJECT, isMapObject);
				// 맵 오브젝트만 데이터 색. 나머지는 USS 의 종류 색으로 되돌림
				marker.Dot.style.backgroundColor = isMapObject ? new StyleColor(candidate.Color) : new StyleColor(StyleKeyword.Null);
				marker.Arrow.style.borderTopColor = isMapObject ? new StyleColor(candidate.Color) : new StyleColor(StyleKeyword.Null);
				marker.Arrow.style.borderRightColor = isMapObject ? new StyleColor(candidate.Color) : new StyleColor(StyleKeyword.Null);
				marker.Element.style.visibility = Visibility.Visible;
			}
			else
			{
				int framesSinceAssigned = Time.frameCount - marker.AssignedFrame;
				if (framesSinceAssigned >= 1 && marker.Pop.ClassListContains(USS_POP_INSTANT))
					marker.Pop.RemoveFromClassList(USS_POP_INSTANT);
				if (framesSinceAssigned >= config.OffscreenPopDelayFrames && marker.Pop.ClassListContains(USS_POP_SHOWN) == false)
					marker.Pop.AddToClassList(USS_POP_SHOWN);
			}

			// 1 가까움, 0 멂
			float nearness = Mathf.InverseLerp(config.OffscreenFarDistance, config.OffscreenNearDistance, candidate.Distance);
			float scale = Mathf.Lerp(config.OffscreenFarScale, config.OffscreenNearScale, nearness) * KindScale(config, candidate.Kind);
			marker.Element.style.left = candidate.Position.x;
			marker.Element.style.top = candidate.Position.y;
			marker.Element.style.scale = new Scale(new Vector2(scale, scale));
			marker.Element.style.opacity = Mathf.Lerp(config.OffscreenFarOpacity, config.OffscreenNearOpacity, nearness);
			marker.Pivot.style.rotate = new UnityEngine.UIElements.Rotate(new Angle(candidate.Angle, AngleUnit.Degree));
		}

		private static float KindScale(SurvivalFeelConfig config, MarkerKind kind) => kind switch
		{
			MarkerKind.Boss => config.OffscreenBossScale,
			MarkerKind.Item => config.OffscreenItemScale,
			MarkerKind.MapObject => MapObjectSettings.Load().OffscreenScale,
			_ => 1f,
		};
	}
}
