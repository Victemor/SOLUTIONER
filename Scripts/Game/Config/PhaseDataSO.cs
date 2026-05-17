using System;
using UnityEngine;

namespace MobControl.Config
{
    /// <summary>
    /// Datos de una fase del nivel: qué torretas y paneles spawnear y dónde.
    /// Un asset por fase → fácil de editar sin tocar código.
    ///
    /// Las posiciones son en espacio de mundo (no relativas) para que el
    /// diseñador pueda colocar un GameObject vacío como referencia visual
    /// y copiar su Transform directamente aquí.
    /// </summary>
    [CreateAssetMenu(fileName = "PhaseData", menuName = "MobControl/Phase Data")]
    public class PhaseDataSO : ScriptableObject
    {
        [Header("Cañón")]
        [SerializeField, Tooltip("Posición X donde el cañón aparece al iniciar esta fase. " +
                                 "Y y Z se ignoran — el cañón mantiene su posición fija en Z.")]
        private float _cannonStartX = 0f;

        [Header("Torretas")]
        [SerializeField, Tooltip("Torretas que se spawnean en esta fase.")]
        private TurretSpawnData[] _turrets;

        [Header("Paneles")]
        [SerializeField, Tooltip("Paneles de multiplicación de esta fase.")]
        private PanelSpawnData[] _panels;

        // ── Propiedades ──────────────────────────────────────────────────

        public float            CannonStartX => _cannonStartX;
        public TurretSpawnData[] Turrets     => _turrets  ?? Array.Empty<TurretSpawnData>();
        public PanelSpawnData[]  Panels      => _panels   ?? Array.Empty<PanelSpawnData>();
    }

    // ── Structs de spawn ─────────────────────────────────────────────────

    /// <summary>Posición y configuración de una torreta en una fase.</summary>
    [Serializable]
    public class TurretSpawnData
    {
        [Tooltip("ScriptableObject con HP, recompensa, etc.")]
        public TurretConfigSO Config;

        [Tooltip("Posición de mundo donde se instancia la torreta.")]
        public Vector3 Position;
    }

    /// <summary>Posición, escala y configuración de un panel en una fase.</summary>
    [Serializable]
    public class PanelSpawnData
    {
        [Tooltip("ScriptableObject con operación, colores, etc.")]
        public PanelConfigSO Config;

        [Tooltip("Posición de mundo donde se instancia el panel.")]
        public Vector3 Position;

        [Tooltip("Escala del panel. X = ancho del carril, Y = altura (fino), Z = profundidad.")]
        public Vector3 Scale = new Vector3(2f, 0.15f, 1.5f);
    }
}