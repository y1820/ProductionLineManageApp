namespace ProductionLineManage.Core.CommandCodes
{
    /// <summary> 已编译进程序的指令码模板键；工位下拉只出现登记表里的项 </summary>
    public static class CommandCodeTemplateKeys
    {
        /// <summary> 现有现场：握手 100、流水码 200、物料 500、保存 8000、电机码 900 </summary>
        public const string Code100 = "Code100";

        /// <summary> 1/2 系列：握手 1、流水码 2、物料 3、保存 4、电机码 5 </summary>
        public const string Code12 = "Code12";
    }
}
