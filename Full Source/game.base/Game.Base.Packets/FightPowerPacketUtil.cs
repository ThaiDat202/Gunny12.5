using System;

namespace Game.Base.Packets
{
    /// <summary>Chuyen luc chien long -> int cho packet client cu (clamp 32-bit, khong tran am).</summary>
    public static class FightPowerPacketUtil
    {
        public static int ToInt(long value)
        {
            if (value > int.MaxValue) return int.MaxValue;
            if (value < int.MinValue) return int.MinValue;
            return (int)value;
        }
    }
}
