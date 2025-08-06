using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class Player : Singleton<Player>
	{
		public PlayerObject Object { get; private set; }
		public PlayerMovement Movement { get; private set; }
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
			aim = new(transform, ObjectBufferManager.GetObjects(ObjectType.Monster));
			Object = GetComponent<PlayerObject>();
			Movement = GetComponent<PlayerMovement>();
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
		}

		public void TryInteract()
		{
			if (UIManager.Instance.IsPanelOpen)
				return;

			interaction.TryInteraction();
		}

		public void TryUseSkill(int skillIndex)
		{
			if (Object.UnitStat[UnitStatType.CASTING_SKILL] > 0)
				return;

			Object.UseSkill(skillIndex);
		}

		public void SetAutoAim(bool isAutoAim)
		{
			Debug.Log($"SetAutoAim: {isAutoAim}");
			IsAutoAim = isAutoAim;
		}
	}
}