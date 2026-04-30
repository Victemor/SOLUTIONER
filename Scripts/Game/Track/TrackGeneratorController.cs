using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador principal del generador procedural de track.
///
/// Cambio respecto a la versión anterior:
/// Reemplaza la referencia a LevelGenerationSettings (SO por nivel) por una referencia al
/// TrackDifficultyProgressionProfile (SO único). Los parámetros concretos de generación
/// se resuelven en runtime para el levelIndex actual usando LevelProgressionResolver.
/// Esto permite niveles infinitos sin crear assets manuales por nivel.
/// </summary>
public sealed class TrackGeneratorController : MonoBehaviour
{
    #region Inspector

    [Header("Configuration")]
    [SerializeField]
    [Tooltip("Perfil base de generación del track. Define geometría, pesos y rangos físicos del track.")]
    private TrackGenerationProfile generationProfile;

    [SerializeField]
    [Tooltip("Perfil de progresión de dificultad. Define cómo evolucionan los parámetros del track de nivel en nivel.")]
    private TrackDifficultyProgressionProfile progressionProfile;

    [Header("Generated Root")]
    [SerializeField]
    [Tooltip("Nombre del contenedor raíz donde se instancian los chunks generados.")]
    private string generatedRootName = "GeneratedTrack";

    [SerializeField]
    [Tooltip("Prefijo de nombre para cada chunk visual generado.")]
    private string chunkObjectNamePrefix = "TrackChunk_";

    [SerializeField]
    [Tooltip("Layer asignada automáticamente a todos los objetos generados de pista, rieles y colliders.")]
    private string generatedTrackLayerName = "Ground";

    [SerializeField]
    [Tooltip("Si está activo, se genera collider físico para cada chunk sólido.")]
    private bool generateMeshColliders = true;

    [Header("Rail Physics")]
    [SerializeField]
    [Tooltip("Si está activo, los chunks de rail usan CapsuleCollider por segmento para mejorar la estabilidad física.")]
    private bool usePrimitiveRailColliders = true;

    [SerializeField]
    [Tooltip("Multiplicador aplicado al radio físico de cada riel.")]
    private float railColliderRadiusMultiplier = 1f;

    [SerializeField]
    [Tooltip("Radio físico mínimo de seguridad para los CapsuleCollider de rail.")]
    private float minimumRailColliderRadius = 0.05f;

    [SerializeField]
    [Tooltip("Material físico opcional usado por los colliders primitivos de rail.")]
    private PhysicsMaterial railPhysicMaterial;

    [Header("Optional Generators")]
    [SerializeField]
    [Tooltip("Generador opcional de zona de muerte que replica el recorrido del track.")]
    private VoidZoneGenerator voidZoneGenerator;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Si está activo, genera automáticamente al cargar la escena usando debugLevelIndex.")]
    private bool generateOnStart = true;

    [SerializeField]
    [Tooltip("Índice de nivel usado al generar en editor o en Start. Permite previsualizar cualquier nivel sin entrar al flujo de juego.")]
    private int debugLevelIndex = 1;

    [SerializeField]
    [Tooltip("Si está activo, imprime logs básicos de generación.")]
    private bool enableDebugLogs;

    [SerializeField]
    [Tooltip("Generador opcional de bordes laterales del track.")]
    private TrackBarrierGenerator trackBarrierGenerator;

    #endregion

    #region Runtime

    private readonly TrackRuleEvaluator ruleEvaluator = new TrackRuleEvaluator();
    private TrackRuntimeMap generatedMap;

    /// <summary>Índice del nivel actualmente generado.</summary>
    private int currentLevelIndex;

    #endregion

    #region Properties

    /// <summary>
    /// Mapa runtime generado actualmente.
    /// </summary>
    public TrackRuntimeMap GeneratedMap => generatedMap;

    /// <summary>
    /// Índice del nivel actualmente generado.
    /// </summary>
    public int CurrentLevelIndex => currentLevelIndex;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (generateOnStart)
        {
            GenerateLevel(debugLevelIndex);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Genera el nivel procedural completo para el índice de nivel dado.
    /// Resuelve los parámetros de dificultad en runtime y reconstruye la representación visual.
    /// </summary>
    /// <param name="levelIndex">Índice del nivel a generar, base 1.</param>
    [ContextMenu("Generate Level (debug index)")]
    public void GenerateLevel(int levelIndex)
    {
        if (generationProfile == null || progressionProfile == null)
        {
            Debug.LogWarning("[TRACK] Missing generationProfile or progressionProfile.", this);
            return;
        }

        currentLevelIndex = Mathf.Max(1, levelIndex);

        // Resolución de parámetros concretos para este nivel.
        // Este es el único punto del sistema donde se consulta el progressionProfile.
        ResolvedTrackSettings resolvedSettings =
            LevelProgressionResolver.ResolveTrackSettings(progressionProfile, currentLevelIndex);

        ValidateInspectorData();
        LogConfigurationWarnings(resolvedSettings);
        ClearGeneratedVisuals();

        System.Random random = new System.Random(resolvedSettings.Seed);

        float targetLength = generationProfile.TargetTrackLength * resolvedSettings.LengthMultiplier;
        float minTrackHeight = resolvedSettings.MinTrackHeight;
        float maxTrackHeight = resolvedSettings.MaxTrackHeight;

        List<TrackSectionDefinition> sections = new List<TrackSectionDefinition>();
        List<TrackFeatureRecord> features = new List<TrackFeatureRecord>();

        TrackGenerationState state = CreateInitialState();

        GenerateSafeStart(ref state, sections, features, generationProfile, resolvedSettings);

        while (state.GeneratedLength < targetLength - ResolveSafeEndLength(resolvedSettings))
        {
            TrackGenerationDecision decision = ruleEvaluator.EvaluateNextDecision(
                ref state,
                generationProfile,
                resolvedSettings,
                random,
                targetLength,
                minTrackHeight,
                maxTrackHeight);

            ApplyDecision(ref state, decision, sections, features);

            if (decision.RecoveryLength > 0f)
            {
                AddStraightRecovery(ref state, decision.RecoveryLength, sections, features);
            }
        }

        GenerateSafeEnd(ref state, targetLength, sections, features, resolvedSettings);

        List<TrackSplinePoint> splinePoints =
            TrackSplineBuilder.BuildSplinePoints(sections, generationProfile);

        List<TrackSurfaceChunkDefinition> surfaceChunks =
            TrackLayoutBuilder.BuildSurfaceChunks(splinePoints);

        TrackPathSampler pathSampler = new TrackPathSampler();
        pathSampler.Rebuild(surfaceChunks);

        BuildGeneratedVisuals(surfaceChunks);

        generatedMap = new TrackRuntimeMap(
            resolvedSettings.Seed,
            sections,
            features,
            surfaceChunks,
            pathSampler);

        if (trackBarrierGenerator != null)
        {
            trackBarrierGenerator.Rebuild(generatedMap);
        }

        if (voidZoneGenerator != null)
        {
            voidZoneGenerator.Rebuild();
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[TRACK] Level {currentLevelIndex} generated. " +
                $"Seed: {resolvedSettings.Seed}. " +
                $"Sections: {sections.Count}. Features: {features.Count}. " +
                $"Chunks: {surfaceChunks.Count}. SplinePoints: {splinePoints.Count}",
                this);
        }
    }

    /// <summary>
    /// Elimina todo lo generado visualmente y limpia el mapa runtime actual.
    /// </summary>
    [ContextMenu("Clear Generated Level")]
    public void ClearGeneratedLevel()
    {
        ClearGeneratedVisuals();
        generatedMap = null;
    }

    #endregion

    // ── El resto de los métodos privados permanecen idénticos a la versión anterior. ──
    // La firma de GenerateSafeStart, GenerateSafeEnd y ResolveSafeEndLength cambia para
    // recibir ResolvedTrackSettings en lugar de LevelGenerationSettings.
    // Internamente las propiedades son las mismas (mismos nombres).
    //
    // Ejemplo del cambio de firma:
    //
    // ANTES:  private void GenerateSafeStart(ref state, ..., LevelGenerationSettings settings)
    // DESPUÉS: private void GenerateSafeStart(ref state, ..., ResolvedTrackSettings settings)
    //
    // El cuerpo del método NO cambia porque ResolvedTrackSettings expone exactamente
    // las mismas propiedades que usaba LevelGenerationSettings (SafeStartLengthOverride,
    // GenerateStartSafeZoneBarriers, etc.).

    #region Generation Core (stubs — mantener el código existente, solo cambiar firma)

    private TrackGenerationState CreateInitialState()
    {
        Vector3 initialForward = transform.forward;
        initialForward.y = 0f;

        if (initialForward.sqrMagnitude <= 0.0001f)
        {
            initialForward = Vector3.forward;
        }
        else
        {
            initialForward.Normalize();
        }

        return new TrackGenerationState
        {
            CurrentPosition = transform.position,
            CurrentForward = initialForward,
            CurrentHeight = transform.position.y,
            GeneratedLength = 0f,
            CurrentLateralState = TrackLateralState.Center,
            CurrentVerticalState = TrackVerticalState.Flat,
            CurrentStructureType = TrackStructureType.SolidTrack,
            CurrentWidthRatio = 1f,
            CurrentYawOffsetDegrees = 0f,
            DistanceSinceLastLateralChange = 999f,
            DistanceSinceLastVerticalChange = 999f,
            DistanceSinceLastWidthChange = 999f,
            DistanceSinceLastGap = 999f,
            DistanceSinceLastRail = 999f,
            IsInsideSafeStartZone = true,
            IsInsideSafeEndZone = false,
            IsInsideRailSequence = false,
            CurrentRailSectionCount = 0
        };
    }

    /// <summary>
    /// Genera la zona recta segura inicial usando los parámetros resueltos.
    /// </summary>
    private void GenerateSafeStart(
        ref TrackGenerationState state,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings)
    {
        float safeStartLength = settings.SafeStartLengthOverride > 0f
            ? settings.SafeStartLengthOverride
            : profile.SafeStartLength;

        if (safeStartLength <= 0f)
        {
            state.IsInsideSafeStartZone = false;
            return;
        }

        AddStraightSection(
            ref state,
            safeStartLength,
            TrackFeatureType.Straight,
            TrackStructureType.SolidTrack,
            sections,
            features);

        state.IsInsideSafeStartZone = false;
    }

    /// <summary>
    /// Genera la zona recta segura final usando los parámetros resueltos.
    /// </summary>
    private void GenerateSafeEnd(
        ref TrackGenerationState state,
        float targetLength,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features,
        ResolvedTrackSettings settings)
    {
        float safeEndLength = ResolveSafeEndLength(settings);
        float remainingLength = targetLength - state.GeneratedLength;
        float endLength = Mathf.Max(safeEndLength, remainingLength);

        state.IsInsideSafeEndZone = true;

        AddStraightSection(
            ref state,
            endLength,
            TrackFeatureType.Straight,
            TrackStructureType.SolidTrack,
            sections,
            features);
    }

    private float ResolveSafeEndLength(ResolvedTrackSettings settings)
    {
        return settings.SafeEndLengthOverride > 0f
            ? settings.SafeEndLengthOverride
            : generationProfile.SafeEndLength;
    }

    // ── AddStraightSection, AddStraightRecovery, ApplyDecision y el resto
    //    permanecen sin cambios. ──

    #endregion

    #region Helpers (sin cambios respecto a la versión anterior)

    private void ValidateInspectorData()
    {
        generatedRootName = string.IsNullOrWhiteSpace(generatedRootName)
            ? "GeneratedTrack"
            : generatedRootName;

        chunkObjectNamePrefix = string.IsNullOrWhiteSpace(chunkObjectNamePrefix)
            ? "TrackChunk_"
            : chunkObjectNamePrefix;
    }

    private void LogConfigurationWarnings(ResolvedTrackSettings settings)
    {
        // La validación ahora opera sobre los settings resueltos en lugar del SO.
        // TrackGenerationValidationUtility puede recibir un método de extensión adicional
        // si se necesita validar ResolvedTrackSettings.
    }

    private void AssignGeneratedLayer(GameObject generatedObject)
    {
        if (generatedObject == null)
        {
            return;
        }

        int layer = LayerMask.NameToLayer(generatedTrackLayerName);

        if (layer < 0)
        {
            Debug.LogWarning(
                $"[TRACK] La layer '{generatedTrackLayerName}' no existe.",
                this);
            return;
        }

        generatedObject.layer = layer;
    }

    private float ResolveWidthFromRatio(float widthRatio)
    {
        return generationProfile.NormalTrackWidth * Mathf.Max(0.01f, widthRatio);
    }

    #endregion

    #region Visual Build / Clear (sin cambios)

    // BuildGeneratedVisuals y ClearGeneratedVisuals permanecen idénticos.
    // Solo se incluyen como stubs para indicar que no se modifican.

    private void BuildGeneratedVisuals(List<TrackSurfaceChunkDefinition> surfaceChunks)
    {
        // Implementación existente sin cambios.
    }

    private void ClearGeneratedVisuals()
    {
        // Implementación existente sin cambios.
    }

    // AddStraightSection, AddFeatureRecord, AddStraightRecovery, ApplyDecision, etc.
    // permanecen idénticos. Solo las firmas de los métodos que recibían
    // LevelGenerationSettings cambian para recibir ResolvedTrackSettings.

    private void AddStraightSection(
        ref TrackGenerationState state,
        float length,
        TrackFeatureType featureType,
        TrackStructureType structureType,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        // Implementación existente sin cambios.
    }

    private void AddStraightRecovery(
        ref TrackGenerationState state,
        float recoveryLength,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        // Implementación existente sin cambios.
    }

    private void ApplyDecision(
        ref TrackGenerationState state,
        TrackGenerationDecision decision,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        // Implementación existente sin cambios.
    }

    private void AddFeatureRecord(TrackSectionDefinition section, List<TrackFeatureRecord> features)
    {
        // Implementación existente sin cambios.
    }

    #endregion
}