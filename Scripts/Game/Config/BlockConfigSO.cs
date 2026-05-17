using UnityEngine;

namespace MobControl.Config
{
    /// <summary>
    /// Datos de configuración de un bloque obstáculo.
    /// Separado en ScriptableObject para permitir múltiples tipos
    /// (madera, piedra, metal) con diferente HP sin modificar código.
    /// </summary>
    [CreateAssetMenu(fileName = "BlockConfig", menuName = "MobControl/Block Config")]
    public class BlockConfigSO : ScriptableObject
    {
        [SerializeField, Tooltip("Puntos de vida del bloque. Cada unidad aliada que impacta resta 1.")]
        private int _baseHP = 20;

        [SerializeField, Tooltip("Color del bloque con HP completo.")]
        private Color _fullColor = new Color(0.8f, 0.2f, 0.2f, 1f);

        [SerializeField, Tooltip("Color del bloque cuando le queda poca vida (< 25% HP).")]
        private Color _lowHPColor = new Color(1f, 0.5f, 0f, 1f);

        [SerializeField, Tooltip("Recompensa en monedas al destruir el bloque (Fase 7).")]
        private int _coinReward = 5;

        public int   BaseHP     => _baseHP;
        public Color FullColor  => _fullColor;
        public Color LowHPColor => _lowHPColor;
        public int   CoinReward => _coinReward;
    }
}