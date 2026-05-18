using System.Text.Json;
using System.Text.Json.Serialization;

namespace WitchMendokusai
{
	public static class JsonUgcSerializer
	{
		private static readonly JsonSerializerOptions DefaultOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			WriteIndented = true,
			IncludeFields = true,
			Converters = { new JsonStringEnumConverter() }
		};

		public static string SerializeItem(ItemInfoSaveData itemData)
		{
			return JsonSerializer.Serialize(itemData, DefaultOptions);
		}

		public static ItemInfoSaveData DeserializeItem(string json)
		{
			if (string.IsNullOrWhiteSpace(json) == false)
			{
				return JsonSerializer.Deserialize<ItemInfoSaveData>(json, DefaultOptions);
			}
			return default;
		}

		public static string SerializeSeedItem(SeedItemInfoSaveData seedItemData)
		{
			return JsonSerializer.Serialize(seedItemData, DefaultOptions);
		}

		public static SeedItemInfoSaveData DeserializeSeedItem(string json)
		{
			if (string.IsNullOrWhiteSpace(json) == false)
			{
				return JsonSerializer.Deserialize<SeedItemInfoSaveData>(json, DefaultOptions);
			}
			return default;
		}
	}
}
