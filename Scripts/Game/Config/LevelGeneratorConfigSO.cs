using System;
using UnityEngine;

namespace MobControl.Config
{
    /// <summary>
    /// Configuración maestra del generador de niveles.
    /// Contiene todos los prefabs, configs de dificultad y parámetros de generación.
    /// Un solo asset en el proyecto — el diseñador modifica aquí para cambiar
    /// el comportamiento del generador en todos los niveles.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelGeneratorConfig",
                     menuName  = "MobControl/Level Generator Config")]
    public class LevelGeneratorConfigSO : ScriptableObject
    {
        [Header("Semilla y niveles")]
        [SerializeField, Tooltip("Semilla base. Cambiándola se obtienen layouts distintos " +
                                 "manteniendo la misma curva de dificultad.")]
        private int _baseSeed = 42;

        [SerializeField, Tooltip("Cada cuántos niveles aparece un nivel jefe.")]
        private int _bossEveryNLevels = 10;

        [SerializeField, Tooltip("Unidades aliadas al inicio de cada fase.")]
        private int _startingPlayerUnits = 50;

        [Header("Prefabs — Torretas")]
        [SerializeField, Tooltip("Prefab de torreta normal (EnemyTurret).")]
        private GameObject _turretPrefab;

        [SerializeField, Tooltip("Prefab de torreta jefe Fase 1 (EnemyTurret normal, stats más duros).")]
        private GameObject _bossTurretPhase1Prefab;

        [SerializeField, Tooltip("Prefab de torreta jefe Fase 2 (BossTurretController).")]
        private GameObject _bossTurretPhase2Prefab;

        [Header("Prefabs — Paneles")]
        [SerializeField, Tooltip("Prefab de panel estático (PanelController).")]
        private GameObject _panelPrefab;

        [SerializeField, Tooltip("Prefab de panel móvil (PanelController + PanelMover).")]
        private GameObject _movingPanelPrefab;

        [Header("Prefabs — Bloques")]
        [SerializeField, Tooltip("Prefab de bloque obstáculo (BlockController).")]
        private GameObject _blockPrefab;

        [Header("Configs de Torretas")]
        [SerializeField, Tooltip("Config para torretas fáciles (niveles 1-5).")]
        private TurretConfigSO _turretEasy;

        [SerializeField, Tooltip("Config para torretas medias (niveles 6-15).")]
        private TurretConfigSO _turretMedium;

        [SerializeField, Tooltip("Config para torretas difíciles (niveles 16-30).")]
        private TurretConfigSO _turretHard;

        [SerializeField, Tooltip("Config para torretas extremas (niveles 31+).")]
        private TurretConfigSO _turretExtreme;

        [SerializeField, Tooltip("Config para la torreta jefe Fase 1.")]
        private TurretConfigSO _bossPhase1Config;

        [SerializeField, Tooltip("Config para la torreta jefe Fase 2 (spawn agresivo).")]
        private TurretConfigSO _bossPhase2Config;

        [Header("Configs de Paneles Positivos")]
        [SerializeField, Tooltip("Paneles ×N y +N disponibles para generación. " +
                                 "El generador elige aleatoriamente de esta lista.")]
        private PanelConfigSO[] _positivePanelConfigs;

        [Header("Configs de Paneles Negativos (nivel 16+)")]
        [SerializeField, Tooltip("Paneles /N y -N. Solo aparecen en rangos de dificultad avanzados.")]
        private PanelConfigSO[] _negativePanelConfigs;

        [Header("Configs de Bloques")]
        [SerializeField, Tooltip("Configs de bloques disponibles. El generador elige aleatoriamente.")]
        private BlockConfigSO[] _blockConfigs;

        [Header("Rangos de Dificultad")]
        [SerializeField, Tooltip("Cuatro rangos: 1-5, 6-15, 16-30, 31+.")]
        private DifficultyRange[] _difficultyRanges;

        // ── Propiedades ──────────────────────────────────────────────────

        public int   BaseSeed            => _baseSeed;
        public int   BossEveryNLevels    => _bossEveryNLevels;
        public int   StartingPlayerUnits => _startingPlayerUnits;

        public GameObject TurretPrefab          => _turretPrefab;
        public GameObject BossTurretPhase1Prefab => _bossTurretPhase1Prefab;
        public GameObject BossTurretPhase2Prefab => _bossTurretPhase2Prefab;
        public GameObject PanelPrefab            => _panelPrefab;
        public GameObject MovingPanelPrefab      => _movingPanelPrefab;
        public GameObject BlockPrefab            => _blockPrefab;

        public TurretConfigSO TurretEasy       => _turretEasy;
        public TurretConfigSO TurretMedium     => _turretMedium;
        public TurretConfigSO TurretHard       => _turretHard;
        public TurretConfigSO TurretExtreme    => _turretExtreme;
        public TurretConfigSO BossPhase1Config => _bossPhase1Config;
        public TurretConfigSO BossPhase2Config => _bossPhase2Config;

        public PanelConfigSO[] PositivePanelConfigs => _positivePanelConfigs;
        public PanelConfigSO[] NegativePanelConfigs => _negativePanelConfigs;
        public BlockConfigSO[] BlockConfigs          => _blockConfigs;
        public DifficultyRange[] DifficultyRanges    => _difficultyRanges;

        /// <summary>
        /// Devuelve el rango de dificultad correspondiente al nivel dado.
        /// Si no hay rangos configurados, devuelve null.
        /// </summary>
        public DifficultyRange GetRangeForLevel(int levelIndex)
        {
            if (_difficultyRanges == null) return null;

            foreach (DifficultyRange range in _difficultyRanges)
            {
                // MaxLevel == -1 significa "este rango y superiores"
                if (levelIndex >= range.MinLevel &&
                    (range.MaxLevel < 0 || levelIndex <= range.MaxLevel))
                    return range;
            }

            // Fallback: último rango
            return _difficultyRanges[^1];
        }
    }

    // ── Structs de configuración ─────────────────────────────────────────

    /// <summary>
    /// Parámetros de generación para un rango de niveles.
    /// </summary>
    [Serializable]
    public class DifficultyRange
    {
        [Tooltip("Nivel mínimo de este rango.")]
        public int MinLevel = 1;

        [Tooltip("Nivel máximo de este rango. -1 = sin límite superior.")]
        public int MaxLevel = 5;

        [Header("Torretas")]
        [Tooltip("Número mínimo de torretas por fase.")]
        public int MinTurrets = 1;

        [Tooltip("Número máximo de torretas por fase.")]
        public int MaxTurrets = 1;

        [Tooltip("Multiplicador mínimo aplicado al BaseHP de la config de torreta. " +
                 "Permite escalar HP numéricamente por nivel sin nuevos assets.")]
        [Range(0.5f, 5f)]
        public float MinHPMultiplier = 1f;

        [Tooltip("Multiplicador máximo de HP de torreta.")]
        [Range(0.5f, 5f)]
        public float MaxHPMultiplier = 1f;

        [Header("Paneles")]
        [Tooltip("Número mínimo de paneles positivos por fase.")]
        public int MinPositivePanels = 1;

        [Tooltip("Número máximo de paneles positivos por fase.")]
        public int MaxPositivePanels = 2;

        [Tooltip("Si true, el generador puede incluir paneles negativos (/N, -N).")]
        public bool AllowNegativePanels = false;

        [Tooltip("Número máximo de paneles negativos por fase.")]
        public int MaxNegativePanels = 1;

        [Tooltip("Si true, algunos paneles pueden ser móviles.")]
        public bool AllowMovingPanels = false;

        [Range(0f, 1f)]
        [Tooltip("Probabilidad de que un panel sea móvil (0-1).")]
        public float MovingPanelChance = 0f;

        [Header("Bloques")]
        [Tooltip("Número mínimo de bloques obstáculo por fase.")]
        public int MinBlocks = 0;

        [Tooltip("Número máximo de bloques obstáculo por fase.")]
        public int MaxBlocks = 0;
    }
}