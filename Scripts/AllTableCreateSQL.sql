-- PlantSCADA 全量建表脚本（新库执行一次即可）
-- SSMS：USE 目标库 → 全选 → F5
-- 增量变更见 Scripts 目录下各 *schema*.sql

USE MES_Local_DB;
GO

-- ===================== 由于加了关联约束基础信息表移到最最前面 =====================

-- 基础信息
-- 8.产品型号信息表
IF OBJECT_ID(N'dbo.craft_TypeInfo', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[craft_TypeInfo](
		[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[Name] [varchar](24) NOT NULL,
		[Remarks] [varchar](50) NULL,
		[CreateTime] [datetime] NULL DEFAULT GETDATE(),
		[UpdateTime] [datetime] NULL,
		[IssueCode] [int] NOT NULL DEFAULT 0
	) ;
END
GO




-- 9.产线信息表
IF OBJECT_ID(N'dbo.craft_LineInfo', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[craft_LineInfo](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[Name] [varchar](20) NOT NULL,
	[Remarks] [varchar](50) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NULL
	) ;
END
GO




-- 10.工位信息表
IF OBJECT_ID(N'dbo.craft_StationInfo', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[craft_StationInfo](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[Code] [nvarchar](20) NOT NULL,
	[Name] [nvarchar](20) NOT NULL,
	[LineId] [int] NOT NULL,
	[Remarks] [nvarchar](64) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NULL
	) ;
END
GO




-- 11.工艺流程表
IF OBJECT_ID(N'dbo.craft_ProcessInfo', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[craft_ProcessInfo](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[TypeId] [int] NOT NULL,
	[LineId] [int] NOT NULL,
	[StationId] [int] NOT NULL,
	[IsRepeatWork] [bit] NOT NULL DEFAULT 0,
	[UpperWorkstationId] [int] NOT NULL DEFAULT 0,
	[Sequence] [int] NOT NULL DEFAULT 0,
	[IsEnable] [bit] NOT NULL,
	[Remarks] [varchar](50) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NULL,
	[IsRepairStation] [bit] NOT NULL DEFAULT 0
	) ;
END
GO



-- 12.数据采集配置表 
IF OBJECT_ID(N'dbo.craft_DataCollectConfig', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[craft_DataCollectConfig](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[ProductTypeId] [int] NOT NULL,
	[LineId] [int] NOT NULL,
	[StationId] [int] NOT NULL,
	[DataName] [nvarchar](100) NOT NULL,
	[DataUnit] [nvarchar](50) NULL,
	[Address] [nvarchar](200) NOT NULL,
	[DataType] [nvarchar](20) NOT NULL,
	[DataLength] [int] NULL,
	[IsEnabled] [bit] NOT NULL,
	[SortOrder] [int] NOT NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL,
	[CreateBy] [nvarchar](50) NULL,
	[UpdateBy] [nvarchar](50) NULL,
	[Remarks] [nvarchar](500) NULL
	) ;
END
GO




-- 13.工位流水码编码规则表 
IF OBJECT_ID(N'dbo.craft_FlowCodeRules', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[craft_FlowCodeRules](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[ProductTypeId] [int] NOT NULL,
	[LineId] [int] NOT NULL,
	[StationId] [int] NOT NULL,
	[GroupNo] [int] NOT NULL,
	[RuleType] [int] NOT NULL,
	[RuleName] [nvarchar](100) NOT NULL,
	[StartBit] [int] NOT NULL,
	[Length] [int] NOT NULL,
	[RuleContent] [nvarchar](500) NULL,
	[ExampleCode] [nvarchar](100) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL,
	[IsEnabled] [bit] NOT NULL,
	[Remarks] [nvarchar](500) NULL
	) ;
END
GO




-- 14.工位传值表 
IF OBJECT_ID(N'dbo.craft_StationDataTransfer', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[craft_StationDataTransfer](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[RequestStationId] [int] NOT NULL,
	[RequestDataName] [nvarchar](100) NOT NULL,
	[SourceStationId] [int] NOT NULL,
	[SourceAddress] [nvarchar](200) NOT NULL,
	[DataType] [nvarchar](20) NOT NULL,
	[DataLength] [int] NULL,
	[ProductTypeId] [int] NOT NULL,
	[LineId] [int] NOT NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL,
	[IsEnabled] [bit] NOT NULL,
	[Remarks] [nvarchar](500) NULL
	) ;

    CREATE INDEX IX_StationDataTransfer_Request
        ON dbo.craft_StationDataTransfer (RequestStationId, ProductTypeId, LineId, IsEnabled);
END
GO



-- 电机码规则相关表（PlantSCADA 工艺域）
-- 若 Host 使用的不是 MES_Local_DB，请修改上面 USE 的数据库名

-- 1. 按型号序列号配置（每型号独立位数/步长/复位周期）
IF OBJECT_ID(N'dbo.craft_MotorCodeSequenceConfig', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.craft_MotorCodeSequenceConfig
    (
        Id             INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductTypeId  INT           NOT NULL,
        DigitLength    INT           NOT NULL DEFAULT 4,
        StartValue     INT           NOT NULL DEFAULT 1,
        Step           INT           NOT NULL DEFAULT 1,  -- 自增系数
        ResetCycle     INT           NOT NULL DEFAULT 1, -- 1日 2周 3月 4年 5永不
        IsEnabled      BIT           NOT NULL DEFAULT 1,
        CreateTime     DATETIME      NULL DEFAULT GETDATE(),
        UpdateTime     DATETIME      NULL,
        Remarks        NVARCHAR(200) NOT NULL DEFAULT N'',
        CONSTRAINT UQ_MotorCodeSequenceConfig_Type UNIQUE (ProductTypeId)
    );
END
GO

-- 2. 按型号序列号运行时状态
IF OBJECT_ID(N'dbo.craft_MotorCodeSequenceState', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.craft_MotorCodeSequenceState
    (
        Id             INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductTypeId  INT           NOT NULL,
        BucketKey      NVARCHAR(32)  NOT NULL DEFAULT N'GLOBAL',
        CurrentValue   INT           NOT NULL DEFAULT 0,
        LastResetTime  DATETIME      NULL,
        UpdateTime     DATETIME      NULL,
        CONSTRAINT UQ_MotorCodeSequenceState_Type UNIQUE (ProductTypeId)
    );
END
GO

-- 3. 按型号：日期代号对照（年可选；月1-12、日1-31 保存时校验）
IF OBJECT_ID(N'dbo.craft_MotorCodeDateMap', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.craft_MotorCodeDateMap
    (
        Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductTypeId INT           NOT NULL,
        MapType       INT           NOT NULL, -- 1年 2月 3日
        MapKey        INT           NOT NULL,
        MapCode       NVARCHAR(20)  NOT NULL DEFAULT N'',
        CreateTime    DATETIME      NULL DEFAULT GETDATE(),
        UpdateTime    DATETIME      NULL,
        Remarks       NVARCHAR(200) NOT NULL DEFAULT N'',
        CONSTRAINT UQ_MotorCodeDateMap UNIQUE (ProductTypeId, MapType, MapKey)
    );

    CREATE INDEX IX_MotorCodeDateMap_Type ON dbo.craft_MotorCodeDateMap (ProductTypeId, MapType);
END
GO

-- 4. 按型号：固定信息片段
IF OBJECT_ID(N'dbo.craft_MotorCodeFixedSegment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.craft_MotorCodeFixedSegment
    (
        Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductTypeId INT           NOT NULL,
        SegmentCode   NVARCHAR(10)  NOT NULL,
        FixedValue    NVARCHAR(50)  NOT NULL DEFAULT N'',
        SortOrder     INT           NOT NULL DEFAULT 0,
        CreateTime    DATETIME      NULL DEFAULT GETDATE(),
        UpdateTime    DATETIME      NULL,
        Remarks       NVARCHAR(200) NOT NULL DEFAULT N'',
		UsageType	  INT			NOT NULL DEFAULT 0 ,
        CONSTRAINT UQ_MotorCodeFixedSegment UNIQUE (ProductTypeId, SegmentCode)
    );
	
END
GO

-- 5. 按型号：生成规则公式
IF OBJECT_ID(N'dbo.craft_MotorCodeRule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.craft_MotorCodeRule
    (
        Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductTypeId INT           NOT NULL,
        Formula       NVARCHAR(200) NOT NULL DEFAULT N'',
        IsEnabled     BIT           NOT NULL DEFAULT 1,
        CreateTime    DATETIME      NULL DEFAULT GETDATE(),
        UpdateTime    DATETIME      NULL,
        Remarks       NVARCHAR(200) NOT NULL DEFAULT N'',
        CONSTRAINT UQ_MotorCodeRule UNIQUE (ProductTypeId)
    );
	--加型号表ID约束
	ALTER TABLE [dbo].[craft_MotorCodeRule]  WITH CHECK ADD  CONSTRAINT [FK_craft_MotorCodeRule_craft_TypeInfo] FOREIGN KEY([ProductTypeId])
	REFERENCES [dbo].[craft_TypeInfo] ([Id])
END
GO

-- 6. 按型号：片段绑定（D1=年代号 等）
IF OBJECT_ID(N'dbo.craft_MotorCodeSegmentBind', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.craft_MotorCodeSegmentBind
    (
        Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductTypeId INT           NOT NULL,
        SegmentCode   NVARCHAR(10)  NOT NULL,
        SegmentType   INT           NOT NULL, -- 1年 2月 3日 4序列 5固定
        SortOrder     INT           NOT NULL DEFAULT 0,
        CreateTime    DATETIME      NULL DEFAULT GETDATE(),
        UpdateTime    DATETIME      NULL,
        Remarks       NVARCHAR(200) NOT NULL DEFAULT N'',
		FixedSegmentId INT NOT NULL CONSTRAINT DF_MotorCodeSegmentBind_FixedSegmentId DEFAULT 0,
        CONSTRAINT UQ_MotorCodeSegmentBind UNIQUE (ProductTypeId, SegmentCode)
    );
END
GO

-- 7. 序列号手动调整审计（可选）
IF OBJECT_ID(N'dbo.craft_MotorCodeSequenceAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.craft_MotorCodeSequenceAudit
    (
        Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductTypeId INT           NOT NULL DEFAULT 0,
        OldValue      INT           NOT NULL,
        NewValue      INT           NOT NULL,
        OldBucketKey  NVARCHAR(32)  NOT NULL DEFAULT N'',
        NewBucketKey  NVARCHAR(32)  NOT NULL DEFAULT N'',
        Operator      NVARCHAR(50)  NOT NULL DEFAULT N'',
        Reason        NVARCHAR(200) NOT NULL DEFAULT N'',
        OperateTime   DATETIME      NOT NULL DEFAULT GETDATE()
    );
END
GO








-- 设备相关
-- 15.设备连接信息表
IF OBJECT_ID(N'dbo.device_ConnectInfo', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[device_ConnectInfo](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[StationId] [int] NOT NULL,
	[DeviceCode] [varchar](50) NULL,
	[ProtocolType] [varchar](30) NOT NULL,
	[ConnectionMode] [varchar](30) NOT NULL,
	[ConnectionString] [text] NULL,
	[ScanIntervalMs] [int] NOT NULL,
	[HeartbeatIntervalMs] [int] NOT NULL,
	[HeartbeatTimeoutMs] [int] NOT NULL,
	[ReconnectIntervalMs] [int] NOT NULL,
	[MaxReconnectAttempts] [int] NOT NULL,
	[InteractionType] [varchar](20) NOT NULL DEFAULT ('Command'),
	[IsIssueModel] [bit] NULL,
	[IsEnabled] [bit] NOT NULL,
	[Remarks] [varchar](255) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL
	) ;
END
GO

-- 16.设备交互地址映射表
IF OBJECT_ID(N'dbo.device_AddressMapping', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[device_AddressMapping](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[StationId] [int] NOT NULL,
	[DataName] [nvarchar](50) NOT NULL,
	[DataAddress] [nvarchar](200) NOT NULL,
	[DataType] [nvarchar](20) NOT NULL,
	[DataLen] [int] NULL,
	[DataDirection] [nvarchar](10) NOT NULL,
	[IsEnabled] [bit] NULL,
	[Remarks] [nvarchar](200) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL
	) ;
END
GO


-- 物料相关
-- 17.物料信息表
IF OBJECT_ID(N'dbo.material_Info', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[material_Info](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[Code] [nvarchar](20) NOT NULL,
	[Name] [nvarchar](20) NOT NULL,
	[TypeId] [int] NOT NULL,
	[Remarks] [varchar](50) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL
	) ;
END
GO


-- 18.物料编码规则表
IF OBJECT_ID(N'dbo.material_CodeRules', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[material_CodeRules](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[TypeId] [int] NOT NULL,
	[MaterialId] [int] NOT NULL,
	[GroupNo] [int] NOT NULL,
	[RuleType] [int] NOT NULL,
	[RuleName] [nvarchar](100) NOT NULL,
	[StartBit] [int] NOT NULL,
	[Length] [int] NOT NULL,
	[RuleContent] [nvarchar](500) NOT NULL,
	[ExampleCode] [nvarchar](200) NULL,
	[IsEnabled] [bit] NULL,
	[Remarks] [varchar](50) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL
	) ;
END
GO


-- 19.工位物料表
IF OBJECT_ID(N'dbo.material_Station', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[material_Station](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[TypeId] [int] NOT NULL,
	[LineId] [int] NOT NULL,
	[StationId] [int] NOT NULL,
	[MaterialId] [int] NOT NULL,
	[CheckMaterialStationId] [int] NOT NULL,
	[Sequence] [int] NOT NULL,
	[ParentMaterialId] [int] NOT NULL,
	[Remarks] [varchar](50) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL
	) ;
END
GO



-- 生产相关
-- 20.产品工位状态表
IF OBJECT_ID(N'dbo.production_ProductStationStatus', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[production_ProductStationStatus](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[FlowCode] [nvarchar](100) NOT NULL,
	[TrayCode] [nvarchar](100) NOT NULL,
	[ProductTypeId] [int] NOT NULL,
	[LineId] [int] NOT NULL,
	[StationId] [int] NOT NULL,
	[Status] [int] NOT NULL,
	[IsRepair] [bit] NOT NULL,
	[RepairTargetStationId] [int] NOT NULL,
	[RepairCount] [int] NOT NULL,
	[Remarks] [varchar](50) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL
	) ;

    CREATE UNIQUE INDEX UX_ProductStationStatus_Unique
        ON dbo.production_ProductStationStatus (ProductTypeId, LineId, StationId, FlowCode);
END
GO


-- 报表相关
-- 21.物料绑定表
IF OBJECT_ID(N'dbo.report_MaterialBind', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[report_MaterialBind](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[ProductTypeId] [int] NOT NULL,
	[LineId] [int] NOT NULL,
	[StationId] [int] NOT NULL,
	[FlowCode] [nvarchar](200) NOT NULL,
	[BindMaterialName] [nvarchar](100) NOT NULL,
	[BindMaterialCode] [nvarchar](200) NOT NULL,
	[BindStatus] [int] NOT NULL,
	[BindTime] [datetime] NOT NULL,
	[UnbindTime] [datetime] NULL,
	[Remarks] [varchar](50) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL
	) ;
END
GO


-- 22.历史加工数据表
IF OBJECT_ID(N'dbo.report_ProcessHistory', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[report_ProcessHistory](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[StationRecordId] [int] NOT NULL,
	[ProductTypeId] [int] NOT NULL,
	[LineId] [int] NOT NULL,
	[FlowCode] [nvarchar](200) NOT NULL,
	[TrayCode] [nvarchar](100) NULL,
	[StationId] [int] NOT NULL,
	[DataName] [nvarchar](100) NOT NULL,
	[DataValue] [nvarchar](500) NOT NULL,
	[DataType] [nvarchar](20) NULL,
	[DataUnit] [nvarchar](20) NULL,
	[WorkOrderId] [nvarchar](50) NULL,
	[OperatorId] [int] NULL,
	[Shift] [nvarchar](20) NULL,
	[IsQualified] [bit] NULL,
	[DefectiveReason] [nvarchar](50) NULL,
	[IsRepair] [bit] NULL,
	[RepairCount] [int] NULL,
	[RepairReason] [nvarchar](200) NULL,
	[Remarks] [varchar](50) NULL,
	[CreateTime] [datetime] NOT NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL
	) ;
END
GO


-- 23.过站明细表
IF OBJECT_ID(N'dbo.report_StationPassRecord', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[report_StationPassRecord](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[FlowCode] [nvarchar](100) NOT NULL,
	[TrayCode] [nvarchar](100) NOT NULL,
	[ProductTypeId] [int] NOT NULL,
	[LineId] [int] NOT NULL,
	[StationId] [int] NOT NULL,
	[StartTime] [datetime] NOT NULL,
	[EndTime] [datetime] NULL,
	[Remarks] [varchar](50) NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL
	) ;

    CREATE INDEX IX_StationPassRecord_Query
        ON dbo.report_StationPassRecord (FlowCode, ProductTypeId, LineId, StationId, StartTime DESC);
END
GO



-- 其他
-- 24.用户登录信息表（DataLoadService 查询表名 UsersInfo）
IF OBJECT_ID(N'dbo.UsersInfo', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[UsersInfo](
	[Id] [int] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[UserName] [nvarchar](50) NOT NULL,
	[PasswordHash] [nvarchar](256) NOT NULL,
	[Salt] [nvarchar](128) NOT NULL,
	[Level] [int] NOT NULL,
	[CreateTime] [datetime] NULL DEFAULT GETDATE(),
	[UpdateTime] [datetime] NOT NULL,
	CONSTRAINT UQ_UsersInfo_UserName UNIQUE (UserName)
	) ;
END
GO
