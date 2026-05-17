using UnityEngine;
using MobControl.Config;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Herramienta de debug visual para Fase 1.
    /// Dibuja Gizmos en el editor para verificar posiciones de la pista,
    /// cañón, FirePoint y torretas sin necesidad de ejecutar el juego.
    /// Adjuntar a cualquier GameObject vacío en la escena. Eliminar en producción.
    /// </summary>
    public class GameplayDebugger : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField, Tooltip("Config del nivel para dibujar los límites de la pista.")]
        private LevelConfigSO _levelConfig;

        [SerializeField, Tooltip("Transform del cañón.")]
        private Transform _launcher;

        [SerializeField, Tooltip("Transform del FirePoint.")]
        private Transform _firePoint;

        [SerializeField, Tooltip("Torretas en la escena.")]
        private EnemyTurret[] _turrets;

        [Header("Debug en Pantalla")]
        [SerializeField, Tooltip("Muestra HP y unit count en pantalla durante el juego.")]
        private bool _showHUD = true;

        [SerializeField]
        private ArmyManager _armyManager;

        private void OnDrawGizmos()
        {
            if (_levelConfig == null) return;

            DrawTrackBounds();
            DrawFirePoint();
            DrawTurretPositions();
            DrawDistanceCheck();
        }

        private void DrawTrackBounds()
        {
            // Pista completa — cubo azul semitransparente
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.15f);
            Vector3 trackCenter = new Vector3(0f, 0f, _levelConfig.TrackLength * 0.5f);
            Vector3 trackSize   = new Vector3(_levelConfig.TrackWidth, 0.1f, _levelConfig.TrackLength);
            Gizmos.DrawCube(trackCenter, trackSize);

            // Borde de la pista — línea azul sólida
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.8f);
            Gizmos.DrawWireCube(trackCenter, trackSize);

            // Carriles — líneas grises
            Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
            for (int i = 1; i < _levelConfig.LaneCount; i++)
            {
                float x = _levelConfig.GetLaneCenterX(i) - _levelConfig.LaneWidth * 0.5f;
                Vector3 start = new Vector3(x, 0.05f, 0f);
                Vector3 end   = new Vector3(x, 0.05f, _levelConfig.TrackLength);
                Gizmos.DrawLine(start, end);
            }

            // Etiqueta de inicio y fin
#if UNITY_EDITOR
            UnityEditor.Handles.Label(new Vector3(0f, 0.5f, 0f), "CAÑÓN (Z=0)");
            UnityEditor.Handles.Label(new Vector3(0f, 0.5f, _levelConfig.TrackLength), "FINAL PISTA");
#endif
        }

        private void DrawFirePoint()
        {
            if (_firePoint == null) return;

            // FirePoint — esfera verde
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_firePoint.position, 0.15f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(_firePoint.position + Vector3.up * 0.4f,
                $"FirePoint Z={_firePoint.position.z:F1}");
#endif
        }

        private void DrawTurretPositions()
        {
            if (_turrets == null) return;

            foreach (EnemyTurret turret in _turrets)
            {
                if (turret == null) continue;

                // Torreta — cubo rojo
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
                Gizmos.DrawCube(turret.transform.position, Vector3.one);
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(turret.transform.position, Vector3.one);

                // Radio de hit threshold — disco amarillo
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawSphere(turret.transform.position, 0.6f);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(turret.transform.position + Vector3.up * 1.2f,
                    $"{turret.name}\nZ={turret.transform.position.z:F1}\nHP={turret.CurrentHP}");
#endif
            }
        }

        private void DrawDistanceCheck()
        {
            if (_firePoint == null || _turrets == null) return;

            foreach (EnemyTurret turret in _turrets)
            {
                if (turret == null) continue;

                float dist = turret.transform.position.z - _firePoint.position.z;

                // Línea de distancia — verde si OK, roja si muy cerca
                Gizmos.color = dist > 2f ? Color.green : Color.red;
                Gizmos.DrawLine(_firePoint.position, turret.transform.position);

#if UNITY_EDITOR
                Vector3 midPoint = (_firePoint.position + turret.transform.position) * 0.5f;
                string  warning  = dist <= 2f ? " ⚠ MUY CERCA" : " ✓ OK";
                UnityEditor.Handles.Label(midPoint, $"Distancia Z: {dist:F1}{warning}");
#endif
            }
        }

        // ── HUD en pantalla ──────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_showHUD || !Application.isPlaying) return;

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize  = 16,
                alignment = TextAnchor.UpperLeft
            };

            string hudText = "── DEBUG HUD ──\n";

            if (GameManager.Instance != null)
                hudText += $"Estado: {GameManager.Instance.CurrentState}\n";

            if (_armyManager != null)
                hudText += $"Unidades en campo: {_armyManager.UnitCount}\n";

            if (_turrets != null)
            {
                foreach (EnemyTurret t in _turrets)
                {
                    if (t != null)
                        hudText += $"{t.name} HP: {t.CurrentHP}\n";
                }
            }

            GUI.Box(new Rect(10, 10, 220, 120), hudText, style);
        }
    }
}