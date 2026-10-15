using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Motion.Serialization
{
    public static class PoseClipJson
    {
        private static readonly JsonSerializerSettings SETTINGS = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            MaxDepth = 32
        };

        public static PoseClip Read(string json)
        {
            if (json == null || json.Length > 32000000)
            {
                throw new ArgumentException("Invalid pose clip JSON size");
            }
            JObject root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            foreach (string field in new[] { "Format", "Version", "Id", "Duration", "Loop", "Tracks" })
            {
                if (root[field] == null)
                {
                    throw new ArgumentException("Missing pose clip field: " + field);
                }
            }
            PoseClip clip = JsonConvert.DeserializeObject<PoseClip>(json, SETTINGS);
            new PoseClipSampler(clip);
            return clip;
        }

        public static string Write(PoseClip clip)
        {
            new PoseClipSampler(clip);
            return JsonConvert.SerializeObject(clip, Formatting.Indented, SETTINGS);
        }
    }
}
