using UnityEngine;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Añade oscilación horizontal a un panel.
    /// Componente independiente de PanelController (SRP) — se puede añadir
    /// a cualquier panel para hacerlo móvil sin modificar su lógica de trigger.
    ///
    /// La velocidad es FIJA — no escala con la dificultad como establece el GDD.
    /// El rango (_moveRange) sí varía: PhaseGenerator lo configura al instanciar.
    ///
    /// SETUP en prefab:
    /// Añadir este componente al mismo GameObject que PanelController.
    /// Los parámetros se sobreescriben con Initialize() desde PhaseGenerator.
    /// </summary>
    public class PanelMover : MonoBehaviour
    {
        [SerializeField, Tooltip("Mitad del rango de oscilación en unidades de mundo. " +
                                 "El panel se mueve ±_moveRange desde su posición inicial.")]
        private float _moveRange = 1.5f;

        [SerializeField, Tooltip("Velocidad de oscilación (fija por diseño — no escala con dificultad).")]
        private float _moveSpeed = 1.2f;

        private float _startX;
        private bool  _isInitialized;

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            // Start() en lugar de Awake() para que la posición ya haya sido
            // establecida por PhaseGenerator antes de que arranque la oscilación
            _startX        = transform.position.x;
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            float x = _startX + Mathf.Sin(Time.time * _moveSpeed) * _moveRange;
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
        }

        // ── API pública ──────────────────────────────────────────────────

        /// <summary>
        /// PhaseGenerator llama este método para configurar el rango de oscilación.
        /// El moveSpeed no cambia — es fijo por diseño.
        /// </summary>
        public void Initialize(float moveRange)
        {
            _moveRange = moveRange;
            // Reiniciar _startX en caso de que la posición ya haya cambiado
            _startX = transform.position.x;
        }
    }
}