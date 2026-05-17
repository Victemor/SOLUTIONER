using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using MobControl.Core;
using MobControl.Gameplay;

namespace MobControl.UI
{
    /// <summary>
    /// UI de resultado de partida.
    ///
    /// Victoria: muestra el nivel, sobrevivientes y multiplicador de bonus.
    ///           Botón "Siguiente Nivel" guarda el progreso y recarga.
    ///
    /// Derrota: botón "Reintentar" recarga sin cambiar el nivel.
    ///
    /// Se conecta a LevelManager.OnLevelComplete para recibir los datos del bonus.
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        [Header("Paneles")]
        [SerializeField] private GameObject _victoryPanel;
        [SerializeField] private GameObject _defeatPanel;

        [Header("Textos de victoria")]
        [SerializeField, Tooltip("Label del número de nivel completado.")]
        private TextMeshProUGUI _levelLabel;

        [SerializeField, Tooltip("Label de unidades sobrevivientes.")]
        private TextMeshProUGUI _survivorsLabel;

        [SerializeField, Tooltip("Label del multiplicador de bonus.")]
        private TextMeshProUGUI _multiplierLabel;

        [SerializeField, Tooltip("Label de resumen (ej. '¡Nivel 5 completado!').")]
        private TextMeshProUGUI _victoryTitleLabel;

        // ── Estado ───────────────────────────────────────────────────────

        private BonusData   _lastBonus;
        private LevelManager _levelManager;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            HideAll();
        }

        private void Start()
        {
            // Suscribir a GameManager para el estado
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            else
                Debug.LogError("[GameResultUI] GameManager.Instance null en Start().");

            // Suscribir a LevelManager para recibir los datos del bonus
            _levelManager = FindFirstObjectByType<LevelManager>();
            if (_levelManager != null)
                _levelManager.OnLevelComplete += HandleLevelComplete;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;

            if (_levelManager != null)
                _levelManager.OnLevelComplete -= HandleLevelComplete;
        }

        // ── Botones ──────────────────────────────────────────────────────

        /// <summary>
        /// "Siguiente Nivel" — guarda el progreso y recarga la escena.
        /// Asignar al onClick del botón en el VictoryPanel.
        /// </summary>
        public void OnNextLevelButtonPressed()
        {
            LevelManager.SaveNextLevel(_lastBonus.LevelIndex);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// "Reintentar" — recarga sin cambiar el nivel guardado.
        /// Asignar al onClick del botón en el DefeatPanel.
        /// </summary>
        public void OnRetryButtonPressed()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ── Manejadores ──────────────────────────────────────────────────

        private void HandleGameStateChanged(GameState newState)
        {
            if (newState == GameState.Defeat)
                ShowDefeat();
            // Victory se maneja en HandleLevelComplete para tener los datos del bonus
        }

        private void HandleLevelComplete(BonusData bonus)
        {
            _lastBonus = bonus;
            ShowVictory(bonus);
        }

        // ── UI ───────────────────────────────────────────────────────────

        private void ShowVictory(BonusData bonus)
        {
            HideAll();
            if (_victoryPanel == null) return;

            _victoryPanel.SetActive(true);

            if (_victoryTitleLabel != null)
                _victoryTitleLabel.text = $"¡Nivel {bonus.LevelIndex} completado!";

            if (_survivorsLabel != null)
                _survivorsLabel.text = $"Sobrevivientes: {bonus.Survivors}";

            if (_multiplierLabel != null)
            {
                // Visualizar el multiplicador con indicador de calidad
                string quality = bonus.Multiplier >= 2.5f ? "⭐⭐⭐"
                               : bonus.Multiplier >= 1.8f ? "⭐⭐"
                               : "⭐";
                _multiplierLabel.text = $"Bonus: x{bonus.Multiplier:F2}  {quality}";
            }

            if (_levelLabel != null)
                _levelLabel.text = $"Nivel {bonus.LevelIndex}";
        }

        private void ShowDefeat()
        {
            HideAll();
            if (_defeatPanel != null)
                _defeatPanel.SetActive(true);
        }

        private void HideAll()
        {
            if (_victoryPanel != null) _victoryPanel.SetActive(false);
            if (_defeatPanel  != null) _defeatPanel.SetActive(false);
        }
    }
}