using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MobControl.Config;
using MobControl.Core;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Genera niveles de forma procedural a partir de LevelGeneratorConfigSO.
    ///
    /// DETERMINISMO Y SEMILLAS:
    /// Usa System.Random (no UnityEngine.Random, que es estado global).
    /// seed = config.BaseSeed + levelIndex * 7919 (primo para distribuir bien los seeds).
    /// La misma combinación de BaseSeed + levelIndex siempre produce el mismo nivel.
    /// El sessionSeed (configurable en LevelManager) permite al jugador compartir
    /// seeds interesantes o reproducir runs específicos.
    ///
    /// GARANTÍA DE VIABILIDAD:
    /// Después de generar, valida que startingUnits * mejorMultiplicador > HP total torretas.
    /// Si falla, escala el mejor panel positivo hasta que el nivel sea completable.
    ///
    /// PANELES NEGATIVOS:
    /// Siempre existe al menos un carril completamente libre de paneles negativos.
    /// Los negativos se añaden en carriles distintos a los del mejor camino positivo.
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        [SerializeField, Tooltip("Configuración maestra del generador.")]
        private LevelGeneratorConfigSO _config;

        /// <summary>
        /// Genera los datos de un nivel dado un índice y una semilla de sesión.
        /// No instancia GameObjects — solo produce datos puros.
        /// </summary>
        public GeneratedLevelData Generate(int levelIndex, int sessionSeed)
        {
            if (_config == null)
            {
                Debug.LogError("[LevelGenerator] LevelGeneratorConfigSO no asignado.", this);
                return null;
            }

            // Semilla determinista: mismos parámetros = mismo nivel siempre
            int seed = sessionSeed + levelIndex * 7919;
            var rng  = new System.Random(seed);

            bool isBoss = levelIndex > 0 && levelIndex % _config.BossEveryNLevels == 0;

            GeneratedLevelData levelData = new GeneratedLevelData
            {
                LevelIndex  = levelIndex,
                Seed        = seed,
                IsBossLevel = isBoss
            };

            if (isBoss)
                GenerateBossLevel(levelData, rng, levelIndex);
            else
                GenerateNormalLevel(levelData, rng, levelIndex);

            Debug.Log($"[LevelGenerator] Nivel {levelIndex} generado " +
                      $"(seed={seed}, boss={isBoss}).");

            return levelData;
        }

        // ── Generación de nivel normal (3 fases) ─────────────────────────

        private void GenerateNormalLevel(GeneratedLevelData data,
                                         System.Random rng,
                                         int levelIndex)
        {
            DifficultyRange range = _config.GetRangeForLevel(levelIndex);

            for (int phase = 0; phase < 3; phase++)
            {
                // La dificultad sube progresivamente dentro del nivel
                float phaseMultiplier = 1f + phase * 0.3f;

                GeneratedPhaseData phaseData = GeneratePhase(rng, range, levelIndex, phaseMultiplier);
                EnsureViability(phaseData, rng, _config.StartingPlayerUnits);
                data.Phases.Add(phaseData);
            }
        }

        // ── Generación de nivel jefe (2 fases) ───────────────────────────

        private void GenerateBossLevel(GeneratedLevelData data,
                                       System.Random rng,
                                       int levelIndex)
        {
            // Fase 1 del jefe: torreta normal pero con más HP
            GeneratedPhaseData phase1 = new GeneratedPhaseData();
            phase1.Turrets.Add(new TurretGenEntry
            {
                Prefab     = _config.BossTurretPhase1Prefab,
                Config     = _config.BossPhase1Config,
                HPOverride = 0
            });
            // Fase 1 sin paneles negativos para no hacer imposible el inicio
            AddPositivePanels(phase1, rng, 2);
            EnsureViability(phase1, rng, _config.StartingPlayerUnits);
            data.Phases.Add(phase1);

            // Fase 2 del jefe: BossTurretController con ataque de fila
            GeneratedPhaseData phase2 = new GeneratedPhaseData();
            phase2.Turrets.Add(new TurretGenEntry
            {
                Prefab     = _config.BossTurretPhase2Prefab,
                Config     = _config.BossPhase2Config,
                HPOverride = 0
            });
            AddPositivePanels(phase2, rng, 3); // más ayuda en fase 2 para compensar el ataque
            EnsureViability(phase2, rng, _config.StartingPlayerUnits);
            data.Phases.Add(phase2);
        }

        // ── Generación de una fase ────────────────────────────────────────

        private GeneratedPhaseData GeneratePhase(System.Random rng,
                                                  DifficultyRange range,
                                                  int levelIndex,
                                                  float phaseMultiplier)
        {
            var phase = new GeneratedPhaseData();

            AddTurrets(phase, rng, range, levelIndex, phaseMultiplier);
            AddPositivePanels(phase, rng, rng.Next(range.MinPositivePanels,
                                                    range.MaxPositivePanels + 1));

            if (range.AllowNegativePanels && _config.NegativePanelConfigs?.Length > 0)
                AddNegativePanels(phase, rng, range);

            AddBlocks(phase, rng, range);

            return phase;
        }

        // ── Torretas ──────────────────────────────────────────────────────

        private void AddTurrets(GeneratedPhaseData phase,
                                 System.Random rng,
                                 DifficultyRange range,
                                 int levelIndex,
                                 float phaseMultiplier)
        {
            int count = rng.Next(range.MinTurrets, range.MaxTurrets + 1);
            TurretConfigSO config = SelectTurretConfig(levelIndex);

            float hpMult = Lerp(range.MinHPMultiplier, range.MaxHPMultiplier,
                                (float)rng.NextDouble()) * phaseMultiplier;

            for (int i = 0; i < count; i++)
            {
                phase.Turrets.Add(new TurretGenEntry
                {
                    Prefab     = _config.TurretPrefab,
                    Config     = config,
                    HPOverride = Mathf.RoundToInt(config.BaseHP * hpMult)
                });
            }
        }

        private TurretConfigSO SelectTurretConfig(int levelIndex)
        {
            if (levelIndex <= 5)  return _config.TurretEasy;
            if (levelIndex <= 15) return _config.TurretMedium;
            if (levelIndex <= 30) return _config.TurretHard;
            return _config.TurretExtreme;
        }

        // ── Paneles positivos ─────────────────────────────────────────────

        private void AddPositivePanels(GeneratedPhaseData phase,
                                        System.Random rng,
                                        int count)
        {
            if (_config.PositivePanelConfigs == null ||
                _config.PositivePanelConfigs.Length == 0) return;

            for (int i = 0; i < count; i++)
            {
                int idx    = rng.Next(_config.PositivePanelConfigs.Length);
                bool moving = false; // viabilidad: los positivos no se mueven por defecto

                phase.Panels.Add(new PanelGenEntry
                {
                    Prefab        = moving ? _config.MovingPanelPrefab : _config.PanelPrefab,
                    Config        = _config.PositivePanelConfigs[idx],
                    ValueOverride = 0f
                });
            }
        }

        // ── Paneles negativos ─────────────────────────────────────────────

        /// <summary>
        /// Los paneles negativos siempre van en carriles distintos al mejor positivo.
        /// Se añaden al final de la lista para que PhaseGenerator los ubique
        /// en los carriles restantes (distribución por índice % laneCount).
        /// </summary>
        private void AddNegativePanels(GeneratedPhaseData phase,
                                        System.Random rng,
                                        DifficultyRange range)
        {
            int count = rng.Next(1, range.MaxNegativePanels + 1);

            for (int i = 0; i < count; i++)
            {
                int idx = rng.Next(_config.NegativePanelConfigs.Length);

                // Chance de panel móvil
                bool moving = range.AllowMovingPanels &&
                              rng.NextDouble() < range.MovingPanelChance;

                phase.Panels.Add(new PanelGenEntry
                {
                    Prefab        = moving ? _config.MovingPanelPrefab : _config.PanelPrefab,
                    Config        = _config.NegativePanelConfigs[idx],
                    ValueOverride = 0f
                });
            }
        }

        // ── Bloques ───────────────────────────────────────────────────────

        private void AddBlocks(GeneratedPhaseData phase,
                                System.Random rng,
                                DifficultyRange range)
        {
            if (_config.BlockConfigs == null || _config.BlockConfigs.Length == 0) return;

            int count = rng.Next(range.MinBlocks, range.MaxBlocks + 1);
            for (int i = 0; i < count; i++)
            {
                int idx = rng.Next(_config.BlockConfigs.Length);
                phase.Blocks.Add(new BlockGenEntry
                {
                    Prefab = _config.BlockPrefab,
                    Config = _config.BlockConfigs[idx]
                });
            }
        }

        // ── Validación de viabilidad ──────────────────────────────────────

        /// <summary>
        /// Garantiza que el nivel es completable sin power-ups.
        /// Calcula el mejor multiplicador positivo y compara con el HP total de torretas.
        /// Si falla, escala el mejor panel positivo hasta que pase.
        /// </summary>
        private void EnsureViability(GeneratedPhaseData phase,
                                     System.Random rng,
                                     int startingUnits)
        {
            int totalHP = phase.Turrets.Sum(t =>
                t.HPOverride > 0 ? t.HPOverride : t.Config.BaseHP);

            float bestMultiplier = CalculateBestPositiveMultiplier(phase, startingUnits);
            float projectedUnits = startingUnits * bestMultiplier;

            if (projectedUnits > totalHP)
            {
                Debug.Log($"[LevelGenerator] Viabilidad OK — " +
                          $"proyectado: {projectedUnits:F0} vs HP: {totalHP}");
                return;
            }

            // Necesitamos que: startingUnits * newMultiplier > totalHP
            // Por tanto: newMultiplier > totalHP / startingUnits
            float requiredMultiplier = (float)totalHP / startingUnits * 1.2f; // +20% margen

            // Escalar el mejor panel positivo
            PanelGenEntry bestPanel = GetBestPositivePanel(phase);
            if (bestPanel == null)
            {
                // Sin paneles positivos, añadir uno que garantice viabilidad
                if (_config.PositivePanelConfigs?.Length > 0)
                {
                    phase.Panels.Insert(0, new PanelGenEntry
                    {
                        Prefab        = _config.PanelPrefab,
                        Config        = _config.PositivePanelConfigs[0],
                        ValueOverride = requiredMultiplier
                    });
                }
                return;
            }

            bestPanel.ValueOverride = requiredMultiplier;

            Debug.Log($"[LevelGenerator] Viabilidad ajustada — " +
                      $"panel escalado a ×{requiredMultiplier:F1}");
        }

        /// <summary>
        /// Calcula el multiplicador acumulado del mejor camino positivo disponible.
        /// Considera paneles en diferentes carriles (solo se puede tomar uno por Z).
        /// </summary>
        private float CalculateBestPositiveMultiplier(GeneratedPhaseData phase,
                                                       int startingUnits)
        {
            float best = 1f;
            float current = startingUnits;

            foreach (PanelGenEntry panel in phase.Panels)
            {
                if (panel.Config == null) continue;

                float value = panel.ValueOverride > 0f
                    ? panel.ValueOverride
                    : panel.Config.OperationValue;

                OperationType type = panel.Config.OperationType;

                if (type == OperationType.Multiply || type == OperationType.Add)
                {
                    float result = type == OperationType.Multiply
                        ? current * value
                        : current + value;

                    best = Mathf.Max(best, result / startingUnits);
                }
            }

            return best;
        }

        private PanelGenEntry GetBestPositivePanel(GeneratedPhaseData phase)
        {
            PanelGenEntry best     = null;
            float         bestVal  = 0f;

            foreach (PanelGenEntry panel in phase.Panels)
            {
                if (panel.Config == null) continue;

                OperationType type = panel.Config.OperationType;
                if (type != OperationType.Multiply && type != OperationType.Add)
                    continue;

                float val = panel.ValueOverride > 0 ? panel.ValueOverride : panel.Config.OperationValue;
                if (val > bestVal) { bestVal = val; best = panel; }
            }

            return best;
        }

        // ── Utilidad ─────────────────────────────────────────────────────

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}