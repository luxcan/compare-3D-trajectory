using Compare3DTrajectory.Core.Comparison;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>An issue to mark in the scene, numbered as in the issues table.</summary>
public sealed record SceneIssue(int Number, TrajectoryIssue Issue, bool IsSelected);
