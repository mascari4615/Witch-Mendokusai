using NUnit.Framework;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-410 — 땅이 밭이 되는 규칙. 블록의 <b>영구 이름</b>으로만 판정한다(RuntimeId 는 부팅마다 달라진다).
	/// 무엇이 갈리는지는 데이터라 모드·바이옴이 새 흙을 더할 수 있다(6 동기 4·5).
	/// </summary>
	public sealed class FarmGroundTest
	{
		private const string DIRT = "wm:dirt";
		private const string GRASS = "wm:grass";
		private const string STONE = "wm:stone";
		private const string TILLED = "wm:tilled_soil";

		private static FarmGround Ground() => new(TILLED, new[] { DIRT, GRASS });

		[Test]
		public void OnlySoftGround_CanBeTilled()
		{
			FarmGround ground = Ground();

			Assert.That(ground.CanTill(DIRT), Is.True);
			Assert.That(ground.CanTill(GRASS), Is.True);
			Assert.That(ground.CanTill(STONE), Is.False, "돌은 안 갈린다");
			Assert.That(ground.CanTill(null), Is.False, "블록이 없으면(허공) 안 갈린다");
		}

		[Test]
		public void TilledGround_IsNotTilledAgain()
		{
			// 이미 밭인 자리를 또 갈면 시간·기운만 날아간다 — 헛수고에 대가를 물리지 않는다.
			Assert.That(Ground().CanTill(TILLED), Is.False);
		}

		[Test]
		public void PlantingNeeds_TilledGround()
		{
			FarmGround ground = Ground();

			Assert.That(ground.CanPlantOn(TILLED), Is.True);
			Assert.That(ground.CanPlantOn(DIRT), Is.False, "안 간 땅엔 못 심는다");
			Assert.That(ground.CanPlantOn(STONE), Is.False);
		}

		[Test]
		public void ModdedSoil_WorksWithoutTouchingCore()
		{
			// 모드가 새 흙을 더해도 코어는 안 바뀐다 — 규칙이 데이터이기 때문.
			FarmGround modded = new(TILLED, new[] { DIRT, "mymod:ashen_soil" });

			Assert.That(modded.CanTill("mymod:ashen_soil"), Is.True);
			Assert.That(modded.CanTill(GRASS), Is.False, "그 모드는 잔디를 안 넣었다");
		}

		[Test]
		public void CropStands_RightAboveItsSoil()
		{
			FarmCoord soil = new(4, 62, -9);
			FarmCoord spot = FarmGround.PlantSpotAbove(soil);

			Assert.That(spot.X, Is.EqualTo(4));
			Assert.That(spot.Y, Is.EqualTo(63));
			Assert.That(spot.Z, Is.EqualTo(-9));
			Assert.That(FarmGround.SoilUnder(spot), Is.EqualTo(soil), "위·아래는 서로의 역");
		}
	}
}
