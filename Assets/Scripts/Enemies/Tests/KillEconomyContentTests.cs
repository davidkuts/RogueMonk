using NUnit.Framework;
using UnityEditor;

namespace Game.Enemies.Tests
{
    /// <summary>
    /// The proportionality rule on the REAL enemy assets (human call 2026-08-11): a pair of
    /// Swiftjaws is the baseline, one Cerashorn equals that pair, a six-bird swarm never
    /// out-pays it, elites pay more than any trash wave, the boss most of all. Tuning may move
    /// the numbers; these ratios are the design and a retune that breaks one should have to
    /// say so here.
    /// </summary>
    public class KillEconomyContentTests
    {
        static EnemyDefinition Load(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                $"Assets/Settings/Data/Enemies/{name}.asset");
            Assert.That(asset, Is.Not.Null, $"enemy asset '{name}' missing");
            return asset;
        }

        [Test]
        public void ARaptorPairEqualsOneCerashorn()
        {
            EnemyDefinition swiftjaw = Load("Swiftjaw");
            EnemyDefinition cerashorn = Load("Cerashorn");

            Assert.That(cerashorn.SecondsOnKill, Is.EqualTo(swiftjaw.SecondsOnKill * 2),
                "one triceratops pays what the raptor pair it replaced would have");
            Assert.That(cerashorn.MinutesOnKill, Is.EqualTo(swiftjaw.MinutesOnKill * 2));
        }

        [Test]
        public void ASixBirdSwarmNeverOutPaysTheBaselinePair()
        {
            EnemyDefinition scrapfeather = Load("Scrapfeather");
            EnemyDefinition swiftjaw = Load("Swiftjaw");

            Assert.That(scrapfeather.SecondsOnKill * 6, Is.LessThanOrEqualTo(swiftjaw.SecondsOnKill * 2),
                "six birds are easier than two raptors and must not pay more");
            Assert.That(scrapfeather.MinutesOnKill * 6, Is.LessThanOrEqualTo(swiftjaw.MinutesOnKill * 2));
        }

        [Test]
        public void ElitesPayMoreThanTrashAndTheBossPaysMost()
        {
            EnemyDefinition cerashorn = Load("Cerashorn");
            EnemyDefinition ambershell = Load("Ambershell");
            EnemyDefinition twiceStruck = Load("TwiceStruck");
            EnemyDefinition tyrant = Load("Boss_Tyrant");

            Assert.That(ambershell.SecondsOnKill, Is.GreaterThan(cerashorn.SecondsOnKill));
            Assert.That(twiceStruck.SecondsOnKill, Is.GreaterThan(cerashorn.SecondsOnKill));
            Assert.That(tyrant.SecondsOnKill, Is.GreaterThan(ambershell.SecondsOnKill));
            Assert.That(tyrant.SecondsOnKill, Is.GreaterThan(twiceStruck.SecondsOnKill));
            Assert.That(tyrant.MinutesOnKill, Is.GreaterThan(ambershell.MinutesOnKill));
        }
    }
}
