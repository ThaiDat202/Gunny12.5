using System;

namespace Bussiness
{
    /// <summary>
    /// Tuong thich khi cot Sys_Users_Detail.FightPower da doi int -> bigint.
    /// Doc gia tri bigint va bao hoa (clamp) ve int cho code web cu.
    /// </summary>
    public static class FightPowerCompat
    {
        public static int FromDb(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            if (value is long)
            {
                long l = (long)value;
                if (l > int.MaxValue) return int.MaxValue;
                if (l < int.MinValue) return int.MinValue;
                return (int)l;
            }
            if (value is int) return (int)value;
            if (value is short) return (short)value;
            if (value is byte) return (byte)value;
            try { return Convert.ToInt32(value); } catch { return 0; }
        }
    }
}
