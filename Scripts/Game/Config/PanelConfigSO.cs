using UnityEngine;
using MobControl.Core;

namespace MobControl.Config
{
    /// <summary>
    /// Datos de configuración de un panel de multiplicación.
    /// Un ScriptableObject por tipo de panel (×2, ×3, +50, etc.).
    /// Múltiples PanelControllers en la escena pueden compartir el mismo asset.
    /// </summary>
    [CreateAssetMenu(fileName = "PanelConfig", menuName = "MobControl/Panel Config")]
    public class PanelConfigSO : ScriptableObject
    {
        [Header("Operación")]
        [SerializeField, Tooltip("Tipo de operación que aplica este panel al ejército aliado.")]
        private OperationType _operationType = OperationType.Multiply;

        [SerializeField, Tooltip("Valor de la operación. Ej: tipo Multiply valor 3 → ×3.")]
        private float _operationValue = 2f;

        [Header("Visual")]
        [SerializeField, Tooltip("Color del panel en estado normal.")]
        private Color _normalColor = new Color(0.2f, 0.65f, 1f, 1f);

        [SerializeField, Tooltip("Color del panel al ser activado.")]
        private Color _activatedColor = new Color(0.3f, 1f, 0.4f, 1f);

        [SerializeField, Tooltip("Duración del flash de activación en segundos.")]
        private float _flashDuration = 0.25f;

        // ── Propiedades ──────────────────────────────────────────────────

        public OperationType OperationType  => _operationType;
        public float         OperationValue => _operationValue;
        public Color         NormalColor    => _normalColor;
        public Color         ActivatedColor => _activatedColor;
        public float         FlashDuration  => _flashDuration;

        /// <summary>
        /// Texto que el panel muestra al jugador (ej. "X3", "+50").
        /// Usado por PanelController para actualizar el label TMPro.
        /// </summary>
        public string GetLabel()
        {
            string valueStr = _operationValue % 1 == 0
                ? ((int)_operationValue).ToString()
                : _operationValue.ToString("F1");

            return _operationType switch
            {
                OperationType.Multiply => $"X{valueStr}",
                OperationType.Add      => $"+{valueStr}",
                OperationType.Divide   => $"/{valueStr}",
                OperationType.Subtract => $"-{valueStr}",
                _                      => valueStr
            };
        }
    }
}