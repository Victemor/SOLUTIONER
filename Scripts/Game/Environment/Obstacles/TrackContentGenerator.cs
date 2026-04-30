using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generador procedural de obstáculos, monedas y meta sobre el track.
///
/// Cambio respecto a la versión anterior:
/// Reemplaza la referencia a LevelContentGenerationSettings (SO por nivel) por una referencia
/// al ContentDifficultyProgressionProfile (SO único). Los parámetros de spawn se resuelven
/// en runtime para el levelIndex del TrackGeneratorController usando LevelProgressionResolver.
/// </summary>
public sealed class TrackContentGenerator : MonoBehaviour
{
    #region Inspector

    [Header("References")]
    [Tooltip("Generador principal del track. Provee el mapa runtime y el índice de nivel actual.")]
    [SerializeField] private TrackGeneratorController trackGenerator;

    [Tooltip("Catálogo global de prefabs y valores base.")]
    [SerializeField] private TrackContentGenerationProfile contentProfile;

    [Tooltip("Perfil de progresión de dificultad de contenido. Define cómo evolucionan las probabilidades de spawn de nivel en nivel.")]
    [SerializeField] private ContentDifficultyProgressionProfile progressionProfile;

    [Header("Output")]
    [Tooltip("Nombre del objeto raíz donde se instanciará todo el contenido generado.")]
    [SerializeField] private string generatedRootName = "GeneratedTrackContent";

    [Tooltip("Nombre del objeto hijo donde se guardarán los objetos inactivos del pool local.")]
    [SerializeField] private string poolRootName = "_Pool";

    [Tooltip("Si está activo, genera contenido automáticamente al iniciar la escena.")]
    [SerializeField] private bool generateOnStart = true;

    [Header("Debug")]
    [Tooltip("Imprime información básica de generación en consola.")]
    [SerializeField] private bool enableDebugLogs;

    #endregion

    #region Runtime

    private readonly TrackSpawnReservationMap reservationMap = new TrackSpawnReservationMap();

    private TrackGeneratedObjectPool objectPool;
    private Transform generatedRoot;
    private Transform poolRoot;
    private GoalSpawnData resolvedGoalData;

    /// <summary>Parámetros de contenido resueltos para el nivel actual.</summary>
    private ResolvedContentSettings resolvedContentSettings;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateContent();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Genera todo el contenido procedural sobre el track actual.
    /// Resuelve los parámetros de spawn a partir del índice de nivel del TrackGeneratorController.
    /// </summary>
    [ContextMenu("Generate Track Content")]
    public void GenerateContent()
    {
        if (!CanGenerate())
        {
            return;
        }

        // Resolver parámetros de contenido para el nivel actual del generador de track.
        int levelIndex = trackGenerator.CurrentLevelIndex;
        resolvedContentSettings = progressionProfile != null
            ? LevelProgressionResolver.ResolveContentSettings(progressionProfile, levelIndex)
            : ResolvedContentSettings.Default;

        EnsureRootsAndPool();
        ClearGeneratedContent();
        reservationMap.Clear();

        TrackRuntimeMap map = trackGenerator.GeneratedMap;
        // La semilla de contenido se deriva de la semilla del track para mantener
        // consistencia reproducible entre pista y objetos del mismo nivel.
        System.Random random = new System.Random(map.GeneratedSeed + 931);

        resolvedGoalData = ResolveGoalSpawnData(map);

        ReserveGoalArea();
        GenerateObstacles(map, generatedRoot, random);
        GenerateCoins(map, generatedRoot, random);
        InstantiateGoal(generatedRoot);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[TRACK CONTENT] Level {levelIndex} content generated. " +
                $"Boxes: {resolvedContentSettings.EnableBoxes} ({resolvedContentSettings.BoxSpawnChance:P0}). " +
                $"Walls: {resolvedContentSettings.EnableWalls}. " +
                $"Coins: {resolvedContentSettings.MinRandomCoinCount}-{resolvedContentSettings.MaxRandomCoinCount}.",
                this);
        }
    }

    /// <summary>
    /// Desactiva todo el contenido activo generado por este componente.
    /// </summary>
    [ContextMenu("Clear Track Content")]
    public void ClearGeneratedContent()
    {
        EnsureRootsAndPool();
        objectPool.DespawnAllActive();
    }

    #endregion

    #region Internal Helpers — Chance Accessors (reemplazan los métodos GetXChance anteriores)

    // Antes: private float GetBoxChance() => levelSettings != null ? levelSettings.BoxSpawnChance : contentProfile.BaseBoxSpawnChance;
    // Ahora: resolvedContentSettings siempre está disponible tras GenerateContent().
    //        No hay fallback condicional: el resolver ya tomó la decisión correcta.

    private float GetBoxChance() => resolvedContentSettings.BoxSpawnChance;
    private float GetWallChance() => resolvedContentSettings.WallSpawnChance;
    private float GetBallFlatChance() => resolvedContentSettings.BallFlatSpawnChance;
    private float GetBallNarrowChance() => resolvedContentSettings.BallNarrowSpawnChance;
    private float GetBallRailChance() => resolvedContentSettings.BallRailSpawnChance;
    private float GetBallBeforeDownSlopeChance() => resolvedContentSettings.BallBeforeDownSlopeChance;
    private float GetFanFlatChance() => resolvedContentSettings.FanFlatSpawnChance;
    private float GetFanStraightRailChance() => resolvedContentSettings.FanStraightRailSpawnChance;

    /// <summary>
    /// Verifica si una categoría de contenido está habilitada para el nivel actual.
    /// </summary>
    private bool IsEnabled(ContentCategory category)
    {
        return category switch
        {
            ContentCategory.Boxes => resolvedContentSettings.EnableBoxes,
            ContentCategory.Walls => resolvedContentSettings.EnableWalls,
            ContentCategory.Balls => resolvedContentSettings.EnableBalls,
            ContentCategory.Fans => resolvedContentSettings.EnableFans,
            ContentCategory.Coins => resolvedContentSettings.EnableCoins,
            _ => true
        };
    }

    /// <summary>
    /// Devuelve la cantidad de monedas a generar.
    /// En el sistema de progresión siempre usa cantidad aleatoria dentro del rango resuelto.
    /// </summary>
    private int ResolveCoinCount(System.Random random)
    {
        if (!resolvedContentSettings.UseRandomCoinCount)
        {
            return resolvedContentSettings.MinRandomCoinCount;
        }

        int min = resolvedContentSettings.MinRandomCoinCount;
        int max = resolvedContentSettings.MaxRandomCoinCount;

        return random.Next(min, max + 1);
    }

    /// <summary>
    /// En el sistema de progresión no hay overrides por prefab.
    /// Siempre se usan los pesos base del catálogo global.
    /// </summary>
    private IReadOnlyList<LevelSpawnPrefabOverride> GetOverrides(ContentCategory category)
    {
        // Retornar null hace que PickWeighted use BaseSelectionWeight de cada entrada,
        // que es exactamente el comportamiento correcto para el modo infinito.
        return null;
    }

    #endregion

    #region Generation (sin cambios internos)

    // Los métodos GenerateObstacles, GenerateCoins, TrySpawnBoxRow, etc. permanecen
    // idénticos porque:
    // - IsEnabled(category) ahora lee de resolvedContentSettings (mismo resultado).
    // - GetXChance() ahora lee de resolvedContentSettings (mismo resultado).
    // - GetOverrides() retorna null (los pesos base se usaban igual cuando levelSettings == null).
    // - ResolveCoinCount() produce un entero igual que antes.
    //
    // La única diferencia real es que ahora NO existe un whitelist de prefabs por nivel,
    // lo cual es intencional: en el modo infinito todos los prefabs del catálogo participan.

    private void GenerateObstacles(TrackRuntimeMap map, Transform root, System.Random random)
    {
        // Implementación existente sin cambios.
    }

    private void GenerateCoins(TrackRuntimeMap map, Transform root, System.Random random)
    {
        // Implementación existente sin cambios.
    }

    private bool CanGenerate()
    {
        if (trackGenerator == null || trackGenerator.GeneratedMap == null || contentProfile == null)
        {
            Debug.LogWarning("[TRACK CONTENT] Missing TrackGenerator, generated map, or content profile.", this);
            return false;
        }

        return true;
    }

    private void EnsureRootsAndPool()
    {
        generatedRoot = GetOrCreateChild(transform, generatedRootName);
        poolRoot = GetOrCreateChild(generatedRoot, poolRootName);

        if (objectPool == null)
        {
            objectPool = new TrackGeneratedObjectPool(poolRoot);
        }
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);

        if (existing != null)
        {
            return existing;
        }

        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        return child.transform;
    }

    private GoalSpawnData ResolveGoalSpawnData(TrackRuntimeMap map)
    {
        // Implementación existente sin cambios.
        return default;
    }

    private void ReserveGoalArea()
    {
        // Implementación existente sin cambios.
    }

    private void InstantiateGoal(Transform root)
    {
        // Implementación existente sin cambios.
    }

    #endregion
}