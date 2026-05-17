using UnityEngine;
using MobControl.Config;

namespace MobControl.Track
{
    /// <summary>
    /// Genera únicamente la geometría visual de la pista:
    /// el suelo y los divisores de carril.
    ///
    /// Ya no gestiona slots de torretas ni paneles — eso lo hace
    /// PhaseGenerator en runtime usando LevelConfigSO directamente.
    ///
    /// Se posiciona usando _levelConfig.TrackOrigin como punto de partida,
    /// por lo que puede estar en cualquier lugar de la escena sin
    /// desincronizar el resto de los sistemas.
    /// </summary>
    public class TrackGenerator : MonoBehaviour
    {
        [SerializeField, Tooltip("Configuración del nivel a generar.")]
        private LevelConfigSO _levelConfig;

        [Header("Materiales Placeholder")]
        [SerializeField, Tooltip("Material del suelo de la pista.")]
        private Material _groundMaterial;

        [SerializeField, Tooltip("Material de los divisores de carril.")]
        private Material _dividerMaterial;

        // ── Constantes de geometría ──────────────────────────────────────

        private const float GroundThickness  = 0.1f;
        private const float DividerHeight    = 0.15f;
        private const float DividerThickness = 0.05f;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            Generate();
        }

        // ── API pública ──────────────────────────────────────────────────

        /// <summary>
        /// Limpia y regenera la pista visual.
        /// Llamar si se cambia _levelConfig en runtime.
        /// </summary>
        public void Generate()
        {
            if (_levelConfig == null)
            {
                Debug.LogError("[TrackGenerator] LevelConfigSO no asignado.", this);
                return;
            }

            ClearChildren();
            GenerateGround();
            GenerateLaneDividers();
        }

        // ── Generación visual ────────────────────────────────────────────

        private void GenerateGround()
        {
            Vector3 origin = _levelConfig.TrackOrigin;

            GameObject ground = CreatePrimitive("Ground", PrimitiveType.Cube);
            ground.transform.localScale = new Vector3(
                _levelConfig.TrackWidth,
                GroundThickness,
                _levelConfig.TrackLength
            );
            // Centro del suelo: origin + mitad del largo en Z
            ground.transform.position = new Vector3(
                origin.x,
                origin.y - GroundThickness * 0.5f,
                origin.z + _levelConfig.TrackLength * 0.5f
            );

            ApplyMaterial(ground, _groundMaterial);
            Destroy(ground.GetComponent<Collider>());
        }

        private void GenerateLaneDividers()
        {
            Vector3 origin      = _levelConfig.TrackOrigin;
            int     dividerCount = _levelConfig.LaneCount - 1;

            for (int i = 0; i < dividerCount; i++)
            {
                // Un divisor entre cada par de carriles
                float xPos = origin.x
                             - _levelConfig.TrackWidth * 0.5f
                             + _levelConfig.LaneWidth * (i + 1);

                GameObject divider = CreatePrimitive($"Divider_{i}", PrimitiveType.Cube);
                divider.transform.localScale = new Vector3(
                    DividerThickness,
                    DividerHeight,
                    _levelConfig.TrackLength
                );
                divider.transform.position = new Vector3(
                    xPos,
                    origin.y + DividerHeight * 0.5f,
                    origin.z + _levelConfig.TrackLength * 0.5f
                );

                ApplyMaterial(divider, _dividerMaterial);
                Destroy(divider.GetComponent<Collider>());
            }
        }

        // ── Utilidades ───────────────────────────────────────────────────

        private GameObject CreatePrimitive(string objName, PrimitiveType type)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = objName;
            obj.transform.SetParent(transform, false);
            return obj;
        }

        private static void ApplyMaterial(GameObject obj, Material material)
        {
            if (material == null) return;
            obj.GetComponent<Renderer>().material = material;
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }
}