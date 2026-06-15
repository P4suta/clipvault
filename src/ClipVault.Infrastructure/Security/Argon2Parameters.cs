namespace ClipVault.Infrastructure.Security;

/// <summary>
/// Cost parameters for Argon2id.
/// </summary>
/// <param name="MemoryKiB">The memory cost in kibibytes.</param>
/// <param name="Iterations">The number of iterations (time cost).</param>
/// <param name="Parallelism">The degree of parallelism (number of lanes).</param>
public sealed record Argon2Parameters(int MemoryKiB, int Iterations, int Parallelism)
{
    /// <summary>
    /// Gets the production defaults (64 MiB, 3 iterations, parallelism clamped to the CPU count between 1 and 4).
    /// </summary>
    public static Argon2Parameters Secure { get; } =
        new(MemoryKiB: 64 * 1024, Iterations: 3, Parallelism: Math.Clamp(Environment.ProcessorCount, 1, 4));
}
