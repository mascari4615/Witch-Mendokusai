using System;
using UnityEngine;
using System.Text;
using VContainer;
using VContainer.Unity;

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
		// [SerializeField] private float tolerance = 1.0f;

		public bool IsAlive => Health.IsAlive;

		protected TimeManager timeManager;
		protected UnitStatCalculator unitStatCalculator;

		[Inject]
		public void Construct(TimeManager timeManager, UnitStatCalculator unitStatCalculator)
		{
			this.timeManager = timeManager;
			this.unitStatCalculator = unitStatCalculator;
		}

		protected virtual void Awake()
		{
			LifetimeScope.Find<SceneLifetimeScope>()?.Container.Inject(this);
			SpriteRenderer.material.SetFloat("_Emission", 0);
			BindComponents();

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

			if (SkillHandler != null)
			{
				timeManager.RemoveCallback(SkillHandler.Tick);
			}
			SkillHandler = new(this);
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