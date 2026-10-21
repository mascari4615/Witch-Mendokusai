using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wardrobe.Serialization
{
    public static class WardrobeJson
    {
        public static string Write(WardrobeDocument document)
        {
            document.Validate();
            return JsonConvert.SerializeObject(document, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 40 });
        }

        public static WardrobeDocument Read(string json)
        {
            if (json == null || json.Length > 64000000) throw new ArgumentException("Invalid wardrobe JSON size");
            JObject root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            foreach (string field in new[] { "Format", "Version", "Id", "Slots", "Parts", "Outfits" })
                if (root[field] == null) throw new ArgumentException("Missing wardrobe field: " + field);
            WardrobeDocument document = JsonConvert.DeserializeObject<WardrobeDocument>(json, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 40
            });
            document.Validate();
            return document;
        }
    }
}
