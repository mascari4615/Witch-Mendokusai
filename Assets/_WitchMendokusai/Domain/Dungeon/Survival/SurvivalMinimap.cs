using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 생존 던전 HUD 의 작은 지도. 내 위치가 가운데, 반경 안 몬스터 점과 특수 아이템 마름모
	// 일반 몬스터 작은 점, 정예 주황, 보스 진홍, 아이템 금색. 경험치 구슬은 제외
	// 점은 몬스터마다 요소를 만들지 않고 generateVisualContent 로 한 번에 그림
	// 자리, 모양, 점 색은 SurvivalMinimap.uss. 반경, 갱신 간격, 점 크기, 상한은 SurvivalFeelConfig 의 작은 지도 묶음
	// 위쪽은 카메라 시선 방향 (화면 위와 같은 방향)
	public class SurvivalMinimap : MonoBehaviour
	{
		private const string USS_ROOT = "wm-minimap";
		private const string USS_FRAME = "wm-minimap__frame";
		private const string USS_DOTS = "wm-minimap__dots";
		private const string USS_PLAYER = "wm-minimap__player";

		private static readonly CustomStyleProperty<Color> MONSTER_COLOR = new("--wm-minimap-monster");
		private static readonly CustomStyleProperty<Color> ELITE_COLOR = new("--wm-minimap-elite");
		private static readonly CustomStyleProperty<Color> BOSS_COLOR = new("--wm-minimap-boss");
		private static readonly CustomStyleProperty<Color> ITEM_COLOR = new("--wm-minimap-item");
		private static readonly CustomStyleProperty<Color> CHEST_COLOR = new("--wm-minimap-chest");

		private ResourceManager resourceManager;
		private PlayerProvider playerProvider;
		private SurvivalFeel feel;

		private VisualElement root;
		private VisualElement dots;
		private float nextRefreshTime;

		// 지도 좌표. 가운데 0, 반경 끝 1. 오른쪽 +x, 아래쪽 +y (UI 방향)
		private readonly List<Vector2> monsterDots = new();
		private readonly List<Vector2> eliteDots = new();
		private readonly List<Vector2> bossDots = new();
		private readonly List<Vector2> itemDots = new();
		private readonly List<Vector2> chestDots = new();
		private readonly HashSet<MonsterObject> eliteSet = new();
		private readonly ComponentLookup<MonsterObject> monsterLookup = new();
		private readonly ComponentLookup<GameItemObject> itemLookup = new();

		private Color monsterColor = Color.white;
		private Color eliteColor = Color.white;
		private Color bossColor = Color.white;
		private Color itemColor = Color.white;
		private Color chestColor = Color.white;

		private SurvivalFeelConfig Config => resourceManager.SurvivalFeel;

		public void Init(ResourceManager resourceManager, PlayerProvider playerProvider, SurvivalFeel feel)
		{
			this.resourceManager = resourceManager;
			this.playerProvider = playerProvider;
			this.feel = feel;
		}

		private void OnDestroy()
		{
			if (root != null)
				root.RemoveFromHierarchy();
			monsterLookup.Clear();
			itemLookup.Clear();
		}

		// UIRoot 는 이 컴포넌트보다 늦게 생길 수 있어 쓸 때 지음. UIRoot 재활성화로 HUD 층이 새로 생기면 새 층에 다시 지음
		private bool EnsureBuilt(SurvivalFeelConfig config)
		{
			if (UIRoot.Instance == null || UIRoot.Instance.HudLayer == null)
				return false;
			if (root != null && root.parent == UIRoot.Instance.HudLayer)
				return true;
			if (config.MinimapStyleSheet == null)
			{
				// 같은 예외 매 프레임 반복 방지. 한 번 던지고 컴포넌트 끔
				enabled = false;
				throw new InvalidOperationException($"{config.name}: MinimapStyleSheet 가 비어 있음. SurvivalMinimap.uss 를 연결해야 함");
			}

			root?.RemoveFromHierarchy();
			root = Make(USS_ROOT);
			root.styleSheets.Add(config.MinimapStyleSheet);
			VisualElement frame = Make(USS_FRAME);
			dots = Make(USS_DOTS);
			dots.generateVisualContent += DrawDots;
			dots.RegisterCallback<CustomStyleResolvedEvent>(OnDotStyleResolved);
			frame.Add(dots);
			frame.Add(Make(USS_PLAYER));
			root.Add(frame);
			root.style.display = DisplayStyle.None;
			UIRoot.Instance.HudLayer.Add(root);
			return true;
		}

		private static VisualElement Make(string className)
		{
			VisualElement element = new() { pickingMode = PickingMode.Ignore };
			element.AddToClassList(className);
			return element;
		}

		private void OnDotStyleResolved(CustomStyleResolvedEvent resolvedEvent)
		{
			ICustomStyle customStyle = resolvedEvent.customStyle;
			if (customStyle.TryGetValue(MONSTER_COLOR, out Color monster))
				monsterColor = monster;
			if (customStyle.TryGetValue(ELITE_COLOR, out Color elite))
				eliteColor = elite;
			if (customStyle.TryGetValue(BOSS_COLOR, out Color boss))
				bossColor = boss;
			if (customStyle.TryGetValue(ITEM_COLOR, out Color item))
				itemColor = item;
			if (customStyle.TryGetValue(CHEST_COLOR, out Color chest))
				chestColor = chest;
			dots.MarkDirtyRepaint();
		}

		private void Update()
		{
			if (resourceManager == null)
				return;
			SurvivalFeelConfig config = Config;
			if (EnsureBuilt(config) == false)
				return;

			Camera camera = Camera.main;
			PlayerObject player = playerProvider.CurrentObject;
			bool inRun = DungeonManager.Instance != null && DungeonManager.Instance.IsDungeon;
			// 카드 고르기, 상자, 일시정지처럼 시간이 멈춘 동안 숨김 (창 위에 겹침)
			bool isPaused = Time.timeScale <= 0f || (TimeManager.TryGetExistingInstance(out TimeManager timeManager) && timeManager.IsPaused);
			bool visible = config.UseMinimap && inRun && isPaused == false && camera != null && player != null;
			root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
			if (visible == false)
				return;
			if (Time.unscaledTime < nextRefreshTime)
				return;
			nextRefreshTime = Time.unscaledTime + config.MinimapRefreshInterval;

			Collect(config, camera, player.transform.position);
			dots.MarkDirtyRepaint();
		}

		private void Collect(SurvivalFeelConfig config, Camera camera, Vector3 center)
		{
			monsterDots.Clear();
			eliteDots.Clear();
			bossDots.Clear();
			itemDots.Clear();
			chestDots.Clear();

			// 화면 위 방향을 바닥에 눕힌 것이 지도 위쪽. 바로 내려다보는 카메라면 카메라 위쪽 사용
			Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
			if (forward.sqrMagnitude < 0.0001f)
				forward = Vector3.ProjectOnPlane(camera.transform.up, Vector3.up);
			forward.Normalize();
			Vector3 right = Vector3.Cross(Vector3.up, forward);
			float inverseRadius = 1f / config.MinimapRadius;

			eliteSet.Clear();
			IReadOnlyList<MonsterObject> elites = feel.Elites;
			for (int i = 0; i < elites.Count; i++)
			{
				if (elites[i] != null)
					eliteSet.Add(elites[i]);
			}

			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			for (int i = 0; i < monsters.Count; i++)
			{
				GameObject monsterObject = monsters[i];
				if (monsterObject == null || monsterObject.activeInHierarchy == false || monsterLookup.TryGet(monsterObject, out MonsterObject monster) == false || monster.IsAlive == false)
					continue;
				if (TryToMap(monster.transform.position, center, forward, right, inverseRadius, out Vector2 point) == false)
					continue;
				if (monster.UnitData.Type == MonsterType.Boss)
					bossDots.Add(point);
				else if (eliteSet.Contains(monster))
					eliteDots.Add(point);
				else if (monsterDots.Count < config.MinimapMaxDots)
					monsterDots.Add(point);
			}

			List<GameObject> drops = ObjectBufferManager.GetObjects(ObjectType.Drop);
			for (int i = 0; i < drops.Count; i++)
			{
				GameObject drop = drops[i];
				if (drop == null || drop.activeInHierarchy == false || itemLookup.TryGet(drop, out GameItemObject item) == false)
					continue;
				if (TryToMap(item.transform.position, center, forward, right, inverseRadius, out Vector2 point) == false)
					continue;
				if (item is ChestObject)
					chestDots.Add(point);
				else
					itemDots.Add(point);
			}
		}

		// 반경 원 안이면 true 와 지도 좌표
		private static bool TryToMap(Vector3 position, Vector3 center, Vector3 forward, Vector3 right, float inverseRadius, out Vector2 point)
		{
			Vector3 offset = position - center;
			point = new Vector2(Vector3.Dot(offset, right) * inverseRadius, -Vector3.Dot(offset, forward) * inverseRadius);
			return point.sqrMagnitude <= 1f;
		}

		// 아래에서 위로: 일반 몬스터, 아이템, 상자, 정예, 보스
		private void DrawDots(MeshGenerationContext context)
		{
			if (resourceManager == null)
				return;
			Rect rect = dots.contentRect;
			if (rect.width <= 0f || rect.height <= 0f)
				return;
			SurvivalFeelConfig config = Config;
			Vector2 middle = rect.center;
			float radius = Mathf.Min(rect.width, rect.height) * 0.5f;

			DrawSet(context, monsterDots, middle, radius, config.MinimapMonsterDotSize, monsterColor, false);
			DrawSet(context, itemDots, middle, radius, config.MinimapItemDotSize, itemColor, true);
			DrawSet(context, chestDots, middle, radius, config.MinimapChestDotSize, chestColor, true);
			DrawSet(context, eliteDots, middle, radius, config.MinimapEliteDotSize, eliteColor, false);
			DrawSet(context, bossDots, middle, radius, config.MinimapBossDotSize, bossColor, false);
		}

		// 점 하나에 꼭짓점 넷. 네모 (몬스터) 또는 마름모 (아이템)
		private static void DrawSet(MeshGenerationContext context, List<Vector2> points, Vector2 middle, float radius, float size, Color color, bool diamond)
		{
			if (points.Count == 0 || color.a <= 0f)
				return;
			MeshWriteData mesh = context.Allocate(points.Count * 4, points.Count * 6);
			float half = size * 0.5f;
			for (int i = 0; i < points.Count; i++)
			{
				Vector2 position = middle + points[i] * radius;
				if (diamond)
				{
					mesh.SetNextVertex(MakeVertex(position.x, position.y - half, color));
					mesh.SetNextVertex(MakeVertex(position.x + half, position.y, color));
					mesh.SetNextVertex(MakeVertex(position.x, position.y + half, color));
					mesh.SetNextVertex(MakeVertex(position.x - half, position.y, color));
				}
				else
				{
					mesh.SetNextVertex(MakeVertex(position.x - half, position.y - half, color));
					mesh.SetNextVertex(MakeVertex(position.x + half, position.y - half, color));
					mesh.SetNextVertex(MakeVertex(position.x + half, position.y + half, color));
					mesh.SetNextVertex(MakeVertex(position.x - half, position.y + half, color));
				}
			}
			for (int i = 0; i < points.Count; i++)
			{
				ushort first = (ushort)(i * 4);
				mesh.SetNextIndex(first);
				mesh.SetNextIndex((ushort)(first + 1));
				mesh.SetNextIndex((ushort)(first + 2));
				mesh.SetNextIndex(first);
				mesh.SetNextIndex((ushort)(first + 2));
				mesh.SetNextIndex((ushort)(first + 3));
			}
		}

		private static Vertex MakeVertex(float x, float y, Color color) => new() { position = new Vector3(x, y, Vertex.nearZ), tint = color };
	}
}
