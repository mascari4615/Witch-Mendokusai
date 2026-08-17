using UnityEngine;
using WitchMendokusai.DomainSDK.Act;
using WitchMendokusai.DomainSDK.Farming;
using WitchMendokusai.Presentation;

namespace WitchMendokusai
{
	// 밭에서 일어난 일을 <b>귀에 들리게</b> 한다 (TASK-WM-410 — 손맛).
	//
	// ★ 왜 따로인가: 밭은 규칙을 판정하고, 여기는 그 결과를 감각으로 옮긴다.
	//   밭이 소리를 직접 내면 연출을 바꾸려고 규칙을 건드리게 된다(온실 이벤트 선례).
	// ★ 소리는 <b>코드로 만든다</b>(ProceduralSfx) — 세계관·톤이 아직 안 정해졌는데 음원을 사 오면
	//   그 순간 톤이 음원에 끌려간다. 지금 필요한 건 「했다」가 귀에도 들리는 것뿐이다.
	// ★ 같은 일에는 같은 음, 좋아지는 일에는 올라가는 음 — 보는 것과 듣는 것이 같은 말을 하게.
	[RequireComponent(typeof(FarmGroundObject))]
	public sealed class FarmGroundFeedback : MonoBehaviour
	{
		private const int TILL_STEP = 2;
		private const int PLANT_STEP = 4;
		private const int REFUSE_STEP = 0;
		private const float MIN_GAP_SECONDS = 0.05f;

		[Tooltip("소리를 낼까. 끄면 밭은 조용히 돈다(검증·녹화용).")]
		[SerializeField] private bool playSound = true;

		private FarmGroundObject farm;
		private ProceduralSfx sound;

		private void Awake()
		{
			farm = GetComponent<FarmGroundObject>();
		}

		private void OnEnable()
		{
			if (farm == null)
			{
				return;
			}

			farm.OnTilled += HandleTilled;
			farm.OnPlanted += HandlePlanted;
			farm.OnHarvested += HandleHarvested;
			farm.OnRefused += HandleRefused;
		}

		private void OnDisable()
		{
			if (farm == null)
			{
				return;
			}

			farm.OnTilled -= HandleTilled;
			farm.OnPlanted -= HandlePlanted;
			farm.OnHarvested -= HandleHarvested;
			farm.OnRefused -= HandleRefused;
		}

		private void HandleTilled(FarmCoord soil)
		{
			Blip(TILL_STEP);
		}

		private void HandlePlanted(FarmCoord soil, int plantDataId)
		{
			Blip(PLANT_STEP);
		}

		// 거둔 것이 「진짜(표본)」면 한 번 더 밝게 — 봐준 것만 진짜라는 규칙이 귀에도 들린다.
		private void HandleHarvested(FarmCoord soil, HarvestResult harvest)
		{
			ProceduralSfx sfx = EnsureSound();
			if (sfx == null)
			{
				return;
			}

			sfx.Good();

			if (harvest.IsSpecimen)
			{
				sfx.Sweep();
			}
		}

		// 못 한 것은 낮고 짧게 — 「고장」이 아니라 「지금은 안 된다」로 들리게.
		private void HandleRefused(FarmCoord soil, ActRejection rejection)
		{
			Blip(REFUSE_STEP);
		}

		private void Blip(int step)
		{
			EnsureSound()?.Blip(step);
		}

		// 소리는 처음 필요할 때 만든다 — 조용한 밭(playSound=false)은 오디오 자원을 아예 안 든다.
		private ProceduralSfx EnsureSound()
		{
			if (playSound == false)
			{
				return null;
			}

			sound ??= new ProceduralSfx(gameObject, minSecondsBetweenBlips: MIN_GAP_SECONDS);
			return sound;
		}
	}
}
