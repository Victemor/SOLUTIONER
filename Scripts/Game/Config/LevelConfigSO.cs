using UnityEngine;

namespace MobControl.Config
{
    /// <summary>
    /// Configuración completa de un nivel.
    ///
    /// TrackOrigin: punto de referencia en el mundo desde donde se genera todo.
    ///   - TrackGenerator se coloca en este punto.
    ///   - PhaseGenerator offsetea todas las posiciones desde aquí.
    ///   - LevelManager posiciona el cañón en TrackOrigin + (0, GameplayYOffset, 0).
    ///
    /// Esto garantiza que aunque los GameObjects estén en posiciones distintas
    /// en la jerarquía de la escena, todo se genere alineado.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "MobControl/Level Config")]
    public class LevelConfigSO : ScriptableObject
    {
        [Header("Pista")]
        [SerializeField, Tooltip("Número de carriles del nivel.")]
        private int _laneCount = 3;

        [SerializeField, Tooltip("Longitud total de la pista (eje Z). 30 unidades da buen ritmo visual.")]
        private float _trackLength = 30f;

        [SerializeField, Tooltip("Ancho total de la pista (eje X).")]
        private float _trackWidth = 6f;

        [Header("Posicionamiento")]
        [SerializeField, Tooltip("Origen del mundo desde donde se genera la pista. " +
                                 "Dejar en (0,0,0) salvo que la escena lo requiera distinto.")]
        private Vector3 _trackOrigin = Vector3.zero;

        [SerializeField, Tooltip("Altura Y del cañón y las unidades sobre el suelo de la pista.")]
        private float _gameplayYOffset = 0.5f;

        [Header("Jugador")]
        [SerializeField, Tooltip("Unidades iniciales del ejército al comenzar cada fase.")]
        private int _startingPlayerUnits = 50;

        // ── Propiedades ──────────────────────────────────────────────────

        public int     LaneCount           => _laneCount;
        public float   TrackLength         => _trackLength;
        public float   TrackWidth          => _trackWidth;
        public Vector3 TrackOrigin         => _trackOrigin;
        public float   GameplayYOffset     => _gameplayYOffset;
        public float   LaneWidth           => _trackWidth / _laneCount;
        public int     StartingPlayerUnits => _startingPlayerUnits;

        public float LeftBound  => _trackOrigin.x - _trackWidth * 0.5f;
        public float RightBound => _trackOrigin.x + _trackWidth * 0.5f;

        /// <summary>
        /// Posición de mundo del cañón al inicio de una fase.
        /// Siempre en Z = TrackOrigin.Z (inicio de la pista), centrado en X.
        /// </summary>
        public Vector3 CannonStartPosition =>
            new Vector3(_trackOrigin.x, _trackOrigin.y + _gameplayYOffset, _trackOrigin.z);

        /// <summary>
        /// Centro X del carril i en espacio de mundo.
        /// </summary>
        public float GetLaneCenterX(int laneIndex)
        {
            float halfWidth = _trackWidth * 0.5f;
            float laneStart = _trackOrigin.x - halfWidth + LaneWidth * 0.5f;
            return laneStart + laneIndex * LaneWidth;
        }

        /// <summary>
        /// Convierte una Z normalizada (0-1) a posición de mundo.
        /// Z=0 → inicio de pista (cañón). Z=1 → final (torretas enemigas).
        /// </summary>
        public float NormalizedZToWorld(float t) =>
            _trackOrigin.z + t * _trackLength;
    }
}