using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 마을에 사는 것들. 온 주민, 일하는 인형, 동물
	public sealed partial class VillageStage
	{
		[Header("Life")]
		[SerializeField] private float personHeight = 1.15f;
		[SerializeField] private RanchYard ranchYard = new();

		private readonly Dictionary<string, VillageWalker> people = new();
		private readonly Dictionary<string, VillageWalker> workers = new();
		private readonly Dictionary<int, VillageWalker> animals = new();

		private Vector3 DoorOf(string id) => DoorPoint(id, 0.6f, 1.2f);

		private void RefreshLife()
		{
			RefreshPeople();
			RefreshWorkers();
			RefreshAnimals();
		}

		// 건물이 아직이면 길드 (회관) 앞에서 기다림
		private void RefreshPeople()
		{
			foreach (ResidentDef resident in Catalog.Residents)
			{
				if (Sim.HasArrived(resident.Id) == false)
					continue;
				Vector3 home = Sim.IsBuilt(resident.Building) ? DoorOf(resident.Building) : DoorPoint(HomesteadSim.HALL, 1f, -2.4f + people.Count * 0.8f);
				if (people.TryGetValue(resident.Id, out VillageWalker walker))
				{
					walker.SetHome(home);
					continue;
				}
				ResidentDef captured = resident;
				VillageWalker made = VillageWalker.Create(outdoor, home, Art.Icon(resident.Id), null, personHeight, 1.6f);
				HomesteadSpot.Attach(made.gameObject, () => captured.Name, () => Sim.CanGiftToday(captured.Id) ? "선물할 수 있음" : $"호감 {Sim.AffinityStep(captured.Id)}단", () =>
				{
					Sim.Greet(captured.Id);
					manager.Panels.Show(HomesteadPanelType.Guild, GUILD_PEOPLE);
				}, () => "말걸기");
				people.Add(resident.Id, made);
			}
		}

		private void RefreshWorkers()
		{
			foreach (JobDef job in Catalog.Jobs)
			{
				bool staffed = Sim.IsStaffed(job.Id);
				workers.TryGetValue(job.Id, out VillageWalker walker);
				if (staffed == false)
				{
					if (walker != null)
						walker.gameObject.SetActive(false);
					continue;
				}
				Doll doll = SOHelper.GetDoll(Sim.State.Jobs[job.Id].DollId);
				if (walker == null)
				{
					JobDef captured = job;
					walker = VillageWalker.Create(outdoor, WorkPlace(job), HeadOf(doll), BodyOf(doll), personHeight, 2.2f);
					HomesteadSpot.Attach(walker.gameObject, () => SOHelper.GetDoll(Sim.State.Jobs[captured.Id].DollId).Name, () => JobRole(captured), () => UseWorker(captured), () => Sim.JobReady(captured.Id) > 0 ? "거두기" : "맡기기");
					workers[job.Id] = walker;
				}
				walker.gameObject.SetActive(true);
				walker.SetSprites(HeadOf(doll), BodyOf(doll), personHeight);
			}
		}

		// 머리와 몸이 나뉜 인형은 두 장, 아니면 한 장
		private static Sprite HeadOf(Doll doll) => doll.Sprites.Count > 1 ? doll.Sprites[0] : doll.Sprite;

		private static Sprite BodyOf(Doll doll) => doll.Sprites.Count > 1 ? doll.Sprites[1] : null;

		private Vector3 WorkPlace(JobDef job) => job.Building == HomesteadSim.FARM ? farmOrigin + new Vector3(plotGap.x * 2.5f, 0f, 1.4f) : DoorOf(job.Building) + new Vector3(0.8f, 0f, -0.8f);

		private string JobRole(JobDef job)
		{
			int ready = Sim.JobReady(job.Id);
			return ready > 0 ? $"거두기 {ready}" : job.Name;
		}

		private void UseWorker(JobDef job)
		{
			if (Sim.CollectJob(job.Id) > 0)
				UiSfx.Play(UiSfxEvent.Purchase);
			else
				manager.Panels.Show(HomesteadPanelType.Guild, GUILD_JOBS);
		}

		private void RefreshAnimals()
		{
			foreach (AnimalState animal in Sim.State.Animals)
			{
				if (animals.ContainsKey(animal.Uid))
					continue;
				AnimalState captured = animal;
				(float side, float ahead) = ranchYard.Home(animals.Count);
				Vector3 home = DoorPoint(HomesteadSim.RANCH, ahead, side);
				VillageWalker walker = VillageWalker.CreateAnimal(outdoor, home, Art.FramesOf(animal.Id, animal.Uid), ranchYard.Wander);
				HomesteadSpot.Attach(walker.gameObject, () => Catalog.Get<AnimalDef>(captured.Id).Name, () => AnimalRole(captured), () => CareAnimal(captured, walker), () => Sim.IsProductReady(captured) ? "거두기" : "쓰다듬기");
				animals.Add(animal.Uid, walker);
			}
		}

		private string AnimalRole(AnimalState animal)
		{
			if (Sim.IsProductReady(animal))
				return "거두기";
			return animal.CareDay != Sim.Day ? "쓰다듬기" : $"정 {animal.Affinity}";
		}

		private void CareAnimal(AnimalState animal, VillageWalker walker)
		{
			if (Report(Sim.Care(animal.Uid, out bool collected)) == false)
				return;
			UiSfx.Play(collected ? UiSfxEvent.Purchase : UiSfxEvent.Confirm);
			walker.Hop();
		}
	}

	// 제자리 둘레를 느긋하게 거니는 그림. 인형은 머리와 몸 두 장
	public sealed class VillageWalker : MonoBehaviour
	{
		[SerializeField] private float speed = 0.7f;
		[SerializeField] private Vector2 restSeconds = new(1.5f, 5f);
		[SerializeField] private float stepHeight = 0.05f;
		[SerializeField] private float stepsPerSecond = 3f;
		[SerializeField] private float hopHeight = 0.35f;
		[SerializeField] private float hopSeconds = 0.35f;
		// 발밑 원판. 플레이어 프리팹의 Shadow (UI_FadeCircle, 흰색 알파 0.39, 키만 한 지름) 와 같은 값
		[SerializeField] private float discPerSize = 1f;
		[SerializeField] private float discAlpha = 0.39f;
		[SerializeField] private int discSortingOrder = -10;

		private HomesteadSprite head;
		private HomesteadSprite body;
		private Vector3 home;
		private Vector3 target;
		private float range;
		private float rest;
		private float hop;
		private float headBase;
		private Sprite[] frames;

		public static VillageWalker Create(Transform parent, Vector3 home, Sprite headSprite, Sprite bodySprite, float height, float range)
		{
			GameObject walkerObject = new("Walker");
			walkerObject.transform.SetParent(parent, false);
			walkerObject.transform.localPosition = home;
			VillageWalker walker = walkerObject.AddComponent<VillageWalker>();
			walker.body = HomesteadSprite.Create(walkerObject.transform, Vector3.zero, null, height);
			walker.head = HomesteadSprite.Create(walkerObject.transform, Vector3.zero, null, height);
			walker.range = range;
			walker.SetHome(home);
			walker.target = home;
			walker.rest = Random.Range(0f, walker.restSeconds.y);
			walker.SetSprites(headSprite, bodySprite, height);
			walker.AddGroundDisc(height);
			return walker;
		}

		// 배율 1 로 제 크기 (PPU 32). 걸음은 그림 장면을 끊어서
		public static VillageWalker CreateAnimal(Transform parent, Vector3 home, Sprite[] frames, float range)
		{
			VillageWalker walker = Create(parent, home, frames[0], null, frames[0].bounds.size.y, range);
			walker.frames = frames;
			walker.SetGroundDiscSize(Mathf.Max(frames[0].bounds.size.x, frames[0].bounds.size.y));
			return walker;
		}

		private SpriteRenderer disc;

		private void AddGroundDisc(float size)
		{
			Sprite discSprite = HomesteadArt.Instance.GroundDisc;
			GameObject discObject = new("Disc");
			discObject.transform.SetParent(transform, false);
			discObject.transform.localPosition = Vector3.up * 0.001f;
			discObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			SpriteRenderer discRenderer = discObject.AddComponent<SpriteRenderer>();
			discRenderer.sprite = discSprite;
			discRenderer.color = new Color(1f, 1f, 1f, discAlpha);
			discRenderer.sortingOrder = discSortingOrder;
			disc = discRenderer;
			SetGroundDiscSize(size);
		}

		private void SetGroundDiscSize(float size) => disc.transform.localScale = Vector3.one * (size * discPerSize / disc.sprite.bounds.size.x);

		public void SetHome(Vector3 newHome)
		{
			if ((newHome - home).sqrMagnitude < 0.01f)
				return;
			home = newHome;
			target = newHome;
		}

		public void SetSprites(Sprite headSprite, Sprite bodySprite, float height)
		{
			bool isDoll = bodySprite != null;
			body.gameObject.SetActive(isDoll);
			if (isDoll)
			{
				body.SetSprite(bodySprite, height * 0.42f);
				head.SetSprite(headSprite, height * 0.68f);
				headBase = height * 0.34f;
				head.Renderer.sortingOrder = 1;
			}
			else
			{
				head.SetSprite(headSprite, height);
				headBase = 0f;
			}
			if (disc != null)
				SetGroundDiscSize(height);
			Place(0f);
		}

		public void Hop() => hop = hopSeconds;

		private void Place(float lift)
		{
			head.transform.localPosition = Vector3.up * (headBase + lift);
			body.transform.localPosition = Vector3.up * lift;
		}

		private void ShowFrame(bool isWalking)
		{
			if (frames == null)
				return;
			int step = isWalking ? 1 + (int)(Time.time * stepsPerSecond) % (frames.Length - 1) : 0;
			head.Renderer.sprite = frames[step];
		}

		private void Update()
		{
			float lift = 0f;
			if (hop > 0f)
			{
				hop -= Time.deltaTime;
				lift = Mathf.Sin(Mathf.Clamp01(hop / hopSeconds) * Mathf.PI) * hopHeight;
			}
			if (rest > 0f)
			{
				rest -= Time.deltaTime;
				ShowFrame(false);
				Place(lift);
				return;
			}
			Vector3 delta = target - transform.localPosition;
			if (delta.magnitude < 0.05f)
			{
				Vector2 offset = Random.insideUnitCircle * range;
				target = home + new Vector3(offset.x, 0f, offset.y);
				rest = Random.Range(restSeconds.x, restSeconds.y);
				return;
			}
			transform.localPosition += delta.normalized * Mathf.Min(speed * Time.deltaTime, delta.magnitude);
			// 자유 카메라라 화면 기준으로 왼쪽
			Camera view = Camera.main;
			bool left = view != null && Vector3.Dot(delta, view.transform.right) < 0f;
			ShowFrame(true);
			head.Renderer.flipX = left;
			body.Renderer.flipX = left;
			Place(frames != null ? lift : lift + Mathf.Abs(Mathf.Sin(Time.time * stepsPerSecond * Mathf.PI)) * stepHeight);
		}
	}
}
