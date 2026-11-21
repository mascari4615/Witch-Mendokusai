using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 쿨마다, 그리고 레벨이 오르는 순간 즉시 주변에 파동. 레벨업 때는 쿨과 무관하게 발동하고 쿨을 다시 시작
	public class EquipmentLevelUpPulse : SkillComponent
	{
		[SerializeField] private GameObject pulsePrefab;
		[SerializeField, Min(0.1f)] private float cooldown = 45f;
		[SerializeField, Min(0.1f)] private float pulseScale = 2.2f;
		[SerializeField, Range(0f, 1f)] private float cooldownReductionPerStack = 0.15f;
		[SerializeField, Range(0.01f, 1f)] private float minCooldownRatio = 0.25f;
		[SerializeField] private UnitStatType damageBonusStat = UnitStatType.NONE;

		private SkillObject owner;
		private LevelUpPulseState state;
		private GameEventManager subscribedEvents;
		private int lastLevelUpFrame = -1;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;
		private GameEventManager gameEventManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager, GameEventManager gameEventManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
			this.gameEventManager = gameEventManager;
		}

		public override void InitContext(SkillObject skillObject)
		{
			owner = skillObject;
			state = new LevelUpPulseState(cooldown);
		}

		private void OnEnable()
		{
			state ??= new LevelUpPulseState(cooldown);
			subscribedEvents = gameEventManager;
			subscribedEvents.RegisterCallback(GameEventType.OnLevelUp, HandleLevelUp);
		}

		private void OnDisable()
		{
			subscribedEvents?.UnregisterCallback(GameEventType.OnLevelUp, HandleLevelUp);
			subscribedEvents = null;
		}

		private void Update()
		{
			transform.position = playerProvider.Current.transform.position;
			if (state.Tick(Time.deltaTime * CompanionTargeting.CooldownRate(playerProvider.Current.UnitStat, cooldownReductionPerStack, minCooldownRatio)))
				Pulse();
		}

		private void HandleLevelUp()
		{
			// 한 프레임에 여러 레벨이 오르면 한 번만
			if (lastLevelUpFrame == Time.frameCount)
				return;
			lastLevelUpFrame = Time.frameCount;
			if (state.OnLevelUp())
				Pulse();
		}

		private void Pulse()
		{
			GameObject pulse = objectPoolManager.Spawn(pulsePrefab, playerProvider.Current.transform.position, Quaternion.identity);
			if (pulse.TryGetComponent(out SkillObject skillObject))
				skillObject.InitAttack(owner.Context);
			pulse.transform.localScale *= pulseScale;
			DamagingObject damagingObject = pulse.GetComponentInChildren<DamagingObject>(true);
			if (damagingObject != null)
				damagingObject.SetDamageBonus(playerProvider.Current.UnitStat[damageBonusStat]);
			pulse.SetActive(true);
		}
	}
}
