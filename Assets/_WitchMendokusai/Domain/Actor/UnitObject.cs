using System;
using UnityEngine;
using System.Text;

namespace WitchMendokusai
{
	[RequireComponent(typeof(Rigidbody), typeof(UnitMovement), typeof(UnitHealth))]
	public abstract class UnitObject : MonoBehaviour, IDamageSource
	{
		[field: SerializeField] public Unit UnitData { get; private set; } = null;
		public UnitStat UnitStat { get; private set; } = new();
		public SkillHandler SkillHandler { get; protected set; } = null;
		[field: SerializeField] public Transform MeshParent { get; protected set; } = null;
		[field: SerializeField] public SpriteRenderer SpriteRenderer { get; protected set; } = null;
		// public NavMeshAgent NavMeshAgent { get; protected set; } = null;
		public Rigidbody RigidBody { get; protected set; } = null;
		public UnitMovement UnitMovement { get; protected set; } = null;
		public UnitHealth Health { get; protected set; } = null;

#pragma warning disable CS0414 // NavMeshAgent 주석처리 상태 — NavMesh 재도입 시 사용
		[SerializeField] private float stoppingDistance = 0.1f;
		[SerializeField] private bool updateRotation = false;
		[SerializeField] private float acceleration = 40.0f;
#pragma warning restore CS0414

		public bool IsAlive => Health.IsAlive;

		// base-deps relay 패턴. ★ 진짜 이유 (TASK-WM-109-A, VContainer 1.17.0 source 정독):
		// "generator 가 abstract/base 의 [Inject] 를 안 만든다"는 *틀린* 모델이었다 —
		// 구체 서브클래스의 generated injector 는 base [Inject] 멤버를 *포함*한다
		// (TypeMeta.GetAllMembers 가 base 타입 walk, SymbolExtensions.cs:14). 릴레이가
		// 필요한 진짜 제약은 [Inject] *메서드는 타입당 1개*룰 (Emitter.cs:161, base+derived
		// 합산): 자식 [Inject] Construct 1개를 단일 진입점으로 쓰려면 base 에 또 다른
		// [Inject] 메서드를 둘 수 없다. (대안: base deps 를 [Inject] public/internal
		// field/property 로 노출하면 개수 제한 없어 릴레이 불요 — 이건 설계 선택.)
		// 정본: Assets/_WitchMendokusai/Domain/Application/DI/VCONTAINER-MECHANISM.md
		// 자식 concrete 클래스가 [Inject] Construct 에 base-deps 받아 SetBaseDeps 로 base 전달.
		// TASK-WM-107 Slice 4 — SkillHandler(모든 UnitObject 균일 capability)→SkillContext 가
		// objectPoolManager/playerProvider 필요 → 기존 base-deps 채널 확장(새 메커니즘 X).
		protected TimeManager timeManager;
		protected UnitStatCalculator unitStatCalculator;
		protected ObjectPoolManager objectPoolManager;
		protected PlayerProvider playerProvider;

		protected void SetBaseDeps(TimeManager timeManager, UnitStatCalculator unitStatCalculator,
			ObjectPoolManager objectPoolManager, PlayerProvider playerProvider)
		{
			this.timeManager = timeManager;
			this.unitStatCalculator = unitStatCalculator;
			this.objectPoolManager = objectPoolManager;
			this.playerProvider = playerProvider;
		}

		protected virtual void Awake()
		{
			SpriteRenderer.material.SetFloat("_Emission", 0);
			BindComponents();
		}

		protected virtual void Start()
		{
			// Init() 이 timeManager (자식 Construct → SetBaseDeps) 사용. ★ race 가 아니라
			// *결정적 순서* (TASK-WM-109-A 소스 정독): scope-build inject(scope.Awake -5000)
			// 와 pool inject(ObjectPoolManager 비활성 Instantiate 후 InjectGameObject) 가
			// 끝나는 지점이 일반 컴포넌트 Awake 보다 *경로마다 다르게* 앞/뒤. Start 는 두
			// inject 경로가 모두 끝난 뒤 결정적으로 도는 유일 지점이라 견고 (TASK-WM-078).
			if (UnitData != null)
				Init(UnitData);
		}
		
		private void BindComponents()
		{
			if (RigidBody != null && UnitMovement != null && Health != null)
				return;

			// NavMeshAgent = GetComponent<NavMeshAgent>();
			RigidBody = GetComponent<Rigidbody>();
			UnitMovement = GetComponent<UnitMovement>();
			Health = GetComponent<UnitHealth>();
			Health.Init(this);
		}

		public virtual void Init(Unit unitData)
		{
			UnitData = unitData;

			// 재-Init(예: 아레나 스폰 후 Start 자동 Init) 간 AutoCastEnabled 보존 — 안 그러면 새 SkillHandler 가
			// 디폴트 true 로 리셋돼 전술 코어(아레나)가 끈 자동시전이 부활한다(WM-165 트랩#1). 비-아레나는 항상 true 라 무변경.
			bool autoCastEnabled = SkillHandler != null ? SkillHandler.AutoCastEnabled : true;
			if (SkillHandler != null)
			{
				timeManager.RemoveCallback(SkillHandler.Tick);
			}
			SkillHandler = new(this, playerProvider, objectPoolManager);
			SkillHandler.AutoCastEnabled = autoCastEnabled;
			timeManager.RegisterCallback(SkillHandler.Tick);

			UnitStat.Set(UnitData.InitStatInfos.GetUnitStat());
			UpdateStat();

			// if (NavMeshAgent)
			// {
			// 	NavMeshAgent.stoppingDistance = stoppingDistance;
			// 	NavMeshAgent.speed = UnitStat[UnitStatType.MOVEMENT_SPEED];
			// 	// agent.destination = moveDest;
			// 	NavMeshAgent.updateRotation = updateRotation;
			// 	NavMeshAgent.acceleration = acceleration;
			// }
		}

		public void UpdateStat()
		{
			unitStatCalculator.CalcStat(UnitData, UnitStat);
		}

		public virtual bool UseSkill(int index)
		{
			return SkillHandler.UseSkill(index);
		}

		public virtual void ReceiveHeal(int healAmount)
		{
			Health.ReceiveHeal(healAmount);
		}

		public virtual void ReceiveDamage(DamageInfo damageInfo)
		{
			Health.ReceiveDamage(damageInfo);
		}

		[ContextMenu("Log All Stats")]
		private void LogAllStats()
		{
			StringBuilder sb = new();
			sb.AppendLine($"[{name}] UnitData: {(UnitData != null ? UnitData.name : "NULL")}");
			sb.AppendLine($"  UnitData Type: {UnitData?.GetType().Name ?? "N/A"}");
			sb.AppendLine($"  HP_MAX_STAT = {UnitStat[UnitStatType.HP_MAX_STAT]}");
			sb.AppendLine($"  HP_MAX = {UnitStat[UnitStatType.HP_MAX]}");
			sb.AppendLine($"  HP_CUR = {UnitStat[UnitStatType.HP_CUR]}");
			foreach (UnitStatType statType in Enum.GetValues(typeof(UnitStatType)))
			{
				int value = UnitStat[statType];
				if (value != 0)
					sb.AppendLine($"  {statType} = {value}");
			}
			Debug.Log(sb.ToString());
		}
	}
}