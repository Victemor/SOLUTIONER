using System.Collections.Generic;
using UnityEngine;
using MobControl.Config;
using MobControl.Core;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Instancia y posiciona los objetos de una fase usando LevelConfigSO
    /// como única fuente de verdad para las posiciones de mundo.
    ///
    /// LAYOUT (en % del largo de la pista, de cañón a torretas):
    ///
    ///  Z=0%           Z=30%       Z=55%      Z=75%      Z=90%
    ///  [CAÑÓN]────────[PANELES]───[BLOQUES]──[......]───[TORRETAS]
    ///
    /// Todas las posiciones usan NormalizedZToWorld() para que el layout
    /// funcione correctamente sin importar dónde esté el TrackOrigin.
    /// </summary>
    public class PhaseGenerator : MonoBehaviour
    {
        [SerializeField] private LevelConfigSO      _levelConfig;
        [SerializeField] private EnemyUnitPool      _enemyUnitPool;
        [SerializeField] private ArmyManager        _armyManager;
        [SerializeField] private UnitPool           _unitPool;
        [SerializeField] private Transform          _cannonTransform;
        [SerializeField] private LauncherController _launcher;

        // ── Ratios del layout (0 = cañón, 1 = torretas) ─────────────────

        /// <summary>Zona donde spawnean las torretas enemigas.</summary>
        private const float TurretZRatio = 0.90f;

        /// <summary>Donde empieza el primer panel.</summary>
        private const float PanelStartZRatio = 0.30f;

        /// <summary>Separación en Z entre paneles del mismo carril.</summary>
        private const float PanelZSpacing = 5f;

        /// <summary>Donde empieza el primer bloque.</summary>
        private const float BlockStartZRatio = 0.60f;

        /// <summary>Separación en Z entre bloques del mismo carril.</summary>
        private const float BlockZSpacing = 4f;

        private static readonly Vector3 DefaultPanelScale = new Vector3(2f, 0.15f, 1.5f);
        private static readonly Vector3 DefaultBlockScale = new Vector3(1.8f, 1f, 1f);

        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

        // ── API pública ──────────────────────────────────────────────────

        /// <summary>Genera la fase desde datos del LevelGenerator.</summary>
        public void GenerateFromData(GeneratedPhaseData data)
        {
            Clear();
            if (data == null) return;

            SpawnTurretsFromData(data.Turrets);
            SpawnPanelsFromData(data.Panels);
            SpawnBlocksFromData(data.Blocks);
        }

        /// <summary>Genera la fase desde arrays de prefabs (modo manual/debug).</summary>
        public void Generate(GameObject[] turretPrefabs,
                             GameObject[] panelPrefabs,
                             GameObject[] blockPrefabs = null)
        {
            Clear();
            SpawnTurretsPrefabs(turretPrefabs);
            SpawnPanelsPrefabs(panelPrefabs);
            SpawnBlocksPrefabs(blockPrefabs);
        }

        public void Clear()
        {
            foreach (GameObject go in _spawnedObjects)
                if (go != null) Destroy(go);
            _spawnedObjects.Clear();
        }

        // ── Spawn de torretas ────────────────────────────────────────────

        private void SpawnTurretsFromData(List<TurretGenEntry> entries)
        {
            if (entries == null || entries.Count == 0) return;

            float worldZ  = _levelConfig.NormalizedZToWorld(TurretZRatio);
            float spacing = _levelConfig.TrackWidth / (entries.Count + 1);
            float startX  = _levelConfig.LeftBound + spacing;

            for (int i = 0; i < entries.Count; i++)
            {
                TurretGenEntry entry = entries[i];
                if (entry?.Prefab == null) continue;

                Vector3 pos = new Vector3(
                    startX + spacing * i,
                    _levelConfig.GameplayYOffset,
                    worldZ
                );

                GameObject  go     = Instantiate(entry.Prefab, pos, Quaternion.identity, transform);
                EnemyTurret turret = go.GetComponent<EnemyTurret>()
                                    ?? go.GetComponentInChildren<EnemyTurret>();

                if (turret != null)
                {
                    turret.Initialize(entry.Config, entry.HPOverride);
                    // Pasar el Transform completo del cañón para que las unidades
                    // enemigas sigan su posición X dinámica en tiempo real
                    turret.InjectDependencies(_enemyUnitPool, _cannonTransform, _armyManager);

                    if (turret is BossTurretController boss)
                        boss.InjectBossDependencies(_levelConfig, _launcher);
                }
                else
                    Debug.LogWarning($"[PhaseGenerator] '{entry.Prefab.name}' sin EnemyTurret.", this);

                _spawnedObjects.Add(go);
            }
        }

        private void SpawnPanelsFromData(List<PanelGenEntry> entries)
        {
            if (entries == null || entries.Count == 0) return;

            int[]  countPerLane = new int[_levelConfig.LaneCount];
            float  startZ       = _levelConfig.NormalizedZToWorld(PanelStartZRatio);

            for (int i = 0; i < entries.Count; i++)
            {
                PanelGenEntry entry = entries[i];
                if (entry?.Prefab == null) continue;

                int   lane   = i % _levelConfig.LaneCount;
                float laneX  = _levelConfig.GetLaneCenterX(lane);
                float panelZ = startZ + countPerLane[lane] * PanelZSpacing;
                countPerLane[lane]++;

                Vector3    pos   = new Vector3(laneX, 0.1f, panelZ);
                GameObject go    = Instantiate(entry.Prefab, pos, Quaternion.identity, transform);
                go.transform.localScale = DefaultPanelScale;

                PanelController panel = go.GetComponent<PanelController>()
                                       ?? go.GetComponentInChildren<PanelController>();
                panel?.Initialize(entry.Config, _armyManager, _unitPool, entry.ValueOverride);

                _spawnedObjects.Add(go);
            }
        }

        private void SpawnBlocksFromData(List<BlockGenEntry> entries)
        {
            if (entries == null || entries.Count == 0) return;

            int[]  countPerLane = new int[_levelConfig.LaneCount];
            float  startZ       = _levelConfig.NormalizedZToWorld(BlockStartZRatio);

            for (int i = 0; i < entries.Count; i++)
            {
                BlockGenEntry entry = entries[i];
                if (entry?.Prefab == null) continue;

                int   lane   = i % _levelConfig.LaneCount;
                float laneX  = _levelConfig.GetLaneCenterX(lane);
                float blockZ = startZ + countPerLane[lane] * BlockZSpacing;
                countPerLane[lane]++;

                Vector3    pos   = new Vector3(laneX, _levelConfig.GameplayYOffset, blockZ);
                GameObject go    = Instantiate(entry.Prefab, pos, Quaternion.identity, transform);
                go.transform.localScale = DefaultBlockScale;

                BlockController block = go.GetComponent<BlockController>()
                                       ?? go.GetComponentInChildren<BlockController>();
                if (block != null)
                {
                    block.Initialize(entry.Config);
                    block.InjectSceneReferences(_armyManager);
                }

                _spawnedObjects.Add(go);
            }
        }

        // ── Fallback manual ───────────────────────────────────────────────

        private void SpawnTurretsPrefabs(GameObject[] prefabs)
        {
            if (prefabs == null) return;
            var entries = new List<TurretGenEntry>();
            foreach (var p in prefabs)
                if (p != null) entries.Add(new TurretGenEntry { Prefab = p });
            SpawnTurretsFromData(entries);
        }

        private void SpawnPanelsPrefabs(GameObject[] prefabs)
        {
            if (prefabs == null) return;
            var entries = new List<PanelGenEntry>();
            foreach (var p in prefabs)
                if (p != null) entries.Add(new PanelGenEntry { Prefab = p });
            SpawnPanelsFromData(entries);
        }

        private void SpawnBlocksPrefabs(GameObject[] prefabs)
        {
            if (prefabs == null) return;
            var entries = new List<BlockGenEntry>();
            foreach (var p in prefabs)
                if (p != null) entries.Add(new BlockGenEntry { Prefab = p });
            SpawnBlocksFromData(entries);
        }
    }
}