using UnityEngine;
using TMPro;
using MobControl.Config;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Bloque obstáculo en la pista. Intercambio 1:1 con unidades aliadas.
    ///
    /// COMBATE:
    /// - Vs UnitController: consume la unidad aliada + bloque pierde 1 HP.
    /// - Vs SuperSoldierController: golpea al SS (SS.TakeHit) + bloque pierde 1 HP.
    ///   El SS puede destruir el bloque si su HP restante supera el HP del bloque.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class BlockController : MonoBehaviour
    {
        [SerializeField, Tooltip("Config del bloque.")]
        private BlockConfigSO _config;

        [SerializeField, Tooltip("Renderer del cuerpo.")]
        private Renderer _bodyRenderer;

        [SerializeField, Tooltip("Label HP.")]
        private TextMeshPro _hpLabel;

        private ArmyManager _armyManager;

        public int  CurrentHP { get; private set; }
        public bool IsAlive   => CurrentHP > 0;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_armyManager == null)
                _armyManager = FindFirstObjectByType<ArmyManager>();

            GetComponent<BoxCollider>().isTrigger = true;

            if (_config != null) ApplyConfig();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsAlive) return;

            // Vs unidad aliada
            if (other.TryGetComponent<UnitController>(out UnitController unit))
            {
                unit.ConsumeByOpponent();
                _armyManager?.RemoveUnits(1);
                TakeDamage(1);
                return;
            }

            // Vs Súper Soldado — el SS recibe daño Y el bloque recibe daño
            if (other.TryGetComponent<SuperSoldierController>(out SuperSoldierController ss))
            {
                ss.TakeHit(1);
                TakeDamage(1);
            }
        }

        // ── API pública ──────────────────────────────────────────────────

        public void Initialize(BlockConfigSO config)
        {
            _config = config;
            ApplyConfig();
        }

        public void InjectSceneReferences(ArmyManager armyManager)
        {
            _armyManager = armyManager;
        }

        // ── Internos ─────────────────────────────────────────────────────

        private void ApplyConfig()
        {
            CurrentHP = _config.BaseHP;
            UpdateVisual();
        }

        private void TakeDamage(int amount)
        {
            if (!IsAlive) return;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);
            UpdateVisual();
            if (CurrentHP == 0) Destroy(gameObject);
        }

        private void UpdateVisual()
        {
            if (_bodyRenderer != null && _config != null)
            {
                float t = _config.BaseHP > 0 ? 1f - (float)CurrentHP / _config.BaseHP : 0f;
                _bodyRenderer.material.color = Color.Lerp(_config.FullColor, _config.LowHPColor, t);
            }

            if (_hpLabel != null)
                _hpLabel.text = CurrentHP.ToString();
        }
    }
}