namespace MobControl.Core
{
    /// <summary>
    /// Datos del bonus al completar un nivel.
    /// Pasado por evento desde LevelManager a GameResultUI.
    /// </summary>
    public struct BonusData
    {
        public int   LevelIndex;
        public int   Survivors;
        public float Multiplier;
    }
}