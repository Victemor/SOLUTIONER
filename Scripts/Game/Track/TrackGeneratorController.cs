using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador principal del generador procedural de track.
///
/// En modo de progresión infinita, la generación es iniciada exclusivamente por
/// <see cref="InfiniteLevelManager"/>, que inyecta los <see cref="LevelGenerationSettings"/>
/// ya configurados a través de <see cref="GenerateLevel(LevelGenerationSettings)"/>.
/// Los settings no se asignan en el Inspector de este componente.
/// </summary>
public sealed class TrackGeneratorController : MonoBehaviour
{
    #region Inspector

    [Header("Configuration")]
    [SerializeField]
    [Tooltip("Perfil base de generación del track. Define los valores máximos de todas las probabilidades.")]
    private TrackGenerationProfile generationProfile;

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
    [Tooltip("Multiplicador aplicado al radio físico de cada riel. Mantenerlo cerca de 1 evita que la bola se atasque entre rieles.")]
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
    [Tooltip("Si está activo, genera automáticamente al cargar la escena. " +
             "InfiniteLevelManager lo desactiva en su Awake.")]
    private bool generateOnStart = true;

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

    /// <summary>
    /// Settings de nivel activos en la generación actual.
    /// Inyectados por <see cref="InfiniteLevelManager"/> vía <see cref="GenerateLevel(LevelGenerationSettings)"/>.
    /// </summary>
    private LevelGenerationSettings activeSettings;

    /// <summary>
    /// Profile visual temporal para sobreescribir solo materiales (ej: nivel bonus).
    /// Si es null, se usa <see cref="generationProfile"/> para todo.
    /// </summary>
    private TrackGenerationProfile activeVisualProfile;
    

    #endregion

    #region Properties

    /// <summary>
    /// Mapa runtime generado actualmente.
    /// </summary>
    public TrackRuntimeMap GeneratedMap => generatedMap;

    public TrackGenerationProfile GenerationProfile => generationProfile;
    
    /// <summary>
    /// Generador de barreras. InfiniteLevelManager lo usa para inyectar
    /// la probabilidad de barreras antes de cada generación.
    /// </summary>
    public TrackBarrierGenerator BarrierGenerator => trackBarrierGenerator;
    

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (generateOnStart)
        {
            Debug.LogWarning(
                "[TRACK] generateOnStart está activo pero no hay settings inyectados. " +
                "Asigna un InfiniteLevelManager en la escena o desactiva generateOnStart.",
                this);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Desactiva la generación automática al inicio de escena.
    /// <see cref="InfiniteLevelManager"/> lo llama en su Awake para tomar control del ciclo.
    /// </summary>
    public void DisableAutoGeneration()
    {
        generateOnStart = false;
    }

    /// <summary>
    /// Inyecta los settings del nivel actual y genera el track procedural completo.
    /// Punto de entrada principal desde <see cref="InfiniteLevelManager"/>.
    /// </summary>
    /// <param name="settings">Settings configurados para el nivel actual.</param>
    public void GenerateLevel(LevelGenerationSettings settings)
    {
        if (settings == null)
        {
            Debug.LogWarning("[TRACK] Se llamó GenerateLevel sin settings. Generación cancelada.", this);
            return;
        }

        activeSettings = settings;
        GenerateLevel();
    }

    /// <summary>
    /// Sobreescribe temporalmente el profile de materiales visuales para la próxima generación.
    /// Usado para niveles bonus que necesitan texturas de suelo distintas.
    /// </summary>
    public void SetVisualProfileOverride(TrackGenerationProfile profile)
    {
        activeVisualProfile = profile;
    }
    
    /// <summary>
    /// Elimina el override de profile visual, volviendo al profile base del Inspector.
    /// </summary>
    public void ClearVisualProfileOverride()
    {
        activeVisualProfile = null;
    }

    /// <summary>
    /// Genera el nivel usando los settings activos actuales.
    /// Requiere que <see cref="GenerateLevel(LevelGenerationSettings)"/> haya sido llamado
    /// al menos una vez para inyectar los settings.
    /// </summary>
    [ContextMenu("Generate Level")]
    public void GenerateLevel()
    {
        if (generationProfile == null)
        {
            Debug.LogWarning("[TRACK] generationProfile no asignado.", this);
            return;
        }

        if (activeSettings == null)
        {
            Debug.LogWarning("[TRACK] No hay settings activos. Llama GenerateLevel(LevelGenerationSettings) primero.", this);
            return;
        }

        ValidateInspectorData();
        LogConfigurationWarnings();
        ClearGeneratedVisuals();

        int resolvedSeed = ResolveSeed(activeSettings);
        System.Random random = new System.Random(resolvedSeed);

        float targetLength = generationProfile.TargetTrackLength * activeSettings.LengthMultiplier;

        float minTrackHeight = activeSettings.OverrideMinHeight
            ? activeSettings.MinHeightOverride
            : generationProfile.MinTrackHeight;

        float maxTrackHeight = activeSettings.OverrideMaxHeight
            ? activeSettings.MaxHeightOverride
            : generationProfile.MaxTrackHeight;

        List<TrackSectionDefinition> sections = new List<TrackSectionDefinition>();
        List<TrackFeatureRecord> features = new List<TrackFeatureRecord>();

        TrackGenerationState state = CreateInitialState();

        GenerateSafeStart(ref state, sections, features, generationProfile, activeSettings);

        while (state.GeneratedLength < targetLength - ResolveSafeEndLength())
        {
            TrackGenerationDecision decision = ruleEvaluator.EvaluateNextDecision(
                ref state,
                generationProfile,
                activeSettings,
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

        GenerateSafeEnd(ref state, targetLength, sections, features);

        List<TrackSplinePoint> splinePoints =
            TrackSplineBuilder.BuildSplinePoints(sections, generationProfile);

        List<TrackSurfaceChunkDefinition> surfaceChunks =
            TrackLayoutBuilder.BuildSurfaceChunks(splinePoints);

        TrackPathSampler pathSampler = new TrackPathSampler();
        pathSampler.Rebuild(surfaceChunks);

        BuildGeneratedVisuals(surfaceChunks);

        generatedMap = new TrackRuntimeMap(
            resolvedSeed,
            sections,
            features,
            surfaceChunks,
            pathSampler);

        if (trackBarrierGenerator != null)
        {
            trackBarrierGenerator.Rebuild(generatedMap, activeSettings);
        }

        if (voidZoneGenerator != null)
        {
            voidZoneGenerator.Rebuild();
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[TRACK] Generated level with seed {resolvedSeed}. " +
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

    #region Generation Core

    /// <summary>
    /// Crea el estado inicial del generador.
    /// </summary>
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
    /// Genera la zona recta segura inicial.
    /// </summary>
    private void GenerateSafeStart(
        ref TrackGenerationState state,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features,
        TrackGenerationProfile profile,
        LevelGenerationSettings settings)
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
    /// Genera la zona recta segura final y cierra el nivel.
    /// </summary>
    private void GenerateSafeEnd(
        ref TrackGenerationState state,
        float targetLength,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        float remaining = Mathf.Max(0f, targetLength - state.GeneratedLength);

        if (remaining <= 0f)
        {
            return;
        }

        state.CurrentStructureType = TrackStructureType.SolidTrack;
        state.IsInsideRailSequence = false;
        state.CurrentRailSectionCount = 0;
        state.CurrentWidthRatio = 1f;

        AddStraightSection(
            ref state,
            remaining,
            TrackFeatureType.Finish,
            TrackStructureType.SolidTrack,
            sections,
            features);
    }

    /// <summary>
    /// Aplica una decisión al estado actual y genera sus secciones correspondientes.
    /// </summary>
    private void ApplyDecision(
        ref TrackGenerationState state,
        TrackGenerationDecision decision,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        switch (decision.FeatureType)
        {
            case TrackFeatureType.Straight:
                AddStraightSection(
                    ref state,
                    decision.ChangeLength,
                    TrackFeatureType.Straight,
                    state.CurrentStructureType,
                    sections,
                    features);
                break;

            case TrackFeatureType.LateralEnterLeft45:
            case TrackFeatureType.LateralEnterLeft90:
            case TrackFeatureType.LateralEnterRight45:
            case TrackFeatureType.LateralEnterRight90:
            case TrackFeatureType.LateralReturnToCenterFromLeft45:
            case TrackFeatureType.LateralReturnToCenterFromLeft90:
            case TrackFeatureType.LateralReturnToCenterFromRight45:
            case TrackFeatureType.LateralReturnToCenterFromRight90:
                AddLateralSection(
                    ref state,
                    decision.ChangeLength,
                    decision.FeatureType,
                    state.CurrentStructureType,
                    sections,
                    features);
                break;

            case TrackFeatureType.SlopeUp:
            case TrackFeatureType.SlopeDown:
                AddVerticalSection(
                    ref state,
                    decision.ChangeLength,
                    decision.FeatureType,
                    decision.VerticalDelta,
                    sections,
                    features);
                break;

            case TrackFeatureType.NarrowStart:
                AddNarrowStartSection(
                    ref state,
                    decision.ChangeLength,
                    decision.TargetWidthRatio,
                    sections,
                    features);
                break;

            case TrackFeatureType.NarrowEnd:
                AddNarrowEndSection(
                    ref state,
                    decision.ChangeLength,
                    sections,
                    features);
                break;

            case TrackFeatureType.Gap:
                AddPreGapRampSection(
                    ref state,
                    decision.PreGapRampLength,
                    decision.PreGapRampHeight,
                    sections,
                    features);

                AddGapSection(
                    ref state,
                    decision.ChangeLength,
                    sections,
                    features);
                break;

            case TrackFeatureType.RailStart:
                AddRailStartSection(
                    ref state,
                    decision.ChangeLength,
                    decision.RailSeparation,
                    decision.RailWidth,
                    sections,
                    features);
                break;

            case TrackFeatureType.RailSegment:
                AddRailSegmentSection(
                    ref state,
                    decision.ChangeLength,
                    decision.RailSeparation,
                    decision.RailWidth,
                    sections,
                    features);
                break;

            case TrackFeatureType.RailEnd:
                AddRailEndSection(
                    ref state,
                    decision.ChangeLength,
                    decision.RailSeparation,
                    decision.RailWidth,
                    sections,
                    features);
                break;

            case TrackFeatureType.Finish:
                AddStraightSection(
                    ref state,
                    decision.ChangeLength,
                    TrackFeatureType.Finish,
                    TrackStructureType.SolidTrack,
                    sections,
                    features);
                break;
        }
    }

    /// <summary>
    /// Agrega la recta de recuperación posterior a un cambio.
    /// </summary>
    private void AddStraightRecovery(
        ref TrackGenerationState state,
        float recoveryLength,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        AddStraightSection(
            ref state,
            recoveryLength,
            TrackFeatureType.Straight,
            state.CurrentStructureType,
            sections,
            features);
    }

    #endregion

    #region Section Builders

    /// <summary>
    /// Agrega una sección recta simple.
    /// </summary>
    private void AddStraightSection(
        ref TrackGenerationState state,
        float length,
        TrackFeatureType featureType,
        TrackStructureType structureType,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);
        if (length <= 0.0001f)
        {
            return;
        }

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + (state.CurrentForward * length);
        endPosition.y = state.CurrentHeight;

        float startWidth = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = featureType,
            StructureType = structureType,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = startWidth,
            EndWidth = startWidth,
            TargetWidthRatio = state.CurrentWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = structureType != TrackStructureType.Gap,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = structureType == TrackStructureType.RailTrack ? generationProfile.RailSeparation : 0f,
            RailWidth = structureType == TrackStructureType.RailTrack ? generationProfile.RailWidth : 0f,
            StartsFromCutCenter = false,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        AdvanceStateStraight(ref state, length, endPosition, structureType);
    }

    /// <summary>
    /// Agrega una sección de cambio lateral.
    /// </summary>
    private void AddLateralSection(
        ref TrackGenerationState state,
        float length,
        TrackFeatureType featureType,
        TrackStructureType structureType,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);
        if (length <= 0.0001f)
        {
            return;
        }

        float signedAngle = TrackFeatureUtility.GetSignedTurnAngle(featureType);
        float endYawOffset = GetEndYawOffsetForFeature(featureType, state.CurrentYawOffsetDegrees);
        TrackLateralState endLateralState = GetEndLateralStateForYaw(endYawOffset);

        float radius = generationProfile.CurveRadius;
        Vector3 startPosition = state.CurrentPosition;
        Vector3 startForward = state.CurrentForward;
        startForward.y = 0f;
        startForward.Normalize();

        Vector3 startRight = Vector3.Cross(Vector3.up, startForward).normalized;
        float turnSign = Mathf.Sign(signedAngle);

        Vector3 center = startPosition + (startRight * radius * turnSign);
        Vector3 radialStart = startPosition - center;
        Vector3 radialEnd = Quaternion.AngleAxis(signedAngle, Vector3.up) * radialStart;
        Vector3 endPosition = center + radialEnd;
        endPosition.y = state.CurrentHeight;

        Vector3 endForward = Quaternion.AngleAxis(signedAngle, Vector3.up) * startForward;
        endForward.y = 0f;
        endForward.Normalize();

        float width = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = featureType,
            StructureType = structureType,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = startForward,
            EndForward = endForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = width,
            EndWidth = width,
            TargetWidthRatio = state.CurrentWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = endLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = structureType != TrackStructureType.Gap,
            TurnAngleDegrees = signedAngle,
            TurnRadius = radius,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = structureType == TrackStructureType.RailTrack ? generationProfile.RailSeparation : 0f,
            RailWidth = structureType == TrackStructureType.RailTrack ? generationProfile.RailWidth : 0f,
            StartsFromCutCenter = false,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentLateralState = endLateralState;
        state.CurrentYawOffsetDegrees = endYawOffset;
        state.CurrentPosition = endPosition;
        state.CurrentForward = endForward;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange = 0f;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = structureType;

        if (structureType == TrackStructureType.RailTrack)
        {
            state.CurrentRailSectionCount++;
            state.IsInsideRailSequence = true;
        }
    }

    /// <summary>
    /// Agrega una sección de cambio vertical normal.
    /// </summary>
    private void AddVerticalSection(
        ref TrackGenerationState state,
        float length,
        TrackFeatureType featureType,
        float verticalDelta,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);
        if (length <= 0.0001f)
        {
            return;
        }

        TrackStructureType structureType = state.CurrentStructureType;

        Vector3 startPosition = state.CurrentPosition;
        float endHeight = state.CurrentHeight + verticalDelta;

        Vector3 endPosition = startPosition + (state.CurrentForward * length);
        endPosition.y = endHeight;

        float width = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackVerticalState endVerticalState = verticalDelta >= 0f
            ? TrackVerticalState.Ascending
            : TrackVerticalState.Descending;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = featureType,
            StructureType = structureType,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = endHeight,
            StartWidth = width,
            EndWidth = width,
            TargetWidthRatio = state.CurrentWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = endVerticalState,
            HasSurface = true,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = verticalDelta,
            RampHeightDelta = 0f,
            RailSeparation = 0f,
            RailWidth = 0f,
            StartsFromCutCenter = false,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentHeight = endHeight;
        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange = 0f;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = endVerticalState;
        state.CurrentStructureType = structureType;
    }

    /// <summary>
    /// Agrega la transición de inicio de estrechamiento.
    /// </summary>
    private void AddNarrowStartSection(
        ref TrackGenerationState state,
        float length,
        float targetWidthRatio,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(length, generationProfile.NarrowTransitionLength);

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + (state.CurrentForward * length);
        endPosition.y = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = TrackFeatureType.NarrowStart,
            StructureType = TrackStructureType.SolidTrack,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = ResolveWidthFromRatio(state.CurrentWidthRatio),
            EndWidth = ResolveWidthFromRatio(targetWidthRatio),
            TargetWidthRatio = targetWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = true,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = 0f,
            RailWidth = 0f,
            StartsFromCutCenter = false,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentWidthRatio = targetWidthRatio;
        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange = 0f;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = TrackStructureType.SolidTrack;
    }

    /// <summary>
    /// Agrega la transición de salida de estrechamiento.
    /// </summary>
    private void AddNarrowEndSection(
        ref TrackGenerationState state,
        float length,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(length, generationProfile.NarrowTransitionLength);

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + (state.CurrentForward * length);
        endPosition.y = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = TrackFeatureType.NarrowEnd,
            StructureType = TrackStructureType.SolidTrack,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = ResolveWidthFromRatio(state.CurrentWidthRatio),
            EndWidth = ResolveWidthFromRatio(1f),
            TargetWidthRatio = 1f,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = true,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = 0f,
            RailWidth = 0f,
            StartsFromCutCenter = false,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentWidthRatio = 1f;
        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange = 0f;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = TrackStructureType.SolidTrack;
    }

    /// <summary>
    /// Agrega la mini rampa previa al gap.
    /// </summary>
    private void AddPreGapRampSection(
        ref TrackGenerationState state,
        float rampLength,
        float rampHeight,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        if (rampLength <= 0f || Mathf.Abs(rampHeight) <= 0.001f)
        {
            return;
        }

        AddVerticalSection(ref state, rampLength, TrackFeatureType.SlopeUp, rampHeight, sections, features);
    }

    /// <summary>
    /// Agrega un hueco (gap) en la pista.
    /// </summary>
    private void AddGapSection(
        ref TrackGenerationState state,
        float length,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);
        if (length <= 0.0001f)
        {
            return;
        }

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + (state.CurrentForward * length);
        endPosition.y = state.CurrentHeight;

        float width = ResolveWidthFromRatio(state.CurrentWidthRatio);

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = TrackFeatureType.Gap,
            StructureType = TrackStructureType.Gap,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = width,
            EndWidth = width,
            TargetWidthRatio = state.CurrentWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = TrackVerticalState.Flat,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = false,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = 0f,
            RailWidth = 0f,
            StartsFromCutCenter = true,
            EndsAtCutCenter = true
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap = 0f;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = TrackStructureType.SolidTrack;
    }

    /// <summary>
    /// Agrega la entrada a la estructura rail.
    /// </summary>
    private void AddRailStartSection(
        ref TrackGenerationState state,
        float length,
        float railSeparation,
        float railWidth,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);
        if (length <= 0.0001f)
        {
            return;
        }

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + (state.CurrentForward * length);
        endPosition.y = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = TrackFeatureType.RailStart,
            StructureType = TrackStructureType.RailTrack,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = generationProfile.NormalTrackWidth,
            EndWidth = generationProfile.NormalTrackWidth,
            TargetWidthRatio = state.CurrentWidthRatio,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = true,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = railSeparation,
            RailWidth = railWidth,
            StartsFromCutCenter = true,
            EndsAtCutCenter = false
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail = 0f;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = TrackStructureType.RailTrack;
        state.IsInsideRailSequence = true;
        state.CurrentRailSectionCount = 1;
        state.CurrentWidthRatio = 1f;
    }

    /// <summary>
    /// Agrega una sección interna de rail.
    /// </summary>
    private void AddRailSegmentSection(
        ref TrackGenerationState state,
        float length,
        float railSeparation,
        float railWidth,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        AddStraightSection(
            ref state,
            length,
            TrackFeatureType.RailSegment,
            TrackStructureType.RailTrack,
            sections,
            features);

        TrackSectionDefinition last = sections[sections.Count - 1];
        last.RailSeparation = railSeparation;
        last.RailWidth = railWidth;
        sections[sections.Count - 1] = last;

        TrackFeatureRecord lastFeature = features[features.Count - 1];
        lastFeature.StructureType = TrackStructureType.RailTrack;
        features[features.Count - 1] = lastFeature;

        state.CurrentStructureType = TrackStructureType.RailTrack;
        state.IsInsideRailSequence = true;
        state.CurrentRailSectionCount++;
        state.DistanceSinceLastRail = 0f;
    }

    /// <summary>
    /// Agrega la salida de rail hacia pista sólida.
    /// </summary>
    private void AddRailEndSection(
        ref TrackGenerationState state,
        float length,
        float railSeparation,
        float railWidth,
        List<TrackSectionDefinition> sections,
        List<TrackFeatureRecord> features)
    {
        length = Mathf.Max(0f, length);
        if (length <= 0.0001f)
        {
            return;
        }

        Vector3 startPosition = state.CurrentPosition;
        Vector3 endPosition = startPosition + (state.CurrentForward * length);
        endPosition.y = state.CurrentHeight;

        TrackSectionDefinition section = new TrackSectionDefinition
        {
            FeatureType = TrackFeatureType.RailEnd,
            StructureType = TrackStructureType.RailTrack,
            StartDistance = state.GeneratedLength,
            EndDistance = state.GeneratedLength + length,
            Length = length,
            StartPosition = startPosition,
            EndPosition = endPosition,
            StartForward = state.CurrentForward,
            EndForward = state.CurrentForward,
            StartHeight = state.CurrentHeight,
            EndHeight = state.CurrentHeight,
            StartWidth = generationProfile.NormalTrackWidth,
            EndWidth = generationProfile.NormalTrackWidth,
            TargetWidthRatio = 1f,
            LateralStateBefore = state.CurrentLateralState,
            LateralStateAfter = state.CurrentLateralState,
            VerticalStateBefore = state.CurrentVerticalState,
            VerticalStateAfter = TrackVerticalState.Flat,
            HasSurface = true,
            TurnAngleDegrees = 0f,
            TurnRadius = 0f,
            SlopeHeightDelta = 0f,
            RampHeightDelta = 0f,
            RailSeparation = railSeparation,
            RailWidth = railWidth,
            StartsFromCutCenter = false,
            EndsAtCutCenter = true
        };

        sections.Add(section);
        AddFeatureRecord(section, features);

        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail = 0f;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = TrackStructureType.SolidTrack;
        state.IsInsideRailSequence = false;
        state.CurrentRailSectionCount = 0;
        state.CurrentWidthRatio = 1f;
    }

    #endregion

    #region Visual Build

    /// <summary>
    /// Construye los objetos visuales y físicos de todos los chunks sólidos.
    /// </summary>
    private void BuildGeneratedVisuals(IReadOnlyList<TrackSurfaceChunkDefinition> surfaceChunks)
    {
        if (surfaceChunks == null || surfaceChunks.Count == 0)
        {
            return;
        }

        Transform root = GetOrCreateGeneratedRoot();

        for (int i = 0; i < surfaceChunks.Count; i++)
        {
            CreateChunkObject(surfaceChunks[i], root);
        }
    }

    /// <summary>
    /// Crea el GameObject asociado a un chunk.
    /// </summary>
    private void CreateChunkObject(TrackSurfaceChunkDefinition chunk, Transform parent)
    {
        TrackMeshBuilder.TrackMeshBuildResult result =
            TrackMeshBuilder.BuildChunkMesh(chunk, activeVisualProfile ?? generationProfile);

        GameObject chunkObject = new GameObject($"{chunkObjectNamePrefix}{chunk.ChunkIndex:D2}");
        AssignGeneratedLayer(chunkObject);
        chunkObject.transform.SetParent(parent);
        chunkObject.transform.localPosition = Vector3.zero;
        chunkObject.transform.localRotation = Quaternion.identity;
        chunkObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh = result.Mesh;
        meshRenderer.sharedMaterials = result.Materials;

        if (generateMeshColliders && result.Mesh != null)
        {
            CreateChunkPhysics(chunk, chunkObject, result.Mesh);
        }
    }

    /// <summary>
    /// Crea la física del chunk según su estructura.
    /// </summary>
    private void CreateChunkPhysics(TrackSurfaceChunkDefinition chunk, GameObject chunkObject, Mesh mesh)
    {
        if (chunk.StructureType == TrackStructureType.RailTrack && usePrimitiveRailColliders)
        {
            CreateRailPrimitiveColliders(chunk, chunkObject.transform);
            return;
        }

        MeshCollider meshCollider = chunkObject.AddComponent<MeshCollider>();
        meshCollider.cookingOptions =
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.CookForFasterSimulation;

        meshCollider.sharedMesh = mesh;
    }

    /// <summary>
    /// Crea colliders primitivos para los dos rieles del chunk.
    /// </summary>
    private void CreateRailPrimitiveColliders(TrackSurfaceChunkDefinition chunk, Transform parent)
    {
        if (chunk == null || chunk.Samples == null || chunk.Samples.Count < 2 || parent == null)
        {
            return;
        }

        CreateSingleRailPrimitiveColliders(chunk, parent, -1f, "Left");
        CreateSingleRailPrimitiveColliders(chunk, parent, 1f, "Right");
    }

    /// <summary>
    /// Crea colliders primitivos para uno de los dos rieles.
    /// </summary>
    private void CreateSingleRailPrimitiveColliders(
        TrackSurfaceChunkDefinition chunk,
        Transform parent,
        float sideSign,
        string sideName)
    {
        for (int i = 0; i < chunk.Samples.Count - 1; i++)
        {
            TrackLayoutSamplePoint startSample = chunk.Samples[i];
            TrackLayoutSamplePoint endSample = chunk.Samples[i + 1];

            ResolveRailColliderFrame(startSample, sideSign, chunk.StartDistance, chunk.EndDistance,
                out Vector3 startCenter, out float startRadius);

            ResolveRailColliderFrame(endSample, sideSign, chunk.StartDistance, chunk.EndDistance,
                out Vector3 endCenter, out float endRadius);

            Vector3 segment = endCenter - startCenter;
            float segmentLength = segment.magnitude;

            if (segmentLength <= 0.05f)
            {
                continue;
            }

            float radius = Mathf.Max(
                minimumRailColliderRadius,
                Mathf.Min(startRadius, endRadius) * railColliderRadiusMultiplier);

            CreateRailCapsuleCollider(
                parent,
                startCenter,
                endCenter,
                radius,
                segmentLength,
                $"RailCollider_{sideName}_{i:D3}");
        }
    }

    /// <summary>
    /// Resuelve el centro y radio para un frame de collider de rail.
    /// </summary>
    private static void ResolveRailColliderFrame(
        TrackLayoutSamplePoint sample,
        float sideSign,
        float chunkStart,
        float chunkEnd,
        out Vector3 center,
        out float radius)
    {
        float separation = Mathf.Max(0f, sample.RailSeparation);
        float railWidth = Mathf.Max(0.01f, sample.RailWidth);

        Vector3 right = ResolveSafeRight(sample.Right, sample.Forward);
        center = sample.Position + right * (separation * 0.5f * sideSign);
        radius = railWidth * 0.5f;
    }

    /// <summary>
    /// Resuelve un right seguro para el frame del rail.
    /// </summary>
    private static Vector3 ResolveSafeRight(Vector3 right, Vector3 forward)
    {
        if (right.sqrMagnitude >= 0.0001f)
        {
            return right.normalized;
        }

        Vector3 horizontalForward = new Vector3(forward.x, 0f, forward.z);

        if (horizontalForward.sqrMagnitude < 0.0001f)
        {
            return Vector3.right;
        }

        horizontalForward.Normalize();
        Vector3 resolvedRight = Vector3.Cross(Vector3.up, horizontalForward);

        return resolvedRight.sqrMagnitude < 0.0001f ? Vector3.right : resolvedRight.normalized;
    }

    /// <summary>
    /// Crea una cápsula física alineada entre dos centros de rail.
    /// </summary>
    private void CreateRailCapsuleCollider(
        Transform parent,
        Vector3 startCenter,
        Vector3 endCenter,
        float radius,
        float segmentLength,
        string objectName)
    {
        Vector3 direction = (endCenter - startCenter).normalized;
        Vector3 midPoint = (startCenter + endCenter) * 0.5f;

        GameObject capsuleObject = new GameObject(objectName);
        AssignGeneratedLayer(capsuleObject);
        capsuleObject.transform.SetParent(parent);
        capsuleObject.transform.position = midPoint;
        capsuleObject.transform.rotation = ResolveCapsuleRotation(direction);
        capsuleObject.transform.localScale = Vector3.one;

        CapsuleCollider capsule = capsuleObject.AddComponent<CapsuleCollider>();
        capsule.radius = radius;
        capsule.height = segmentLength + radius * 2f;
        capsule.direction = 2; // Z axis

        if (railPhysicMaterial != null)
        {
            capsule.sharedMaterial = railPhysicMaterial;
        }
    }

    /// <summary>
    /// Resuelve la rotación de una cápsula a partir de su dirección.
    /// </summary>
    private static Quaternion ResolveCapsuleRotation(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Quaternion.identity;
        }

        Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f
            ? Vector3.forward
            : Vector3.up;

        return Quaternion.LookRotation(direction, up);
    }

    /// <summary>
    /// Obtiene o crea el contenedor raíz de lo generado.
    /// </summary>
    private Transform GetOrCreateGeneratedRoot()
    {
        Transform existingRoot = transform.Find(generatedRootName);

        if (existingRoot != null)
        {
            return existingRoot;
        }

        GameObject root = new GameObject(generatedRootName);
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        return root.transform;
    }

    /// <summary>
    /// Elimina todos los objetos generados previamente.
    /// </summary>
    private void ClearGeneratedVisuals()
    {
        Transform existingRoot = transform.Find(generatedRootName);

        if (existingRoot == null)
        {
            return;
        }

        List<GameObject> children = new List<GameObject>();

        for (int i = 0; i < existingRoot.childCount; i++)
        {
            children.Add(existingRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < children.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(children[i]);
            }
            else
            {
                Destroy(children[i]);
            }
#else
            Destroy(children[i]);
#endif
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Añade un registro de feature a partir de una sección.
    /// </summary>
    private void AddFeatureRecord(TrackSectionDefinition section, List<TrackFeatureRecord> features)
    {
        features.Add(new TrackFeatureRecord
        {
            FeatureType = section.FeatureType,
            StructureType = section.StructureType,
            StartDistance = section.StartDistance,
            EndDistance = section.EndDistance,
            StartPosition = section.StartPosition,
            EndPosition = section.EndPosition,
            CenterPosition = Vector3.Lerp(section.StartPosition, section.EndPosition, 0.5f),
            LateralState = section.LateralStateAfter,
            VerticalState = section.VerticalStateAfter,
            WidthRatio = section.TargetWidthRatio,
            HasSurface = section.HasSurface
        });
    }

    /// <summary>
    /// Avanza el estado tras una sección recta.
    /// </summary>
    private void AdvanceStateStraight(
        ref TrackGenerationState state,
        float length,
        Vector3 endPosition,
        TrackStructureType structureType)
    {
        state.CurrentPosition = endPosition;
        state.GeneratedLength += length;
        state.DistanceSinceLastLateralChange += length;
        state.DistanceSinceLastVerticalChange += length;
        state.DistanceSinceLastWidthChange += length;
        state.DistanceSinceLastGap += length;
        state.DistanceSinceLastRail += length;
        state.CurrentVerticalState = TrackVerticalState.Flat;
        state.CurrentStructureType = structureType;

        if (structureType == TrackStructureType.RailTrack)
        {
            state.IsInsideRailSequence = true;
            state.CurrentRailSectionCount++;
            state.DistanceSinceLastRail = 0f;
        }
    }

    /// <summary>
    /// Resuelve el ancho real a partir de una relación respecto al ancho normal.
    /// </summary>
    private float ResolveWidthFromRatio(float widthRatio)
    {
        return generationProfile.NormalTrackWidth * Mathf.Max(0.01f, widthRatio);
    }

    /// <summary>
    /// Resuelve la semilla final del nivel a partir de los settings activos.
    /// </summary>
    private static int ResolveSeed(LevelGenerationSettings settings)
    {
        if (settings.UseFixedSeed)
        {
            return settings.FixedSeed;
        }

        return System.Environment.TickCount;
    }

    /// <summary>
    /// Resuelve la longitud segura final efectiva.
    /// </summary>
    private float ResolveSafeEndLength()
    {
        if (activeSettings.SafeEndLengthOverride > 0f)
        {
            return activeSettings.SafeEndLengthOverride;
        }

        return generationProfile.SafeEndLength;
    }

    /// <summary>
    /// Asigna la layer configurada a un objeto generado.
    /// </summary>
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
                $"[TRACK] La layer '{generatedTrackLayerName}' no existe. Se usará la layer actual del objeto.",
                this);

            return;
        }

        generatedObject.layer = layer;
    }

    /// <summary>
    /// Valida y normaliza datos de Inspector básicos.
    /// </summary>
    private void ValidateInspectorData()
    {
        generatedRootName = string.IsNullOrWhiteSpace(generatedRootName)
            ? "GeneratedTrack"
            : generatedRootName;

        chunkObjectNamePrefix = string.IsNullOrWhiteSpace(chunkObjectNamePrefix)
            ? "TrackChunk_"
            : chunkObjectNamePrefix;

        railColliderRadiusMultiplier = Mathf.Max(0.1f, railColliderRadiusMultiplier);
        minimumRailColliderRadius = Mathf.Max(0.01f, minimumRailColliderRadius);

        generatedTrackLayerName = string.IsNullOrWhiteSpace(generatedTrackLayerName)
            ? "Ground"
            : generatedTrackLayerName;
    }

    /// <summary>
    /// Imprime warnings de configuración del perfil y settings activos.
    /// </summary>
    private void LogConfigurationWarnings()
    {
        TrackGenerationValidationUtility.LogWarnings(
            this,
            TrackGenerationValidationUtility.CollectProfileWarnings(generationProfile));

        TrackGenerationValidationUtility.LogWarnings(
            this,
            TrackGenerationValidationUtility.CollectLevelSettingsWarnings(activeSettings));
    }

    /// <summary>
    /// Resuelve el offset angular lateral final a partir del feature elegido.
    /// </summary>
    private static float GetEndYawOffsetForFeature(TrackFeatureType featureType, float currentYawOffset)
    {
        return featureType switch
        {
            TrackFeatureType.LateralEnterLeft45 => -45f,
            TrackFeatureType.LateralEnterLeft90 => -90f,
            TrackFeatureType.LateralEnterRight45 => 45f,
            TrackFeatureType.LateralEnterRight90 => 90f,
            TrackFeatureType.LateralReturnToCenterFromLeft45 => 0f,
            TrackFeatureType.LateralReturnToCenterFromLeft90 => 0f,
            TrackFeatureType.LateralReturnToCenterFromRight45 => 0f,
            TrackFeatureType.LateralReturnToCenterFromRight90 => 0f,
            _ => currentYawOffset
        };
    }

    /// <summary>
    /// Resuelve el estado lateral final a partir del yaw lateral acumulado.
    /// </summary>
    private static TrackLateralState GetEndLateralStateForYaw(float yawOffset)
    {
        if (Mathf.Approximately(yawOffset, 0f))
        {
            return TrackLateralState.Center;
        }

        return yawOffset < 0f ? TrackLateralState.Left : TrackLateralState.Right;
    }

    #endregion
}