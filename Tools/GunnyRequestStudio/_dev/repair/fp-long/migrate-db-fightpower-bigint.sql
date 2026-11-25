-- ============================================================================
--  migrate-db-fightpower-bigint.sql
--  Chuyen cot luc chien ca nhan (Db_Tank.dbo.Sys_Users_Detail.FightPower)
--  tu int -> bigint de ho tro > 2,147,483,647
--
--  An toan:
--   - Backup nguyen bang Sys_Users_Detail -> Sys_Users_Detail_bak_fpbigint
--   - Giu default constraint (0)
--   - Sua SP_Users_Update: @FightPower int -> bigint (tu dong tu definition)
--   - Refresh cac view lien quan
--   - Co the chay lai nhieu lan (kiem tra truoc khi lam)
--  Chay tren: DB_TANK
-- ============================================================================
USE [Db_Tank];
GO

SET NOCOUNT ON;
PRINT N'=== 1. Backup Sys_Users_Detail ===';
IF OBJECT_ID('dbo.Sys_Users_Detail_bak_fpbigint') IS NULL
BEGIN
    SELECT * INTO dbo.Sys_Users_Detail_bak_fpbigint FROM dbo.Sys_Users_Detail;
    PRINT N'  da tao Sys_Users_Detail_bak_fpbigint';
END
ELSE PRINT N'  backup da ton tai - bo qua';
GO

PRINT N'=== 2. ALTER cot FightPower -> bigint ===';
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id
           WHERE c.object_id=OBJECT_ID('dbo.Sys_Users_Detail') AND c.name='FightPower' AND ty.name='bigint')
    PRINT N'  da la bigint - bo qua';
ELSE
BEGIN
    DECLARE @dfName sysname;
    SELECT @dfName = dc.name FROM sys.default_constraints dc
    WHERE dc.parent_object_id = OBJECT_ID('dbo.Sys_Users_Detail')
      AND dc.parent_column_id = COLUMNPROPERTY(OBJECT_ID('dbo.Sys_Users_Detail'),'FightPower','ColumnId');
    IF @dfName IS NOT NULL
        EXEC('ALTER TABLE dbo.Sys_Users_Detail DROP CONSTRAINT [' + @dfName + ']');

    ALTER TABLE dbo.Sys_Users_Detail ALTER COLUMN FightPower bigint NOT NULL;

    ALTER TABLE dbo.Sys_Users_Detail ADD CONSTRAINT DF_Sys_Users_Detail_FightPower DEFAULT ((0)) FOR FightPower;
    PRINT N'  ALTER OK + da them lai default 0';
END
GO

PRINT N'=== 3. Sua SP_Users_Update: @FightPower bigint ===';
DECLARE @def nvarchar(max), @pos int;
SELECT @def = m.definition
FROM sys.sql_modules m JOIN sys.objects o ON o.object_id = m.object_id
WHERE o.name = 'SP_Users_Update' AND o.type = 'P';

IF @def IS NULL
    PRINT N'  KHONG tim thay SP_Users_Update';
ELSE IF @def NOT LIKE N'%@FightPower bigint%'
BEGIN
    SET @def = REPLACE(@def, '@FightPower int', '@FightPower bigint');
    SET @pos = CHARINDEX('CREATE', UPPER(@def));
    IF @pos > 0 SET @def = STUFF(@def, @pos, 6, 'ALTER ');
    BEGIN TRY
        EXEC sp_executesql @def;
        PRINT N'  SP_Users_Update -> @FightPower bigint OK';
    END TRY
    BEGIN CATCH
        PRINT N'  LOI alter SP: ' + ERROR_MESSAGE();
    END CATCH
END
ELSE PRINT N'  SP da la bigint - bo qua';
GO

PRINT N'=== 4. Refresh view lien quan ===';
BEGIN TRY EXEC sp_refreshview N'dbo.V_Sys_Users_Detail'; PRINT N'  refresh V_Sys_Users_Detail'; END TRY BEGIN CATCH PRINT N'  ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY EXEC sp_refreshview N'dbo.V_Sys_Users_Friends'; PRINT N'  refresh V_Sys_Users_Friends'; END TRY BEGIN CATCH PRINT N'  ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY EXEC sp_refreshview N'dbo.V_Sys_Marry_Info'; PRINT N'  refresh V_Sys_Marry_Info'; END TRY BEGIN CATCH PRINT N'  ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY EXEC sp_refreshview N'dbo.V_Consortia'; PRINT N'  refresh V_Consortia'; END TRY BEGIN CATCH PRINT N'  ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY EXEC sp_refreshview N'dbo.V_Consortia_Users'; PRINT N'  refresh V_Consortia_Users'; END TRY BEGIN CATCH PRINT N'  ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY EXEC sp_refreshview N'dbo.V_Consortia_Apply_Users'; PRINT N'  refresh V_Consortia_Apply_Users'; END TRY BEGIN CATCH PRINT N'  ' + ERROR_MESSAGE(); END CATCH
GO

PRINT N'=== 5. Kiem tra ===';
SELECT COL_LENGTH('dbo.Sys_Users_Detail','FightPower') AS FightPower_bytes,
       ty.name AS FightPower_type
FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE c.object_id=OBJECT_ID('dbo.Sys_Users_Detail') AND c.name='FightPower';

SELECT p.name AS ParamName, ty.name AS ParamType
FROM sys.parameters p JOIN sys.types ty ON ty.user_type_id = p.user_type_id
WHERE p.object_id = OBJECT_ID('dbo.SP_Users_Update') AND p.name = '@FightPower';
GO
