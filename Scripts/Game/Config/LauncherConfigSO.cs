using UnityEngine;

namespace MobControl.Config
{
    /// <summary>
    /// Parámetros de comportamiento del cañón.
    /// Incluye movimiento, disparo básico, barras de carga y el Súper Soldado.
    /// </summary>
    [CreateAssetMenu(fileName = "LauncherConfig", menuName = "MobControl/Launcher Config")]
    public class LauncherConfigSO : ScriptableObject
    {
        [Header("Movimiento")]
        [SerializeField, Tooltip("Sensibilidad del swipe horizontal. Mayor = cañón más reactivo.")]
        private float _moveSensitivity = 3f;

        [SerializeField, Tooltip("Margen desde el borde de la pista que el cañón no puede superar.")]
        private float _horizontalMargin = 0.3f;

        [Header("Disparo básico")]
        [SerializeField, Tooltip("Unidades básicas disparadas por segundo.")]
        private float _fireRate = 8f;

        [SerializeField, Tooltip("Velocidad de movimiento de las unidades hacia las torretas.")]
        private float _unitMoveSpeed = 7f;

        [SerializeField, Tooltip("Velocidad de corrección lateral de las unidades hacia la torreta.")]
        private float _unitSteerSpeed = 5f;

        [Header("Barra Izquierda — Súper Soldado")]
        [SerializeField, Tooltip("Cuánto carga cada disparo normal a la barra del Súper Soldado (0-1).")]
        private float _superSoldierChargeRate = 0.033f; // llena en ~30 disparos

        [Header("Barra Derecha — Unidades Especiales")]
        [SerializeField, Tooltip("Cuánto carga cada disparo normal a la barra de Especiales (0-1).")]
        private float _specialUnitChargeRate = 0.05f; // llena en ~20 disparos

        [SerializeField, Tooltip("Segundos que dura el burst de unidades especiales.")]
        private float _specialUnitDuration = 4f;

        [SerializeField, Tooltip("Unidades especiales disparadas por segundo durante el burst. " +
                                 "Más alto que _fireRate para que el burst sea notablemente distinto.")]
        private float _specialUnitFireRate = 14f;

        [SerializeField, Tooltip("Color de las unidades especiales (placeholder).")]
        private Color _specialUnitColor = Color.cyan;

        [Header("Súper Soldado")]
        [SerializeField, Tooltip("HP inicial del Súper Soldado (equivale a N unidades del ejército).")]
        private int _superSoldierMaxHP = 30;

        [SerializeField, Tooltip("Velocidad de movimiento del Súper Soldado. Más lento que unidades normales.")]
        private float _superSoldierMoveSpeed = 4.5f;

        [SerializeField, Tooltip("Velocidad de corrección lateral del Súper Soldado.")]
        private float _superSoldierSteerSpeed = 3f;

        [SerializeField, Tooltip("Color del Súper Soldado (placeholder).")]
        private Color _superSoldierColor = new Color(1f, 0.85f, 0f, 1f); // dorado

        // ── Propiedades ──────────────────────────────────────────────────

        public float MoveSensitivity        => _moveSensitivity;
        public float HorizontalMargin       => _horizontalMargin;
        public float FireRate               => _fireRate;
        public float UnitMoveSpeed          => _unitMoveSpeed;
        public float UnitSteerSpeed         => _unitSteerSpeed;

        public float SuperSoldierChargeRate => _superSoldierChargeRate;
        public float SpecialUnitChargeRate  => _specialUnitChargeRate;
        public float SpecialUnitDuration    => _specialUnitDuration;
        public float SpecialUnitFireRate    => _specialUnitFireRate;
        public Color SpecialUnitColor       => _specialUnitColor;

        public int   SuperSoldierMaxHP      => _superSoldierMaxHP;
        public float SuperSoldierMoveSpeed  => _superSoldierMoveSpeed;
        public float SuperSoldierSteerSpeed => _superSoldierSteerSpeed;
        public Color SuperSoldierColor      => _superSoldierColor;
    }
}