using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Convierte la trayectoria continua del track en chunks de superficie sampleados.
/// 
/// Responsabilidades:
/// - Agrupar puntos continuos por continuidad de superficie.
/// - Cortar chunks al encontrar gaps.
/// - Cortar chunks al cambiar la estructura física.
/// - Compartir una muestra de costura entre chunks de distinta estructura para evitar huecos.
/// - Filtrar micro-segmentos dentro de una misma estructura para evitar cortes visuales.
/// </summary>
public static class TrackLayoutBuilder
{
    #region Constants

    /// <summary>
    /// Distancia mínima entre samples ordinarios dentro de un mismo chunk.
    /// </summary>
    private const float MinimumChunkPointSpacing = 0.01f;

    /// <summary>
    /// Distancia mínima usada únicamente para costuras forzadas entre estructuras.
    /// </summary>
    private const float MinimumForcedSeamSpacing = 0.0005f;

    #endregion

    #region Public API

    /// <summary>
    /// Construye chunks de superficie continua a partir de puntos continuos ya resueltos.
    /// </summary>
    public static List<TrackSurfaceChunkDefinition> BuildSurfaceChunks(
        IReadOnlyList<TrackSplinePoint> splinePoints)
    {
        List<TrackSurfaceChunkDefinition> chunks = new List<TrackSurfaceChunkDefinition>();

        if (splinePoints == null || splinePoints.Count < 2)
        {
            return chunks;
        }

        List<TrackLayoutSamplePoint> currentChunkSamples = null;
        int chunkIndex = 0;
        float chunkStartDistance = 0f;
        TrackStructureType currentStructureType = TrackStructureType.SolidTrack;

        TrackLayoutSamplePoint? pendingSeamSample = null;

        for (int i = 0; i < splinePoints.Count; i++)
        {
            TrackSplinePoint point = splinePoints[i];

            if (!point.HasSurface || point.StructureType == TrackStructureType.Gap)
            {
                FinalizeCurrentChunkIfValid(
                    chunks,
                    ref currentChunkSamples,
                    ref chunkIndex,
                    chunkStartDistance,
                    currentStructureType);

                pendingSeamSample = null;
                continue;
            }

            TrackLayoutSamplePoint currentSample = ConvertToLayoutSample(point);

            bool shouldStartNewChunk =
                currentChunkSamples == null
                || currentStructureType != point.StructureType;

            if (shouldStartNewChunk)
            {
                if (currentChunkSamples != null)
                {
                    AddSampleIfNeeded(
                        currentChunkSamples,
                        currentSample,
                        forceSeamSample: true);

                    FinalizeCurrentChunkIfValid(
                        chunks,
                        ref currentChunkSamples,
                        ref chunkIndex,
                        chunkStartDistance,
                        currentStructureType);

                    pendingSeamSample = currentSample;
                }

                currentChunkSamples = new List<TrackLayoutSamplePoint>();
                chunkStartDistance = currentSample.Distance;
                currentStructureType = point.StructureType;

                if (pendingSeamSample.HasValue)
                {
                    AddSampleIfNeeded(
                        currentChunkSamples,
                        pendingSeamSample.Value,
                        forceSeamSample: true);

                    pendingSeamSample = null;
                }

                AddSampleIfNeeded(
                    currentChunkSamples,
                    currentSample,
                    forceSeamSample: false);

                continue;
            }

            AddSampleIfNeeded(
                currentChunkSamples,
                currentSample,
                forceSeamSample: false);
        }

        FinalizeCurrentChunkIfValid(
            chunks,
            ref currentChunkSamples,
            ref chunkIndex,
            chunkStartDistance,
            currentStructureType);

        return chunks;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Convierte un punto continuo a un sample final de layout.
    /// </summary>
    private static TrackLayoutSamplePoint ConvertToLayoutSample(TrackSplinePoint point)
    {
        return new TrackLayoutSamplePoint(
            point.Position,
            point.Forward,
            point.Right,
            point.Width,
            point.Distance,
            point.StructureType,
            point.RailSeparation,
            point.RailWidth);
    }

    /// <summary>
    /// Finaliza el chunk actual si contiene suficientes samples válidos.
    /// </summary>
    private static void FinalizeCurrentChunkIfValid(
        List<TrackSurfaceChunkDefinition> chunks,
        ref List<TrackLayoutSamplePoint> currentChunkSamples,
        ref int chunkIndex,
        float chunkStartDistance,
        TrackStructureType structureType)
    {
        if (currentChunkSamples == null)
        {
            return;
        }

        if (currentChunkSamples.Count >= 2)
        {
            float endDistance = currentChunkSamples[currentChunkSamples.Count - 1].Distance;

            chunks.Add(new TrackSurfaceChunkDefinition(
                chunkIndex,
                chunkStartDistance,
                endDistance,
                structureType,
                currentChunkSamples));

            chunkIndex++;
        }

        currentChunkSamples = null;
    }

    /// <summary>
    /// Añade un sample evitando duplicados y micro-segmentos.
    /// Las costuras forzadas solo se usan al cambiar de estructura física.
    /// </summary>
    private static void AddSampleIfNeeded(
        List<TrackLayoutSamplePoint> target,
        TrackLayoutSamplePoint sample,
        bool forceSeamSample)
    {
        if (sample.Forward.sqrMagnitude < 0.0001f || sample.Right.sqrMagnitude < 0.0001f)
        {
            return;
        }

        if (target.Count == 0)
        {
            target.Add(sample);
            return;
        }

        TrackLayoutSamplePoint last = target[target.Count - 1];

        float positionDistance = Vector3.Distance(last.Position, sample.Position);
        float distanceDelta = Mathf.Abs(last.Distance - sample.Distance);

        if (positionDistance <= 0.0005f && distanceDelta <= 0.0005f)
        {
            return;
        }

        float requiredSpacing = forceSeamSample
            ? MinimumForcedSeamSpacing
            : MinimumChunkPointSpacing;

        if (positionDistance < requiredSpacing)
        {
            return;
        }

        target.Add(sample);
    }

    #endregion
}