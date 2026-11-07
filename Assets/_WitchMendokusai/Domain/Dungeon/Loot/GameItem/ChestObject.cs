using FMODUnity;

namespace WitchMendokusai
{
	public class ChestObject : GameItemObject
	{
		protected override void OnEffect()
		{
			UiSfx.Play(UiSfxEvent.Chest);
			EventBusBridge.Publish(new ChestPickedUpEvent { Position = transform.position });
		}
	}
}
