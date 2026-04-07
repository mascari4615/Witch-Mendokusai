using UnityEngine;

namespace WitchMendokusai
{
	public class UGCDevSampleRunner : MonoBehaviour
	{
		[SerializeField] private bool loadOnStart = true;
		[SerializeField] private bool ignoreConditionsInDev = false;
		[SerializeField] private string manifestFileName = "wm_jump_001.manifest.json";
		[SerializeField] private string triggerFileName = "wm_jump_001.triggers.json";

		private readonly UGCRuntimeSession session = new();

		private void Start()
		{
			if (loadOnStart)
				LoadSamples();
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.F6))
				RunOpenGate();
			if (Input.GetKeyDown(KeyCode.F7))
				RunStartPlatform();
			if (Input.GetKeyDown(KeyCode.F8))
				RunCheckpoint();
		}

		public void Setup(string manifest, string triggers, bool ignoreConditions)
		{
			manifestFileName = manifest;
			triggerFileName = triggers;
			ignoreConditionsInDev = ignoreConditions;
		}

		[ContextMenu("UGC/Load Samples")]
		public void LoadSamples()
		{
			if (session.TryLoadSamples(manifestFileName, triggerFileName, out string error))
			{
				UGCLog.Info($"Samples loaded. mapId={session.Manifest?.mapId}, triggers={session.TriggerMap.Count}");
			}
			else
			{
				UGCLog.Error($"Sample load failed: {error}");
			}
		}

		[ContextMenu("UGC/Run evt_open_gate_001")]
		public void RunOpenGate()
		{
			RunTrigger("evt_open_gate_001");
		}

		[ContextMenu("UGC/Run evt_start_platform_001")]
		public void RunStartPlatform()
		{
			RunTrigger("evt_start_platform_001");
		}

		[ContextMenu("UGC/Run evt_checkpoint_003")]
		public void RunCheckpoint()
		{
			RunTrigger("evt_checkpoint_003");
		}

		public bool RunTrigger(string triggerId)
		{
			if (session.TryExecuteTrigger(triggerId, ignoreConditionsInDev, out string error))
			{
				UGCLog.Info($"Trigger executed: {triggerId}");
				return true;
			}

			UGCLog.Warn($"Trigger execution skipped: {triggerId}, reason={error}");
			return false;
		}
	}
}
