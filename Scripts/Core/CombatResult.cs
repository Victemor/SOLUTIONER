namespace MobControl.Core
{
    /// <summary>
    /// Resultado inmutable de una resolución de combate.
    /// Struct porque es un contenedor de datos pequeño y de corta vida.
    /// </summary>
    public readonly struct CombatResult
    {
        /// <summary>Unidades aliadas que sobreviven tras el combate. Negativo = derrota del jugador.</summary>
        public readonly int PlayerSurvivors;

        /// <summary>HP restante del enemigo tras el combate. Negativo = enemigo derrotado.</summary>
        public readonly int EnemyRemainingHP;

        public readonly bool IsPlayerDefeated;
        public readonly bool IsEnemyDefeated;

        public CombatResult(int playerUnits, int enemyHP)
        {
            PlayerSurvivors   = playerUnits - enemyHP;
            EnemyRemainingHP  = enemyHP - playerUnits;
            IsPlayerDefeated  = PlayerSurvivors <= 0;
            IsEnemyDefeated   = EnemyRemainingHP <= 0;
        }
    }
}
