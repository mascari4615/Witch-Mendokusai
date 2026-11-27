using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class StageManager : MonoBehaviour
	{
		public static StageManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out StageManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		public static event Action<Stage, StageObject> OnStageChanged = delegate { };

		[field: SerializeField] public Stage StartStage { get; private set; } = null;
		public Stage LastStage { get; private set; } = null;
		public Stage CurStage { get; private set; } = null;
		public StageObject CurStageObject { get; private set; } = null;
		public StageStrategy CurStageStrategy { get; private set; } = null;

		private Vector3 lastPosDiff;

		// 스테이지에 들어선 자리. 스테이지 기준 상대 위치, 스테이지 이동에도 유지
		private Vector3 entryOffset;
		[Tooltip("진입 자리보다 이만큼 아래로 떨어지면 진입 자리로 되돌림 (m). 0 이하면 끔")]
		[SerializeField] private float fallRecoverDepth = 30f;

		private PlayerProvider playerProvider;
		// 스테이지가 정한 떠도는 입자 (바깥 먼지). 플레이어 자리를 따라감
		private readonly StageAmbience ambience = new();
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
			ambience.Clear();
			// 스테이지 물체는 풀 (씬 밖, DontDestroyOnLoad) 소속. 월드가 내려갈 때 끄지 않으면 로비에 남고 다시 들어오면 둘이 됨
			if (CurStageObject != null)
				CurStageObject.gameObject.SetActive(false);
		}

		private void Start()
		{
			LoadStage(StartStage);
		}

		private void Update()
		{
			if (fallRecoverDepth <= 0f || CurStageObject == null || playerProvider.HasObject == false)
				return;
			if (playerProvider.CurrentObject.UnitMovement.Position.y < EntryPosition.y - fallRecoverDepth)
			{
				Debug.Log($"[{nameof(StageManager)}] {CurStage.name} 아래로 떨어져 진입 자리로 되돌림");
				ReturnToEntry();
			}
		}

		private void LateUpdate()
		{
			ambience.Follow(playerProvider.HasObject ? playerProvider.CurrentObject.transform : null);
		}

		public Vector3 EntryPosition => CurStageObject.transform.position + entryOffset;

		// 끼임이나 낙하 때 지금 스테이지의 진입 자리로 되돌림
		public void ReturnToEntry() => playerProvider.CurrentObject.UnitMovement.Teleport(EntryPosition);

		public void LoadStage(Stage stage, int spawnPortalIndex = -1, bool isBackToLastStage = false)
		{
			// 릴리스에서는 개발 전용 스테이지로 가지 않음. 포털은 스스로 꺼지고 (Portal.OnEnable), 여기는 포털 밖 경로의 그물
			if (stage.DevOnly && Debug.isDebugBuild == false)
			{
				Debug.LogWarning($"[{nameof(StageManager)}] 개발 전용 스테이지 {stage.name} 는 릴리스에서 열지 않음");
				return;
			}

			// 플레이어의 위치는 그대로, 이동할 스테이지가 플레이어 위치에 생성됨

			// isBackToLastStage: 어떠한 이유로 포탈 타기 전 마지막 위치로 돌아간다는 뜻.
			// i.e. 던전에서 나갈 때 던전을 진입했던 위치로 돌아감 (이렇게 포탈을 타는 것이 아닌, 특정한 이유로 원래 스테이지로 돌아가는 경우)
			// i.e. 포탈을 타고 A -> B 스테이지로 왔다가 다시 B -> A로 돌아가는 경우는 해당 사항이 아님.

			// 주석 참고:
			// 함수 시작: 이전 스테이지 Z -> 현재 스테이지 A -> 이동할 스테이지 B
			// 함수 종료: 이전 스테이지 A -> 현재 스테이지 B (스테이지 Z는 사용되지 않음.)

			Vector3 curStagePos = CurStageObject != null ? CurStageObject.gameObject.transform.position : Vector3.zero;
			Vector3 playerPosition = playerProvider.CurrentObject.UnitMovement.Position;
			Vector3 newLastPosDiff = playerPosition - curStagePos;

			// LastStage를 A로 갱신하고, 비활성화
			if (CurStageObject != null)
				CurStageObject.gameObject.SetActive(false);
			LastStage = CurStage;

			// 새로운 스테이지 B 생성 (비활성화 상태)
			GameObject targetStage = stage.Prefab.gameObject;
			CurStageObject = objectPoolManager.Spawn(targetStage).GetComponent<StageObject>();

			// 새로운 스테이지 B 위치 변환
			// TODO:
			// Vector3 portalTPPos = stage.Prefab.Portals[spawnPortalIndex].TpPos.position;
			Vector3 newStagePos;
			{
				if (isBackToLastStage == true)
				{
					newStagePos = playerPosition - lastPosDiff;
				}
				else
				{
					Portal[] portals = stage.Prefab.Portals;
					if (portals.Length == 0)
					{
						newStagePos = playerPosition;
					}
					else
					{
						Portal targetPortal = stage.Prefab.Portals.FirstOrDefault(p => p.TargetStage == LastStage);
						if (targetPortal == null)
							targetPortal = stage.Prefab.Portals[0]; // 임의로 첫 번째 포탈 선택 - KarmoDDrine 2026-01-01

						Vector3 portalTPPos = targetPortal.TpPos.position;
						newStagePos = playerPosition - portalTPPos;
					}
				}
			}
			CurStageObject.transform.position = newStagePos;

			// 새로운 스테이지 B 활성화, CurStage를 B로 갱신
			CurStageObject.gameObject.SetActive(true);

			CurStage = stage;
			CurStageStrategy = StageStrategyFactory.Create(stage);
			CurStageStrategy.Init(stage);
			playerProvider.CurrentObject.UnitMovement.Teleport(playerPosition);
			entryOffset = playerPosition - newStagePos;
			ambience.Apply(CurStage);

			OnStageChanged(CurStage, CurStageObject);

			// 마지막 이동 위치 갱신 (B -> A로 다시 되돌아가는 경우)
			lastPosDiff = newLastPosDiff;
		}
	}
}
