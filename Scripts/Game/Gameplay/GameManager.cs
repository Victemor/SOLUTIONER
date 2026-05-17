using System;
using System.Collections.Generic;
using UnityEngine;
using MobControl.Core;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Singleton orquestador del estado global de la partida.
    ///
    /// FLUJO DE ESTADOS:
    /// WaitingForInput → (primer input del jugador) → Playing
    /// Playing → (todas las torretas caen) → OnAllTurretsDefeated
    ///   LevelManager decide: ¿hay otra fase? → WaitingForInput (entre fases)
    ///                        ¿última fase?    → Victory
    /// Playing → (enemigos llegan al cañón) → Defeat
    ///
    /// DeclareReady(): llamado por LauncherController cuando detecta el primer input.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ── Estado ───────────────────────────────────────────────────────

        public GameState CurrentState { get; private set; } = GameState.WaitingForInput;

        /// <summary>Disparado cuando GameState cambia.</summary>
        public event Action<GameState> OnGameStateChanged;

        /// <summary>
        /// Disparado cuando todas las torretas activas caen.
        /// LevelManager decide si avanzar fase o terminar el nivel.
        /// </summary>
        public event Action OnAllTurretsDefeated;

        // ── Registro de sistemas ─────────────────────────────────────────

        private readonly List<EnemyTurret> _activeTurrets = new List<EnemyTurret>();
        private ArmyManager _armyManager;

        public IReadOnlyList<EnemyTurret> ActiveTurrets => _activeTurrets;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Arrancar en WaitingForInput — nada ocurre hasta el primer toque
        }

        // ── API pública ──────────────────────────────────────────────────

        /// <summary>
        /// Llamado por LauncherController cuando detecta el primer input.
        /// Transiciona de WaitingForInput → Playing.
        /// </summary>
        public void DeclareReady()
        {
            if (CurrentState == GameState.WaitingForInput)
                TransitionTo(GameState.Playing);
        }

        /// <summary>
        /// Llamado por LevelManager al terminar la última fase del nivel.
        /// </summary>
        public void DeclareVictory() => TransitionTo(GameState.Victory);

        /// <summary>
        /// Llamado por LevelManager o EnemyUnitController cuando los enemigos llegan al cañón.
        /// </summary>
        public void DeclareDefeat() => TransitionTo(GameState.Defeat);

        /// <summary>
        /// Llamado por LevelManager al iniciar una nueva fase intermedia.
        /// Vuelve al estado de espera — nada dispara hasta el primer toque.
        /// </summary>
        public void DeclareWaitingForInput()
        {
            if (CurrentState == GameState.Playing)
                TransitionTo(GameState.WaitingForInput);
        }

        // ── Registro de torretas ─────────────────────────────────────────

        public void RegisterTurret(EnemyTurret turret)
        {
            if (_activeTurrets.Contains(turret)) return;
            _activeTurrets.Add(turret);
            turret.OnDefeated += HandleTurretDefeated;
        }

        public void UnregisterTurret(EnemyTurret turret)
        {
            if (!_activeTurrets.Remove(turret)) return;
            turret.OnDefeated -= HandleTurretDefeated;
        }

        // ── Registro de ArmyManager ──────────────────────────────────────

        public void RegisterArmyManager(ArmyManager armyManager)
        {
            if (_armyManager != null)
                _armyManager.OnArmyDefeated -= HandleArmyDefeated;

            _armyManager = armyManager;
            // Fase 4+: descomentar cuando exista daño enemigo real
            // _armyManager.OnArmyDefeated += HandleArmyDefeated;
        }

        // ── Consulta de torretas ─────────────────────────────────────────

        public EnemyTurret GetNearestActiveTurret(Vector3 fromPosition)
        {
            EnemyTurret nearest = null;
            float       minSqr  = float.MaxValue;

            foreach (EnemyTurret turret in _activeTurrets)
            {
                float sqrDist = (turret.transform.position - fromPosition).sqrMagnitude;
                if (sqrDist < minSqr) { minSqr = sqrDist; nearest = turret; }
            }
            return nearest;
        }

        // ── Manejadores privados ─────────────────────────────────────────

        private void HandleArmyDefeated() { /* Fase 4: DeclareDefeat(); */ }

        private void HandleTurretDefeated(EnemyTurret turret)
        {
            UnregisterTurret(turret);
            if (_activeTurrets.Count == 0)
                OnAllTurretsDefeated?.Invoke();
        }

        // ── Transiciones ─────────────────────────────────────────────────

        private void TransitionTo(GameState newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            OnGameStateChanged?.Invoke(newState);
            Debug.Log($"[GameManager] Estado → {newState}");
        }
    }
}