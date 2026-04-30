using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera barreras cilíndricas laterales y una pared inicial independiente usando el mapa runtime final.
/// </summary>
public sealed class TrackBarrierGenerator : MonoBehaviour
{
    #region Inspector

    [Header("References")]
    [SerializeField]
    [Tooltip("Generador principal del track.")]
    private TrackGeneratorController trackGenerator;

    [SerializeField]
    [Tooltip("Configuración específica del nivel actual.")]
    private LevelGenerationSettings levelGenerationSettings;

    [Header("Generated Root")]
    [SerializeField]
    [Tooltip("Nombre del contenedor raíz de las barreras generadas.")]
    private string generatedRootName = "GeneratedTrackBarriers";

    [SerializeField]
    [Tooltip("Layer asignada automáticamente a todas las barreras generadas.")]
    private string barrierLayerName = "Barrier";

    [Header("Cylindrical Side Barriers")]
    [SerializeField]
    [Tooltip("Material propio usado por las barreras cilíndricas laterales.")]
    private Material cylindricalBarrierMaterial;

    [SerializeField]
    [Tooltip("Radio visual de las barreras cilíndricas laterales.")]
    private float cylindricalBarrierRadius = 0.18f;

    [SerializeField]
    [Tooltip("Cantidad de segmentos radiales de las barreras cilíndricas.")]
    private int cylindricalBarrierRadialSegments = 12;

    [SerializeField]
    [Tooltip("Distancia lateral desde el borde de referencia hasta el centro del cilindro.")]
    private float cylindricalBarrierLateralOffset = 0.18f;

    [SerializeField]
    [Tooltip("Offset vertical del centro del cilindro respecto a la superficie del track.")]
    private float cylindricalBarrierVerticalOffset = 0.45f;

    [SerializeField]
    [Tooltip("Iteraciones de suavizado aplicadas a los anchors de la barrera cilíndrica.")]
    private int cylindricalBarrierSmoothingIterations = 2;

    [Header("Cylindrical Barrier End Posts")]
    [SerializeField]
    [Tooltip("Si está activo, cada tramo de barrera genera postes verticales al inicio y al final.")]
    private bool generateCylindricalBarrierEndPosts = true;

    [SerializeField]
    [Tooltip("Offset vertical de la base de los postes de inicio y final.")]
    private float cylindricalBarrierPostBaseVerticalOffset = 0.02f;

    [Header("Start Wall")]
    [SerializeField]
    [Tooltip("Si está activo, se genera una pared inicial independiente al inicio del nivel.")]
    private bool generateStartWall = true;

    [SerializeField]
    [Tooltip("Material propio usado por la pared inicial.")]
    private Material startWallMaterial;

    [SerializeField]
    [Tooltip("Ancho de la pared inicial. Si está en 0 o menos, usa el ancho del primer sample.")]
    private float startWallWidthOverride = -1f;

    [SerializeField]
    [Tooltip("Altura de la pared inicial.")]
    private float startWallHeight = 2f;

    [SerializeField]
    [Tooltip("Grosor de la pared inicial en dirección del track.")]
    private float startWallThickness = 0.35f;

    [SerializeField]
    [Tooltip("Offset longitudinal desde el primer sample. Valores negativos la colocan antes del inicio.")]
    private float startWallForwardOffset = -0.25f;

    [SerializeField]
    [Tooltip("Offset vertical de la base de la pared inicial.")]
    private float startWallVerticalOffset = 0f;

    [Header("General Distribution")]
    [SerializeField]
    [Tooltip("Probabilidad de que un chunk elegible reciba barreras laterales generales.")]
    [Range(0f, 1f)]
    private float generalBarrierChance = 0.55f;

    [SerializeField]
    [Tooltip("Porcentaje máximo aproximado del mapa que puede recibir barreras laterales generales, sin contar zonas seguras.")]
    [Range(0f, 1f)]
    private float generalCoverageRatio = 0.5f;

    [Header("Physics")]
    [SerializeField]
    [Tooltip("Si está activo, las barreras generan colliders.")]
    private bool generateColliders = true;

    [SerializeField]
    [Tooltip("Si está activo, las barreras cilíndricas usan CapsuleCollider por segmento en vez de MeshCollider.")]
    private bool usePrimitiveCylindricalBarrierColliders = true;

    [SerializeField]
    [Tooltip("Multiplicador del radio físico de las barreras cilíndricas. Usa valores mayores a 1 si la bola atraviesa visualmente la textura.")]
    private float cylindricalBarrierColliderRadiusMultiplier = 1.35f;

    [SerializeField]
    [Tooltip("Radio físico mínimo de seguridad para los colliders cilíndricos.")]
    private float minimumCylindricalBarrierColliderRadius = 0.12f;

    [SerializeField]
    [Tooltip("Material físico opcional para las barreras cilíndricas.")]
    private PhysicsMaterial cylindricalBarrierPhysicMaterial;

    [SerializeField]
    [Tooltip("Material físico opcional para la pared inicial.")]
    private PhysicsMaterial startWallPhysicMaterial;

    [Header("Build")]
    [SerializeField]
    [Tooltip("Si está activo, reconstruye barreras automáticamente en Start usando el mapa actual del TrackGeneratorController.")]
    private bool rebuildOnStart = false;

    #endregion

    #region Public API

    /// <summary>
    /// Reconstruye las barreras usando el mapa runtime actual del TrackGeneratorController.
    /// </summary>
    [ContextMenu("Rebuild Barriers From Track Generator")]
    public void RebuildFromTrackGenerator()
    {
        if (trackGenerator == null)
        {
            Debug.LogWarning("[TRACK BARRIERS] TrackGeneratorController no está asignado.", this);
            return;
        }

        Rebuild(trackGenerator.GeneratedMap);
    }

    /// <summary>
    /// Reconstruye todas las barreras a partir de un mapa runtime generado.
    /// </summary>
    public void Rebuild(TrackRuntimeMap runtimeMap)
    {
        ClearGeneratedBarriers();
        ValidateInspectorData();

        if (runtimeMap == null || runtimeMap.SurfaceChunks == null || runtimeMap.SurfaceChunks.Count == 0)
        {
            return;
        }

        IReadOnlyList<TrackSurfaceChunkDefinition> chunks = runtimeMap.SurfaceChunks;
        bool[] selectedChunks = ResolveSelectedChunks(runtimeMap);
        List<TrackBarrierRun> runs = BuildRuns(selectedChunks);

        Transform root = GetOrCreateGeneratedRoot();

        for (int i = 0; i < runs.Count; i++)
        {
            BuildCylindricalBarrierRun(chunks, runs[i], TrackBarrierSide.Left, root);
            BuildCylindricalBarrierRun(chunks, runs[i], TrackBarrierSide.Right, root);
        }

        if (generateStartWall)
        {
            BuildStartWall(chunks, root);
        }
    }

    /// <summary>
    /// Elimina todas las barreras generadas.
    /// </summary>
    [ContextMenu("Clear Barriers")]
    public void ClearGeneratedBarriers()
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

    #region Unity Lifecycle

    private void Reset()
    {
        trackGenerator = GetComponent<TrackGeneratorController>();
    }

    private void Start()
    {
        if (!rebuildOnStart)
        {
            return;
        }

        RebuildFromTrackGenerator();
    }

    #endregion

    #region Selection

    /// <summary>
    /// Decide qué chunks recibirán barreras laterales usando la misma semilla del nivel.
    /// </summary>
    private bool[] ResolveSelectedChunks(TrackRuntimeMap runtimeMap)
    {
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks = runtimeMap.SurfaceChunks;
        bool[] selected = new bool[chunks.Count];

        float totalDistance = ResolveTotalDistance(runtimeMap);
        float safeStartLength = ResolveSafeStartLength(totalDistance);
        float safeEndLength = ResolveSafeEndLength(totalDistance);
        float generalCoverageLimit = Mathf.Max(0f, totalDistance * generalCoverageRatio);
        float coveredGeneralDistance = 0f;

        System.Random random = new System.Random(runtimeMap.GeneratedSeed);

        for (int i = 0; i < chunks.Count; i++)
        {
            TrackSurfaceChunkDefinition chunk = chunks[i];

            if (!IsChunkValidForBarrier(chunk))
            {
                selected[i] = false;
                continue;
            }

            bool isStartSafeChunk = ChunkOverlapsRange(chunk, 0f, safeStartLength);
            bool isEndSafeChunk = ChunkOverlapsRange(chunk, totalDistance - safeEndLength, totalDistance);

            if (isStartSafeChunk && levelGenerationSettings != null && levelGenerationSettings.GenerateStartSafeZoneBarriers)
            {
                selected[i] = true;
                continue;
            }

            if (isEndSafeChunk && levelGenerationSettings != null && levelGenerationSettings.GenerateEndSafeZoneBarriers)
            {
                selected[i] = true;
                continue;
            }

            float chunkLength = Mathf.Max(0f, chunk.EndDistance - chunk.StartDistance);

            if (coveredGeneralDistance >= generalCoverageLimit)
            {
                selected[i] = false;
                continue;
            }

            if (random.NextDouble() > generalBarrierChance)
            {
                selected[i] = false;
                continue;
            }

            selected[i] = true;
            coveredGeneralDistance += chunkLength;
        }

        EnsureAtLeastOneGeneralChunkIfNeeded(
            chunks,
            selected,
            totalDistance,
            safeStartLength,
            safeEndLength);

        return selected;
    }

    /// <summary>
    /// Garantiza que si se pidió cobertura general, al menos exista un segmento completo seleccionado.
    /// </summary>
    private void EnsureAtLeastOneGeneralChunkIfNeeded(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        bool[] selected,
        float totalDistance,
        float safeStartLength,
        float safeEndLength)
    {
        if (generalCoverageRatio <= 0f || generalBarrierChance <= 0f)
        {
            return;
        }

        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] && !IsSafeChunk(chunks[i], totalDistance, safeStartLength, safeEndLength))
            {
                return;
            }
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            TrackSurfaceChunkDefinition chunk = chunks[i];

            if (!IsChunkValidForBarrier(chunk))
            {
                continue;
            }

            if (IsSafeChunk(chunk, totalDistance, safeStartLength, safeEndLength))
            {
                continue;
            }

            selected[i] = true;
            return;
        }
    }

    /// <summary>
    /// Construye runs continuos de chunks seleccionados para evitar cortes internos.
    /// </summary>
    private static List<TrackBarrierRun> BuildRuns(bool[] selectedChunks)
    {
        List<TrackBarrierRun> runs = new List<TrackBarrierRun>();

        if (selectedChunks == null || selectedChunks.Length == 0)
        {
            return runs;
        }

        int currentStart = -1;

        for (int i = 0; i < selectedChunks.Length; i++)
        {
            if (selectedChunks[i])
            {
                if (currentStart < 0)
                {
                    currentStart = i;
                }

                continue;
            }

            if (currentStart >= 0)
            {
                runs.Add(new TrackBarrierRun(currentStart, i - 1));
                currentStart = -1;
            }
        }

        if (currentStart >= 0)
        {
            runs.Add(new TrackBarrierRun(currentStart, selectedChunks.Length - 1));
        }

        return runs;
    }

    #endregion

    #region Build Cylindrical Barriers

    /// <summary>
    /// Construye visual y física de un run cilíndrico para un lado.
    /// </summary>
    private void BuildCylindricalBarrierRun(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        TrackBarrierRun run,
        TrackBarrierSide side,
        Transform root)
    {
        Mesh mesh = TrackBarrierMeshBuilder.BuildBarrierMesh(
            chunks,
            run.StartIndex,
            run.EndIndex,
            chunks.Count,
            side,
            cylindricalBarrierLateralOffset,
            cylindricalBarrierRadius,
            cylindricalBarrierVerticalOffset,
            cylindricalBarrierRadialSegments,
            cylindricalBarrierSmoothingIterations);

        if (mesh == null || mesh.vertexCount == 0)
        {
            return;
        }

        GameObject barrierObject = new GameObject($"CylindricalBarrier_{side}_{run.StartIndex:D3}_{run.EndIndex:D3}");
        AssignLayer(barrierObject);

        barrierObject.transform.SetParent(root);
        barrierObject.transform.localPosition = Vector3.zero;
        barrierObject.transform.localRotation = Quaternion.identity;
        barrierObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = barrierObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = barrierObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = cylindricalBarrierMaterial;

        if (!generateColliders)
        {
            return;
        }

        if (usePrimitiveCylindricalBarrierColliders)
        {
            CreateCylindricalBarrierPrimitiveColliders(
                chunks,
                run,
                side,
                barrierObject.transform);

            if (generateCylindricalBarrierEndPosts)
            {
                CreateCylindricalBarrierEndPostColliders(
                    chunks,
                    run,
                    side,
                    barrierObject.transform);
            }

            return;
        }

        MeshCollider meshCollider = barrierObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.sharedMaterial = cylindricalBarrierPhysicMaterial;
    }

    /// <summary>
    /// Crea CapsuleColliders por segmento para una barrera cilíndrica lateral.
    /// </summary>
    private void CreateCylindricalBarrierPrimitiveColliders(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        TrackBarrierRun run,
        TrackBarrierSide side,
        Transform parent)
    {
        List<Vector3> centers = CollectCylindricalBarrierCenters(
            chunks,
            run.StartIndex,
            run.EndIndex,
            side);

        if (centers.Count < 2)
        {
            return;
        }

        float radius = ResolvePhysicalBarrierRadius();

        for (int i = 0; i < centers.Count - 1; i++)
        {
            Vector3 start = centers[i];
            Vector3 end = centers[i + 1];
            Vector3 segment = end - start;
            float segmentLength = segment.magnitude;

            if (segmentLength <= 0.05f)
            {
                continue;
            }

            CreateCapsuleColliderSegment(
                parent,
                start,
                end,
                radius,
                segmentLength,
                $"CylindricalBarrierCollider_{side}_{i:D3}");
        }
    }

    /// <summary>
    /// Crea colliders verticales para los postes de inicio y final de la barrera cilíndrica.
    /// </summary>
    private void CreateCylindricalBarrierEndPostColliders(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        TrackBarrierRun run,
        TrackBarrierSide side,
        Transform parent)
    {
        List<Vector3> centers = CollectCylindricalBarrierCenters(
            chunks,
            run.StartIndex,
            run.EndIndex,
            side);

        if (centers.Count < 2)
        {
            return;
        }

        float radius = ResolvePhysicalBarrierRadius();

        CreateVerticalPostCollider(
            parent,
            centers[0],
            radius,
            $"CylindricalBarrierStartPostCollider_{side}_{run.StartIndex:D3}");

        CreateVerticalPostCollider(
            parent,
            centers[centers.Count - 1],
            radius,
            $"CylindricalBarrierEndPostCollider_{side}_{run.EndIndex:D3}");
    }

    /// <summary>
    /// Crea un CapsuleCollider vertical desde la base de la pista hasta el centro visual de la barrera.
    /// </summary>
    private void CreateVerticalPostCollider(
        Transform parent,
        Vector3 topCenter,
        float radius,
        string objectName)
    {
        float bottomY = topCenter.y - cylindricalBarrierVerticalOffset + cylindricalBarrierPostBaseVerticalOffset;
        Vector3 bottomCenter = new Vector3(topCenter.x, bottomY, topCenter.z);

        Vector3 segment = topCenter - bottomCenter;
        float segmentLength = segment.magnitude;

        if (segmentLength <= 0.05f)
        {
            return;
        }

        CreateCapsuleColliderSegment(
            parent,
            bottomCenter,
            topCenter,
            radius,
            segmentLength,
            objectName);
    }

    /// <summary>
    /// Recolecta centros físicos de una barrera cilíndrica siguiendo los samples del track.
    /// </summary>
    private List<Vector3> CollectCylindricalBarrierCenters(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        int startIndex,
        int endIndex,
        TrackBarrierSide side)
    {
        List<Vector3> centers = new List<Vector3>();

        if (chunks == null || chunks.Count == 0)
        {
            return centers;
        }

        int safeStartIndex = Mathf.Clamp(startIndex, 0, chunks.Count - 1);
        int safeEndIndex = Mathf.Clamp(endIndex, safeStartIndex, chunks.Count - 1);
        float sideSign = side == TrackBarrierSide.Right ? 1f : -1f;

        for (int i = safeStartIndex; i <= safeEndIndex; i++)
        {
            IReadOnlyList<TrackLayoutSamplePoint> samples = chunks[i].Samples;

            for (int j = 0; j < samples.Count; j++)
            {
                TrackLayoutSamplePoint sample = samples[j];

                Vector3 right = ResolveSafeRight(sample.Right, sample.Forward);
                float halfReferenceWidth = ResolveHalfReferenceWidth(sample);
                float resolvedOffset = halfReferenceWidth + cylindricalBarrierLateralOffset;

                Vector3 center = sample.Position
                                 + right * resolvedOffset * sideSign
                                 + Vector3.up * cylindricalBarrierVerticalOffset;

                if (centers.Count > 0 && Vector3.Distance(centers[centers.Count - 1], center) <= 0.001f)
                {
                    continue;
                }

                centers.Add(center);
            }
        }

        SmoothColliderCenters(centers, cylindricalBarrierSmoothingIterations);

        return centers;
    }

    /// <summary>
    /// Suaviza centros físicos internos sin mover extremos.
    /// </summary>
    private static void SmoothColliderCenters(List<Vector3> centers, int iterations)
    {
        if (centers == null || centers.Count < 3 || iterations <= 0)
        {
            return;
        }

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            List<Vector3> copy = new List<Vector3>(centers);

            for (int i = 1; i < centers.Count - 1; i++)
            {
                centers[i] = (copy[i - 1] + copy[i] + copy[i + 1]) / 3f;
            }
        }
    }

    /// <summary>
    /// Crea un CapsuleCollider alineado entre dos puntos.
    /// </summary>
    private void CreateCapsuleColliderSegment(
        Transform parent,
        Vector3 start,
        Vector3 end,
        float radius,
        float segmentLength,
        string objectName)
    {
        GameObject colliderObject = new GameObject(objectName);
        AssignLayer(colliderObject);

        Vector3 segment = end - start;
        Vector3 direction = segment.normalized;
        Vector3 center = (start + end) * 0.5f;

        colliderObject.transform.position = center;
        colliderObject.transform.rotation = ResolveCapsuleRotation(direction);
        colliderObject.transform.SetParent(parent, true);

        CapsuleCollider capsuleCollider = colliderObject.AddComponent<CapsuleCollider>();
        capsuleCollider.direction = 2;
        capsuleCollider.radius = radius;
        capsuleCollider.height = segmentLength + radius * 2f;
        capsuleCollider.sharedMaterial = cylindricalBarrierPhysicMaterial;
    }

    #endregion

    #region Build Start Wall

    /// <summary>
    /// Construye una pared inicial independiente al comienzo del nivel.
    /// </summary>
    private void BuildStartWall(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        Transform root)
    {
        if (!TryGetFirstValidSample(chunks, out TrackLayoutSamplePoint firstSample))
        {
            return;
        }

        GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallObject.name = "StartWall";
        AssignLayer(wallObject);

        wallObject.transform.SetParent(root);

        Vector3 forward = ResolveSafeForward(firstSample.Forward);

        float wallWidth = startWallWidthOverride > 0f
            ? startWallWidthOverride
            : firstSample.Width;

        Vector3 position = firstSample.Position
                           + forward * startWallForwardOffset
                           + Vector3.up * (startWallVerticalOffset + startWallHeight * 0.5f);

        wallObject.transform.position = position;
        wallObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        wallObject.transform.localScale = new Vector3(
            Mathf.Max(0.01f, wallWidth),
            Mathf.Max(0.01f, startWallHeight),
            Mathf.Max(0.01f, startWallThickness));

        MeshRenderer meshRenderer = wallObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial = startWallMaterial;
        }

        Collider collider = wallObject.GetComponent<Collider>();

        if (collider == null)
        {
            return;
        }

        if (!generateColliders)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(collider);
            }
            else
            {
                Destroy(collider);
            }
#else
            Destroy(collider);
#endif
            return;
        }

        collider.sharedMaterial = startWallPhysicMaterial;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Obtiene el primer sample válido del mapa.
    /// </summary>
    private static bool TryGetFirstValidSample(
        IReadOnlyList<TrackSurfaceChunkDefinition> chunks,
        out TrackLayoutSamplePoint sample)
    {
        sample = default;

        if (chunks == null)
        {
            return false;
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            TrackSurfaceChunkDefinition chunk = chunks[i];

            if (chunk == null || chunk.Samples == null || chunk.Samples.Count == 0)
            {
                continue;
            }

            sample = chunk.Samples[0];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Indica si un chunk puede recibir barreras laterales.
    /// </summary>
    private static bool IsChunkValidForBarrier(TrackSurfaceChunkDefinition chunk)
    {
        if (chunk == null || chunk.Samples == null || chunk.Samples.Count < 2)
        {
            return false;
        }

        return chunk.StructureType != TrackStructureType.Gap;
    }

    /// <summary>
    /// Indica si un chunk cruza un rango de distancia.
    /// </summary>
    private static bool ChunkOverlapsRange(
        TrackSurfaceChunkDefinition chunk,
        float rangeStart,
        float rangeEnd)
    {
        if (chunk == null)
        {
            return false;
        }

        return chunk.EndDistance >= rangeStart && chunk.StartDistance <= rangeEnd;
    }

    /// <summary>
    /// Indica si un chunk pertenece a zona segura inicial o final.
    /// </summary>
    private static bool IsSafeChunk(
        TrackSurfaceChunkDefinition chunk,
        float totalDistance,
        float safeStartLength,
        float safeEndLength)
    {
        return ChunkOverlapsRange(chunk, 0f, safeStartLength)
               || ChunkOverlapsRange(chunk, totalDistance - safeEndLength, totalDistance);
    }

    /// <summary>
    /// Resuelve la distancia total del mapa.
    /// </summary>
    private static float ResolveTotalDistance(TrackRuntimeMap runtimeMap)
    {
        if (runtimeMap.PathSampler != null)
        {
            return runtimeMap.PathSampler.TotalDistance;
        }

        IReadOnlyList<TrackSurfaceChunkDefinition> chunks = runtimeMap.SurfaceChunks;

        if (chunks == null || chunks.Count == 0)
        {
            return 0f;
        }

        return chunks[chunks.Count - 1].EndDistance;
    }

    /// <summary>
    /// Resuelve longitud efectiva de zona segura inicial.
    /// </summary>
    private float ResolveSafeStartLength(float totalDistance)
    {
        if (levelGenerationSettings != null && levelGenerationSettings.SafeStartLengthOverride > 0f)
        {
            return Mathf.Min(levelGenerationSettings.SafeStartLengthOverride, totalDistance);
        }

        return 0f;
    }

    /// <summary>
    /// Resuelve longitud efectiva de zona segura final.
    /// </summary>
    private float ResolveSafeEndLength(float totalDistance)
    {
        if (levelGenerationSettings != null && levelGenerationSettings.SafeEndLengthOverride > 0f)
        {
            return Mathf.Min(levelGenerationSettings.SafeEndLengthOverride, totalDistance);
        }

        return 0f;
    }

    /// <summary>
    /// Obtiene o crea el contenedor raíz.
    /// </summary>
    private Transform GetOrCreateGeneratedRoot()
    {
        Transform existingRoot = transform.Find(generatedRootName);

        if (existingRoot != null)
        {
            return existingRoot;
        }

        GameObject root = new GameObject(generatedRootName);
        AssignLayer(root);

        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        return root.transform;
    }

    /// <summary>
    /// Asigna la layer configurada a un objeto.
    /// </summary>
    private void AssignLayer(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        int layer = LayerMask.NameToLayer(barrierLayerName);

        if (layer < 0)
        {
            Debug.LogWarning($"[TRACK BARRIERS] La layer '{barrierLayerName}' no existe.", this);
            return;
        }

        target.layer = layer;
    }

    /// <summary>
    /// Resuelve el semiancho de referencia usado para ubicar la barrera.
    /// </summary>
    private static float ResolveHalfReferenceWidth(TrackLayoutSamplePoint sample)
    {
        if (sample.StructureType == TrackStructureType.RailTrack)
        {
            float railSeparation = Mathf.Max(0f, sample.RailSeparation);
            float railWidth = Mathf.Max(0f, sample.RailWidth);

            if (railSeparation <= 0.0001f || railWidth <= 0.0001f)
            {
                return Mathf.Max(0f, sample.Width) * 0.5f;
            }

            return railSeparation * 0.5f + railWidth * 0.5f;
        }

        return Mathf.Max(0f, sample.Width) * 0.5f;
    }

    /// <summary>
    /// Resuelve un vector lateral seguro.
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

        return resolvedRight.sqrMagnitude < 0.0001f
            ? Vector3.right
            : resolvedRight.normalized;
    }

    /// <summary>
    /// Resuelve un forward seguro.
    /// </summary>
    private static Vector3 ResolveSafeForward(Vector3 forward)
    {
        return forward.sqrMagnitude >= 0.0001f
            ? forward.normalized
            : Vector3.forward;
    }

    /// <summary>
    /// Resuelve rotación para una cápsula cuyo eje local Z representa su longitud.
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
    /// Resuelve el radio físico final de la barrera.
    /// </summary>
    private float ResolvePhysicalBarrierRadius()
    {
        return Mathf.Max(
            minimumCylindricalBarrierColliderRadius,
            cylindricalBarrierRadius * cylindricalBarrierColliderRadiusMultiplier);
    }

    /// <summary>
    /// Valida valores configurables del inspector.
    /// </summary>
    private void ValidateInspectorData()
    {
        generatedRootName = string.IsNullOrWhiteSpace(generatedRootName)
            ? "GeneratedTrackBarriers"
            : generatedRootName;

        barrierLayerName = string.IsNullOrWhiteSpace(barrierLayerName)
            ? "Barrier"
            : barrierLayerName;

        cylindricalBarrierRadius = Mathf.Max(0.01f, cylindricalBarrierRadius);
        cylindricalBarrierRadialSegments = Mathf.Max(3, cylindricalBarrierRadialSegments);
        cylindricalBarrierLateralOffset = Mathf.Max(0f, cylindricalBarrierLateralOffset);
        cylindricalBarrierSmoothingIterations = Mathf.Max(0, cylindricalBarrierSmoothingIterations);

        cylindricalBarrierPostBaseVerticalOffset = Mathf.Max(0f, cylindricalBarrierPostBaseVerticalOffset);

        cylindricalBarrierColliderRadiusMultiplier = Mathf.Max(0.1f, cylindricalBarrierColliderRadiusMultiplier);
        minimumCylindricalBarrierColliderRadius = Mathf.Max(0.01f, minimumCylindricalBarrierColliderRadius);

        startWallHeight = Mathf.Max(0.01f, startWallHeight);
        startWallThickness = Mathf.Max(0.01f, startWallThickness);

        generalBarrierChance = Mathf.Clamp01(generalBarrierChance);
        generalCoverageRatio = Mathf.Clamp01(generalCoverageRatio);
    }

    #endregion
}