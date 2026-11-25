SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.Cloud_Buy_Lottery','U') IS NULL
BEGIN
    CREATE TABLE dbo.Cloud_Buy_Lottery
    (
        ID int IDENTITY(1,1) NOT NULL,
        templateId int NOT NULL CONSTRAINT DF_Cloud_Buy_Lottery_templateId DEFAULT (0),
        templatedIdCount int NOT NULL CONSTRAINT DF_Cloud_Buy_Lottery_templatedIdCount DEFAULT (0),
        validDate int NOT NULL CONSTRAINT DF_Cloud_Buy_Lottery_validDate DEFAULT (0),
        property nvarchar(150) NOT NULL CONSTRAINT DF_Cloud_Buy_Lottery_property DEFAULT (N''),
        buyItemsArr nvarchar(150) NOT NULL CONSTRAINT DF_Cloud_Buy_Lottery_buyItemsArr DEFAULT (N''),
        buyMoney int NOT NULL CONSTRAINT DF_Cloud_Buy_Lottery_buyMoney DEFAULT (0),
        maxNum int NOT NULL CONSTRAINT DF_Cloud_Buy_Lottery_maxNum DEFAULT (0),
        currentNum int NOT NULL CONSTRAINT DF_Cloud_Buy_Lottery_currentNum DEFAULT (0)
    );

    SET IDENTITY_INSERT dbo.Cloud_Buy_Lottery ON;
    INSERT INTO dbo.Cloud_Buy_Lottery
        (ID, templateId, templatedIdCount, validDate, property, buyItemsArr, buyMoney, maxNum, currentNum)
    SELECT ID, templateId, templatedIdCount, validDate, property, buyItemsArr, buyMoney, maxNum, currentNum
    FROM Db_Tank_All.dbo.Cloud_Buy_Lottery;
    SET IDENTITY_INSERT dbo.Cloud_Buy_Lottery OFF;
END
GO

IF OBJECT_ID('dbo.Server_List','U') IS NULL
BEGIN
    CREATE TABLE dbo.Server_List
    (
        ID int NOT NULL,
        Name nvarchar(50) NULL,
        IP nvarchar(50) NULL,
        Port int NULL CONSTRAINT DF_Server_List_Port DEFAULT (0),
        State int NOT NULL CONSTRAINT DF_Server_List_State DEFAULT (0),
        Online int NOT NULL CONSTRAINT DF_Server_List_Online DEFAULT (0),
        Total int NOT NULL CONSTRAINT DF_Server_List_Total DEFAULT (1000),
        Room int NOT NULL CONSTRAINT DF_Server_List_Room DEFAULT (200),
        Remark nvarchar(200) NULL,
        RSA nvarchar(1000) NULL,
        MustLevel int NOT NULL CONSTRAINT DF_Server_List_MustLevel DEFAULT (0),
        LowestLevel int NOT NULL CONSTRAINT DF_Server_List_LowestLevel DEFAULT (0),
        NewerServer bit NOT NULL CONSTRAINT DF_Server_List_NewerServer DEFAULT (0),
        ZoneId int NOT NULL CONSTRAINT DF_Server_List_ZoneId DEFAULT (1),
        CONSTRAINT PK_Server_List PRIMARY KEY CLUSTERED (ID)
    );

    INSERT INTO dbo.Server_List
        (ID, Name, IP, Port, State, Online, Total, Room, Remark, RSA, MustLevel, LowestLevel, NewerServer, ZoneId)
    SELECT ID, Name, IP, Port, State, Online, Total, Room, Remark, RSA, MustLevel, LowestLevel, NewerServer, ZoneId
    FROM Db_Tank_All.dbo.Server_List;
END
GO

COMMIT TRANSACTION;
GO

SELECT 'Cloud_Buy_Lottery' AS tbl, COUNT(*) AS rows FROM dbo.Cloud_Buy_Lottery
UNION ALL
SELECT 'Server_List', COUNT(*) FROM dbo.Server_List;
GO

SELECT OBJECT_ID('dbo.Cloud_Buy_Lottery') AS lottery_objid, OBJECT_ID('dbo.Server_List') AS serverlist_objid;
GO
