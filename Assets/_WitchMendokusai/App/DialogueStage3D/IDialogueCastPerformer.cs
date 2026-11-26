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
		// 화면에 보이는 몸 전체. 샷 거리 계산과 인물끼리 가림 검사에 씀
		Bounds VisualBounds { get; }
		// 샷의 몸 비율에 곱하는 값. 머리가 큰 2등신 그림은 같은 미디엄에서 몸을 더 담아야 같은 크기로 보임
		float FrameScale { get; }

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
