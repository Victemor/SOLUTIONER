using System.Collections.Generic;
using UnityEngine;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Pool auto-creciente de unidades aliadas.
    ///
    /// PROBLEMA RESUELTO:
    /// Con pools de tamaño fijo, un portal ×5 con 50 unidades en campo
    /// intenta spawnear 200 clones — el pool se agota y los portales
    /// dejan de funcionar silenciosamente.
    ///
    /// SOLUCIÓN:
    /// Cuando el pool se agota, instancia nuevas unidades en runtime
    /// en lugar de devolver null. Registra el high-water mark (máximo
    /// de unidades simultáneas) para que el diseñador ajuste _poolSize
    /// con datos reales y evite el overhead de instanciación en partidas futuras.
    ///
    /// HIGH-WATER MARK:
    /// Se imprime en consola cada vez que se alcanza un nuevo máximo.
    /// Si en tus pruebas ves "HWM: 240", pon _poolSize = 260.
    /// </summary>
    public class UnitPool : MonoBehaviour
    {
        [SerializeField, Tooltip("Prefab de la unidad aliada. Arrastrar desde Assets/Prefabs/Gameplay/.")]
        private GameObject _unitPrefab;

        [SerializeField, Tooltip("Unidades pre-instanciadas al iniciar. " +
                                 "Si el HWM supera este valor el pool crece en runtime. " +
                                 "Ajustar según el HWM reportado en consola.")]
        private int _poolSize = 150;

        private readonly Queue<UnitController>   _available   = new Queue<UnitController>();
        private readonly HashSet<UnitController> _activeUnits = new HashSet<UnitController>();

        private int _highWaterMark = 0;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_unitPrefab == null)
            {
                Debug.LogError("[UnitPool] _unitPrefab no asignado.", this);
                return;
            }
            Prewarm(_poolSize);
        }

        // ── API pública ──────────────────────────────────────────────────

        /// <summary>
        /// Obtiene una unidad del pool. Si el pool está agotado, instancia una nueva
        /// en lugar de devolver null — garantiza que los portales siempre funcionen.
        /// </summary>
        public UnitController GetUnit(Vector3 position)
        {
            UnitController unit;

            if (_available.Count > 0)
            {
                unit = _available.Dequeue();
            }
            else
            {
                // Pool agotado — crecer dinámicamente
                unit = InstantiateUnit();
                if (unit == null) return null;

                Debug.LogWarning($"[UnitPool] Pool agotado — unidad creada en runtime. " +
                                 $"Activas: {_activeUnits.Count + 1}. " +
                                 $"Considera subir _poolSize.");
            }

            unit.transform.position = position;
            unit.gameObject.SetActive(true);
            _activeUnits.Add(unit);

            // Registrar high-water mark
            if (_activeUnits.Count > _highWaterMark)
            {
                _highWaterMark = _activeUnits.Count;
                Debug.Log($"[UnitPool] HWM: {_highWaterMark} unidades simultáneas. " +
                          $"Recomendado _poolSize ≥ {_highWaterMark + 20}");
            }

            return unit;
        }

        /// <summary>Devuelve todas las unidades activas al pool. Llamado entre fases.</summary>
        public void ReturnAll()
        {
            UnitController[] units = new UnitController[_activeUnits.Count];
            _activeUnits.CopyTo(units);
            foreach (UnitController u in units)
                if (u != null) ForceReturn(u);
        }

        // ── Internos ─────────────────────────────────────────────────────

        private void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                UnitController u = InstantiateUnit();
                if (u != null) _available.Enqueue(u);
            }
        }

        private UnitController InstantiateUnit()
        {
            GameObject go = Instantiate(_unitPrefab, transform);
            go.SetActive(false);

            UnitController unit = go.GetComponent<UnitController>();
            if (unit == null)
            {
                Debug.LogError("[UnitPool] Prefab sin UnitController.", this);
                Destroy(go);
                return null;
            }

            unit.OnReturnToPool += ReturnUnit;
            return unit;
        }

        private void ReturnUnit(UnitController unit)
        {
            _activeUnits.Remove(unit);
            unit.gameObject.SetActive(false);
            unit.transform.SetParent(transform, false);
            _available.Enqueue(unit);
        }

        private void ForceReturn(UnitController unit)
        {
            _activeUnits.Remove(unit);
            unit.gameObject.SetActive(false);
            unit.transform.SetParent(transform, false);
            _available.Enqueue(unit);
        }
    }
}