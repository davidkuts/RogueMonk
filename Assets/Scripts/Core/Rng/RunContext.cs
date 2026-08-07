using Game.Core.Diagnostics;

namespace Game.Core.Rng
{
    /// <summary>
    /// Owns the single RNG for a run. Room order, spawn population and later boon offers all
    /// draw from here, so quoting the seed is enough to reproduce a run exactly (DESIGN.md
    /// § RNG &amp; death flow). The seed is logged the moment a run starts.
    /// </summary>
    public sealed class RunContext
    {
        public RunContext(uint seed)
        {
            Seed = seed;
            Random = new XorShiftRandom(seed);
            GameLog.Info(LogCategory.Level, $"RUN START  seed {seed}");
        }

        public uint Seed { get; }

        /// <summary>The run's only randomness. Never call UnityEngine.Random in gameplay.</summary>
        public IRandomSource Random { get; }

        // --- Run stats, for the death screen in M7 ---

        public float ElapsedSeconds { get; private set; }
        public int RoomsCleared { get; private set; }
        public int LevelsCleared { get; private set; }
        public float DamageDealt { get; private set; }
        public float DamageTaken { get; private set; }
        public int PerfectDodges { get; private set; }
        public int EnemiesKilled { get; private set; }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f)
                ElapsedSeconds += deltaTime;
        }

        public void RecordRoomCleared() => RoomsCleared++;
        public void RecordLevelCleared() => LevelsCleared++;
        public void RecordDamageDealt(float amount) => DamageDealt += amount;
        public void RecordDamageTaken(float amount) => DamageTaken += amount;
        public void RecordPerfectDodge() => PerfectDodges++;
        public void RecordKill() => EnemiesKilled++;

        /// <summary>Derives a fresh seed for the next run from this run's stream.</summary>
        public uint NextRunSeed() => NextDerivedSeed();

        /// <summary>
        /// A fresh seeded stream for a subsystem whose number of draws depends on how the run is
        /// played — boss move selection, for instance, draws once per attack it manages to throw.
        ///
        /// Such a subsystem must never draw from <see cref="Random"/> directly: the draw count
        /// would vary with player skill and frame timing, so everything that drew afterwards
        /// would land on different values and the seed would stop reproducing the run. Deriving
        /// costs exactly one draw at a deterministic point instead.
        /// </summary>
        public IRandomSource DeriveStream() => new XorShiftRandom(NextDerivedSeed());

        uint NextDerivedSeed() => ((XorShiftRandom)Random).NextUInt();
    }
}
