using UnityEngine;

/// <summary>
/// Resuelve los parámetros concretos de generación para un índice de nivel dado.
///
/// Responsabilidades:
/// - Derivar una semilla reproducible única por nivel a partir de la semilla base.
/// - Evaluar cada DifficultyParameterRange del perfil para el nivel pedido.
/// - Producir instancias de ResolvedTrackSettings y ResolvedContentSettings listas para usar.
///
/// Por qué aquí y no en el SO:
/// La lógica de evaluación no pertenece a los datos. Mantenerla separada permite
/// testear el resolver de forma aislada sin instanciar ScriptableObjects.
/// </summary>
public static class LevelProgressionResolver
{
    /// <summary>
    /// Constante multiplicativa usada en la derivación de semilla.
    /// El valor primo elegido asegura buena distribución entre semillas consecutivas.
    /// </summary>
    private const int SeedMultiplierPrime = 1000003;

    #region Public API

    /// <summary>
    /// Resuelve los parámetros de generación de pista para el nivel indicado.
    /// </summary>
    /// <param name="profile">Perfil de progresión de dificultad de pista.</param>
    /// <param name="levelIndex">Índice del nivel actual, base 1.</param>
    /// <returns>Configuración de generación de pista lista para usar.</returns>
    public static ResolvedTrackSettings ResolveTrackSettings(
        TrackDifficultyProgressionProfile profile,
        int levelIndex)
    {
        if (profile == null)
        {
            Debug.LogError("[PROGRESSION] TrackDifficultyProgressionProfile es null. Se devuelven valores por defecto.");
            return ResolvedTrackSettings.Default;
        }

        int resolvedSeed = DeriveSeed(profile.BaseSeed, levelIndex);

        return new ResolvedTrackSettings(
            seed: resolvedSeed,
            lengthMultiplier: profile.LengthMultiplier.Evaluate(levelIndex),
            lateralChanceMultiplier: profile.LateralChanceMultiplier.Evaluate(levelIndex),
            verticalChanceMultiplier: profile.VerticalChanceMultiplier.Evaluate(levelIndex),
            narrowChanceMultiplier: profile.NarrowChanceMultiplier.Evaluate(levelIndex),
            gapChanceMultiplier: profile.GapChanceMultiplier.Evaluate(levelIndex),
            railChanceMultiplier: profile.RailChanceMultiplier.Evaluate(levelIndex),
            safeStartLengthOverride: profile.SafeStartLength.Evaluate(levelIndex),
            safeEndLengthOverride: profile.SafeEndLength.Evaluate(levelIndex),
            generateStartSafeZoneBarriers: profile.AlwaysGenerateStartBarriers,
            generateEndSafeZoneBarriers: profile.AlwaysGenerateEndBarriers,
            minTrackHeight: profile.MinTrackHeight.Evaluate(levelIndex),
            maxTrackHeight: profile.MaxTrackHeight.Evaluate(levelIndex)
        );
    }

    /// <summary>
    /// Resuelve los parámetros de generación de contenido para el nivel indicado.
    /// </summary>
    /// <param name="profile">Perfil de progresión de dificultad de contenido.</param>
    /// <param name="levelIndex">Índice del nivel actual, base 1.</param>
    /// <returns>Configuración de generación de contenido lista para usar.</returns>
    public static ResolvedContentSettings ResolveContentSettings(
        ContentDifficultyProgressionProfile profile,
        int levelIndex)
    {
        if (profile == null)
        {
            Debug.LogError("[PROGRESSION] ContentDifficultyProgressionProfile es null. Se devuelven valores por defecto.");
            return ResolvedContentSettings.Default;
        }

        return new ResolvedContentSettings(
            enableBoxes: profile.IsCategoryUnlocked(ContentCategory.Boxes, levelIndex),
            enableWalls: profile.IsCategoryUnlocked(ContentCategory.Walls, levelIndex),
            enableBalls: profile.IsCategoryUnlocked(ContentCategory.Balls, levelIndex),
            enableFans: profile.IsCategoryUnlocked(ContentCategory.Fans, levelIndex),
            enableCoins: profile.IsCategoryUnlocked(ContentCategory.Coins, levelIndex),
            boxSpawnChance: profile.BoxSpawnChance.Evaluate(levelIndex),
            wallSpawnChance: profile.WallSpawnChance.Evaluate(levelIndex),
            ballFlatSpawnChance: profile.BallFlatSpawnChance.Evaluate(levelIndex),
            ballNarrowSpawnChance: profile.BallNarrowSpawnChance.Evaluate(levelIndex),
            ballRailSpawnChance: profile.BallRailSpawnChance.Evaluate(levelIndex),
            ballBeforeDownSlopeChance: profile.BallBeforeDownSlopeChance.Evaluate(levelIndex),
            fanFlatSpawnChance: profile.FanFlatSpawnChance.Evaluate(levelIndex),
            fanStraightRailSpawnChance: profile.FanStraightRailSpawnChance.Evaluate(levelIndex),
            useRandomCoinCount: profile.UseRandomCoinCount,
            minRandomCoinCount: profile.MinCoinCount.EvaluateInt(levelIndex),
            maxRandomCoinCount: profile.MaxCoinCount.EvaluateInt(levelIndex)
        );
    }

    #endregion

    #region Seed Derivation

    /// <summary>
    /// Deriva una semilla única y reproducible para un nivel específico.
    ///
    /// Por qué multiplicación por un primo:
    /// Evita que semillas de niveles consecutivos sean predeciblemente similares,
    /// lo cual causaría que los tracks generados se parezcan entre sí.
    /// El XOR con la baseSeed distribuye el espacio de valores de forma uniforme.
    /// </summary>
    private static int DeriveSeed(int baseSeed, int levelIndex)
    {
        unchecked
        {
            return baseSeed ^ (levelIndex * SeedMultiplierPrime);
        }
    }

    #endregion
}