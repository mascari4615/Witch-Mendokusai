using UnityEngine;

namespace WitchMendokusai.Presentation
{
	/// <summary>
	/// 소리 — <b>에셋 없이 코드로 만든다</b>.
	///
	/// ★ 왜 만들어 쓰나 — 세계관도 톤도 아직 안 정해졌다. 음원을 사 오면 그 순간
	///   <b>톤이 음원에 끌려간다</b>. 파형은 몇 줄이고, 나중에 진짜 음원으로 갈아 끼우기도 쉽다.
	///   지금 필요한 건 「눌렀다」·「잡았다」가 <b>귀에도 들리는</b> 것뿐이다.
	///
	/// ★ 보는 것과 듣는 것이 <b>같은 말</b>을 하게 — 부르는 쪽이 단계를 주면 음이 따라 올라간다.
	///
	/// ★ 겹침을 막는다. 초당 수십 마리가 죽는 화면이라 그대로 울리면 <b>소음</b>이 된다.
	/// </summary>
	public sealed class ProceduralSfx
	{
		/// <summary>
		/// 표본율 — <b>손잡이가 아니다</b>. 소리 규격이라 사람이 돌릴 값이 아니고,
		/// 바꾸면 음이 아니라 <b>재생 자체</b>가 어긋난다. 그래서 인스펙터에 안 낸다.
		/// </summary>
		private const int SAMPLES_PER_SECOND = 44100;

		/// <summary>같은 소리를 이 간격보다 자주 안 낸다 — 겹치면 소음이다. 부르는 쪽이 정한다.</summary>
		private readonly float minGap;

		private readonly AudioSource source;
		private readonly AudioClip[] blips = new AudioClip[9];
		private AudioClip click;
		private AudioClip chime;
		private AudioClip whoosh;
		private AudioClip tick;
		private float lastBlipAt;
		private float lastTickAt;

		public ProceduralSfx(GameObject host, float volume = 0.35f, float minSecondsBetweenBlips = 0.06f)
		{
			minGap = minSecondsBetweenBlips;

			source = host.AddComponent<AudioSource>();
			source.playOnAwake = false;
			source.spatialBlend = 0f;
			source.volume = volume;

			// 등급마다 반음씩 올라가는 짧은 소리 — 깊이 갈수록 높아진다.
			for (int tier = 0; tier < blips.Length; tier++)
			{
				float hertz = 320f * Mathf.Pow(1.06f, tier * 2f);
				blips[tier] = MakeTone("idle-blip-" + tier, hertz, 0.07f, 0.55f);
			}

			click = MakeTone("idle-click", 180f, 0.05f, 0.8f);
			chime = MakeChord("idle-chime", 440f, 0.28f);
			whoosh = MakeNoise("idle-whoosh", 0.35f);
			tick = MakeTone("idle-tick", 620f, 0.03f, 0.2f);
		}

		public bool Muted
		{
			get => source.mute;
			set => source.mute = value;
		}

		/// <summary>짧게 「띡」 — <paramref name="step"/> 이 클수록 높은 음.</summary>
		public void Blip(int step)
		{
			if (Time.unscaledTime - lastBlipAt < minGap)
			{
				return;
			}

			lastBlipAt = Time.unscaledTime;

			int index = Mathf.Clamp(step - 1, 0, blips.Length - 1);
			source.PlayOneShot(blips[index], 0.5f);
		}

		/// <summary>
		/// 아주 짧고 옅게 — <b>장단</b>. 때리는 박자를 귀로 알려준다.
		///
		/// ★ 「띡」(<see cref="Blip"/>)과 나눠 둔다 — 잡은 것과 때린 것은 다른 일이고,
		///   같은 소리로 내면 <b>둘 다 안 들린다</b>. 잦아서 크면 소음이라 아주 옅게 낸다.
		/// </summary>
		public void Tick(float minGapSeconds)
		{
			if (Time.unscaledTime - lastTickAt < minGapSeconds)
			{
				return;
			}

			lastTickAt = Time.unscaledTime;
			source.PlayOneShot(tick, 0.16f);
		}

		/// <summary>낮고 짧게 — 손끝 느낌(누름).</summary>
		public void Click()
		{
			source.PlayOneShot(click, 0.7f);
		}

		/// <summary>화음 — 「좋은 일」.</summary>
		public void Good()
		{
			source.PlayOneShot(chime, 0.6f);
		}

		/// <summary>쓸어내는 소리 — 「판이 갈렸다」.</summary>
		public void Sweep()
		{
			source.PlayOneShot(whoosh, 0.5f);
		}

		// ── 파형 ────────────────────────────────────────────────────────────

		/// <summary>
		/// 한 음. 뒤로 갈수록 잦아든다(엔벨로프) — 안 그러면 끝에서 「딱」 하고 끊겨 귀에 거슬린다.
		/// </summary>
		private static AudioClip MakeTone(string name, float hertz, float seconds, float bite)
		{
			int count = Mathf.CeilToInt(SAMPLES_PER_SECOND * seconds);
			float[] data = new float[count];

			for (int i = 0; i < count; i++)
			{
				float at = (float)i / SAMPLES_PER_SECOND;
				float fade = 1f - (float)i / count;
				float wave = Mathf.Sin(2f * Mathf.PI * hertz * at);

				// 살짝 각지게 — 순수 사인은 물러서 「눌린 느낌」이 안 난다.
				wave = Mathf.Lerp(wave, Mathf.Sign(wave), bite * 0.35f);
				data[i] = wave * fade * fade;
			}

			AudioClip clip = AudioClip.Create(name, count, 1, SAMPLES_PER_SECOND, false);
			clip.SetData(data, 0);
			return clip;
		}

		/// <summary>세 음이 같이 — 「좋은 일」 신호.</summary>
		private static AudioClip MakeChord(string name, float root, float seconds)
		{
			int count = Mathf.CeilToInt(SAMPLES_PER_SECOND * seconds);
			float[] data = new float[count];
			float[] steps = { 1f, 1.25f, 1.5f };

			for (int i = 0; i < count; i++)
			{
				float at = (float)i / SAMPLES_PER_SECOND;
				float fade = 1f - (float)i / count;
				float sum = 0f;

				for (int one = 0; one < steps.Length; one++)
				{
					sum += Mathf.Sin(2f * Mathf.PI * root * steps[one] * at);
				}

				data[i] = sum / steps.Length * fade * fade;
			}

			AudioClip clip = AudioClip.Create(name, count, 1, SAMPLES_PER_SECOND, false);
			clip.SetData(data, 0);
			return clip;
		}

		/// <summary>쓸어내는 소리 — 잡음을 점점 낮은 쪽으로.</summary>
		private static AudioClip MakeNoise(string name, float seconds)
		{
			int count = Mathf.CeilToInt(SAMPLES_PER_SECOND * seconds);
			float[] data = new float[count];
			float carried = 0f;

			for (int i = 0; i < count; i++)
			{
				float fade = 1f - (float)i / count;
				float raw = Random.Range(-1f, 1f);

				// 낮은 쪽만 남긴다 — 날것 잡음은 귀를 찌른다.
				carried = Mathf.Lerp(carried, raw, 0.06f + 0.2f * fade);
				data[i] = carried * fade;
			}

			AudioClip clip = AudioClip.Create(name, count, 1, SAMPLES_PER_SECOND, false);
			clip.SetData(data, 0);
			return clip;
		}
	}
}
