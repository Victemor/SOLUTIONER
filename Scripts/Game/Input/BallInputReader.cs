using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Traduce input táctil o mouse a intención de movimiento.
/// </summary>
public sealed class BallInputReader : MonoBehaviour
{
    public event Action<float> OnSwipeForward;
    public event Action<float> OnSwipeBackward;
    public event Action<float> OnTurn;

    [SerializeField] private float sensitivity = 0.01f;
    [SerializeField] private float minSwipeDistance = 10f;

    private Vector2 startPos;
    private Vector2 lastPos;
    private bool touching;

    private void Update()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            HandleTouch();
        }
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            HandleMouse();
        }
        else
        {
            EndInput();
        }
    }

    private void HandleTouch()
    {
        Vector2 pos = Touchscreen.current.primaryTouch.position.ReadValue();
        ProcessInput(pos);
    }

    private void HandleMouse()
    {
        Vector2 pos = Mouse.current.position.ReadValue();
        ProcessInput(pos);
    }

    private void ProcessInput(Vector2 pos)
    {
        if (!touching)
        {
            touching = true;
            startPos = pos;
            lastPos = pos;
            return;
        }

        Vector2 delta = pos - lastPos;

        if (delta.magnitude < minSwipeDistance * 0.1f)
            return;

        float x = delta.x * sensitivity;
        float y = delta.y * sensitivity;

        if (Mathf.Abs(y) > Mathf.Abs(x))
        {
            if (y > 0f)
                OnSwipeForward?.Invoke(Mathf.Abs(y));
            else
                OnSwipeBackward?.Invoke(Mathf.Abs(y));

            OnTurn?.Invoke(0f);
        }
        else
        {
            OnTurn?.Invoke(x);
        }

        lastPos = pos;
    }

    private void EndInput()
    {
        if (!touching)
            return;

        touching = false;
        OnTurn?.Invoke(0f);
    }
}