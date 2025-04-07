using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

namespace WitchMendokusai
{
	public abstract class UnitObject : MonoBehaviour
	{
		[field: SerializeField] public Unit UnitData { get; private set; } = null;
		public UnitStat UnitStat { get; private set; } = new();
		public SkillHandler SkillHandler { get; protected set; } = null;
		[field: SerializeField] public Transform MeshParent { get; protected set; } = null;
		[field: SerializeField] public SpriteRenderer SpriteRenderer { get; protected set; } = null;
		public NavMeshAgent NavMeshAgent { get; protected set; } = null;

		private Vector3 originScale = Vector3.zero;

		[SerializeField] private float stoppingDistance = 0.1f;
		[SerializeField] private bool updateRotation = false;
		[SerializeField] private float acceleration = 40.0f;
		// [SerializeField] private float tolerance = 1.0f;

		public bool IsAlive => UnitStat[UnitStatType.HP_CUR] > 0;

		protected virtual void Awake()
		{
			originScale = MeshParent.localScale;
			SpriteRenderer.material.SetFloat("_Emission", 0);
			NavMeshAgent = GetComponent<NavMeshAgent>();

			if (UnitData != null)
				Init(UnitData);
		}

		public virtual void Init(Unit unitData)
		{
			UnitData = unitData;

			if (SkillHandler != null)
			{
				TimeManager.Instance.RemoveCallback(SkillHandler.Tick);
			}
			SkillHandler = new(this);
			TimeManager.Instance.RegisterCallback(SkillHandler.Tick);

			UnitStat.Init(UnitData.InitStatInfos.GetUnitStat());
			UpdateStat();

			MeshParent.localScale = originScale;

			if (NavMeshAgent)
			{
				NavMeshAgent.stoppingDistance = stoppingDistance;
				NavMeshAgent.speed = UnitStat[UnitStatType.MOVEMENT_SPEED];
				// agent.destination = moveDest;
				NavMeshAgent.updateRotation = updateRotation;
				NavMeshAgent.acceleration = acceleration;
			}
		}

		public void UpdateStat()
		{
			UnitStatCalculator.Instance.CalcStat(UnitData, UnitStat);
		}

		public virtual bool UseSkill(int index)
		{
			return SkillHandler.UseSkill(index);
		}

		protected virtual void SetHp(int newHp)
		{
			UnitStat[UnitStatType.HP_CUR] = newHp;
			if (UnitStat[UnitStatType.HP_CUR] <= 0)
			{
				Die();
			}
		}

		public virtual void ReceiveDamage(DamageInfo damageInfo)
		{
			if (IsAlive == false)
			{
				return;
			}

			SetHp(Mathf.Clamp(UnitStat[UnitStatType.HP_CUR] - damageInfo.damage, 0, int.MaxValue));

			// Pivot 스케일 잠깐 키웠다가 줄이기
			MeshParent.DOScale(originScale * 1.4f, .1f).OnComplete(() =>
				MeshParent.DOScale(originScale, .2f));
		}

		protected virtual void Die()
		{
			OnDied();
		}

		protected virtual void OnDied()
		{
		}
	}
}