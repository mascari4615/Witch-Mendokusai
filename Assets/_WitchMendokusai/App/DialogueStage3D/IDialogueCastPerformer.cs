using UnityEngine;

namespace WitchMendokusai.DialogueStage3D
{
	// 대화 카메라와 몸짓이 다루는 인물 하나. 별도 무대의 배우 (DialogueCastActor) 와 월드의 인물 (DialogueWorldActor) 이 같은 모양
	public interface IDialogueCastPerformer
	{
		string ActorName { get; }
		Vector3 Feet { get; }
		Vector3 HeadPosition { get; }
		Vector3 Facing { get; }
		Transform Root { get; }

		void Speak(string lineText);
		void StopSpeaking();
		void Nod();
		void Bob();
		void PlayGesture(AnimationClip clip);
		void TurnToward(Vector3 point);

		// 대화가 끝날 때. 무대 배우는 지우고 월드 인물은 원래 애니메이션으로 되돌림
		void Release();
	}
}
