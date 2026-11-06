using FMODUnity;

namespace WitchMendokusai
{
	public class ChestObject : GameItemObject
	{
		protected override void OnEffect()
		{
			RuntimeManager.PlayOneShot("event:/SFX/LevelUp", transform.position);
			EventBusBridge.Publish(new ChestPickedUpEvent { Position = transform.position });
		}
	}
}
