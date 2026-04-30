using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuración específica de contenido para un nivel.
/// </summary>
[CreateAssetMenu(fileName = "LevelContentGenerationSettings", menuName = "Game/Track/Level Content Generation Settings")]
public sealed class LevelContentGenerationSettings : ScriptableObject
{
    [Header("Catalog Reference")]
    [Tooltip("Catálogo global usado para sincronizar IDs desde el editor. No genera contenido por sí solo.")]
    [SerializeField] private TrackContentGenerationProfile contentCatalog;

    [Header("Override Behaviour")]
    [Tooltip("Si está activo, solo aparecen prefabs listados en overrides. Si está desactivado, los prefabs sin override usan su peso base.")]
    [SerializeField] private bool useOverridesAsWhitelist = true;

    [Header("Category Toggles")]
    [Tooltip("Permite generar cajas en este nivel.")]
    [SerializeField] private bool enableBoxes = true;

    [Tooltip("Permite generar muros en este nivel.")]
    [SerializeField] private bool enableWalls = true;

    [Tooltip("Permite generar pelotas empujables en este nivel.")]
    [SerializeField] private bool enableBalls = true;

    [Tooltip("Permite generar ventiladores o aspas en este nivel.")]
    [SerializeField] private bool enableFans = true;

    [Tooltip("Permite generar monedas en este nivel.")]
    [SerializeField] private bool enableCoins = true;

    [Tooltip("Permite generar la meta en este nivel.")]
    [SerializeField] private bool enableGoal = true;

    [Header("Spawn Chances")]
    [Tooltip("Probabilidad de intentar generar cajas en una fila válida. 0 nunca, 1 siempre.")]
    [Range(0f, 1f)]
    [SerializeField] private float boxSpawnChance = 0.18f;

    [Tooltip("Probabilidad de intentar generar un muro en una pista plana válida. 0 nunca, 1 siempre.")]
    [Range(0f, 1f)]
    [SerializeField] private float wallSpawnChance = 0.04f;

    [Tooltip("Probabilidad de generar pelota en pista plana normal. 0 nunca, 1 siempre.")]
    [Range(0f, 1f)]
    [SerializeField] private float ballFlatSpawnChance = 0.04f;

    [Tooltip("Probabilidad de generar pelota en estrechamientos. 0 nunca, 1 siempre.")]
    [Range(0f, 1f)]
    [SerializeField] private float ballNarrowSpawnChance = 0.08f;

    [Tooltip("Probabilidad de generar pelota en railes. 0 nunca, 1 siempre.")]
    [Range(0f, 1f)]
    [SerializeField] private float ballRailSpawnChance = 0.025f;

    [Tooltip("Probabilidad de generar pelota antes de una bajada. 0 nunca, 1 siempre.")]
    [Range(0f, 1f)]
    [SerializeField] private float ballBeforeDownSlopeChance = 0.2f;

    [Tooltip("Probabilidad de generar ventilador en pista plana. 0 nunca, 1 siempre.")]
    [Range(0f, 1f)]
    [SerializeField] private float fanFlatSpawnChance = 0.015f;

    [Tooltip("Probabilidad de generar ventilador en rail recto. 0 nunca, 1 siempre.")]
    [Range(0f, 1f)]
    [SerializeField] private float fanStraightRailSpawnChance = 0.01f;

    [Header("Coins")]
    [Tooltip("Si está activo, la cantidad de monedas se elige aleatoriamente entre mínimo y máximo.")]
    [SerializeField] private bool useRandomCoinCount = true;

    [Tooltip("Cantidad exacta de monedas cuando Use Random Coin Count está desactivado.")]
    [SerializeField] private int fixedCoinCount = 20;

    [Tooltip("Cantidad mínima de monedas cuando se usa cantidad aleatoria.")]
    [SerializeField] private int minRandomCoinCount = 10;

    [Tooltip("Cantidad máxima de monedas cuando se usa cantidad aleatoria.")]
    [SerializeField] private int maxRandomCoinCount = 30;

    [Header("Allowed Prefabs")]
    [Tooltip("Cajas permitidas o modificadas para este nivel.")]
    [SerializeField] private List<LevelSpawnPrefabOverride> boxOverrides = new List<LevelSpawnPrefabOverride>();

    [Tooltip("Muros permitidos o modificados para este nivel.")]
    [SerializeField] private List<LevelSpawnPrefabOverride> wallOverrides = new List<LevelSpawnPrefabOverride>();

    [Tooltip("Pelotas permitidas o modificadas para este nivel.")]
    [SerializeField] private List<LevelSpawnPrefabOverride> ballOverrides = new List<LevelSpawnPrefabOverride>();

    [Tooltip("Ventiladores permitidos o modificados para este nivel.")]
    [SerializeField] private List<LevelSpawnPrefabOverride> fanOverrides = new List<LevelSpawnPrefabOverride>();

    [Tooltip("Monedas permitidas o modificadas para este nivel.")]
    [SerializeField] private List<LevelSpawnPrefabOverride> coinOverrides = new List<LevelSpawnPrefabOverride>();

    public TrackContentGenerationProfile ContentCatalog => contentCatalog;
    public bool UseOverridesAsWhitelist => useOverridesAsWhitelist;

    public bool EnableBoxes => enableBoxes;
    public bool EnableWalls => enableWalls;
    public bool EnableBalls => enableBalls;
    public bool EnableFans => enableFans;
    public bool EnableCoins => enableCoins;
    public bool EnableGoal => enableGoal;

    public float BoxSpawnChance => boxSpawnChance;
    public float WallSpawnChance => wallSpawnChance;
    public float BallFlatSpawnChance => ballFlatSpawnChance;
    public float BallNarrowSpawnChance => ballNarrowSpawnChance;
    public float BallRailSpawnChance => ballRailSpawnChance;
    public float BallBeforeDownSlopeChance => ballBeforeDownSlopeChance;
    public float FanFlatSpawnChance => fanFlatSpawnChance;
    public float FanStraightRailSpawnChance => fanStraightRailSpawnChance;

    public bool UseRandomCoinCount => useRandomCoinCount;
    public int FixedCoinCount => Mathf.Max(0, fixedCoinCount);
    public int MinRandomCoinCount => Mathf.Max(0, minRandomCoinCount);
    public int MaxRandomCoinCount => Mathf.Max(MinRandomCoinCount, maxRandomCoinCount);

    public IReadOnlyList<LevelSpawnPrefabOverride> BoxOverrides => boxOverrides;
    public IReadOnlyList<LevelSpawnPrefabOverride> WallOverrides => wallOverrides;
    public IReadOnlyList<LevelSpawnPrefabOverride> BallOverrides => ballOverrides;
    public IReadOnlyList<LevelSpawnPrefabOverride> FanOverrides => fanOverrides;
    public IReadOnlyList<LevelSpawnPrefabOverride> CoinOverrides => coinOverrides;

    private void OnValidate()
    {
        fixedCoinCount = Mathf.Max(0, fixedCoinCount);
        minRandomCoinCount = Mathf.Max(0, minRandomCoinCount);
        maxRandomCoinCount = Mathf.Max(minRandomCoinCount, maxRandomCoinCount);

        NormalizeOverrides(boxOverrides);
        NormalizeOverrides(wallOverrides);
        NormalizeOverrides(ballOverrides);
        NormalizeOverrides(fanOverrides);
        NormalizeOverrides(coinOverrides);
    }

    private static void NormalizeOverrides(List<LevelSpawnPrefabOverride> overrides)
    {
        if (overrides == null)
        {
            return;
        }

        for (int i = 0; i < overrides.Count; i++)
        {
            overrides[i]?.Normalize();
        }
    }
}