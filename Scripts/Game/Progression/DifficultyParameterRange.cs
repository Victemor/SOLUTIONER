using UnityEngine;

/// <summary>
/// Rango progresivo de un parámetro numérico.
/// Define el valor inicial (nivel 1), el valor techo (plateau), el nivel en que se alcanza
/// ese techo y la curva de progresión que define la forma de la transición.
/// </summary>
[System.Serializable]
public struct DifficultyParameterRange
{
    #region Inspector

    [Tooltip("Valor del parámetro en el nivel 1.")]
    [SerializeField] private float minValue;

    [Tooltip("Valor máximo del parámetro. Se alcanza en el nivel indicado por plateauLevel y no crece más allá.")]
    [SerializeField] private float maxValue;

    [Tooltip("Número de nivel en el que se alcanza el valor máximo. A partir de aquí el parámetro se estabiliza.")]
    [SerializeField, Min(1)] private int plateauLevel;

    [Tooltip("Curva que define la forma de la progresión. Eje X: nivel normalizado (0=nivel 1, 1=plateau). Eje Y: t de interpolación (0..1).")]
    [SerializeField] private AnimationCurve progressionCurve;

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor explícito usado exclusivamente por los métodos factory.
    /// Unity ignora los constructores al deserializar; siempre usa los campos serializados.
    /// </summary>
    private DifficultyParameterRange(float min, float max, int plateau, AnimationCurve curve)
    {
        minValue = min;
        maxValue = max;
        plateauLevel = Mathf.Max(1, plateau);
        progressionCurve = curve;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Evalúa el valor del parámetro para un índice de nivel dado (base 1).
    /// Los índices posteriores al plateau devuelven maxValue.
    /// </summary>
    public float Evaluate(int levelIndex)
    {
        if (plateauLevel <= 1)
        {
            return maxValue;
        }

        float normalizedLevel = Mathf.Clamp01((float)(levelIndex - 1) / (plateauLevel - 1));

        float curveT = progressionCurve != null && progressionCurve.length > 0
            ? Mathf.Clamp01(progressionCurve.Evaluate(normalizedLevel))
            : normalizedLevel;

        return Mathf.Lerp(minValue, maxValue, curveT);
    }

    /// <summary>
    /// Evalúa el valor y lo redondea al entero más cercano.
    /// Útil para cantidades discretas como monedas u obstáculos.
    /// </summary>
    public int EvaluateInt(int levelIndex)
    {
        return Mathf.RoundToInt(Evaluate(levelIndex));
    }

    #endregion

    #region Static Factory

    /// <summary>
    /// Crea un rango con valor constante (sin progresión).
    /// </summary>
    public static DifficultyParameterRange Constant(float value)
    {
        return new DifficultyParameterRange(
            min: value,
            max: value,
            plateau: 1,
            curve: AnimationCurve.Linear(0f, 1f, 1f, 1f));
    }

    /// <summary>
    /// Crea un rango con progresión lineal entre min y max.
    /// </summary>
    public static DifficultyParameterRange Linear(float min, float max, int plateau)
    {
        return new DifficultyParameterRange(
            min: min,
            max: max,
            plateau: Mathf.Max(1, plateau),
            curve: AnimationCurve.Linear(0f, 0f, 1f, 1f));
    }

    #endregion
}