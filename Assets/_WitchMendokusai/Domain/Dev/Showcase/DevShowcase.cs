using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 개발자 진열장. 진열장 던전이 시작되면 시작 자리 주변에 격자로 진열, 판이 끝나면 걷음
	// - 몬스터는 제자리 허수아비 (DevShowcaseLease 가 두뇌와 자동 시전 끔, 불사). 상태이상 칸은 계속 다시 걺
	// - 부서진 물건, 사라진 몬스터는 유지 간격마다 다시 놓음. 주운 아이템은 주인공이 떨어진 뒤 다시 놓음
	// 소유자 DevWindowController. MonoBehaviour 가 아니라 소유자가 Tick
	public sealed partial class DevShowcase
	{
		private sealed class MonsterCell
		{
			public Monster Monster;
			public Vector3 Home;
			public List<UnitStatusType> Statuses;
			public MonsterObject Object;
		}

		private sealed class BreakableCell
		{
			public int KindIndex;
			public int VariantIndex;
			public Vector3 Position;
			public BreakableObject Object;
		}

		private sealed class DropCell
		{
			public DevShowcaseDropCell Data;
			public Vector3 Position;
			public GameObject Object;
			// 다시 놓을 수 있는 시각 (진열 경과 초). 처음은 바로
			public float RespawnAt;
		}

		private sealed class PadCell
		{
			// null 이면 맨손
			public EquipmentData Weapon;
			public Vector3 Position;
		}

		private readonly DevShowcaseSO data;
		private readonly GameEventManager gameEventManager;
		private readonly PlayerProvider playerProvider;
		private readonly ObjectPoolManager objectPoolManager;
		private readonly DevDropSpawner dropSpawner;

		private readonly List<MonsterCell> monsterCells = new();
		private readonly List<BreakableCell> breakableCells = new();
		private readonly List<DropCell> dropCells = new();
		private readonly List<PadCell> padCells = new();
		private readonly HashSet<BreakableObject> claimedBreakables = new();
		private readonly List<Transform> labels = new();
		private GameObject root;
		private GroundGenerator groundGenerator;
		private PadCell standingPad;
		private float maintenanceElapsed;
		private float statusElapsed;
		private float activeSeconds;

		public bool IsActive => root != null;
		public DevShowcaseSO Data => data;

		public DevShowcase(DevShowcaseSO data, GameEventManager gameEventManager, PlayerProvider playerProvider, ObjectPoolManager objectPoolManager, DevDropSpawner dropSpawner)
		{
			this.data = data;
			this.gameEventManager = gameEventManager;
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
			this.dropSpawner = dropSpawner;
		}

		public void Bind()
		{
			gameEventManager.RegisterCallback(GameEventType.OnDungeonStart, OnDungeonStart);
			gameEventManager.RegisterCallback(GameEventType.OnDungeonReturn, Release);
		}

		public void Unbind()
		{
			gameEventManager.UnregisterCallback(GameEventType.OnDungeonStart, OnDungeonStart);
			gameEventManager.UnregisterCallback(GameEventType.OnDungeonReturn, Release);
			Release();
		}

		// 진열장 판 시작. 진열 설정 누락은 판 전환 전에 실패 (전환 중 예외는 입장을 잠금)
		public void Enter()
		{
			if (data.Dungeon == null)
				throw new System.InvalidOperationException($"{nameof(DevShowcaseSO)}.{nameof(DevShowcaseSO.Dungeon)} 미연결");
			if (data.LabelPrefab == null)
				throw new System.InvalidOperationException($"{nameof(DevShowcaseSO)}.{nameof(DevShowcaseSO.LabelPrefab)} 미연결");
			ResolveDummy();
			LoadBreakableSettings();
			DungeonManager.Instance.StartDungeon(data.Dungeon);
		}

		// 재도전도 이 이벤트로 옴. 앞 판 몬스터는 GameManager.Init 이 이미 껐고 빌린 몸은 그때 되돌아감
		private void OnDungeonStart()
		{
			Release();
			if (DungeonManager.Instance.CurDungeon != data.Dungeon)
				return;
			Layout(playerProvider.CurrentObject.UnitMovement.Position);
		}

		public void Tick(float deltaTime)
		{
			if (IsActive == false)
				return;
			// 결과 화면과 귀환 전환 동안 이름표와 받침을 숨김
			bool inRun = DungeonManagerBridge.IsDungeon;
			if (root.activeSelf != inRun)
				root.SetActive(inRun);
			if (inRun == false)
				return;
			AlignLabels();

			activeSeconds += deltaTime;
			statusElapsed += deltaTime;
			maintenanceElapsed += deltaTime;
			if (maintenanceElapsed < data.MaintenanceSeconds)
				return;
			maintenanceElapsed = 0f;

			bool refreshStatus = statusElapsed >= data.StatusRefreshSeconds;
			if (refreshStatus)
				statusElapsed = 0f;

			foreach (MonsterCell cell in monsterCells)
				MaintainMonster(cell, refreshStatus);
			MaintainBreakables();
			foreach (DropCell cell in dropCells)
				MaintainDrop(cell);
			UpdatePads();
		}

		private void AlignLabels()
		{
			Camera camera = Camera.main;
			if (camera == null)
				return;
			Quaternion rotation = camera.transform.rotation;
			foreach (Transform label in labels)
				label.rotation = rotation;
		}

		private void MaintainMonster(MonsterCell cell, bool refreshStatus)
		{
			if (IsOurs(cell) == false)
			{
				DropBody(cell);
				SpawnMonster(cell);
			}

			MonsterObject monster = cell.Object;
			Vector3 offset = monster.UnitMovement.Position - cell.Home;
			offset.y = 0f;
			if (offset.sqrMagnitude > data.HomeLeash * data.HomeLeash)
				monster.UnitMovement.Teleport(cell.Home);

			if (refreshStatus == false || cell.Statuses == null)
				return;
			PlayerObject player = playerProvider.CurrentObject;
			foreach (UnitStatusType status in cell.Statuses)
				UnitStatus.Apply(monster, status, player, data.StatusSeconds);
		}

		// 빌린 몸이 꺼지면 임대가 빠지므로, 같은 몸을 풀이 다시 내줘도 우리 것이 아님
		private static bool IsOurs(MonsterCell cell)
		{
			if (cell.Object == null || cell.Object.gameObject.activeInHierarchy == false || cell.Object.IsAlive == false)
				return false;
			return cell.Object.TryGetComponent(out DevShowcaseLease lease) && lease.Owner == cell;
		}

		// 아직 우리 임대가 붙은 몸 (죽었거나 밀려난 채 켜진 몸) 은 끄고 놓아줌. 안 끄면 새 몸만 쌓임
		private static void DropBody(MonsterCell cell)
		{
			if (cell.Object != null && cell.Object.gameObject.activeSelf && cell.Object.TryGetComponent(out DevShowcaseLease lease) && lease.Owner == cell)
				cell.Object.gameObject.SetActive(false);
			cell.Object = null;
		}

		private void SpawnMonster(MonsterCell cell)
		{
			GameObject spawned = objectPoolManager.Spawn(cell.Monster.Prefab);
			spawned.transform.position = cell.Home;
			MonsterObject monster = spawned.GetComponent<MonsterObject>();
			monster.Init(cell.Monster);
			spawned.SetActive(true);
			if (monster.IsAlive == false)
			{
				spawned.SetActive(false);
				throw new System.InvalidOperationException($"{cell.Monster.Name} 가 체력 {monster.UnitStat[UnitStatType.HP_CUR]} 으로 생성. 체력 수치 확인 (던전 체력 배율 넘침 등)");
			}
			DevShowcaseLease.Take(monster, cell);
			monster.UnitMovement.Teleport(cell.Home);
			cell.Object = monster;
		}

		private void MaintainBreakables()
		{
			if (breakableCells.Count == 0 || activeSeconds < data.BreakableDelaySeconds)
				return;
			if (groundGenerator == null)
				groundGenerator = Object.FindAnyObjectByType<GroundGenerator>();
			if (groundGenerator == null)
				throw new System.InvalidOperationException($"진열장 판 스테이지에 {nameof(GroundGenerator)} 없음");
			if (groundGenerator.BreakablesReady == false)
				return;

			// 풀이 스택이라 부순 물건이 다른 칸에 다시 놓일 수 있음. 한 물건은 한 칸만 가짐
			BreakableField field = groundGenerator.Breakables;
			claimedBreakables.Clear();
			foreach (BreakableCell cell in breakableCells)
			{
				if (field.IsFixed(cell.Object) && claimedBreakables.Add(cell.Object))
					continue;
				cell.Object = field.PlaceFixed(cell.Position, 0f, cell.KindIndex, cell.VariantIndex);
				claimedBreakables.Add(cell.Object);
			}
		}

		// 주운 뒤 다시 놓기는 대기 시간이 지나고 주인공이 떨어졌을 때만. 바로 놓으면 줍기가 반복됨
		private void MaintainDrop(DropCell cell)
		{
			if (cell.Object != null && cell.Object.activeInHierarchy && IsNear(cell.Object.transform.position, cell.Position, data.HomeLeash))
				return;
			if (cell.Object != null)
			{
				cell.Object = null;
				cell.RespawnAt = activeSeconds + data.DropRespawnSeconds;
			}
			if (activeSeconds < cell.RespawnAt)
				return;
			if (IsNear(playerProvider.CurrentObject.UnitMovement.Position, cell.Position, data.DropRespawnDistance))
				return;

			cell.Object = dropSpawner.Spawn(cell.Data.Type, cell.Position, cell.Data.NyangAmount);
			// 경험치 보석 상한이면 기존 보석에 합쳐져 null. 매 간격 합치지 않게 대기
			if (cell.Object == null)
				cell.RespawnAt = activeSeconds + data.DropRespawnSeconds;
		}

		private static bool IsNear(Vector3 position, Vector3 home, float distance)
		{
			Vector3 offset = position - home;
			offset.y = 0f;
			return offset.sqrMagnitude <= distance * distance;
		}

		// 받침 위에 새로 올라서면 그 무기만. 내려와도 그대로
		private void UpdatePads()
		{
			Vector3 player = playerProvider.CurrentObject.UnitMovement.Position;
			PadCell current = null;
			foreach (PadCell pad in padCells)
			{
				if (IsNear(player, pad.Position, data.PadRadius))
				{
					current = pad;
					break;
				}
			}

			if (current == null || current == standingPad)
			{
				standingPad = current;
				return;
			}
			standingPad = current;
			DevCombatTools.EquipOnly(current.Weapon);
			Debug.Log($"[{nameof(DevShowcase)}] 시험대 무기: {(current.Weapon == null ? data.BareHandsLabel : current.Weapon.Name)}");
		}

		public void Release()
		{
			// 끄면 임대가 두뇌, 자동 시전, 불사를 되돌림. 다른 곳에서 쓰는 몸은 끄지 않음
			foreach (MonsterCell cell in monsterCells)
				if (IsOurs(cell))
					cell.Object.gameObject.SetActive(false);

			if (groundGenerator != null && groundGenerator.Breakables != null)
				foreach (BreakableCell cell in breakableCells)
					groundGenerator.Breakables.ReturnFixed(cell.Object);

			foreach (DropCell cell in dropCells)
				if (cell.Object != null && cell.Object.activeInHierarchy && IsNear(cell.Object.transform.position, cell.Position, data.HomeLeash))
					cell.Object.SetActive(false);

			monsterCells.Clear();
			breakableCells.Clear();
			dropCells.Clear();
			padCells.Clear();
			labels.Clear();
			standingPad = null;
			groundGenerator = null;
			GroundGenerator.SuppressScatter = false;
			if (root != null)
				Object.Destroy(root);
			root = null;
		}
	}
}
