using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ProductionLineManage.Core.Constants
{
    /// <summary> 数据点名称常量：PLC 地址映射 DataName 与 UI 下拉选项 </summary>
    public static class DataNameConstants
    {
        #region ===================== 通用地址 =====================

        /// <summary> 心跳 </summary>
        public const string Heartbeat = "心跳";

        /// <summary> 运行状态 </summary>
        public const string RunState = "运行状态";

        /// <summary> 当前流水码 </summary>
        public const string CurrentFlowCode = "当前流水码";

        /// <summary> 今日产量 </summary>
        public const string TodayCount = "今日产量";

        /// <summary> 故障标志 </summary>
        public const string Faulted = "故障";

        /// <summary> 故障代码 </summary>
        public const string FaultedCode = "故障代码";

        public const string TrayCode = "托盘号";

        #endregion

        #region ===================== 指令型地址 =====================

        /// <summary> 请求指令 </summary>
        public const string RequestCode = "请求指令";

        /// <summary> 响应指令 </summary>
        public const string ResponseCode = "响应指令";

        /// <summary> 数据负载 </summary>
        public const string DataPayload = "数据负载";

        /// <summary> 流水码类型 </summary>
        public const string CodeType = "流水码类型";

        /// <summary> 产品状态 </summary>
        public const string PrductStatus = "产品状态";

        /// <summary> 业务周期结束标志（写 true 表示本周期结束，仅接受 100 重新握手） </summary>
        public const string BusinessCycleEnded = "业务周期结束";

        #endregion

        #region ===================== 信号型地址：接收 =====================

        /// <summary> 开始交互工作 </summary>
        public const string StartWork = "开始交互工作";

        /// <summary> 流水码扫码完成 </summary>
        public const string FlowCodeDone = "流水码扫码完成";

        /// <summary> 物料码扫码完成 </summary>
        public const string MaterialCodeDone = "物料码扫码完成";

        /// <summary> 请求手动合格 </summary>
        public const string ManualPass = "请求手动合格";

        /// <summary> 请求手动不合格 </summary>
        public const string ManualNotPass = "请求手动不合格";

        /// <summary> 返修模式 </summary>
        public const string RepairMode = "返修模式";

        /// <summary> 确认返修 </summary>
        public const string ConfirmRepair = "确认返修";

        /// <summary> 请求保存数据 </summary>
        public const string SaveData = "请求保存数据";

        #endregion

        #region ===================== 信号型地址：数据 =====================

        /// <summary> 物料码 </summary>
        public const string MaterialCode = "物料码";

        /// <summary> 物料类型 </summary>
        public const string MaterialType = "物料类型";

        /// <summary> 请求返修工位 </summary>
        public const string RequestRepairStation = "请求返修工位";

        /// <summary> 工件判定合格 </summary>
        public const string JudgeOK = "工件判定合格";

        /// <summary> 工件判定不合格 </summary>
        public const string JudgeNOK = "工件判定不合格";

        #endregion

        #region ===================== 信号型地址：回复 =====================

        /// <summary> 流水码通过 </summary>
        public const string FlowCodePass = "流水码通过";

        /// <summary> 流水码不通过 </summary>
        public const string FlowCodeNotPass = "流水码不通过";

        /// <summary> 物料合格 </summary>
        public const string MaterialPass = "物料合格";

        /// <summary> 物料不合格 </summary>
        public const string MaterialNotPass = "物料不合格";

        /// <summary> 保存完成 </summary>
        public const string SaveDone = "保存完成";

        #endregion

        #region ===================== 信号型地址：写入 =====================

        /// <summary> 下发型号 </summary>
        public const string IssueProductType = "下发型号";

        /// <summary> 下发电机码 </summary>
        public const string MotorCode = "下发电机码";

        /// <summary> 下发平台代号 </summary>
        public const string PlatformCode = "下发平台代号";

        /// <summary> 允许返修工位 </summary>
        public const string AllowRepairStation = "允许返修工位";

        /// <summary> 8000 保存成功后下发下一工位代号（craft_StationInfo.Code） </summary>
        public const string NextStation = "NextStation";

        /// <summary> 合格或不合格原因 </summary>
        public const string PassReason = "合格或不合格原因";

        /// <summary> 发送流水码 </summary>
        public const string SendFlowCode = "发送流水码";

        /// <summary> 读取型号 </summary>
        public const string ReadProductType = "读取型号";

        #endregion

        #region ===================== 名称列表 =====================

        /// <summary> 所有数据名称列表（用于下拉框） </summary>
        public static IReadOnlyList<string> AllDataNames { get; } = new[]
        {
            FaultedCode,
            Faulted,
            RequestCode,
            ResponseCode,
            Heartbeat,
            RunState,
            CurrentFlowCode,
            TodayCount,
            DataPayload,
            CodeType,
            BusinessCycleEnded,
            ReadProductType,
            IssueProductType,
            RepairMode,
            RequestRepairStation,
            AllowRepairStation,
            NextStation,
            StartWork,
            FlowCodeDone,
            MaterialCodeDone,
            ManualPass,
            ManualNotPass,
            PassReason,
            MaterialCode,
            MaterialType,
            FlowCodePass,
            FlowCodeNotPass,
            MaterialPass,
            MaterialNotPass,
            SendFlowCode,
            MotorCode,
            PlatformCode,
            JudgeNOK,
            JudgeOK,
            SaveData,
            ConfirmRepair,
            SaveDone,
            PrductStatus,

        };

        /// <summary> 指令类型数据名称列表 </summary>
        public static IReadOnlyList<string> CommandTypeNames { get; } = new[]
        {
            Heartbeat,
            RunState,
            CurrentFlowCode,
            TodayCount,
            Faulted,
            FaultedCode,
            TrayCode,

            RequestCode,
            ResponseCode,
            CodeType,
            PrductStatus,
            MaterialCode,
            MaterialType,
            RequestRepairStation,

            IssueProductType,
            MotorCode,
            PlatformCode,
            AllowRepairStation,
            NextStation,

            ReadProductType,
        };

        /// <summary> 信号类型数据名称列表 </summary>
        public static IReadOnlyList<string> SignalTypeNames { get; } = new[]
        {
            Heartbeat,
            RunState,
            CurrentFlowCode,
            TodayCount,
            Faulted,
            FaultedCode,

            StartWork,
            FlowCodeDone,
            MaterialCodeDone,
            RepairMode,
            ConfirmRepair,
            SaveData,

            MaterialCode,
            RequestRepairStation,
            JudgeOK,
            JudgeNOK,

            IssueProductType,
            AllowRepairStation,

            ReadProductType,
        };

        #endregion

        #region ===================== 按交互类型筛选 =====================

        /// <summary> 按交互类型返回地址映射可选数据名称；未识别类型返回空列表 </summary>
        public static IReadOnlyList<string> GetDataNamesForInteraction(string? interactionType) =>
            interactionType switch
            {
                var t when t == InteractionTypeConstants.Command => CommandTypeNames,
                var t when t == InteractionTypeConstants.Signal => SignalTypeNames,
                _ => Array.Empty<string>()
            };

        #endregion
    }
}
