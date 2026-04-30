using UnityEngine;

/// <summary>
/// Parámetros concretos de generación de pista para un nivel específico.
///
/// Es el reemplazo runtime del antiguo LevelGenerationSettings ScriptableObject.
/// Los valores ya están evaluados por LevelProgressionResolver; esta clase solo los transporta.
/// Todos los consumidores existentes (TrackRuleEvaluator, TrackGeneratorController) pueden
/// acceder a los mismos datos sin modificar su lógica interna.
/// </summary>
public sealed class ResolvedTrackSettings
{
    #region Properties

    /// <summary>Semilla derivada para este nivel específico.</summary>
    public int Seed { get; }

    /// <summary>Multiplicador de longitud total del track.</summary>
    public float LengthMultiplier { get; }

    /// <summary>Multiplicador de probabilidad de giro lateral.</summary>
    public float LateralChanceMultiplier { get; }

    /// <summary>Multiplicador de probabilidad de cambio vertical.</summary>
    public float VerticalChanceMultiplier { get; }

    /// <summary>Multiplicador de probabilidad de estrechamiento.</summary>
    public float NarrowChanceMultiplier { get; }

    /// <summary>Multiplicador de probabilidad de gap.</summary>
    public float GapChanceMultiplier { get; }

    /// <summary>Multiplicador de probabilidad de sección rail.</summary>
    public float RailChanceMultiplier { get; }

    /// <summary>Longitud de zona segura inicial en metros.</summary>
    public float SafeStartLengthOverride { get; }

    /// <summary>Longitud de zona segura final en metros.</summary>
    public float SafeEndLengthOverride { get; }

    /// <summary>Si se generan barreras en la zona segura inicial.</summary>
    public bool GenerateStartSafeZoneBarriers { get; }

    /// <summary>Si se generan barreras en la zona segura final.</summary>
    public bool GenerateEndSafeZoneBarriers { get; }

    /// <summary>Altura mínima del track en metros.</summary>
    public float MinTrackHeight { get; }

    /// <summary>Altura máxima del track en metros.</summary>
    public float MaxTrackHeight { get; }

    // ── Compatibilidad con el contrato existente de LevelGenerationSettings ──

    /// <summary>
    /// La semilla siempre es fija cuando se resuelve por progresión,
    /// porque el resolver ya la derivó de forma determinista.
    /// </summary>
    public bool UseFixedSeed => true;

    /// <summary>Alias de Seed para compatibilidad con código existente.</summary>
    public int FixedSeed => Seed;

    /// <summary>
    /// La altura mínima siempre está sobreescrita porque proviene del perfil de progresión.
    /// </summary>
    public bool OverrideMinHeight => true;

    /// <summary>
    /// La altura máxima siempre está sobreescrita porque proviene del perfil de progresión.
    /// </summary>
    public bool OverrideMaxHeight => true;

    /// <summary>Alias de MinTrackHeight para compatibilidad.</summary>
    public float MinHeightOverride => MinTrackHeight;

    /// <summary>Alias de MaxTrackHeight para compatibilidad.</summary>
    public float MaxHeightOverride => MaxTrackHeight;

    #endregion

    #region Constructor

    /// <summary>
    /// Crea una configuración de track resuelta con todos los parámetros explícitos.
    /// Usada únicamente por LevelProgressionResolver.
    /// </summary>
    public ResolvedTrackSettings(
        int seed,
        float lengthMultiplier,
        float lateralChanceMultiplier,
        float verticalChanceMultiplier,
        float narrowChanceMultiplier,
        float gapChanceMultiplier,
        float railChanceMultiplier,
        float safeStartLengthOverride,
        float safeEndLengthOverride,
        bool generateStartSafeZoneBarriers,
        bool generateEndSafeZoneBarriers,
        float minTrackHeight,
        float maxTrackHeight)
    {
        Seed = seed;
        LengthMultiplier = Mathf.Max(0.1f, lengthMultiplier);
        LateralChanceMultiplier = Mathf.Max(0f, lateralChanceMultiplier);
        VerticalChanceMultiplier = Mathf.Max(0f, verticalChanceMultiplier);
        NarrowChanceMultiplier = Mathf.Max(0f, narrowChanceMultiplier);
        GapChanceMultiplier = Mathf.Max(0f, gapChanceMultiplier);
        RailChanceMultiplier = Mathf.Max(0f, railChanceMultiplier);
        SafeStartLengthOverride = Mathf.Max(0f, safeStartLengthOverride);
        SafeEndLengthOverride = Mathf.Max(0f, safeEndLengthOverride);
        GenerateStartSafeZoneBarriers = generateStartSafeZoneBarriers;
        GenerateEndSafeZoneBarriers = generateEndSafeZoneBarriers;
        MinTrackHeight = minTrackHeight;
        MaxTrackHeight = Mathf.Max(minTrackHeight, maxTrackHeight);
    }

    #endregion

    #region Default

    /// <summary>
    /// Configuración por defecto para niveles tempranos o situaciones de error.
    /// Equivale a un nivel 1 estándar sin dificultad adicional.
    /// </summary>
    public static readonly ResolvedTrackSettings Default = new ResolvedTrackSettings(
        seed: 12345,
        lengthMultiplier: 1f,
        lateralChanceMultiplier: 0.3f,
        verticalChanceMultiplier: 0.2f,
        narrowChanceMultiplier: 0f,
        gapChanceMultiplier: 0f,
        railChanceMultiplier: 0f,
        safeStartLengthOverride: 18f,
        safeEndLengthOverride: 10f,
        generateStartSafeZoneBarriers: true,
        generateEndSafeZoneBarriers: true,
        minTrackHeight: -4f,
        maxTrackHeight: 8f
    );

    #endregion
}

/// <summary>
/// Parámetros concretos de generación de contenido para un nivel específico.
///
/// Es el reemplazo runtime del antiguo LevelContentGenerationSettings ScriptableObject.
/// Todos los valores ya están evaluados; esta clase solo los transporta.
///
/// Nota: a diferencia del sistema anterior, no existe el concepto de whitelist de prefabs
/// ni overrides por prefab. En el modo infinito se usan siempre los pesos base del catálogo
/// global (TrackContentGenerationProfile), lo cual simplifica el sistema y reduce la
/// cantidad de datos a mantener.
/// </summary>
public sealed class ResolvedContentSettings
{
    #region Properties

    /// <summary>Si las cajas están habilitadas para este nivel.</summary>
    public bool EnableBoxes { get; }

    /// <summary>Si los muros están habilitados para este nivel.</summary>
    public bool EnableWalls { get; }

    /// <summary>Si las pelotas empujables están habilitadas para este nivel.</summary>
    public bool EnableBalls { get; }

    /// <summary>Si los ventiladores están habilitados para este nivel.</summary>
    public bool EnableFans { get; }

    /// <summary>Si las monedas están habilitadas para este nivel.</summary>
    public bool EnableCoins { get; }

    /// <summary>Probabilidad de spawn de caja en una fila válida.</summary>
    public float BoxSpawnChance { get; }

    /// <summary>Probabilidad de spawn de muro en pista plana.</summary>
    public float WallSpawnChance { get; }

    /// <summary>Probabilidad de pelota en pista plana.</summary>
    public float BallFlatSpawnChance { get; }

    /// <summary>Probabilidad de pelota en estrechamientos.</summary>
    public float BallNarrowSpawnChance { get; }

    /// <summary>Probabilidad de pelota en railes.</summary>
    public float BallRailSpawnChance { get; }

    /// <summary>Probabilidad de pelota antes de una bajada.</summary>
    public float BallBeforeDownSlopeChance { get; }

    /// <summary>Probabilidad de ventilador en pista plana.</summary>
    public float FanFlatSpawnChance { get; }

    /// <summary>Probabilidad de ventilador en rail recto.</summary>
    public float FanStraightRailSpawnChance { get; }

    /// <summary>Si la cantidad de monedas es aleatoria dentro del rango.</summary>
    public bool UseRandomCoinCount { get; }

    /// <summary>Cantidad mínima de monedas al usar cantidad aleatoria.</summary>
    public int MinRandomCoinCount { get; }

    /// <summary>Cantidad máxima de monedas al usar cantidad aleatoria.</summary>
    public int MaxRandomCoinCount { get; }

    #endregion

    #region Constructor

    /// <summary>
    /// Crea una configuración de contenido resuelta con todos los parámetros explícitos.
    /// Usada únicamente por LevelProgressionResolver.
    /// </summary>
    public ResolvedContentSettings(
        bool enableBoxes,
        bool enableWalls,
        bool enableBalls,
        bool enableFans,
        bool enableCoins,
        float boxSpawnChance,
        float wallSpawnChance,
        float ballFlatSpawnChance,
        float ballNarrowSpawnChance,
        float ballRailSpawnChance,
        float ballBeforeDownSlopeChance,
        float fanFlatSpawnChance,
        float fanStraightRailSpawnChance,
        bool useRandomCoinCount,
        int minRandomCoinCount,
        int maxRandomCoinCount)
    {
        EnableBoxes = enableBoxes;
        EnableWalls = enableWalls;
        EnableBalls = enableBalls;
        EnableFans = enableFans;
        EnableCoins = enableCoins;
        BoxSpawnChance = Mathf.Clamp01(boxSpawnChance);
        WallSpawnChance = Mathf.Clamp01(wallSpawnChance);
        BallFlatSpawnChance = Mathf.Clamp01(ballFlatSpawnChance);
        BallNarrowSpawnChance = Mathf.Clamp01(ballNarrowSpawnChance);
        BallRailSpawnChance = Mathf.Clamp01(ballRailSpawnChance);
        BallBeforeDownSlopeChance = Mathf.Clamp01(ballBeforeDownSlopeChance);
        FanFlatSpawnChance = Mathf.Clamp01(fanFlatSpawnChance);
        FanStraightRailSpawnChance = Mathf.Clamp01(fanStraightRailSpawnChance);
        UseRandomCoinCount = useRandomCoinCount;
        MinRandomCoinCount = Mathf.Max(0, minRandomCoinCount);
        MaxRandomCoinCount = Mathf.Max(MinRandomCoinCount, maxRandomCoinCount);
    }

    #endregion

    #region Default

    /// <summary>
    /// Configuración por defecto para nivel 1, sin obstáculos avanzados.
    /// </summary>
    public static readonly ResolvedContentSettings Default = new ResolvedContentSettings(
        enableBoxes: true,
        enableWalls: false,
        enableBalls: false,
        enableFans: false,
        enableCoins: true,
        boxSpawnChance: 0.05f,
        wallSpawnChance: 0f,
        ballFlatSpawnChance: 0f,
        ballNarrowSpawnChance: 0f,
        ballRailSpawnChance: 0f,
        ballBeforeDownSlopeChance: 0f,
        fanFlatSpawnChance: 0f,
        fanStraightRailSpawnChance: 0f,
        useRandomCoinCount: true,
        minRandomCoinCount: 3,
        maxRandomCoinCount: 6
    );

    #endregion
}