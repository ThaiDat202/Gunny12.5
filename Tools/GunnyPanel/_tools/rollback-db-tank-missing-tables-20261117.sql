-- Rollback: xoa 2 bang da tao boi fix-db-tank-missing-tables-20261117.sql
-- Chi chay khi that su can quay lui (2 bang nay truoc do KHONG ton tai o Db_Tank).
USE Db_Tank;
GO
IF OBJECT_ID('dbo.Cloud_Buy_Lottery','U') IS NOT NULL DROP TABLE dbo.Cloud_Buy_Lottery;
GO
IF OBJECT_ID('dbo.Server_List','U') IS NOT NULL DROP TABLE dbo.Server_List;
GO
SELECT OBJECT_ID('dbo.Cloud_Buy_Lottery') AS lottery_objid, OBJECT_ID('dbo.Server_List') AS serverlist_objid;
GO
