using UnityEngine;

/// <summary>
/// Perfil de progresión de dificultad para la generación procedural de contenido del track.
/// 
/// Responsabilidades:
/// - Definir los rangos progresivos de probabilidades de spawn por categoría.
/// - Controlar a partir de qué nivel se habilita cada categoría de obstáculo.
/// - Definir la progresión de monedas por nivel.
///
/// Este SO reemplaza la necesidad de crear un LevelContentGenerationSettings por cada nivel.
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

    [Tooltip("Las monedas están disponibles desde el nivel 1.")]
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

    [Tooltip("Cantidad mínima de monedas a generar (cuando se usa cantidad aleatoria).")]
    [SerializeField] private DifficultyParameterRange minCoinCount = DifficultyParameterRange.Linear(3f, 8f, 20);

    [Tooltip("Cantidad máxima de monedas a generar (cuando se usa cantidad aleatoria).")]
    [SerializeField] private DifficultyParameterRange maxCoinCount = DifficultyParameterRange.Linear(6f, 18f, 25);

    [Tooltip("Si está activo, la cantidad de monedas siempre es aleatoria dentro del rango. Si está desactivado, se usa la cantidad fija evaluada.")]
    [SerializeField] private bool useRandomCoinCount = true;

    #endregion

    #region Properties

    /// <summary>Nivel de desbloqueo de cajas.</summary>
    public int BoxesUnlockLevel => boxesUnlockLevel;

    /// <summary>Nivel de desbloqueo de muros.</summary>
    public int WallsUnlockLevel => wallsUnlockLevel;

    /// <summary>Nivel de desbloqueo de pelotas.</summary>
    public int BallsUnlockLevel => ballsUnlockLevel;

    /// <summary>Nivel de desbloqueo de ventiladores.</summary>
    public int FansUnlockLevel => fansUnlockLevel;

    /// <summary>Nivel de desbloqueo de monedas.</summary>
    public int CoinsUnlockLevel => coinsUnlockLevel;

    /// <summary>Rango de probabilidad de spawn de cajas según nivel.</summary>
    public DifficultyParameterRange BoxSpawnChance => boxSpawnChance;

    /// <summary>Rango de probabilidad de spawn de muros según nivel.</summary>
    public DifficultyParameterRange WallSpawnChance => wallSpawnChance;

    /// <summary>Rango de probabilidad de pelota en pista plana según nivel.</summary>
    public DifficultyParameterRange BallFlatSpawnChance => ballFlatSpawnChance;

    /// <summary>Rango de probabilidad de pelota en estrechamientos según nivel.</summary>
    public DifficultyParameterRange BallNarrowSpawnChance => ballNarrowSpawnChance;

    /// <summary>Rango de probabilidad de pelota en railes según nivel.</summary>
    public DifficultyParameterRange BallRailSpawnChance => ballRailSpawnChance;

    /// <summary>Rango de probabilidad de pelota antes de bajadas según nivel.</summary>
    public DifficultyParameterRange BallBeforeDownSlopeChance => ballBeforeDownSlopeChance;

    /// <summary>Rango de probabilidad de ventilador en pista plana según nivel.</summary>
    public DifficultyParameterRange FanFlatSpawnChance => fanFlatSpawnChance;

    /// <summary>Rango de probabilidad de ventilador en rail recto según nivel.</summary>
    public DifficultyParameterRange FanStraightRailSpawnChance => fanStraightRailSpawnChance;

    /// <summary>Rango de cantidad mínima de monedas según nivel.</summary>
    public DifficultyParameterRange MinCoinCount => minCoinCount;

    /// <summary>Rango de cantidad máxima de monedas según nivel.</summary>
    public DifficultyParameterRange MaxCoinCount => maxCoinCount;

    /// <summary>Si la cantidad de monedas debe ser aleatoria dentro del rango.</summary>
    public bool UseRandomCoinCount => useRandomCoinCount;

    #endregion

    #region Helpers

    /// <summary>
    /// Indica si una categoría de contenido está desbloqueada para el nivel dado.
    /// </summary>
    public bool IsCategoryUnlocked(ContentCategory category, int levelIndex)
    {
        return category switch
        {
            ContentCategory.Boxes => levelIndex >= boxesUnlockLevel,
            ContentCategory.Walls => levelIndex >= wallsUnlockLevel,
            ContentCategory.Balls => levelIndex >= ballsUnlockLevel,
            ContentCategory.Fans => levelIndex >= fansUnlockLevel,
            ContentCategory.Coins => levelIndex >= coinsUnlockLevel,
            _ => true
        };
    }

    #endregion
}

/// <summary>
/// Categorías de contenido generables sobre el track.
/// Declarada aquí porque ContentDifficultyProgressionProfile es su consumidor principal.
/// Si ya existe una versión en TrackContentGenerator, consolidarlas en un archivo compartido.
/// </summary>
public enum ContentCategory
{
    Boxes,
    Walls,
    Balls,
    Fans,
    Coins
}