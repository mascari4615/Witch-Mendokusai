using System;

namespace SceneContent
{
    // Linear RGB tints. OutlineWidth is a fraction of half viewport height, not metres.
    [Serializable]
    public sealed class ToonSurface
    {
        public float[] ShadeTint = new[] { .8f, .86f, .98f };
        public float ShadeExtent = .5f;
        public float ShadeSoftness = .04f;
        public float ShadeRichness;
        public float LightStrength = .9f;
        public float AmbientStrength = .4f;
        public float RimStrength;
        public float RimPower = 3f;
        public float BandStrength;
        public float BandHeight = .35f;
        public float BandWidth = .12f;
        public float OutlineWidth;
        public float[] OutlineTint = new[] { .35f, .3f, .32f };

        public void Validate()
        {
            Color(ShadeTint);
            Color(OutlineTint);
            Range(ShadeExtent, 0, 1);
            Range(ShadeSoftness, .001f, .3f);
            Range(ShadeRichness, 0, 1);
            Range(LightStrength, 0, 2);
            Range(AmbientStrength, 0, 2);
            Range(RimStrength, 0, 1);
            Range(RimPower, .5f, 8);
            Range(BandStrength, 0, 1);
            Range(BandHeight, -1, 1);
            Range(BandWidth, .02f, .5f);
            Range(OutlineWidth, 0, .02f);
        }

        public ToonSurface Copy()
        {
            ToonSurface copy = (ToonSurface)MemberwiseClone();
            copy.ShadeTint = (float[])ShadeTint.Clone();
            copy.OutlineTint = (float[])OutlineTint.Clone();
            return copy;
        }

        private static void Color(float[] value)
        {
            if (value == null || value.Length != 3) throw new ArgumentException("Toon tint requires three linear RGB values");
            foreach (float channel in value) Range(channel, 0, 1);
        }

        private static void Range(float value, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentException("Toon value outside its finite range");
        }
    }
}
