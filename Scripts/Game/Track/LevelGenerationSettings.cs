using UnityEngine;

/// <summary>
/// Configuración específica de un nivel para modificar la generación base del track.
/// </summary>
[CreateAssetMenu(fileName = "LevelGenerationSettings", menuName = "Game/Track/Level Generation Settings")]
public sealed class LevelGenerationSettings : ScriptableObject
{
    #region Inspector

    [Header("Seed")]
    [SerializeField]
    [Tooltip("Si está activo, el nivel usará una semilla fija reproducible.")]
    private bool useFixedSeed = true;

    [SerializeField]
    [Tooltip("Semilla fija del nivel.")]
    private int fixedSeed = 12345;

    [Header("Difficulty")]
    [SerializeField]
    [Tooltip("Multiplicador global de dificultad.")]
    private float difficultyMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de longitud total del track.")]
    private float lengthMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de probabilidad de cambio lateral.")]
    private float lateralChanceMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de probabilidad de cambio vertical.")]
    private float verticalChanceMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de probabilidad de estrechamiento.")]
    private float narrowChanceMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de probabilidad de gap.")]
    private float gapChanceMultiplier = 1f;

    [SerializeField]
    [Tooltip("Multiplicador de probabilidad de generación rail.")]
    private float railChanceMultiplier = 1f;

    [Header("Safe Zones")]
    [SerializeField]
    [Tooltip("Si es mayor que cero, reemplaza la longitud segura inicial del perfil.")]
    private float safeStartLengthOverride = -1f;

    [SerializeField]
    [Tooltip("Si es mayor que cero, reemplaza la longitud segura final del perfil.")]
    private float safeEndLengthOverride = -1f;

    [Header("Safe Zone Barriers")]
    [SerializeField]
    [Tooltip("Si está activo, se generan bordes laterales en la zona segura inicial.")]
    private bool generateStartSafeZoneBarriers = true;

    [SerializeField]
    [Tooltip("Si está activo, se generan bordes laterales en la zona segura final.")]
    private bool generateEndSafeZoneBarriers = true;

    [Header("Height Overrides")]
    [SerializeField]
    [Tooltip("Si está activo, sobrescribe la altura mínima permitida.")]
    private bool overrideMinHeight;

    [SerializeField]
    [Tooltip("Altura mínima del nivel.")]
    private float minHeightOverride = -4f;

    [SerializeField]
    [Tooltip("Si está activo, sobrescribe la altura máxima permitida.")]
    private bool overrideMaxHeight;

    [SerializeField]
    [Tooltip("Altura máxima del nivel.")]
    private float maxHeightOverride = 8f;

    #endregion

    #region Properties

    public bool UseFixedSeed => useFixedSeed;
    public int FixedSeed => fixedSeed;
    public float DifficultyMultiplier => difficultyMultiplier;
    public float LengthMultiplier => lengthMultiplier;
    public float LateralChanceMultiplier => lateralChanceMultiplier;
    public float VerticalChanceMultiplier => verticalChanceMultiplier;
    public float NarrowChanceMultiplier => narrowChanceMultiplier;
    public float GapChanceMultiplier => gapChanceMultiplier;
    public float RailChanceMultiplier => railChanceMultiplier;
    public float SafeStartLengthOverride => safeStartLengthOverride;
    public float SafeEndLengthOverride => safeEndLengthOverride;
    public bool GenerateStartSafeZoneBarriers => generateStartSafeZoneBarriers;
    public bool GenerateEndSafeZoneBarriers => generateEndSafeZoneBarriers;
    public bool OverrideMinHeight => overrideMinHeight;
    public float MinHeightOverride => minHeightOverride;
    public bool OverrideMaxHeight => overrideMaxHeight;
    public float MaxHeightOverride => maxHeightOverride;

    #endregion

    private void OnValidate()
    {
        difficultyMultiplier = Mathf.Max(0.1f, difficultyMultiplier);
        lengthMultiplier = Mathf.Max(0.1f, lengthMultiplier);
        lateralChanceMultiplier = Mathf.Max(0f, lateralChanceMultiplier);
        verticalChanceMultiplier = Mathf.Max(0f, verticalChanceMultiplier);
        narrowChanceMultiplier = Mathf.Max(0f, narrowChanceMultiplier);
        gapChanceMultiplier = Mathf.Max(0f, gapChanceMultiplier);
        railChanceMultiplier = Mathf.Max(0f, railChanceMultiplier);
    }
}