using System.Collections;
using UnityEngine;

/// <summary>
/// Orquestador central de la progresión infinita de niveles.
///
/// Niveles normales: dificultad escala de mínimo (progresión SO) a máximo (profiles).
/// Niveles bonus (múltiplos de <see cref="InfiniteProgressionSettings.BonusLevelInterval"/>):
///   - Solo monedas, sin obstáculos, sin gaps.
///   - Barreras en toda la pista.
///   - Texturas del suelo tomadas de <see cref="bonusTrackProfile"/>.
///
/// Barreras: inversas al resto — empiezan al máximo (Inspector de TrackBarrierGenerator),
/// y bajan hasta el mínimo (progressionSettings) conforme avanza la dificultad.
/// Los primeros <see cref="InfiniteProgressionSettings.GuaranteedFullBarrierLevels"/> niveles
/// siempre tienen barreras completas.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class InfiniteLevelManager : MonoBehaviour
{
    #region Inspector

    [Header("Progresión")]
    [SerializeField]
    [Tooltip("SO con valores iniciales de dificultad y semilla base.")]
    private InfiniteProgressionSettings progressionSettings;

    [Header("Generadores")]
    [SerializeField]
    [Tooltip("Controlador procedural de la pista.")]
    private TrackGeneratorController trackGenerator;

    [SerializeField]
    [Tooltip("Generador de obstáculos, monedas y meta.")]
    private TrackContentGenerator contentGenerator;

    [SerializeField]
    [Tooltip("Generador de barreras laterales. Sus campos Inspector (generalBarrierChance, " +
             "generalCoverageRatio) actúan como el MÁXIMO de barreras.")]
    private TrackBarrierGenerator barrierGenerator;

    [SerializeField]
    [Tooltip("Catálogo global de contenido. Sus valores Base son el máximo de dificultad.")]
    private TrackContentGenerationProfile contentProfile;

    [Header("Nivel Bonus")]
    [SerializeField]
    [Tooltip("Profile de generación con materiales especiales para los niveles bonus.\n" +
             "Si es null, se usa el mismo profile base (sin cambio visual).")]
    private TrackGenerationProfile bonusTrackProfile;

    [Header("Jugador")]
    [SerializeField]
    [Tooltip("Motor de movimiento de la bola.")]
    private BallMovementMotor ballMovementMotor;

    [SerializeField]
    [Tooltip("Controlador de estado de la bola.")]
    private BallStateController ballStateController;

    [SerializeField]
    [Tooltip("Controlador de respawn de la bola.")]
    private BallRespawnController ballRespawnController;

    [SerializeField]
    [Tooltip("Distancia vertical sobre el origen del track donde spawneará la bola.")]
    private float ballSpawnHeightOffset = 1f;

    [Header("Transición")]
    [SerializeField]
    [Tooltip("Segundos entre que la bola toca la meta y se genera el siguiente nivel.")]
    private float levelTransitionDelay = 0.8f;

    [Header("Estado Actual — Solo Lectura en Runtime")]
    [SerializeField]
    [Tooltip("Índice del nivel activo. Comienza en 1.\n" +
             "Modifícalo antes de Play Mode para probar niveles específicos.")]
    private int currentLevelIndex = 1;

    [SerializeField]
    [Tooltip("Semilla activa = BaseSeed + LevelIndex.")]
    private int currentActiveSeed;

    [SerializeField]
    [Tooltip("Factor de progresión [0 = inicio, 1 = dificultad máxima].")]
    [Range(0f, 1f)]
    private float currentProgressionT;

    [SerializeField]
    [Tooltip("¿El nivel actual es un nivel BONUS?")]
    private bool currentIsBonus;

    #endregion

    #region Runtime

    private LevelGenerationSettings runtimeTrackSettings;
    private LevelContentGenerationSettings runtimeContentSettings;
    private Coroutine levelTransitionCoroutine;

    #endregion

    #region Properties

    public int CurrentLevelIndex => currentLevelIndex;
    public int CurrentActiveSeed => currentActiveSeed;
    public bool CurrentIsBonus => currentIsBonus;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!ValidateReferences())
        {
            return;
        }

        runtimeTrackSettings = ScriptableObject.CreateInstance<LevelGenerationSettings>();
        runtimeContentSettings = ScriptableObject.CreateInstance<LevelContentGenerationSettings>();

        trackGenerator.DisableAutoGeneration();
        contentGenerator.DisableAutoGeneration();

        ConfigureAndGenerate();
        RepositionBall();
    }

    private void OnDestroy()
    {
        if (runtimeTrackSettings != null)
        {
            Destroy(runtimeTrackSettings);
        }

        if (runtimeContentSettings != null)
        {
            Destroy(runtimeContentSettings);
        }
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

    #region Event Handlers

    private void HandleGoalReached()
    {
        if (levelTransitionCoroutine != null)
        {
            StopCoroutine(levelTransitionCoroutine);
        }

        levelTransitionCoroutine = StartCoroutine(LevelTransitionRoutine());
    }

    #endregion

    #region Public API

    /// <summary>
    /// Reinicia la progresión al nivel 1 y regenera.
    /// Puede llamarse desde un botón de UI o desde código.
    /// </summary>
    [ContextMenu("Restart To Level One")]
    public void RestartToLevelOne()
    {
        if (levelTransitionCoroutine != null)
        {
            StopCoroutine(levelTransitionCoroutine);
            levelTransitionCoroutine = null;
        }

        currentLevelIndex = 1;
        ConfigureAndGenerate();
        RepositionBall();

        Debug.Log("[INFINITE LEVEL] Juego reiniciado al nivel 1.", this);
    }

    /// <summary>
    /// Regenera el nivel actual sin avanzar el índice.
    /// Útil para probar la configuración actual sin cambiar de nivel.
    /// </summary>
    [ContextMenu("Regenerate Current Level")]
    public void RegenerateCurrentLevel()
    {
        if (levelTransitionCoroutine != null)
        {
            StopCoroutine(levelTransitionCoroutine);
            levelTransitionCoroutine = null;
        }

        ConfigureAndGenerate();
        RepositionBall();
    }

    #endregion

    #region Level Transition

    private IEnumerator LevelTransitionRoutine()
    {
        yield return new WaitForSeconds(levelTransitionDelay);

        currentLevelIndex++;
        ConfigureAndGenerate();
        RepositionBall();

        levelTransitionCoroutine = null;
    }

    /// <summary>
    /// Configura todo para el nivel actual y lanza la regeneración.
    /// </summary>
    private void ConfigureAndGenerate()
    {
        currentProgressionT = ComputeProgressionT(currentLevelIndex, progressionSettings.LevelCountToReachMax);
        currentActiveSeed = progressionSettings.BaseSeed + currentLevelIndex;
        currentIsBonus = IsBonusLevel(currentLevelIndex);

        TrackGenerationProfile trackProfile = trackGenerator.GenerationProfile;

        // ── 1. Configurar track settings ──────────────────────────────
        runtimeTrackSettings.ConfigureForLevel(progressionSettings, trackProfile, currentLevelIndex);

        if (currentIsBonus)
        {
            // Los niveles bonus no tienen gaps.
            runtimeTrackSettings.ApplyBonusLevelOverrides();
        }

        // ── 2. Configurar barreras (antes de GenerateLevel para que Rebuild las use) ──
        ConfigureBarrierProbability(trackProfile);

        // ── 3. Perfil visual: bonus usa texturas especiales ────────────
        if (currentIsBonus && bonusTrackProfile != null)
        {
            trackGenerator.SetVisualProfileOverride(bonusTrackProfile);
        }
        else
        {
            trackGenerator.ClearVisualProfileOverride();
        }

        // ── 4. Generar pista ───────────────────────────────────────────
        trackGenerator.GenerateLevel(runtimeTrackSettings);

        // ── 5. Configurar contenido ────────────────────────────────────
        runtimeContentSettings.ConfigureForLevel(progressionSettings, contentProfile, currentLevelIndex);

        if (currentIsBonus)
        {
            // Solo monedas en niveles bonus, con su propio conteo independiente.
            runtimeContentSettings.ConfigureForBonusLevel(progressionSettings.BonusCoinCount);
        }

        // ── 6. Generar contenido ───────────────────────────────────────
        contentGenerator.GenerateContent(runtimeContentSettings);

        // ── 7. Log ────────────────────────────────────────────────────
        string label = currentIsBonus
            ? $"{currentLevelIndex} ⭐ BONUS"
            : $"{currentLevelIndex}";

        string diffLabel = currentProgressionT >= 1f
            ? "DIFICULTAD MÁXIMA"
            : $"máx en nivel {progressionSettings.LevelCountToReachMax}";

        Debug.Log(
            $"[INFINITE LEVEL] ══════════════════════════════\n" +
            $"  Nivel         : {label}\n" +
            $"  Semilla       : {currentActiveSeed}  (base {progressionSettings.BaseSeed} + {currentLevelIndex})\n" +
            $"  Progresión    : {currentProgressionT:P0}  ({diffLabel})\n" +
            $"  Obstáculos    : máx {runtimeContentSettings.MaxObstacleCount}\n" +
            $"  Monedas       : {runtimeContentSettings.FixedCoinCount}\n" +
            $"══════════════════════════════",
            this);
    }

    /// <summary>
    /// Calcula e inyecta la probabilidad de barreras y las longitudes reales de zona segura.
    ///
    /// Las zonas seguras SIEMPRE tienen barreras si están habilitadas, independientemente
    /// de la probabilidad general. La probabilidad general controla solo el tramo central.
    ///
    /// Las barreras funcionan al REVÉS de la dificultad:
    /// nivel 1 = máximo (Inspector del generador de barreras), nivel máximo = mínimo (progressionSettings).
    /// </summary>
    private void ConfigureBarrierProbability(TrackGenerationProfile trackProfile)
    {
        if (barrierGenerator == null)
        {
            return;
        }

        // Inyectar longitudes reales de zona segura para que el barrier generator
        // las trate como zona siempre-barrera, independientemente de la probabilidad general.
        float safeStartLength = progressionSettings.SafeStartLengthOverride > 0f
            ? progressionSettings.SafeStartLengthOverride
            : trackProfile.SafeStartLength;

        float safeEndLength = progressionSettings.SafeEndLengthOverride > 0f
            ? progressionSettings.SafeEndLengthOverride
            : trackProfile.SafeEndLength;

        barrierGenerator.SetSafeZoneLengths(safeStartLength, safeEndLength);

        // Niveles garantizados y bonus: barreras completas en toda la pista.
        if (currentLevelIndex <= progressionSettings.GuaranteedFullBarrierLevels || currentIsBonus)
        {
            barrierGenerator.ForceFullBarriers();
            return;
        }

        // Interpolación inversa: max (Inspector) → min (progressionSettings) conforme sube t.
        float resolvedChance = Mathf.Lerp(
            barrierGenerator.GeneralBarrierChance,
            progressionSettings.MinBarrierChance,
            currentProgressionT);

        float resolvedCoverage = Mathf.Lerp(
            barrierGenerator.GeneralCoverageRatio,
            progressionSettings.MinBarrierCoverageRatio,
            currentProgressionT);

        barrierGenerator.SetBarrierProbability(resolvedChance, resolvedCoverage);
    }

    private void RepositionBall()
    {
        if (ballMovementMotor == null || ballStateController == null)
        {
            return;
        }

        Vector3 spawnPosition = trackGenerator.transform.position + (Vector3.up * ballSpawnHeightOffset);
        float generatorYaw = trackGenerator.transform.eulerAngles.y;
        Quaternion spawnRotation = Quaternion.Euler(0f, generatorYaw, 0f);

        ballMovementMotor.TeleportTo(spawnPosition, spawnRotation);
        ballStateController.ResetState();

        if (ballRespawnController != null)
        {
            ballRespawnController.CaptureCurrentTransformAsRespawn();
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Indica si un nivel es bonus (múltiplo del intervalo configurado).
    /// Si el intervalo es 0 no hay niveles bonus.
    /// </summary>
    private bool IsBonusLevel(int levelIndex)
    {
        int interval = progressionSettings.BonusLevelInterval;
        return interval > 0 && levelIndex % interval == 0;
    }

    private static float ComputeProgressionT(int levelIndex, int levelCountToReachMax)
    {
        if (levelCountToReachMax <= 1)
        {
            return 1f;
        }

        return Mathf.Clamp01((float)(levelIndex - 1) / (levelCountToReachMax - 1));
    }

    #endregion

    #region Validation

    private bool ValidateReferences()
    {
        bool isValid = true;

        if (progressionSettings == null)
        {
            Debug.LogError("[INFINITE LEVEL] progressionSettings no asignado.", this);
            isValid = false;
        }

        if (trackGenerator == null)
        {
            Debug.LogError("[INFINITE LEVEL] trackGenerator no asignado.", this);
            isValid = false;
        }

        if (contentGenerator == null)
        {
            Debug.LogError("[INFINITE LEVEL] contentGenerator no asignado.", this);
            isValid = false;
        }

        if (contentProfile == null)
        {
            Debug.LogError("[INFINITE LEVEL] contentProfile no asignado.", this);
            isValid = false;
        }

        if (ballMovementMotor == null)
        {
            Debug.LogError("[INFINITE LEVEL] ballMovementMotor no asignado.", this);
            isValid = false;
        }

        if (ballStateController == null)
        {
            Debug.LogError("[INFINITE LEVEL] ballStateController no asignado.", this);
            isValid = false;
        }

        return isValid;
    }

    #endregion
}