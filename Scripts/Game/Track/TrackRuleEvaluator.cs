using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evalúa qué tipo de cambio puede generarse a continuación
/// en función del estado actual del nivel y las reglas configuradas.
/// </summary>
public sealed class TrackRuleEvaluator
{
    #region Nested Types

    private readonly struct Candidate
    {
        public TrackFeatureType FeatureType { get; }
        public float Weight { get; }

        public Candidate(TrackFeatureType featureType, float weight)
        {
            FeatureType = featureType;
            Weight = weight;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Selecciona la siguiente decisión válida del generador.
    /// </summary>
    public TrackGenerationDecision EvaluateNextDecision(
        ref TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings,
        System.Random random,
        float targetLength,
        float minTrackHeight,
        float maxTrackHeight)
    {
        float safeStartLength = ResolveSafeStartLength(profile, settings);
        if (state.GeneratedLength < safeStartLength)
        {
            return CreateStraightDecision(
                Mathf.Max(0f, safeStartLength - state.GeneratedLength),
                0f,
                state.CurrentWidthRatio);
        }

        float safeEndLength = ResolveSafeEndLength(profile, settings);
        float remainingToEnd = Mathf.Max(0f, targetLength - state.GeneratedLength);

        if (state.CurrentStructureType == TrackStructureType.RailTrack
            && remainingToEnd <= safeEndLength + profile.MinimumSectionLength + profile.MinStraightAfterRail)
        {
            return CreateRailEndDecision(profile, random, state.CurrentWidthRatio);
        }

        if (state.GeneratedLength >= targetLength - safeEndLength)
        {
            state.IsInsideSafeEndZone = true;
            return CreateFinishDecision(
                Mathf.Max(0f, targetLength - state.GeneratedLength),
                state.CurrentWidthRatio);
        }

        List<Candidate> candidates = BuildCandidates(
            state,
            profile,
            settings,
            targetLength,
            minTrackHeight,
            maxTrackHeight);

        if (candidates.Count == 0)
        {
            return CreateStraightDecision(
                RandomSolidLength(profile, random),
                0f,
                state.CurrentWidthRatio);
        }

        TrackFeatureType selectedFeature = PickWeighted(candidates, random);
        return BuildDecisionPayload(
            selectedFeature,
            state,
            profile,
            settings,
            random,
            minTrackHeight,
            maxTrackHeight);
    }

    #endregion

    #region Candidate Build

    private List<Candidate> BuildCandidates(
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings,
        float targetLength,
        float minTrackHeight,
        float maxTrackHeight)
    {
        List<Candidate> candidates = new List<Candidate>();

        if (state.CurrentStructureType == TrackStructureType.RailTrack)
        {
            BuildRailCandidates(candidates, state, profile, settings, targetLength, minTrackHeight, maxTrackHeight);
            return candidates;
        }

        BuildSolidCandidates(candidates, state, profile, settings, targetLength, minTrackHeight, maxTrackHeight);

        if (candidates.Count == 0)
        {
            candidates.Add(new Candidate(TrackFeatureType.Straight, 1f));
        }

        return candidates;
    }

    private void BuildSolidCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings,
        float targetLength,
        float minTrackHeight,
        float maxTrackHeight)
    {
        candidates.Add(new Candidate(TrackFeatureType.Straight, 1f));

        AddSolidLateralCandidates(candidates, state, profile, settings);
        AddVerticalCandidates(candidates, state, profile, settings, minTrackHeight, maxTrackHeight);
        AddWidthCandidates(candidates, state, profile, settings);
        AddGapCandidate(candidates, state, profile, settings, targetLength);
        AddRailStartCandidate(candidates, state, profile, settings, targetLength);
    }

    private void BuildRailCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings,
        float targetLength,
        float minTrackHeight,
        float maxTrackHeight)
    {
        if (state.CurrentRailSectionCount >= profile.RailMaxConsecutiveSections)
        {
            candidates.Add(new Candidate(TrackFeatureType.RailEnd, 1f));
            return;
        }

        candidates.Add(new Candidate(TrackFeatureType.RailSegment, 1f));
        candidates.Add(new Candidate(TrackFeatureType.RailEnd, 0.35f));

        AddRailLateralCandidates(candidates, state, profile, settings);
        AddRailVerticalCandidates(candidates, state, profile, settings, minTrackHeight, maxTrackHeight);

        float remainingToEnd = targetLength - state.GeneratedLength;
        if (remainingToEnd <= profile.SafeEndLength + profile.MinimumSectionLength + profile.MinStraightAfterRail)
        {
            candidates.Clear();
            candidates.Add(new Candidate(TrackFeatureType.RailEnd, 1f));
        }
    }

    private void AddSolidLateralCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings)
    {
        if (state.DistanceSinceLastLateralChange < profile.MinStraightAfterLateralChange)
        {
            return;
        }

        float lateralChance = profile.LateralChangeChance
            * settings.LateralChanceMultiplier
            * settings.DifficultyMultiplier;

        if (lateralChance <= 0f)
        {
            return;
        }

        AddLateralCandidatesForState(
            candidates,
            state.CurrentLateralState,
            state.CurrentYawOffsetDegrees,
            lateralChance,
            profile.LeftTurnWeight,
            profile.RightTurnWeight,
            profile.Turn45Weight,
            profile.Turn90Weight);
    }

    private void AddRailLateralCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings)
    {
        if (state.DistanceSinceLastLateralChange < profile.MinStraightAfterLateralChange)
        {
            return;
        }

        float lateralChance = profile.LateralChangeChance
            * settings.LateralChanceMultiplier
            * settings.DifficultyMultiplier
            * profile.RailTurnChanceMultiplier;

        if (lateralChance <= 0f)
        {
            return;
        }

        AddLateralCandidatesForState(
            candidates,
            state.CurrentLateralState,
            state.CurrentYawOffsetDegrees,
            lateralChance,
            profile.LeftTurnWeight,
            profile.RightTurnWeight,
            profile.Turn45Weight,
            profile.Turn90Weight);
    }

    private void AddVerticalCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings,
        float minTrackHeight,
        float maxTrackHeight)
    {
        if (state.DistanceSinceLastVerticalChange < profile.MinStraightAfterVerticalChange)
        {
            return;
        }

        if (state.CurrentStructureType != TrackStructureType.SolidTrack)
        {
            return;
        }

        float verticalChance = profile.VerticalChangeChance
            * settings.VerticalChanceMultiplier
            * settings.DifficultyMultiplier;

        if (verticalChance <= 0f)
        {
            return;
        }

        float nextUpMinimum   = state.CurrentHeight + profile.SlopeHeightStepMin;
        float nextDownMinimum = state.CurrentHeight - profile.SlopeHeightStepMin;

        if (nextUpMinimum <= maxTrackHeight)
        {
            candidates.Add(new Candidate(TrackFeatureType.SlopeUp,   verticalChance * profile.SlopeUpWeight));
        }

        if (nextDownMinimum >= minTrackHeight)
        {
            candidates.Add(new Candidate(TrackFeatureType.SlopeDown, verticalChance * profile.SlopeDownWeight));
        }
    }

    private void AddRailVerticalCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings,
        float minTrackHeight,
        float maxTrackHeight)
    {
        if (state.DistanceSinceLastVerticalChange < profile.MinStraightAfterVerticalChange)
        {
            return;
        }

        float verticalChance = profile.VerticalChangeChance
            * settings.VerticalChanceMultiplier
            * settings.DifficultyMultiplier
            * profile.RailVerticalChanceMultiplier;

        if (verticalChance <= 0f)
        {
            return;
        }

        float nextUpMinimum   = state.CurrentHeight + profile.SlopeHeightStepMin;
        float nextDownMinimum = state.CurrentHeight - profile.SlopeHeightStepMin;

        if (nextUpMinimum <= maxTrackHeight)
        {
            candidates.Add(new Candidate(TrackFeatureType.SlopeUp,   verticalChance * profile.SlopeUpWeight));
        }

        if (nextDownMinimum >= minTrackHeight)
        {
            candidates.Add(new Candidate(TrackFeatureType.SlopeDown, verticalChance * profile.SlopeDownWeight));
        }
    }

    private void AddWidthCandidates(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings)
    {
        if (state.DistanceSinceLastWidthChange < profile.MinStraightAfterWidthChange)
        {
            return;
        }

        if (state.CurrentStructureType != TrackStructureType.SolidTrack)
        {
            return;
        }

        float widthChance = profile.NarrowChance
            * settings.NarrowChanceMultiplier
            * settings.DifficultyMultiplier;

        if (widthChance <= 0f)
        {
            return;
        }

        if (Mathf.Approximately(state.CurrentWidthRatio, 1f))
        {
            candidates.Add(new Candidate(TrackFeatureType.NarrowStart, widthChance));
        }
        else
        {
            candidates.Add(new Candidate(TrackFeatureType.NarrowEnd, widthChance));
        }
    }

    private void AddGapCandidate(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings,
        float targetLength)
    {
        if (state.DistanceSinceLastGap < profile.MinStraightAfterGap)            return;
        if (state.CurrentStructureType != TrackStructureType.SolidTrack)         return;
        if (!Mathf.Approximately(state.CurrentWidthRatio, 1f))                   return;
        if (state.CurrentVerticalState != TrackVerticalState.Flat)               return;
        if (state.GeneratedLength < profile.ForbidGapNearStartDistance)          return;
        if (state.GeneratedLength > targetLength - profile.ForbidGapNearEndDistance) return;
        if (state.DistanceSinceLastLateralChange < profile.ForbidGapAfterRecentHardChangeDistance) return;

        float gapChance = profile.GapChance
            * settings.GapChanceMultiplier
            * settings.DifficultyMultiplier;

        if (gapChance <= 0f)
        {
            return;
        }

        candidates.Add(new Candidate(TrackFeatureType.Gap, gapChance));
    }

    private void AddRailStartCandidate(
        List<Candidate> candidates,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings,
        float targetLength)
    {
        if (state.CurrentStructureType != TrackStructureType.SolidTrack)         return;
        if (!Mathf.Approximately(state.CurrentWidthRatio, 1f))                   return;
        if (state.CurrentVerticalState != TrackVerticalState.Flat)               return;
        if (state.GeneratedLength < profile.ForbidRailNearStartDistance)         return;
        if (state.GeneratedLength > targetLength - profile.ForbidRailNearEndDistance) return;
        if (state.DistanceSinceLastRail < profile.MinStraightAfterRail)          return;

        float railChance = profile.RailGenerationChance
            * settings.RailChanceMultiplier
            * settings.DifficultyMultiplier;

        if (railChance <= 0f)
        {
            return;
        }

        candidates.Add(new Candidate(TrackFeatureType.RailStart, railChance));
    }

    #endregion

    #region Decision Payload

    private TrackGenerationDecision BuildDecisionPayload(
        TrackFeatureType selectedFeature,
        TrackGenerationState state,
        TrackGenerationProfile profile,
        ResolvedTrackSettings settings,
        System.Random random,
        float minTrackHeight,
        float maxTrackHeight)
    {
        if (selectedFeature == TrackFeatureType.Straight)
            return CreateStraightDecision(RandomSolidLength(profile, random), 0f, state.CurrentWidthRatio);

        if (TrackFeatureUtility.IsLateralFeature(selectedFeature))
            return CreateLateralDecision(selectedFeature, profile, state.CurrentWidthRatio);

        if (selectedFeature == TrackFeatureType.SlopeUp || selectedFeature == TrackFeatureType.SlopeDown)
            return CreateVerticalDecision(selectedFeature, state, profile, random, minTrackHeight, maxTrackHeight);

        if (selectedFeature == TrackFeatureType.NarrowStart)
            return CreateNarrowStartDecision(profile, random);

        if (selectedFeature == TrackFeatureType.NarrowEnd)
            return CreateNarrowEndDecision(profile);

        if (selectedFeature == TrackFeatureType.Gap)
            return CreateGapDecision(profile, random, state.CurrentWidthRatio);

        if (selectedFeature == TrackFeatureType.RailStart)
            return CreateRailStartDecision(profile, random, state.CurrentWidthRatio);

        if (selectedFeature == TrackFeatureType.RailSegment)
            return CreateRailSegmentDecision(profile, random, state.CurrentWidthRatio);

        if (selectedFeature == TrackFeatureType.RailEnd)
            return CreateRailEndDecision(profile, random, state.CurrentWidthRatio);

        if (selectedFeature == TrackFeatureType.Finish)
            return CreateFinishDecision(RandomSolidLength(profile, random), state.CurrentWidthRatio);

        return CreateStraightDecision(RandomSolidLength(profile, random), 0f, state.CurrentWidthRatio);
    }

    #endregion

    #region Helpers

    private float ResolveSafeStartLength(TrackGenerationProfile profile, ResolvedTrackSettings settings)
    {
        return settings.SafeStartLengthOverride > 0f
            ? settings.SafeStartLengthOverride
            : profile.SafeStartLength;
    }

    private float ResolveSafeEndLength(TrackGenerationProfile profile, ResolvedTrackSettings settings)
    {
        return settings.SafeEndLengthOverride > 0f
            ? settings.SafeEndLengthOverride
            : profile.SafeEndLength;
    }

    private TrackFeatureType PickWeighted(List<Candidate> candidates, System.Random random)
    {
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += Mathf.Max(0.0001f, candidates[i].Weight);

        double pick = random.NextDouble() * totalWeight;
        float cumulative = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += Mathf.Max(0.0001f, candidates[i].Weight);
            if (pick <= cumulative)
                return candidates[i].FeatureType;
        }

        return candidates[candidates.Count - 1].FeatureType;
    }

    private float RandomSolidLength(TrackGenerationProfile profile, System.Random random)
    {
        return RandomRange(profile.MinimumSectionLength, profile.MaximumSectionLength, random);
    }

    private float RandomRange(float min, float max, System.Random random)
    {
        if (Mathf.Approximately(min, max))
            return min;

        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    // ── Los métodos Create* no reciben settings y no cambian. ─────────────────
    // Dejá intactos todos los métodos:
    //   CreateStraightDecision, CreateLateralDecision, CreateVerticalDecision,
    //   CreateNarrowStartDecision, CreateNarrowEndDecision, CreateGapDecision,
    //   CreateRailStartDecision, CreateRailSegmentDecision, CreateRailEndDecision,
    //   CreateFinishDecision, AddLateralCandidatesForState
    // y cualquier otro método que no tenga LevelGenerationSettings en su firma.

    private TrackGenerationDecision CreateRailEndDecision(
        TrackGenerationProfile profile,
        System.Random random,
        float currentWidthRatio)
    {
        return new TrackGenerationDecision(
            TrackFeatureType.RailEnd,
            RandomRange(profile.RailSectionLengthMin, profile.RailSectionLengthMax, random),
            profile.MinStraightAfterRail,
            currentWidthRatio,
            0f, 0f, 0f,
            profile.RailSeparation,
            profile.RailWidth);
    }

    private TrackGenerationDecision CreateStraightDecision(
        float changeLength,
        float recoveryLength,
        float currentWidthRatio)
    {
        return new TrackGenerationDecision(
            TrackFeatureType.Straight,
            Mathf.Max(0f, changeLength),
            Mathf.Max(0f, recoveryLength),
            currentWidthRatio,
            0f, 0f, 0f, 0f, 0f);
    }

    #endregion
}