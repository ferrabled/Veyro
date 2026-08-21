namespace MotionRunner.Track
{
    /// Injected randomness. Generation code never creates a random source of its own
    /// (CLAUDE.md rule 4) - it is handed one built from a RunSeed.
    public interface IRandomSource
    {
        uint NextUInt();

        /// Uniform-ish in [0, exclusiveMax). Uses modulo, so it carries a negligible bias at
        /// the small ranges we use; reproducibility matters more here than perfect uniformity.
        int NextInt(int exclusiveMax);

        /// [0, 1)
        float NextFloat();
    }
}
