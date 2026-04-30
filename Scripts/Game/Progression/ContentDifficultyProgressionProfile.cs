using UnityEngine;

/// <summary>
/// Perfil de progresión de dificultad para la generación procedural de contenido del track.
///
/// Responsabilidades:
/// - Definir los rangos progresivos de probabilidades de spawn por categoría.
/// - Controlar a partir de qué nivel se habilita cada categoría de obstáculo.
/// - Definir la progresión de monedas por nivel.
/// </summary>
[CreateAssetMenu(fileName = "ContentDifficultyProgressionProfile",
    menuName = "Game/Progression/Content Difficulty Progression Profile")]
public sealed class ContentDifficultyProgressionProfile : ScriptableObject
{
    #region Inspector

    [Header("Category Unlock Levels")]

    [Tooltip("Nivel mínimo para generar cajas. 1 = disponible desde el principio.")]
    [SerializeField, Min(1)] private int boxesUnlockLevel = 1;

    [Tooltip("Nivel mínimo para generar muros.")]
    [SerializeField, Min(1)] private int wallsUnlockLevel = 3;

    [Tooltip("Nivel mínimo para generar pelotas empujables.")]
    [SerializeField, Min(1)] private int ballsUnlockLevel = 2;

    [Tooltip("Nivel mínimo para generar ventiladores o aspas.")]
    [SerializeField, Min(1)] private int fansUnlockLevel = 5;

    [Tooltip("Nivel mínimo para generar monedas.")]
    [SerializeField, Min(1)] private int coinsUnlockLevel = 1;

    [Header("Obstacle Spawn Chances")]

    [Tooltip("Probabilidad de generar cajas en una fila válida.")]
    [SerializeField] private DifficultyParameterRange boxSpawnChance = DifficultyParameterRange.Linear(0.05f, 0.25f, 20);

    [Tooltip("Probabilidad de generar un muro en pista plana.")]
    [SerializeField] private DifficultyParameterRange wallSpawnChance = DifficultyParameterRange.Linear(0.0f, 0.08f, 25);

    [Tooltip("Probabilidad de generar pelota en pista plana.")]
    [SerializeField] private DifficultyParameterRange ballFlatSpawnChance = DifficultyParameterRange.Linear(0.02f, 0.07f, 20);

    [Tooltip("Probabilidad de generar pelota en estrechamientos.")]
    [SerializeField] private DifficultyParameterRange ballNarrowSpawnChance = DifficultyParameterRange.Linear(0.04f, 0.12f, 20);

    [Tooltip("Probabilidad de generar pelota en railes.")]
    [SerializeField] private DifficultyParameterRange ballRailSpawnChance = DifficultyParameterRange.Linear(0.01f, 0.05f, 18);

    [Tooltip("Probabilidad de generar pelota antes de una bajada.")]
    [SerializeField] private DifficultyParameterRange ballBeforeDownSlopeChance = DifficultyParameterRange.Linear(0.1f, 0.3f, 15);

    [Tooltip("Probabilidad de generar ventilador en pista plana.")]
    [SerializeField] private DifficultyParameterRange fanFlatSpawnChance = DifficultyParameterRange.Linear(0.0f, 0.04f, 20);

    [Tooltip("Probabilidad de generar ventilador en rail recto.")]
    [SerializeField] private DifficultyParameterRange fanStraightRailSpawnChance = DifficultyParameterRange.Linear(0.0f, 0.06f, 20);

    [Header("Coins")]

    [Tooltip("Cantidad mínima de monedas a generar por nivel.")]
    [SerializeField] private DifficultyParameterRange minCoinCount = DifficultyParameterRange.Linear(3f, 8f, 20);

    [Tooltip("Cantidad máxima de monedas a generar por nivel.")]
    [SerializeField] private DifficultyParameterRange maxCoinCount = DifficultyParameterRange.Linear(6f, 18f, 25);

    [Tooltip("Si está activo, la cantidad de monedas es aleatoria dentro del rango.")]
    [SerializeField] private bool useRandomCoinCount = true;

    #endregion

    #region Properties

    public DifficultyParameterRange BoxSpawnChance => boxSpawnChance;
    public DifficultyParameterRange WallSpawnChance => wallSpawnChance;
    public DifficultyParameterRange BallFlatSpawnChance => ballFlatSpawnChance;
    public DifficultyParameterRange BallNarrowSpawnChance => ballNarrowSpawnChance;
    public DifficultyParameterRange BallRailSpawnChance => ballRailSpawnChance;
    public DifficultyParameterRange BallBeforeDownSlopeChance => ballBeforeDownSlopeChance;
    public DifficultyParameterRange FanFlatSpawnChance => fanFlatSpawnChance;
    public DifficultyParameterRange FanStraightRailSpawnChance => fanStraightRailSpawnChance;
    public DifficultyParameterRange MinCoinCount => minCoinCount;
    public DifficultyParameterRange MaxCoinCount => maxCoinCount;
    public bool UseRandomCoinCount => useRandomCoinCount;

    #endregion

    #region Unlock API

    /// <summary>Indica si las cajas están desbloqueadas para el nivel dado.</summary>
    public bool IsBoxesUnlocked(int levelIndex) => levelIndex >= boxesUnlockLevel;

    /// <summary>Indica si los muros están desbloqueados para el nivel dado.</summary>
    public bool IsWallsUnlocked(int levelIndex) => levelIndex >= wallsUnlockLevel;

    /// <summary>Indica si las pelotas empujables están desbloqueadas para el nivel dado.</summary>
    public bool IsBallsUnlocked(int levelIndex) => levelIndex >= ballsUnlockLevel;

    /// <summary>Indica si los ventiladores están desbloqueados para el nivel dado.</summary>
    public bool IsFansUnlocked(int levelIndex) => levelIndex >= fansUnlockLevel;

    /// <summary>Indica si las monedas están desbloqueadas para el nivel dado.</summary>
    public bool IsCoinsUnlocked(int levelIndex) => levelIndex >= coinsUnlockLevel;

    #endregion
}