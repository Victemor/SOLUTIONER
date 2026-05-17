namespace MobControl.Core
{
    /// <summary>
    /// Resuelve todo el combate mediante aritmética pura.
    /// Clase estática porque no necesita estado ni instancia — es una función matemática.
    /// Ningún sistema de combate debe tomar decisiones fuera de aquí.
    /// </summary>
    public static class CombatResolver
    {
        /// <summary>
        /// Resuelve el enfrentamiento entre el ejército del jugador y la fuerza de un enemigo.
        /// El resultado indica cuántas unidades sobreviven de cada lado.
        /// </summary>
        /// <param name="playerUnits">Total de unidades activas del jugador.</param>
        /// <param name="enemyStrength">Fuerza o HP del enemigo (torreta, bloque, etc.).</param>
        public static CombatResult Resolve(int playerUnits, int enemyStrength)
        {
            return new CombatResult(playerUnits, enemyStrength);
        }

        /// <summary>
        /// Resuelve el daño que recibe un bloque físico.
        /// El bloque y las unidades se dañan mutuamente en proporción 1 a 1.
        /// </summary>
        /// <param name="playerUnits">Unidades del jugador empujando el bloque.</param>
        /// <param name="blockHP">HP actual del bloque.</param>
        /// <param name="survivingUnits">Unidades que quedan tras destruir o fallar con el bloque.</param>
        /// <param name="remainingBlockHP">HP restante del bloque (0 si fue destruido).</param>
        public static void ResolveVsBlock(int playerUnits, int blockHP,
                                          out int survivingUnits, out int remainingBlockHP)
        {
            survivingUnits   = playerUnits - blockHP;
            remainingBlockHP = blockHP - playerUnits;

            if (survivingUnits < 0) survivingUnits = 0;
            if (remainingBlockHP < 0) remainingBlockHP = 0;
        }
    }
}
