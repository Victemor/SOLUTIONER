using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MobControl.UI
{
    /// <summary>
    /// Barra de carga genérica (0-1).
    /// Puede configurarse desde el Inspector o programáticamente
    /// desde GameplayHUD mediante SetupReferences().
    /// </summary>
    public class ChargeBarController : MonoBehaviour
    {
        [SerializeField, Tooltip("Image de relleno (Image Type: Filled, Horizontal). " +
                                 "Se puede dejar vacío si se llama SetupReferences() por código.")]
        private Image _fillImage;

        [SerializeField] private Color _normalColor = new Color(0.3f, 0.7f, 1f, 1f);
        [SerializeField] private Color _fullColor   = new Color(0.2f, 1f, 0.4f, 1f);

        // ── Estado ───────────────────────────────────────────────────────

        public float Charge { get; private set; }
        public bool  IsFull => Charge >= 1f;

        /// <summary>Disparado una sola vez cuando la barra llega al máximo.</summary>
        public event Action OnChargeFull;

        private bool _notifiedFull;

        // ── Inicialización por código (GameplayHUD) ───────────────────────

        /// <summary>
        /// GameplayHUD llama este método para inyectar las referencias
        /// generadas programáticamente, evitando setup manual en el Inspector.
        /// </summary>
        public void SetupReferences(Image fillImage, Color normalColor, Color fullColor)
        {
            _fillImage   = fillImage;
            _normalColor = normalColor;
            _fullColor   = fullColor;
            Charge       = 0f;
            _notifiedFull = false;
            RefreshVisual();
        }

        // ── API pública ──────────────────────────────────────────────────

        public void AddCharge(float amount)
        {
            if (IsFull) return;

            Charge = Mathf.Clamp01(Charge + amount);
            RefreshVisual();

            if (IsFull && !_notifiedFull)
            {
                _notifiedFull = true;
                OnChargeFull?.Invoke();
            }
        }

        public void ResetCharge()
        {
            Charge        = 0f;
            _notifiedFull = false;
            RefreshVisual();
        }

        // ── Visual ───────────────────────────────────────────────────────

        private void RefreshVisual()
        {
            if (_fillImage == null) return;
            _fillImage.fillAmount = Charge;
            _fillImage.color      = IsFull ? _fullColor : _normalColor;
        }
    }
}