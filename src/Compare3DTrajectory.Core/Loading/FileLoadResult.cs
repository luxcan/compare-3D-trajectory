namespace Compare3DTrajectory.Core.Loading;

/// <summary>Everything one file gave: a trajectory per usable table, and a failure per unusable one.</summary>
public sealed record FileLoadResult(IReadOnlyList<LoadedTrajectory> Trajectories, IReadOnlyList<LoadFailure> Failures);
