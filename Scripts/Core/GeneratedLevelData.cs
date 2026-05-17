using System.Collections.Generic;
using UnityEngine;
using MobControl.Config;

namespace MobControl.Core
{
    /// <summary>
    /// Datos puros de una torreta generada.
    /// Prefab + config + override de HP para escalar dificultad sin crear
    /// un ScriptableObject por cada valor posible.
    /// </summary>
    public class TurretGenEntry
    {
        public GameObject    Prefab;
        public TurretConfigSO Config;
        /// <summary>Si > 0 sobreescribe Config.BaseHP. Permite escalar HP numéricamente.</summary>
        public int           HPOverride;
    }

    /// <summary>
    /// Datos puros de un panel generado.
    /// ValueOverride permite al generador ajustar el valor del panel
    /// para garantizar viabilidad sin assets adicionales.
    /// </summary>
    public class PanelGenEntry
    {
        public GameObject   Prefab;
        public PanelConfigSO Config;
        /// <summary>Si > 0 sobreescribe Config.OperationValue.</summary>
        public float        ValueOverride;
    }

    /// <summary>Datos puros de un bloque generado.</summary>
    public class BlockGenEntry
    {
        public GameObject  Prefab;
        public BlockConfigSO Config;
    }

    /// <summary>Datos puros de una fase generada.</summary>
    public class GeneratedPhaseData
    {
        public List<TurretGenEntry> Turrets = new List<TurretGenEntry>();
        public List<PanelGenEntry>  Panels  = new List<PanelGenEntry>();
        public List<BlockGenEntry>  Blocks  = new List<BlockGenEntry>();
    }

    /// <summary>
    /// Resultado completo de la generación de un nivel.
    /// Inmutable una vez construido por LevelGenerator.
    /// </summary>
    public class GeneratedLevelData
    {
        public int                       LevelIndex;
        public int                       Seed;
        public bool                      IsBossLevel;
        public List<GeneratedPhaseData>  Phases = new List<GeneratedPhaseData>();
    }
}