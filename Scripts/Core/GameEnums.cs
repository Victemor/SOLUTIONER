namespace MobControl.Core
{
    /// <summary>
    /// Estado global de la partida.
    ///
    /// WaitingForInput: estado inicial y entre fases.
    ///   - Nada dispara (ni aliados ni enemigos).
    ///   - El campo está listo para jugar pero espera el primer toque del jugador.
    ///
    /// Playing: estado activo de gameplay.
    ///   - El jugador puede disparar mientras mantiene presionado.
    ///   - Las torretas enemigas spawnean y atacan.
    ///
    /// Victory / Defeat: fin de la partida.
    /// </summary>
    public enum GameState
    {
        WaitingForInput,
        Playing,
        Victory,
        Defeat
    }

    /// <summary>
    /// Operación matemática aplicada por un panel al ejército.
    /// Divide y Subtract aparecen desde nivel 16+.
    /// </summary>
    public enum OperationType
    {
        Multiply,
        Add,
        Divide,
        Subtract
    }
}