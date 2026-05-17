using System;
using UnityEngine;

namespace MobControl.Config
{
    /// <summary>
    /// Define la posición lógica de un slot dentro del nivel.
    /// Se convierte a posición de mundo por TrackGenerator.
    /// Struct porque es un dato pequeño e inmutable usado solo para configuración.
    /// </summary>
    [Serializable]
    public struct SlotData
    {
        [Tooltip("Índice del carril donde aparece el objeto (0 = izquierda).")]
        public int LaneIndex;

        [Tooltip("Posición normalizada a lo largo de la pista (0 = inicio junto al cañón, 1 = final junto a torretas).")]
        [Range(0f, 1f)]
        public float ZNormalized;
    }
}
