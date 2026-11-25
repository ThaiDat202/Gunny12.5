using System;
using System.Data.SqlClient;
using Bussiness;
using SqlDataProvider.Data;

class Program
{
    static void Main()
    {
        try
        {
            using (PlayerBussiness pb = new PlayerBussiness())
            {
                PlayerInfo pi = pb.GetUserSingleByUserID(1);
                if (pi == null) { Console.WriteLine("PlayerInfo null"); return; }
                Console.WriteLine("Nick=" + pi.NickName + " FightPower=" + pi.FightPower + " (type=" + pi.FightPower.GetType().Name + ")");

                // tam set 3 ty de kiem tra doc/ghi bigint qua tang Bussiness
                Exec("UPDATE Db_Tank.dbo.Sys_Users_Detail SET FightPower=3000000000 WHERE UserID=1");
                using (PlayerBussiness pb2 = new PlayerBussiness())
                {
                    PlayerInfo pi2 = pb2.GetUserSingleByUserID(1);
                    Console.WriteLine("Sau khi set 3 ty: FightPower=" + pi2.FightPower + " (expect 3000000000)");
                }
                Exec("UPDATE Db_Tank.dbo.Sys_Users_Detail SET FightPower=242619769 WHERE UserID=1");
                using (PlayerBussiness pb3 = new PlayerBussiness())
                {
                    PlayerInfo pi3 = pb3.GetUserSingleByUserID(1);
                    Console.WriteLine("Restore: FightPower=" + pi3.FightPower);
                }

                // test packet split helper khi > int32
                pi.FightPower = 3000000000L;
                pi.FightPower_Show = 680195045L;
                long total = (long)pi.FightPower_PacketInt + pi.FightPower_PacketShow;
                Console.WriteLine("PacketInt=" + pi.FightPower_PacketInt + " PacketShow=" + pi.FightPower_PacketShow + " sum=" + total + " (expect " + (3000000000L + 680195045L) + ")");
                if (total != 3000000000L + 680195045L) { Console.WriteLine("FAIL split"); Environment.Exit(2); }
                if (pi.FightPower_PacketInt != int.MaxValue) { Console.WriteLine("FAIL clamp"); Environment.Exit(3); }
                Console.WriteLine("SPLIT OK");
            }
        }
        catch (Exception ex) { Console.WriteLine("ERR: " + ex); Environment.Exit(1); }
    }
    static void Exec(string sql)
    {
        using (var c = new SqlConnection("Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Tank;User ID=sa;Password=123456;Connect Timeout=15"))
        { c.Open(); using (var cmd = new SqlCommand(sql, c)) cmd.ExecuteNonQuery(); }
    }
}
