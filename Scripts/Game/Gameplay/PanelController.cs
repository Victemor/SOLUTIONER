using System.Collections;
using UnityEngine;
using TMPro;
using MobControl.Config;
using MobControl.Core;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Panel de multiplicación.
    /// Los clones heredan el color de la unidad que activó el panel:
    /// si una unidad especial (morado/cyan) pasa por un ×3, genera clones especiales.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class PanelController : MonoBehaviour
    {
        [SerializeField] private PanelConfigSO   _config;
        [SerializeField] private TextMeshPro     _label;
        [SerializeField] private Renderer        _bodyRenderer;

        private ArmyManager      _armyManager;
        private UnitPool         _unitPool;
        private LauncherConfigSO _launcherConfig;
        private float            _valueOverride;

        private bool      _addBonusUsed;
        private Coroutine _flashCoroutine;
        private const float CloneSpawnOffset = 0.8f;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_armyManager == null) _armyManager = FindFirstObjectByType<ArmyManager>();
            if (_unitPool    == null) _unitPool    = FindFirstObjectByType<UnitPool>();
            ResolveLauncherConfig();
            EnsureTriggerCollider();
            RefreshVisual();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent<UnitController>(out UnitController unit)) return;
            if (unit.IsSpawnedByPanel) return;
            HandleUnitEntered(unit);
        }

        // ── API pública ──────────────────────────────────────────────────

        public void Initialize(PanelConfigSO config,
                                ArmyManager armyManager,
                                UnitPool unitPool,
                                float valueOverride = 0f)
        {
            _config        = config;
            _armyManager   = armyManager;
            _unitPool      = unitPool;
            _valueOverride = valueOverride;
            ResolveLauncherConfig();
            ResetPanel();
            RefreshVisual();
        }

        public void InjectSceneReferences(ArmyManager armyManager, UnitPool unitPool)
        {
            _armyManager = armyManager;
            _unitPool    = unitPool;
            ResolveLauncherConfig();
        }

        public void ResetPanel()
        {
            _addBonusUsed = false;
            StopActiveFlash();
            if (_config != null) SetColor(_config.NormalColor);
        }

        // ── Valor efectivo ────────────────────────────────────────────────

        private float EffectiveValue => _valueOverride > 0f
            ? _valueOverride
            : (_config != null ? _config.OperationValue : 1f);

        private string EffectiveLabel()
        {
            if (_config == null) return "?";
            string v = EffectiveValue % 1 == 0
                ? ((int)EffectiveValue).ToString()
                : EffectiveValue.ToString("F1");
            return _config.OperationType switch
            {
                OperationType.Multiply => $"X{v}",
                OperationType.Add      => $"+{v}",
                OperationType.Divide   => $"/{v}",
                OperationType.Subtract => $"-{v}",
                _                      => v
            };
        }

        // ── Lógica de activación ─────────────────────────────────────────

        private void HandleUnitEntered(UnitController unit)
        {
            if (_config == null) return;

            switch (_config.OperationType)
            {
                case OperationType.Multiply: ApplyMultiply(unit); break;
                case OperationType.Add:      ApplyAdd(unit);      break;
                case OperationType.Divide:
                case OperationType.Subtract:
                    if (!_addBonusUsed)
                    {
                        _addBonusUsed = true;
                        _armyManager?.ApplyOperation(_config.OperationType, EffectiveValue);
                        TriggerFlash();
                    }
                    break;
            }
        }

        private void ApplyMultiply(UnitController triggeringUnit)
        {
            int clones = Mathf.Max(0, Mathf.RoundToInt(EffectiveValue) - 1);
            if (clones == 0) return;

            EnemyTurret target = triggeringUnit.CurrentTarget
                                 ?? GameManager.Instance.GetNearestActiveTurret(transform.position);

            // Los clones heredan el color de la unidad que activó el panel
            Color unitColor = triggeringUnit.CurrentColor;

            for (int i = 0; i < clones; i++)
                SpawnClone(target, triggeringUnit.transform.position, unitColor);

            TriggerFlashBrief();
        }

        private void ApplyAdd(UnitController triggeringUnit)
        {
            if (_addBonusUsed) return;
            _addBonusUsed = true;

            int count = Mathf.RoundToInt(EffectiveValue);
            EnemyTurret target = triggeringUnit.CurrentTarget
                                 ?? GameManager.Instance.GetNearestActiveTurret(transform.position);

            Color unitColor = triggeringUnit.CurrentColor;

            for (int i = 0; i < count; i++)
                SpawnClone(target, transform.position, unitColor);

            TriggerFlash();
        }

        /// <summary>
        /// Spawnea un clon y le aplica el color heredado de la unidad original.
        /// Garantiza que unidades especiales (morado/cyan) generan clones especiales.
        /// </summary>
        private void SpawnClone(EnemyTurret target, Vector3 basePos, Color inheritedColor)
        {
            if (_unitPool == null || _launcherConfig == null) return;

            Vector3 pos = new Vector3(
                basePos.x + Random.Range(-0.3f, 0.3f),
                basePos.y,
                Mathf.Max(basePos.z, transform.position.z) + CloneSpawnOffset
            );

            UnitController clone = _unitPool.GetUnit(pos);
            if (clone == null) return;

            clone.Initialize(target, _launcherConfig, isSpawnedByPanel: true);
            clone.SetColor(inheritedColor); // hereda el color de la unidad que activó el panel
            _armyManager?.AddUnits(1);
        }

        // ── Refs ─────────────────────────────────────────────────────────

        private void ResolveLauncherConfig()
        {
            if (_launcherConfig != null) return;
            LauncherController lc = FindFirstObjectByType<LauncherController>();
            if (lc != null) _launcherConfig = lc.Config;
        }

        // ── Visual ───────────────────────────────────────────────────────

        private void RefreshVisual()
        {
            if (_config == null) return;
            if (_label  != null) _label.text = EffectiveLabel();
            SetColor(_config.NormalColor);
        }

        private void TriggerFlash()
        {
            StopActiveFlash();
            _flashCoroutine = StartCoroutine(FlashRoutine(_config.FlashDuration));
        }

        private void TriggerFlashBrief()
        {
            if (_flashCoroutine != null) return;
            _flashCoroutine = StartCoroutine(FlashRoutine(0.06f));
        }

        private IEnumerator FlashRoutine(float duration)
        {
            SetColor(_config.ActivatedColor);
            yield return new WaitForSeconds(duration);
            SetColor(_config.NormalColor);
            _flashCoroutine = null;
        }

        private void StopActiveFlash()
        {
            if (_flashCoroutine == null) return;
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        private void SetColor(Color c)
        {
            if (_bodyRenderer != null) _bodyRenderer.material.color = c;
        }

        private void EnsureTriggerCollider()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnDrawGizmos()
        {
            if (_config == null) return;
            Gizmos.color = new Color(0.2f, 0.65f, 1f, 0.3f);
            Gizmos.DrawCube(transform.position, transform.localScale);
            Gizmos.color = new Color(0.2f, 0.65f, 1f, 0.9f);
            Gizmos.DrawWireCube(transform.position, transform.localScale);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (transform.localScale.y * 0.5f + 0.3f),
                EffectiveLabel()
            );
#endif
        }
    }
}