using UnityEngine;

/// <summary>
/// Resuelve los parámetros concretos de generación para un índice de nivel dado.
///
/// Responsabilidades:
/// - Derivar una semilla reproducible única por nivel a partir de la semilla base.
/// - Evaluar cada DifficultyParameterRange del perfil para el nivel pedido.
/// - Producir instancias de ResolvedTrackSettings y ResolvedContentSettings.
/// </summary>
public static class LevelProgressionResolver
{
    /// <summary>
    /// Primo usado en la derivación de semilla.
    /// Asegura distribución uniforme entre semillas de niveles consecutivos.
    /// </summary>
    private const int SeedMultiplierPrime = 1000003;

    #region Public API

    /// <summary>
    /// Resuelve los parámetros de generación de pista para el nivel indicado.
    /// </summary>
    public static ResolvedTrackSettings ResolveTrackSettings(
        TrackDifficultyProgressionProfile profile,
        int levelIndex)
    {
        if (profile == null)
        {
            Debug.LogError("[PROGRESSION] TrackDifficultyProgressionProfile es null.");
            return ResolvedTrackSettings.Default;
        }

        int resolvedSeed = DeriveSeed(profile.BaseSeed, levelIndex);

        return new ResolvedTrackSettings(
            seed:                        resolvedSeed,
            lengthMultiplier:            profile.LengthMultiplier.Evaluate(levelIndex),
            lateralChanceMultiplier:     profile.LateralChanceMultiplier.Evaluate(levelIndex),
            verticalChanceMultiplier:    profile.VerticalChanceMultiplier.Evaluate(levelIndex),
            narrowChanceMultiplier:      profile.NarrowChanceMultiplier.Evaluate(levelIndex),
            gapChanceMultiplier:         profile.GapChanceMultiplier.Evaluate(levelIndex),
            railChanceMultiplier:        profile.RailChanceMultiplier.Evaluate(levelIndex),
            safeStartLengthOverride:     profile.SafeStartLength.Evaluate(levelIndex),
            safeEndLengthOverride:       profile.SafeEndLength.Evaluate(levelIndex),
            generateStartSafeZoneBarriers: profile.AlwaysGenerateStartBarriers,
            generateEndSafeZoneBarriers:   profile.AlwaysGenerateEndBarriers,
            minTrackHeight:              profile.MinTrackHeight.Evaluate(levelIndex),
            maxTrackHeight:              profile.MaxTrackHeight.Evaluate(levelIndex)
        );
    }

    /// <summary>
    /// Resuelve los parámetros de generación de contenido para el nivel indicado.
    /// </summary>
    public static ResolvedContentSettings ResolveContentSettings(
        ContentDifficultyProgressionProfile profile,
        int levelIndex)
    {
        if (profile == null)
        {
            Debug.LogError("[PROGRESSION] ContentDifficultyProgressionProfile es null.");
            return ResolvedContentSettings.Default;
        }

        return new ResolvedContentSettings(
            enableBoxes:               profile.IsBoxesUnlocked(levelIndex),
            enableWalls:               profile.IsWallsUnlocked(levelIndex),
            enableBalls:               profile.IsBallsUnlocked(levelIndex),
            enableFans:                profile.IsFansUnlocked(levelIndex),
            enableCoins:               profile.IsCoinsUnlocked(levelIndex),
            boxSpawnChance:            profile.BoxSpawnChance.Evaluate(levelIndex),
            wallSpawnChance:           profile.WallSpawnChance.Evaluate(levelIndex),
            ballFlatSpawnChance:       profile.BallFlatSpawnChance.Evaluate(levelIndex),
            ballNarrowSpawnChance:     profile.BallNarrowSpawnChance.Evaluate(levelIndex),
            ballRailSpawnChance:       profile.BallRailSpawnChance.Evaluate(levelIndex),
            ballBeforeDownSlopeChance: profile.BallBeforeDownSlopeChance.Evaluate(levelIndex),
            fanFlatSpawnChance:        profile.FanFlatSpawnChance.Evaluate(levelIndex),
            fanStraightRailSpawnChance: profile.FanStraightRailSpawnChance.Evaluate(levelIndex),
            useRandomCoinCount:        profile.UseRandomCoinCount,
            minRandomCoinCount:        profile.MinCoinCount.EvaluateInt(levelIndex),
            maxRandomCoinCount:        profile.MaxCoinCount.EvaluateInt(levelIndex)
        );
    }

    #endregion

    #region Seed Derivation

    private static int DeriveSeed(int baseSeed, int levelIndex)
    {
        unchecked
        {
            return baseSeed ^ (levelIndex * SeedMultiplierPrime);
        }
    }

    #endregion
}