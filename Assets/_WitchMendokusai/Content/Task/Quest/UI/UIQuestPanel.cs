namespace WitchMendokusai
{
	public class UIQuestPanel : UIPanel
	{
		private UIQuestGrid questGrid;

		public override bool IsFullscreen => true;

		private void Awake()
		{
			Init(null);
			gameObject.SetActive(false);
		}

		private void Start()
		{
			InputManager.Instance.RegisterInputEvent(InputEventType.QuestToggle, InputEventResponseType.Performed, Toggle);
		}

		private void OnDestroy()
		{
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.QuestToggle, InputEventResponseType.Performed, Toggle);
		}

		private void Toggle() => SetActive(gameObject.activeSelf == false);

		protected override void OnInit()
		{
			questGrid = GetComponentInChildren<UIQuestGrid>(true);
			questGrid.Init();
		}

		public override void UpdateUI()
		{
			questGrid.UpdateUI();
		}
	}
}
