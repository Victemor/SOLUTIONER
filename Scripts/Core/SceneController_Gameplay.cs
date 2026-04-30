using TamarilloGames.Core.GameFramework;
using UnityEngine;

/// <summary>
/// Controlador de la escena de gameplay.
///
/// Responsabilidades:
/// - Leer el índice de nivel actual desde la persistencia al iniciar.
/// - Orquestar la generación de pista y contenido para ese nivel.
/// - Escuchar el evento de meta alcanzada y avanzar el índice de nivel.
/// - Guardar el índice de nivel actualizado en la persistencia.
///
/// Este es el único lugar del juego que conoce el número de nivel actual.
/// Los generadores solo reciben ese número como parámetro; no saben ni les importa
/// cómo se calculó.
/// </summary>
public sealed class SceneController_Gameplay : SceneController
{
    #region Inspector

    [Header("Generadores")]

    [Tooltip("Generador procedural de pista. Se le pasa el índice de nivel para resolver sus parámetros.")]
    [SerializeField] private TrackGeneratorController trackGenerator;

    [Tooltip("Generador procedural de contenido (obstáculos, monedas, meta).")]
    [SerializeField] private TrackContentGenerator contentGenerator;

    [Header("Persistencia")]

    [Tooltip("Clave usada para leer y guardar el nivel actual en PlayerPrefs.")]
    [SerializeField] private string levelIndexKey = "CurrentLevelIndex";

    [Tooltip("Nivel inicial cuando el jugador arranca por primera vez (o si se resetea la partida).")]
    [SerializeField, Min(1)] private int firstLevelIndex = 1;

    [Header("Debug")]

    [Tooltip("Si está activo, ignora PlayerPrefs y usa debugForcedLevelIndex para testear un nivel específico sin afectar la partida guardada.")]
    [SerializeField] private bool debugForceLevel;

    [Tooltip("Índice de nivel forzado en debug. Solo actúa si debugForceLevel está activo.")]
    [SerializeField, Min(1)] private int debugForcedLevelIndex = 1;

    #endregion

    #region Runtime

    /// <summary>
    /// Índice del nivel que se está jugando actualmente.
    /// </summary>
    public int CurrentLevelIndex { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Deshabilitar generateOnStart en los generadores: nosotros controlamos cuándo generan.
        // Si generateOnStart está activo en los componentes, generarán antes de que
        // este controlador les diga el número de nivel.
        // La alternativa es simplemente que generateOnStart esté desactivado en el Inspector.
    }

    private void Start()
    {
        CurrentLevelIndex = ResolveCurrentLevelIndex();
        GenerateCurrentLevel();
    }

    private void OnEnable()
    {
        GameEvents.OnGoalReached += HandleGoalReached;
    }

    private void OnDisable()
    {
        GameEvents.OnGoalReached -= HandleGoalReached;
    }

    #endregion

    #region Level Flow

    /// <summary>
    /// El jugador llegó a la meta: avanzar al siguiente nivel y reiniciar la escena.
    /// </summary>
    private void HandleGoalReached()
    {
        int nextLevelIndex = CurrentLevelIndex + 1;
        SaveLevelIndex(nextLevelIndex);

        // Recargar la misma escena de gameplay para el nuevo nivel.
        // RequestNewActiveScene recarga la escena limpiamente sin acumular objetos generados.
        RequestNewActiveScene(new SceneLoadRequest(gameObject.scene.name));
    }

    /// <summary>
    /// Genera la pista y el contenido para el nivel actual.
    /// Siempre en este orden: primero la pista, luego el contenido
    /// (el contenido necesita el mapa runtime que produce el generador de pista).
    /// </summary>
    private void GenerateCurrentLevel()
    {
        if (trackGenerator == null)
        {
            Debug.LogError("[GAMEPLAY] TrackGeneratorController no está asignado en SceneController_Gameplay.", this);
            return;
        }

        trackGenerator.GenerateLevel(CurrentLevelIndex);

        if (contentGenerator != null)
        {
            contentGenerator.GenerateContent();
        }
    }

    #endregion

    #region Persistence

    /// <summary>
    /// Lee el índice de nivel desde PlayerPrefs o usa el override de debug.
    /// </summary>
    private int ResolveCurrentLevelIndex()
    {
#if UNITY_EDITOR
        if (debugForceLevel)
        {
            return Mathf.Max(1, debugForcedLevelIndex);
        }
#endif
        return Mathf.Max(firstLevelIndex, PlayerPrefs.GetInt(levelIndexKey, firstLevelIndex));
    }

    /// <summary>
    /// Guarda el índice de nivel en PlayerPrefs.
    /// Se llama antes de recargar la escena para que el siguiente ciclo lo lea.
    /// </summary>
    private void SaveLevelIndex(int levelIndex)
    {
        PlayerPrefs.SetInt(levelIndexKey, Mathf.Max(1, levelIndex));
        PlayerPrefs.Save();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Resetea la progresión al nivel inicial y recarga la escena.
    /// Útil para un botón "Reiniciar partida" en el menú de opciones.
    /// </summary>
    public void ResetProgressionAndReload()
    {
        SaveLevelIndex(firstLevelIndex);
        RequestNewActiveScene(new SceneLoadRequest(gameObject.scene.name));
    }

    #endregion
}