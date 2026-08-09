using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    /// <summary>
    /// DESIGN.md's telegraph grammar says "same colour = same threat type". These pin the parts of
    /// that rule a compiler cannot: that no two threat classes resolve to the same hue, and that
    /// the twenty attack assets authored before the palette existed are not repainted by it.
    /// </summary>
    public class TelegraphPaletteTests
    {
        static TelegraphPalette Make() => ScriptableObject.CreateInstance<TelegraphPalette>();

        [TearDown]
        public void TearDown() { }

        [Test]
        public void CustomKeepsTheAuthoredColour()
        {
            TelegraphPalette palette = Make();
            var authored = new Color(0.11f, 0.22f, 0.33f, 1f);

            // Every attack that shipped before the palette defaults to Custom. If this resolved
            // through the table instead, the whole existing game would silently change colour.
            Assert.AreEqual(authored, palette.Resolve(TelegraphChannel.Custom, authored));

            Object.DestroyImmediate(palette);
        }

        [Test]
        public void ANamedChannelIgnoresTheAuthoredColour()
        {
            TelegraphPalette palette = Make();
            var authored = new Color(0.11f, 0.22f, 0.33f, 1f);

            Assert.AreNotEqual(authored, palette.Resolve(TelegraphChannel.MeleeArc, authored));

            Object.DestroyImmediate(palette);
        }

        [Test]
        public void EveryThreatClassHasItsOwnHue()
        {
            TelegraphPalette palette = Make();

            var channels = new[]
            {
                TelegraphChannel.MeleeArc,
                TelegraphChannel.Projectile,
                TelegraphChannel.GapCloser,
                TelegraphChannel.GroundHazard,
                TelegraphChannel.HardenedTime,
                TelegraphChannel.Echo,
            };

            for (int i = 0; i < channels.Length; i++)
            {
                for (int j = i + 1; j < channels.Length; j++)
                {
                    Color a = palette.Resolve(channels[i], Color.black);
                    Color b = palette.Resolve(channels[j], Color.black);

                    Assert.Greater(ColorDistance(a, b), 0.2f,
                        $"{channels[i]} and {channels[j]} are too close to tell apart at a glance");
                }
            }

            Object.DestroyImmediate(palette);
        }

        [Test]
        public void ProjectileIsNoLongerAmber()
        {
            TelegraphPalette palette = Make();

            Color projectile = palette.Resolve(TelegraphChannel.Projectile, Color.black);
            Color amber = palette.Resolve(TelegraphChannel.HardenedTime, Color.black);

            // The 2026-08-09 call. ENEMIES_BIOME1.md § 1 reserves amber for solidified time across
            // the whole game, so an incoming glob and the plate that stops it cannot share a hue.
            Assert.Greater(ColorDistance(projectile, amber), 0.4f);

            Object.DestroyImmediate(palette);
        }

        [Test]
        public void TheEchoIsDistinctFromTheLockedDashHue()
        {
            TelegraphPalette palette = Make();

            Color echo = palette.Resolve(TelegraphChannel.Echo, Color.black);
            var dash = new Color(0.29f, 0.85f, 0.92f);

            // The dash blue is the Second Hand's own colour, locked 2026-08-08. An enemy echo
            // wearing it exactly would put the player's signature hue on the thing killing them.
            Assert.Greater(ColorDistance(echo, dash), 0.2f);

            Object.DestroyImmediate(palette);
        }

        [Test]
        public void ANullPaletteFallsBackRatherThanDrawingBlack()
        {
            var authored = new Color(0.9f, 0.1f, 0.1f, 1f);

            // A prefab with the palette left unassigned must still telegraph. Drawing every
            // wind-up black would be worse than the wiring mistake it came from.
            Assert.AreEqual(authored, TelegraphPalette.Resolve(null, TelegraphChannel.MeleeArc, authored));
        }

        static float ColorDistance(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
