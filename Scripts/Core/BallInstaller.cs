using UnityEngine;

/// <summary>
/// Ensambla los sistemas del jugador.
/// 
/// Responsabilidades:
/// - Conectar input con movimiento
/// - Garantizar desacople entre sistemas
/// </summary>
public sealed class BallInstaller : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [Tooltip("Sistema de lectura de input.")]
    [SerializeField] private BallInputReader input;

    [Tooltip("Motor de movimiento del jugador.")]
    [SerializeField] private BallMovementMotor movement;

    [Tooltip("Estado del jugador.")]
    [SerializeField] private BallStateController state;

    #endregion

    private void OnEnable()
    {
        if (input == null || movement == null)
            return;

        input.OnSwipeForward += HandleForward;
        input.OnSwipeBackward += HandleBackward;
        input.OnTurn += HandleTurn;
    }

    private void OnDisable()
    {
        if (input == null || movement == null)
            return;

        input.OnSwipeForward -= HandleForward;
        input.OnSwipeBackward -= HandleBackward;
        input.OnTurn -= HandleTurn;
    }

    #region Handlers

    private void HandleForward(float value)
    {
        if (state != null && !state.CanControl)
            return;

        movement.AddSpeed(value);
    }

    private void HandleBackward(float value)
    {
        if (state != null && !state.CanControl)
            return;

        movement.Brake(value);
    }

    private void HandleTurn(float value)
    {
        if (state != null && !state.CanControl)
        {
            movement.SetTurn(0f);
            return;
        }

        movement.SetTurn(value);
    }

    #endregion
}