using UnityEngine;

namespace MobControl.Config
{
    /// <summary>
    /// Configuración completa de una torreta enemiga.
    /// Incluye HP, spawn de unidades enemigas e INCOMING.
    /// </summary>
    [CreateAssetMenu(fileName = "TurretConfig", menuName = "MobControl/Turret Config")]
    public class TurretConfigSO : ScriptableObject
    {
        [Header("Vida")]
        [SerializeField, Tooltip("HP inicial de la torreta.")]
        private int _baseHP = 30;

        [Header("Spawn de unidades enemigas")]
        [SerializeField, Tooltip("Unidades enemigas spawneadas por segundo en modo normal. " +
                                 "0 = la torreta no dispara (útil para Fase 1-2).")]
        private float _baseSpawnRate = 1.5f;

        [SerializeField, Tooltip("Velocidad de movimiento de las unidades enemigas spawneadas.")]
        private float _enemyUnitSpeed = 5f;

        [Header("INCOMING")]
        [SerializeField, Tooltip("Segundos entre cada activación de INCOMING.")]
        private float _incomingCooldown = 10f;

        [SerializeField, Tooltip("Unidades enemigas por segundo durante el INCOMING.")]
        private float _incomingSpawnRate = 5f;

        [SerializeField, Tooltip("Duración en segundos de cada oleada INCOMING.")]
        private float _incomingDuration = 3f;

        [SerializeField, Tooltip("Color de la torreta durante el INCOMING (feedback visual).")]
        private Color _incomingColor = new Color(1f, 0.4f, 0f, 1f);

        [Header("Recompensa")]
        [SerializeField, Tooltip("Monedas otorgadas al destruir esta torreta (Fase 7).")]
        private int _coinReward = 10;

        // ── Propiedades ──────────────────────────────────────────────────

        public int   BaseHP           => _baseHP;
        public float BaseSpawnRate    => _baseSpawnRate;
        public float EnemyUnitSpeed   => _enemyUnitSpeed;
        public float IncomingCooldown => _incomingCooldown;
        public float IncomingSpawnRate => _incomingSpawnRate;
        public float IncomingDuration => _incomingDuration;
        public Color IncomingColor    => _incomingColor;
        public int   CoinReward       => _coinReward;
    }
}