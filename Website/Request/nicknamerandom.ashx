<%@ WebHandler Language="C#" Class="VnNickNameRandom" %>
// =====================================================================================
//  nicknamerandom.ashx - goi y ten nhan vat (tieng Viet)
//
//  Thay cho handler goc Tank.Request.nicknamerandom (trong Website\Request\Bin\Tank.Request.dll).
//  Giu nguyen URL va dinh dang tra ve:  <Result name="..." />
//  Tham so: sex = 0 -> nu, sex <> 0 -> nam  (giong handler goc)
//
//  File goc duoc backup: nicknamerandom.ashx.bak-dsh-*
//  Muon quay lai ban cu: doi ten file backup thanh nicknamerandom.ashx
//
//  RANG BUOC ten nhan vat (NickNameCheck.ashx):
//    - <= 14 byte theo encoding ANSI cua server
//    - khong chua cac ky tu: % ! @ # $ ^ & * ( ) ? / > . < , : ; ' \ | } ] { [ _ ~ ` + = - "
//    - chua ton tai trong DB
// =====================================================================================
using System;
using System.Globalization;
using System.Text;
using System.Web;
using System.Xml.Linq;

public class VnNickNameRandom : IHttpHandler
{
    // ================== TUY CHINH ==================
    // true  : ten CO DAU   -> "Nguyễn Văn An"
    // false : ten KHONG DAU -> "Nguyen Van An"
    private const bool CoDau = true;

    // Do dai toi da cho phep (NickNameCheck.ashx gioi han 14)
    private const int MaxLen = 14;
    // ===============================================

    private static readonly string[] Ho = new string[]
    {
        "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng",
        "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý", "Đinh", "Trịnh", "Mai", "Tô",
        "Lâm", "Phùng", "Cao", "Đoàn", "Trương", "Châu", "Quách", "Tạ", "Lưu", "Thái"
    };

    private static readonly string[] DemNu = new string[]
    {
        "Thị", "Ngọc", "Thu", "Thanh", "Mỹ", "Bích", "Kim", "Phương", "Hồng", "Xuân"
    };

    private static readonly string[] TenNu = new string[]
    {
        "An", "Anh", "Ánh", "Bảo", "Bình", "Chi", "Dung", "Duyên", "Giang", "Hà",
        "Hải", "Hằng", "Hiền", "Hoa", "Hồng", "Huệ", "Hương", "Lan", "Linh", "Mai",
        "My", "Nga", "Ngân", "Ngọc", "Nhi", "Nhung", "Oanh", "Phương", "Quỳnh", "Tâm",
        "Thảo", "Thắm", "Thu", "Thủy", "Trang", "Trinh", "Tuyết", "Uyên", "Vân", "Vi",
        "Yến", "Hạnh", "Hảo", "Diệp", "Diệu", "Kiều", "Liên", "Loan", "Nhàn", "Thư"
    };

    private static readonly string[] DemNam = new string[]
    {
        "Văn", "Hữu", "Đức", "Minh", "Quốc", "Gia", "Tuấn", "Bá", "Đình", "Xuân"
    };

    private static readonly string[] TenNam = new string[]
    {
        "An", "Bảo", "Cường", "Dũng", "Duy", "Đạt", "Đức", "Hải", "Hiếu", "Hòa",
        "Hoàng", "Hùng", "Huy", "Khánh", "Kiên", "Long", "Luân", "Minh", "Nam", "Nghĩa",
        "Phong", "Phú", "Quân", "Quang", "Sơn", "Tài", "Thắng", "Thiện", "Thịnh", "Toàn",
        "Trí", "Trung", "Tuấn", "Tùng", "Vinh", "Việt", "Bình", "Hưng", "Lâm", "Lộc",
        "Mạnh", "Nguyên", "Phát", "Phước", "Quý", "Thành", "Thái", "Tiến", "Trường", "Xuân", "Đạt",
    };

    private static readonly Random Rnd = new Random();
    private static readonly object RndLock = new object();

    public bool IsReusable
    {
        get { return false; }
    }

    private static string Pick(string[] arr)
    {
        lock (RndLock)
        {
            return arr[Rnd.Next(arr.Length)];
        }
    }

    // Bo dau tieng Viet (dung khi CoDau = false)
    private static string BoDau(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }
        string formD = s.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(formD.Length);
        for (int i = 0; i < formD.Length; i++)
        {
            char ch = formD[i];
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }

    private static bool HopLe(string ten)
    {
        if (string.IsNullOrEmpty(ten))
        {
            return false;
        }
        if (ten.Length > MaxLen)
        {
            return false;
        }
        try
        {
            if (Encoding.Default.GetByteCount(ten) > MaxLen)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
        return true;
    }

    public void ProcessRequest(HttpContext context)
    {
        string name = "fail!";
        XElement result = new XElement("Result");
        try
        {
            int sex;
            if (!int.TryParse(context.Request["sex"], out sex))
            {
                sex = 0;
            }

            string[] demArr = (sex != 0) ? DemNam : DemNu;
            string[] tenArr = (sex != 0) ? TenNam : TenNu;

            string candidate = null;

            // Uu tien Ho + dem + ten
            for (int i = 0; i < 40 && candidate == null; i++)
            {
                string t = Pick(Ho) + " " + Pick(demArr) + " " + Pick(tenArr);
                if (!CoDau)
                {
                    t = BoDau(t);
                }
                if (HopLe(t))
                {
                    candidate = t;
                }
            }

            // Qua dai thi bo dem: Ho + ten
            for (int i = 0; i < 40 && candidate == null; i++)
            {
                string t = Pick(Ho) + " " + Pick(tenArr);
                if (!CoDau)
                {
                    t = BoDau(t);
                }
                if (HopLe(t))
                {
                    candidate = t;
                }
            }

            if (candidate != null)
            {
                name = candidate;
            }
        }
        catch (Exception)
        {
            // tra ve "fail!" giong handler goc khi loi
        }

        result.Add(new XAttribute("name", name));
        context.Response.ContentType = "text/plain";
        context.Response.Write(result.ToString(SaveOptions.DisableFormatting));
    }
}
