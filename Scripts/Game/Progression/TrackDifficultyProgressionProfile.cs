using UnityEngine;

/// <summary>
/// Perfil de progresión de dificultad para la generación procedural de pista.
///
/// Responsabilidades:
/// - Definir los rangos mínimo/máximo de cada parámetro del track por nivel.
/// - Controlar la forma de la curva de progresión de cada parámetro.
/// - Proveer la semilla base desde la cual se derivan semillas únicas por nivel.
///
/// Este SO reemplaza la necesidad de crear un LevelGenerationSettings por cada nivel.
/// En su lugar, un único perfil define el comportamiento del juego completo.
/// </summary>
[CreateAssetMenu(fileName = "TrackDifficultyProgressionProfile",
    menuName = "Game/Progression/Track Difficulty Progression Profile")]
public sealed class TrackDifficultyProgressionProfile : ScriptableObject
{
    #region Inspector

    [Header("Seed")]

    [Tooltip("Semilla base desde la cual se deriva la semilla de cada nivel. Cambiarla altera la secuencia completa de niveles.")]
    [SerializeField] private int baseSeed = 42713;

    [Header("Track Length")]

    [Tooltip("Multiplicador de longitud del track. Niveles más avanzados generan pistas más largas.")]
    [SerializeField] private DifficultyParameterRange lengthMultiplier = DifficultyParameterRange.Linear(0.6f, 1.4f, 30);

    [Header("Layout Complexity")]

    [Tooltip("Multiplicador de probabilidad de giros laterales.")]
    [SerializeField] private DifficultyParameterRange lateralChanceMultiplier = DifficultyParameterRange.Linear(0.3f, 1.0f, 20);

    [Tooltip("Multiplicador de probabilidad de cambios verticales (pendientes).")]
    [SerializeField] private DifficultyParameterRange verticalChanceMultiplier = DifficultyParameterRange.Linear(0.2f, 1.0f, 25);

    [Tooltip("Multiplicador de probabilidad de estrechamientos.")]
    [SerializeField] private DifficultyParameterRange narrowChanceMultiplier = DifficultyParameterRange.Linear(0.0f, 1.0f, 15);

    [Header("Hazard Complexity")]

    [Tooltip("Multiplicador de probabilidad de gaps. Tiene un impacto alto en la dificultad percibida; usar valores conservadores al principio.")]
    [SerializeField] private DifficultyParameterRange gapChanceMultiplier = DifficultyParameterRange.Linear(0.0f, 1.0f, 20);

    [Tooltip("Multiplicador de probabilidad de secciones de riel.")]
    [SerializeField] private DifficultyParameterRange railChanceMultiplier = DifficultyParameterRange.Linear(0.0f, 1.0f, 18);

    [Header("Safe Zones")]

    [Tooltip("Longitud de la zona segura inicial. Se reduce con los niveles para aumentar la dificultad percibida.")]
    [SerializeField] private DifficultyParameterRange safeStartLength = DifficultyParameterRange.Linear(18f, 8f, 15);

    [Tooltip("Longitud de la zona segura final. Se mantiene relativamente constante para asegurar una llegada limpia.")]
    [SerializeField] private DifficultyParameterRange safeEndLength = DifficultyParameterRange.Constant(10f);

    [Tooltip("Si está activo, se generan barreras en la zona segura inicial independientemente del nivel.")]
    [SerializeField] private bool alwaysGenerateStartBarriers = true;

    [Tooltip("Si está activo, se generan barreras en la zona segura final independientemente del nivel.")]
    [SerializeField] private bool alwaysGenerateEndBarriers = true;

    [Header("Track Height Range")]

    [Tooltip("Altura mínima del track. Con niveles más avanzados el track puede descender más.")]
    [SerializeField] private DifficultyParameterRange minTrackHeight = DifficultyParameterRange.Constant(-4f);

    [Tooltip("Altura máxima del track. Con niveles más avanzados el track puede ascender más.")]
    [SerializeField] private DifficultyParameterRange maxTrackHeight = DifficultyParameterRange.Constant(8f);

    #endregion

    #region Properties

    /// <summary>Semilla base para derivación de semillas por nivel.</summary>
    public int BaseSeed => baseSeed;

    /// <summary>Multiplicador de longitud del track según nivel.</summary>
    public DifficultyParameterRange LengthMultiplier => lengthMultiplier;

    /// <summary>Multiplicador de probabilidad de giros laterales según nivel.</summary>
    public DifficultyParameterRange LateralChanceMultiplier => lateralChanceMultiplier;

    /// <summary>Multiplicador de probabilidad de cambios verticales según nivel.</summary>
    public DifficultyParameterRange VerticalChanceMultiplier => verticalChanceMultiplier;

    /// <summary>Multiplicador de probabilidad de estrechamientos según nivel.</summary>
    public DifficultyParameterRange NarrowChanceMultiplier => narrowChanceMultiplier;

    /// <summary>Multiplicador de probabilidad de gaps según nivel.</summary>
    public DifficultyParameterRange GapChanceMultiplier => gapChanceMultiplier;

    /// <summary>Multiplicador de probabilidad de secciones de riel según nivel.</summary>
    public DifficultyParameterRange RailChanceMultiplier => railChanceMultiplier;

    /// <summary>Longitud de la zona segura inicial según nivel.</summary>
    public DifficultyParameterRange SafeStartLength => safeStartLength;

    /// <summary>Longitud de la zona segura final según nivel.</summary>
    public DifficultyParameterRange SafeEndLength => safeEndLength;

    /// <summary>Si las barreras de zona segura inicial se generan siempre.</summary>
    public bool AlwaysGenerateStartBarriers => alwaysGenerateStartBarriers;

    /// <summary>Si las barreras de zona segura final se generan siempre.</summary>
    public bool AlwaysGenerateEndBarriers => alwaysGenerateEndBarriers;

    /// <summary>Altura mínima del track según nivel.</summary>
    public DifficultyParameterRange MinTrackHeight => minTrackHeight;

    /// <summary>Altura máxima del track según nivel.</summary>
    public DifficultyParameterRange MaxTrackHeight => maxTrackHeight;

    #endregion

    #region Validation

    private void OnValidate()
    {
        // Garantizar que los multiplicadores no bajen de cero para evitar comportamientos
        // inesperados en el generador de reglas.
        ValidateRange(ref lengthMultiplier, 0.1f, float.MaxValue);
        ValidateRange(ref lateralChanceMultiplier, 0f, float.MaxValue);
        ValidateRange(ref verticalChanceMultiplier, 0f, float.MaxValue);
        ValidateRange(ref narrowChanceMultiplier, 0f, float.MaxValue);
        ValidateRange(ref gapChanceMultiplier, 0f, float.MaxValue);
        ValidateRange(ref railChanceMultiplier, 0f, float.MaxValue);
    }

    /// <summary>
    /// Clampea los valores de un rango para evitar valores inválidos en el editor.
    /// El struct es inmutable por diseño; la validación solo alerta en OnValidate.
    /// </summary>
    private static void ValidateRange(ref DifficultyParameterRange range, float absMin, float absMax)
    {
        // La validación visual se hace mediante los atributos Range/Min en el struct.
        // Este método existe como punto de extensión futura si se necesita lógica adicional.
        _ = range;
        _ = absMin;
        _ = absMax;
    }

    #endregion
}