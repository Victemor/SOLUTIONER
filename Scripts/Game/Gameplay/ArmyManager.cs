using System;
using UnityEngine;
using MobControl.Core;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Fuente de verdad del ejército aliado.
    /// Gestiona el contador de unidades y el visual placeholder.
    ///
    /// DERROTA:
    /// En Fase 1-2 no existe derrota por unidades agotadas.
    /// La derrota solo ocurre en Fase 4 cuando el ejército enemigo
    /// llega físicamente al cañón del jugador. OnArmyDefeated existe
    /// como contrato para esa fase pero no provoca nada todavía.
    /// </summary>
    public class ArmyManager : MonoBehaviour
    {
        [Header("Visual Placeholder")]
        [SerializeField, Tooltip("Cubo que escala visualmente según el número de unidades.")]
        private Transform _crowdVisual;

        [SerializeField, Tooltip("Escala mínima cuando no hay unidades activas.")]
        private float _minVisualScale = 0.15f;

        [SerializeField, Tooltip("Escala base con 1 unidad.")]
        private float _baseVisualScale = 0.3f;

        [SerializeField, Tooltip("Factor de crecimiento. scale = base + sqrt(count) * factor.")]
        private float _scaleGrowthFactor = 0.08f;

        [SerializeField, Tooltip("Escala máxima del cubo placeholder.")]
        private float _maxVisualScale = 3f;

        // ── Estado ───────────────────────────────────────────────────────

        /// <summary>Número actual de unidades del ejército aliado.</summary>
        public int UnitCount { get; private set; }

        /// <summary>Disparado cada vez que el conteo cambia.</summary>
        public event Action<int> OnUnitsChanged;

        /// <summary>
        /// Disparado cuando el conteo llega a cero.
        /// Fase 4+ conectará esto a la lógica de derrota.
        /// En Fase 1-2 no tiene efecto en GameState.
        /// </summary>
        public event Action OnArmyDefeated;

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            GameManager.Instance.RegisterArmyManager(this);

            if (_crowdVisual != null)
            {
                _crowdVisual.gameObject.SetActive(true);
                RefreshVisual();
            }
        }

        // ── API pública ──────────────────────────────────────────────────

        /// <summary>Añade unidades. Llamado por LauncherController y PanelController.</summary>
        public void AddUnits(int amount)
        {
            if (amount <= 0) return;
            UnitCount += amount;
            Broadcast();
        }

        /// <summary>
        /// Elimina unidades. En Fase 4 será llamado por el sistema de daño enemigo.
        /// </summary>
        public void RemoveUnits(int amount)
        {
            if (amount <= 0) return;
            UnitCount = Mathf.Max(0, UnitCount - amount);
            Broadcast();

            if (UnitCount == 0)
                OnArmyDefeated?.Invoke();
        }

        /// <summary>
        /// Aplica la operación de un panel al total del ejército.
        /// Mínimo resultado: 1 (un panel nunca puede destruir el ejército).
        /// Usado por operaciones one-time (Add, Divide, Subtract).
        /// Para Multiply el cálculo lo hace PanelController por unidad.
        /// </summary>
        public void ApplyOperation(OperationType operationType, float value)
        {
            if (UnitCount <= 0) return;

            int newCount = operationType switch
            {
                OperationType.Multiply => Mathf.RoundToInt(UnitCount * value),
                OperationType.Add      => UnitCount + Mathf.RoundToInt(value),
                OperationType.Divide   => Mathf.RoundToInt(UnitCount / value),
                OperationType.Subtract => UnitCount - Mathf.RoundToInt(value),
                _                      => UnitCount
            };

            newCount = Mathf.Max(1, newCount);
            int delta = newCount - UnitCount;

            if (delta > 0)      AddUnits(delta);
            else if (delta < 0) RemoveUnits(-delta);
        }

        /// <summary>
        /// Limpia el ejército y oculta el visual.
        /// Solo el sistema de fases debe llamar este método.
        /// </summary>
        public void ClearAll()
        {
            UnitCount = 0;
            if (_crowdVisual != null) _crowdVisual.gameObject.SetActive(false);
            OnUnitsChanged?.Invoke(UnitCount);
        }

        // ── Internos ─────────────────────────────────────────────────────

        private void Broadcast()
        {
            RefreshVisual();
            OnUnitsChanged?.Invoke(UnitCount);
        }

        private void RefreshVisual()
        {
            if (_crowdVisual == null) return;

            float scale = UnitCount > 0
                ? Mathf.Min(_baseVisualScale + Mathf.Sqrt(UnitCount) * _scaleGrowthFactor, _maxVisualScale)
                : _minVisualScale;

            _crowdVisual.localScale = Vector3.one * scale;
        }
    }
}