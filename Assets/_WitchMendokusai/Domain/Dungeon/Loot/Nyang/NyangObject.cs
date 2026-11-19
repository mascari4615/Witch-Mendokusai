using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 줍는 냥 동전. 닿으면 끌려와 냥에 더함
	public class NyangObject : LootObject
	{
		[SerializeField] private Transform coinTransform;
		[SerializeField, Tooltip("초당 회전 각도")] private float spinSpeed = 220f;
		[SerializeField, Tooltip("위아래 흔들림 높이")] private float bobHeight = .08f;
		[SerializeField, Tooltip("초당 위아래 흔들림 횟수")] private float bobFrequency = 1.2f;
		[SerializeField] private UiSfxEvent pickupSfx = UiSfxEvent.Purchase;

		private int amount;
		private float phase;
		private DataManager dataManager;

		[Inject]
		public void Construct(DataManager dataManager)
		{
			this.dataManager = dataManager;
		}

		public void Init(int nyang)
		{
			amount = nyang;
			phase = Random.value;
		}

		private void Update()
		{
			float time = Time.time + phase;
			coinTransform.localRotation = Quaternion.Euler(90f, time * spinSpeed, 0f);
			coinTransform.localPosition = Vector3.up * Mathf.Sin(time * bobFrequency * Mathf.PI * 2f) * bobHeight;
		}

		protected override void OnEffect()
		{
			SurvivalFeel feel = SurvivalFeel.Instance;
			if (feel == null || feel.TakeSound(nameof(NyangObject)))
				UiSfx.Play(pickupSfx);
			dataManager.GameStat[GameStatType.NYANG] += amount;
		}
	}
}
