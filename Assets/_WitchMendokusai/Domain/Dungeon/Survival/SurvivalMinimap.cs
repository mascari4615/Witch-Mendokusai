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
	// 큰 지도: 입력 MinimapLargeView 를 누르는 동안 (폰은 작은 지도 터치로 열고 큰 지도 터치로 닫음) 화면 가운데에 같은 그리기로 넓은 반경
	// 큰 지도가 열리면 작은 지도는 숨김. 큰 지도 반경은 SurvivalFeelConfig.MinimapLargeRadius, 모양은 SurvivalMinimap.uss 의 wm-minimap-large
	// 시간 정지 중과 던전 밖에서는 안 열림. 던전을 벗어나면 터치로 연 상태도 닫음
	public class SurvivalMinimap : MonoBehaviour
	{
		private const string USS_ROOT = "wm-minimap";
		private const string USS_FRAME = "wm-minimap__frame";
		private const string USS_DOTS = "wm-minimap__dots";
		private const string USS_PLAYER = "wm-minimap__player";
		private const string USS_LARGE_ROOT = "wm-minimap-large";
		private const string USS_LARGE_FRAME = "wm-minimap-large__frame";

		private static readonly CustomStyleProperty<Color> MONSTER_COLOR = new("--wm-minimap-monster");
		private static readonly CustomStyleProperty<Color> ELITE_COLOR = new("--wm-minimap-elite");
		private static readonly CustomStyleProperty<Color> BOSS_COLOR = new("--wm-minimap-boss");
		private static readonly CustomStyleProperty<Color> ITEM_COLOR = new("--wm-minimap-item");
		private static readonly CustomStyleProperty<Color> CHEST_COLOR = new("--wm-minimap-chest");

		// 지도 한 장의 점 목록과 그리는 요소. 작은 지도와 큰 지도가 하나씩
		// 지도 좌표. 가운데 0, 반경 끝 1. 오른쪽 +x, 아래쪽 +y (UI 방향)
		private sealed class MapView
		{
			public VisualElement Dots;
			public readonly List<Vector2> MonsterDots = new();
			public readonly List<Vector2> EliteDots = new();
			public readonly List<Vector2> BossDots = new();
			public readonly List<Vector2> ItemDots = new();
			public readonly List<Vector2> ChestDots = new();

			public void Clear()
			{
				MonsterDots.Clear();
				EliteDots.Clear();
				BossDots.Clear();
				ItemDots.Clear();
				ChestDots.Clear();
			}
		}

		private ResourceManager resourceManager;
		private PlayerProvider playerProvider;
		private SurvivalFeel feel;
		private InputManager inputManager;

		private VisualElement root;
		private VisualElement largeRoot;
		private BlurPanel largeBlur;
		private float nextRefreshTime;

		// 큰 지도 열림 조건. 키를 누르는 동안, 또는 작은 지도를 터치해 연 상태
		private bool isLargeHeld;
		private bool isLargeToggled;
		private bool isLargeShown;

		private readonly MapView smallView = new();
		private readonly MapView largeView = new();
		private readonly HashSet<MonsterObject> eliteSet = new();
		private readonly ComponentLookup<MonsterObject> monsterLookup = new();
		private readonly ComponentLookup<GameItemObject> itemLookup = new();

		private Color monsterColor = Color.white;
		private Color eliteColor = Color.white;
		private Color bossColor = Color.white;
		private Color itemColor = Color.white;
		private Color chestColor = Color.white;

		private SurvivalFeelConfig Config => resourceManager.SurvivalFeel;

		public void Init(ResourceManager resourceManager, PlayerProvider playerProvider, SurvivalFeel feel, InputManager inputManager)
		{
			this.resourceManager = resourceManager;
			this.playerProvider = playerProvider;
			this.feel = feel;
			this.inputManager = inputManager;
			inputManager.RegisterInputEvent(InputEventType.MinimapLargeView, InputEventResponseType.Started, OnLargeKeyStarted);
			inputManager.RegisterInputEvent(InputEventType.MinimapLargeView, InputEventResponseType.Canceled, OnLargeKeyCanceled);
		}

		private void OnLargeKeyStarted() => isLargeHeld = true;

		private void OnLargeKeyCanceled() => isLargeHeld = false;

		private void OnSmallClicked(ClickEvent clickEvent)
		{
			isLargeToggled = true;
			clickEvent.StopPropagation();
		}

		private void OnLargeClicked(ClickEvent clickEvent)
		{
			isLargeToggled = false;
			isLargeHeld = false;
			clickEvent.StopPropagation();
		}

		private void OnDestroy()
		{
			inputManager.UnregisterInputEvent(InputEventType.MinimapLargeView, InputEventResponseType.Started, OnLargeKeyStarted);
			inputManager.UnregisterInputEvent(InputEventType.MinimapLargeView, InputEventResponseType.Canceled, OnLargeKeyCanceled);
			if (root != null)
				root.RemoveFromHierarchy();
			if (largeRoot != null)
				largeRoot.RemoveFromHierarchy();
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
			largeRoot?.RemoveFromHierarchy();

			root = Make(USS_ROOT);
			root.styleSheets.Add(config.MinimapStyleSheet);
			VisualElement frame = Make(USS_FRAME);
			// 폰 조작: 작은 지도를 누르면 큰 지도
			frame.pickingMode = PickingMode.Position;
			frame.RegisterCallback<ClickEvent>(OnSmallClicked);
			smallView.Dots = MakeDots(smallView);
			frame.Add(smallView.Dots);
			frame.Add(Make(USS_PLAYER));
			root.Add(frame);
			root.style.display = DisplayStyle.None;
			UIRoot.Instance.HudLayer.Add(root);

			// 큰 지도 자리는 화면 전체 (클릭 통과), 지도 판만 클릭을 받아 닫음. 흐림은 판 뒤에만, 보일 때만 켬
			largeRoot = Make(USS_LARGE_ROOT);
			largeRoot.styleSheets.Add(config.MinimapStyleSheet);
			VisualElement largeFrame = Make(USS_LARGE_FRAME);
			largeFrame.pickingMode = PickingMode.Position;
			largeFrame.RegisterCallback<ClickEvent>(OnLargeClicked);
			largeBlur = BlurPanel.AttachBehind(largeFrame);
			largeBlur.SetActive(false);
			largeView.Dots = MakeDots(largeView);
			largeFrame.Add(largeView.Dots);
			largeFrame.Add(Make(USS_PLAYER));
			largeRoot.Add(largeFrame);
			largeRoot.style.display = DisplayStyle.None;
			isLargeShown = false;
			UIRoot.Instance.HudLayer.Add(largeRoot);
			return true;
		}

		private VisualElement MakeDots(MapView view)
		{
			VisualElement element = Make(USS_DOTS);
			element.generateVisualContent += context => DrawView(context, view);
			element.RegisterCallback<CustomStyleResolvedEvent>(OnDotStyleResolved);
			return element;
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
			if (resolvedEvent.target is VisualElement element)
				element.MarkDirtyRepaint();
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
			if (inRun == false)
			{
				isLargeToggled = false;
				isLargeHeld = false;
			}
			// 카드 고르기, 상자, 일시정지처럼 시간이 멈춘 동안 숨김 (창 위에 겹침)
			bool isPaused = Time.timeScale <= 0f || (TimeManager.TryGetExistingInstance(out TimeManager timeManager) && timeManager.IsPaused);
			bool visible = config.UseMinimap && inRun && isPaused == false && camera != null && player != null;
			bool wantLarge = visible && (isLargeHeld || isLargeToggled);
			if (wantLarge != isLargeShown)
			{
				isLargeShown = wantLarge;
				largeBlur.SetActive(wantLarge);
				// 바뀐 지도를 바로 채움
				nextRefreshTime = 0f;
			}
			root.style.display = visible && wantLarge == false ? DisplayStyle.Flex : DisplayStyle.None;
			largeRoot.style.display = wantLarge ? DisplayStyle.Flex : DisplayStyle.None;
			if (visible == false)
				return;
			if (Time.unscaledTime < nextRefreshTime)
				return;
			nextRefreshTime = Time.unscaledTime + config.MinimapRefreshInterval;

			MapView view = isLargeShown ? largeView : smallView;
			float radius = isLargeShown ? config.MinimapLargeRadius : config.MinimapRadius;
			Collect(config, camera, player.transform.position, view, radius);
			view.Dots.MarkDirtyRepaint();
		}

		private void Collect(SurvivalFeelConfig config, Camera camera, Vector3 center, MapView view, float radius)
		{
			view.Clear();

			// 화면 위 방향을 바닥에 눕힌 것이 지도 위쪽. 바로 내려다보는 카메라면 카메라 위쪽 사용
			Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
			if (forward.sqrMagnitude < 0.0001f)
				forward = Vector3.ProjectOnPlane(camera.transform.up, Vector3.up);
			forward.Normalize();
			Vector3 right = Vector3.Cross(Vector3.up, forward);
			float inverseRadius = 1f / radius;

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
					view.BossDots.Add(point);
				else if (eliteSet.Contains(monster))
					view.EliteDots.Add(point);
				else if (view.MonsterDots.Count < config.MinimapMaxDots)
					view.MonsterDots.Add(point);
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
					view.ChestDots.Add(point);
				else
					view.ItemDots.Add(point);
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
		private void DrawView(MeshGenerationContext context, MapView view)
		{
			if (resourceManager == null)
				return;
			Rect rect = view.Dots.contentRect;
			if (rect.width <= 0f || rect.height <= 0f)
				return;
			SurvivalFeelConfig config = Config;
			Vector2 middle = rect.center;
			float radius = Mathf.Min(rect.width, rect.height) * 0.5f;

			DrawSet(context, view.MonsterDots, middle, radius, config.MinimapMonsterDotSize, monsterColor, false);
			DrawSet(context, view.ItemDots, middle, radius, config.MinimapItemDotSize, itemColor, true);
			DrawSet(context, view.ChestDots, middle, radius, config.MinimapChestDotSize, chestColor, true);
			DrawSet(context, view.EliteDots, middle, radius, config.MinimapEliteDotSize, eliteColor, false);
			DrawSet(context, view.BossDots, middle, radius, config.MinimapBossDotSize, bossColor, false);
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
