namespace SqlDataProvider.Data;

/// <summary>
/// Một loại nông sản/vật phẩm trong kho Nông trại 2.0 — bảng dbo.Sys_User_ManorGoods.
/// Unique theo (UserID, TemplateId, Property).
/// TemplateId tương ứng TemplateID item (Shop_Goods) mà client nhận khi thu hoạch/bán.
/// </summary>
public class UserManorGoodsInfo : DataObject
{
	/// <summary>ID identity trong DB (0 = chưa insert).</summary>
	public int ID { get; set; }

	/// <summary>UserID chủ kho nông sản.</summary>
	public int UserID { get; set; }

	/// <summary>TemplateID nông sản/vật phẩm.</summary>
	public int TemplateId { get; set; }

	/// <summary>Thuộc tính/phân loại thêm của vật phẩm (protocol gửi property).</summary>
	public int Property { get; set; }

	/// <summary>Số lượng.</summary>
	public int Count { get; set; }
}
