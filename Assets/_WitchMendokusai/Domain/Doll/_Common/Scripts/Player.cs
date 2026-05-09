using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class Player : Singleton<Player>
	{
		public PlayerObject Object { get; private set; }
		public PlayerRotation Rotation { get; private set; }
		[field: SerializeField] public GameObject ExpCollider { get; private set; }

		private PlayerInteraction interaction;
		private PlayerAim aim;

		public Vector3 AimDirection { get; private set; }
		public Vector3 AimPos { get; private set; }
		public bool IsAutoAim { get; private set; }
		public Transform NearestTarget { get; private set; }

		public UnitStat UnitStat => Object.UnitStat;

		protected override void Awake()
		{
			base.Awake();
			interaction = new(transform);
			aim = new(transform, ObjectBufferManager.GetObjects(ObjectType.Monster), ObjectBufferManager.GetObjects(ObjectType.ResourceNode));
			Object = GetComponent<PlayerObject>();
			Rotation = GetComponent<PlayerRotation>();

			PlayerRegistry.Instance.Register(this);
		}

		protected override void OnDestroy()
		{
			if (PlayerRegistry.TryGetExistingInstance(out PlayerRegistry registry))
				registry.Unregister(this);

			base.OnDestroy();
		}

		private void Start()
		{
			Object.Init(GetDoll(DataManager.Instance.CurDollID));
		}

		private void Update()
		{
			AimPos = aim.CalcAim(useAutoAim: IsAutoAim);
			AimDirection = aim.CalcAimDirection(useAutoAim: IsAutoAim);
			NearestTarget = aim.GetNearestTarget()?.transform;

			CalcMoveDirection();
		}

		public void TryInteract()
		{
			interaction.TryInteraction();
		}

		public void TryUseSkill(int skillIndex)
		{
			if (Object.UnitStat[UnitStatType.CASTING_SKILL] > 0)
				return;

			Object.UseSkill(skillIndex);
		}

		public void TryJump()
		{
			if (Object.UnitStat[UnitStatType.DEAD] > 0)
				return;

			if (Object.UnitStat[UnitStatType.CASTING_SKILL] > 0)
				return;

			Object.UnitMovement.TryJump();
		}

		public void StopJump()
		{
			Object.UnitMovement.StopJump();
		}

		public void SetAutoAim(bool isAutoAim)
		{
			Debug.Log($"SetAutoAim: {isAutoAim}");
			IsAutoAim = isAutoAim;
		}

		private void CalcMoveDirection()
		{
			Object.UnitMovement.SetMoveDirection(InputManager.Instance.MoveInput);
		}

		public void SetSprinting(bool isSprinting)
		{
			UnitStat[UnitStatType.IS_SPRINTING] = isSprinting ? 1 : 0;
		}

		public void SetCrouching(bool isCrouching)
		{
			UnitStat[UnitStatType.IS_CROUCHING] = isCrouching ? 1 : 0;
		}
	}
}