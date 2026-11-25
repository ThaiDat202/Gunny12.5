using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using log4net;
using SqlDataProvider.BaseClass;

namespace Bussiness;

public class BaseBussiness : IDisposable
{
	protected static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	protected Sql_DbObject db;

	public BaseBussiness()
	{
		string conn = ConfigurationManager.AppSettings["conString"];
		if (string.IsNullOrWhiteSpace(conn))
		{
			var cs = ConfigurationManager.ConnectionStrings["PlayerData"];
			if (cs != null) conn = cs.ConnectionString;
		}
		if (string.IsNullOrWhiteSpace(conn))
			conn = "Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Tank;Persist Security Info=True;User ID=sa;Password=123456;";
		db = new Sql_DbObject("File", conn);
	}

	public BaseBussiness(string config)
	{
		db = new Sql_DbObject("AppConfig", config);
	}

	public DataTable GetPage(string queryStr, string queryWhere, int pageCurrent, int pageSize, string fdShow, string fdOreder, string fdKey, ref int total)
	{
		try
		{
			SqlParameter[] array = new SqlParameter[8]
			{
				new SqlParameter("@QueryStr", queryStr),
				new SqlParameter("@QueryWhere", queryWhere),
				new SqlParameter("@PageSize", pageSize),
				new SqlParameter("@PageCurrent", pageCurrent),
				new SqlParameter("@FdShow", fdShow),
				new SqlParameter("@FdOrder", fdOreder),
				new SqlParameter("@FdKey", fdKey),
				new SqlParameter("@TotalRow", total)
			};
			array[7].Direction = ParameterDirection.Output;
			DataTable dataTable = db.GetDataTable(queryStr, "SP_CustomPage", array, 120);
			total = (int)array[7].Value;
			return dataTable;
		}
		catch (Exception exception)
		{
			if (log.IsErrorEnabled)
			{
				log.Error("Init", exception);
			}
		}
		finally
		{
		}
		return new DataTable(queryStr);
	}

	public void Dispose()
	{
		db.Dispose();
		GC.SuppressFinalize(this);
	}
}
