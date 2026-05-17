using System.Collections.Generic;
using UnityEngine;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Pool auto-creciente de unidades enemigas.
    /// Mismo patrón que UnitPool — crece dinámicamente si se agota
    /// y reporta el high-water mark en consola.
    /// </summary>
    public class EnemyUnitPool : MonoBehaviour
    {
        [SerializeField, Tooltip("Prefab de la unidad enemiga. Arrastrar desde Assets/Prefabs/Gameplay/.")]
        private GameObject _unitPrefab;

        [SerializeField, Tooltip("Unidades pre-instanciadas. Ajustar según HWM reportado en consola.")]
        private int _poolSize = 100;

        private readonly Queue<EnemyUnitController>   _available   = new Queue<EnemyUnitController>();
        private readonly HashSet<EnemyUnitController> _activeUnits = new HashSet<EnemyUnitController>();

        private int _highWaterMark = 0;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_unitPrefab == null)
            {
                Debug.LogError("[EnemyUnitPool] _unitPrefab no asignado.", this);
                return;
            }
            Prewarm(_poolSize);
        }

        // ── API pública ──────────────────────────────────────────────────

        /// <summary>
        /// Obtiene una unidad del pool. Crece si está agotado.
        /// Nunca devuelve null (salvo error de prefab).
        /// </summary>
        public EnemyUnitController GetUnit(Vector3 position)
        {
            EnemyUnitController unit;

            if (_available.Count > 0)
            {
                unit = _available.Dequeue();
            }
            else
            {
                unit = InstantiateUnit();
                if (unit == null) return null;

                Debug.LogWarning($"[EnemyUnitPool] Pool agotado — unidad creada en runtime. " +
                                 $"Considera subir _poolSize.");
            }

            unit.transform.position = position;
            unit.gameObject.SetActive(true);
            _activeUnits.Add(unit);

            if (_activeUnits.Count > _highWaterMark)
            {
                _highWaterMark = _activeUnits.Count;
                Debug.Log($"[EnemyUnitPool] HWM: {_highWaterMark}. " +
                          $"Recomendado _poolSize ≥ {_highWaterMark + 20}");
            }

            return unit;
        }

        /// <summary>Devuelve todas las unidades activas al pool. Llamado entre fases.</summary>
        public void ReturnAll()
        {
            EnemyUnitController[] units = new EnemyUnitController[_activeUnits.Count];
            _activeUnits.CopyTo(units);
            foreach (EnemyUnitController u in units)
                if (u != null) ForceReturn(u);
        }

        // ── Internos ─────────────────────────────────────────────────────

        private void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                EnemyUnitController u = InstantiateUnit();
                if (u != null) _available.Enqueue(u);
            }
        }

        private EnemyUnitController InstantiateUnit()
        {
            GameObject go = Instantiate(_unitPrefab, transform);
            go.SetActive(false);

            EnemyUnitController unit = go.GetComponent<EnemyUnitController>();
            if (unit == null)
            {
                Debug.LogError("[EnemyUnitPool] Prefab sin EnemyUnitController.", this);
                Destroy(go);
                return null;
            }

            unit.OnReturnToPool += ReturnUnit;
            return unit;
        }

        private void ReturnUnit(EnemyUnitController unit)
        {
            _activeUnits.Remove(unit);
            unit.gameObject.SetActive(false);
            unit.transform.SetParent(transform, false);
            _available.Enqueue(unit);
        }

        private void ForceReturn(EnemyUnitController unit)
        {
            _activeUnits.Remove(unit);
            unit.gameObject.SetActive(false);
            unit.transform.SetParent(transform, false);
            _available.Enqueue(unit);
        }
    }
}