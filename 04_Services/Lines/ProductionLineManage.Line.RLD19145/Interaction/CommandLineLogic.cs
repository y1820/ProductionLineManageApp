using Azure;
using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;
using ProductionLineManage.Core.Services.MotorCode;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Line.RLD19145.Interaction
{
    /// <summary>
    /// 指令类型交互逻辑：处理业务中介转发的 PLC 请求码消息。
    /// 依赖设备状态、工位状态、地址映射及已注册的业务 Handler。
    /// </summary>
    public class CommandLineLogic : IInteractionType
    {
        #region ===================== 字段与构造 =====================

        private readonly IDeviceTaskContext _context;
        private readonly IDeviceStatusManager _deviceStatusManager;
        private readonly IReadOnlyDictionary<string, IDeviceDataHandler> _handlers;
        private readonly IDataCacheService _cacheService;
        private readonly IRepairService _repairService;
        private readonly IMotorCodeDispatchService _motorCodeDispatch;
        private readonly IStationDataTransferService _stationDataTransfer;

        /// <summary>本实例私有的交互编排状态，不交给 Mediator 管理</summary>
        private readonly StationWorkState _workState;

        /// <summary>工位 Id</summary>
        public int StationId => _context.StationId;

        /// <summary>交互类型标识：Command</summary>
        public string LogicType => InteractionTypeConstants.Command;

        /// <summary>
        /// 注入工位上下文、状态管理器及业务服务
        /// 底座：_context / _deviceStatusManager / _cacheService
        /// 字典：_handlers（200/500/8000/返修确认）
        /// 漏出的直接调用：物料清单、返修规则、返修查询、900、工位传值
        /// </summary>
        public CommandLineLogic(
            IDeviceTaskContext context,
            IDeviceStatusManager statusManager,
            IReadOnlyDictionary<string, IDeviceDataHandler> handlers,
            IDataCacheService cacheService,
            IRepairService repairService,
            IMotorCodeDispatchService motorCodeDispatch,
            IStationDataTransferService stationDataTransfer)
        {
            _context = context;
            _deviceStatusManager = statusManager;
            _handlers = handlers;
            _cacheService = cacheService;
            _repairService = repairService;
            _motorCodeDispatch = motorCodeDispatch;
            _stationDataTransfer = stationDataTransfer;
            _workState = new StationWorkState { StationId = context.StationId, LineId = context.StationInfo.LineId };
        }

        #endregion

        #region ===================== 消息分发 =====================

        /// <summary>业务中介入口：按数据名称分发到对应处理方法</summary>
        public async Task HandleMessageAsync(DeviceDataMessage message, IDeviceTaskContext context)
        {
            await HandleDataNameAsync(message.DataType, message.Data);
        }

        /// <summary>按 PLC 采集数据名称路由到状态更新或请求码处理</summary>
        private async Task HandleDataNameAsync(string dataName, object? value)
        {
            try
            {
                switch (dataName)
                {
                    case DataNameConstants.RunState:
                        RunStateAsync(Convert.ToInt32(value)); // 更新运行状态
                        break;

                    case DataNameConstants.CurrentFlowCode:
                        if(value is string)
                        {
                            string flowCode = (value as string)?.Replace("\0", "") ?? "";
                            flowCode = flowCode.Trim();
                            CurrentFlowCodeAsync(flowCode); // 同步当前流水码到 DeviceStatus
                        }
                        break;

                    case DataNameConstants.TodayCount:
                        TodayCountAsync(Convert.ToInt32(value)); // 更新今日加工计数
                        break;

                    case DataNameConstants.RequestCode:
                        await RequestCodeAsync(Convert.ToInt32(value)); // 核心：处理 PLC 请求指令码
                        break;
                    case DataNameConstants.TrayCode://托盘号
                        _workState.TrayCode = value?.ToString() ?? "NA";
                        _deviceStatusManager.UpdateStatus(_context.StationId, st => st.TrayCode = value?.ToString() ?? "NA");
                        break;

                    case DataNameConstants.DataPayload:
                    case DataNameConstants.CodeType:
                    case DataNameConstants.RepairMode:
                    case DataNameConstants.MaterialCode:
                    case DataNameConstants.MaterialType:
                    case DataNameConstants.PrductStatus:
                        break; // 由请求码处理流程主动读取，此处忽略采集推送

                    case DataNameConstants.ReadProductType:
                        ProductTypeAsync(value?.ToString() ?? "--"); // PLC 决定型号时更新型号上下文
                        break;

                    default:
                        _context.Log($"{dataName} 该数据名称未设置处理方式", LogLevel.Warning);
                        break;
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _context.Log($"读取数据后处理过程异常，异常数据名称：{dataName} ,数据值：{value}," + ex.Message, LogLevel.Error);
            }
        }

        /// <summary>将 PLC 运行状态码映射为 RunState 枚举并写入 DeviceStatus</summary>
        private void RunStateAsync(int state)
        {
            _deviceStatusManager.UpdateStatus(_context.StationId,
                st =>
                {
                    st.RunState = state switch
                    {
                        0 => RunState.Stopped,
                        1 => RunState.Running,
                        2 => RunState.Idle,
                        3 => RunState.Alarm,
                        4 => RunState.Maintenance,
                        _ => RunState.Stopped,
                    };
                });
        }

        /// <summary>更新 DeviceStatus 中的当前流水码</summary>
        private void CurrentFlowCodeAsync(string code)
        {
            string currentCode = _deviceStatusManager.GetStatus(_context.StationId)?.CurrentFlowCode ?? string.Empty;
            if (string.IsNullOrEmpty(currentCode) || string.IsNullOrEmpty(code.Trim()))
                return;
            if (currentCode != code.Trim())
            {
                _deviceStatusManager.UpdateStatus(_context.StationId,
                st => st.CurrentFlowCode = code);
                _context.Log($"流水码采集更新:{code}");
            }
            
        }

        /// <summary>更新 DeviceStatus 中的今日加工计数</summary>
        private void TodayCountAsync(int value)
        {
            _deviceStatusManager.UpdateStatus(_context.StationId,
                st => st.TodayProcessedCount = value);
        }

        /// <summary>解析 PLC 读取的型号文本，匹配 craft_TypeInfo 并更新工作态</summary>
        private void ProductTypeAsync(string value)
        {
            if (string.IsNullOrWhiteSpace(value.Replace("\0", "").Trim()) || value == "--" || string.IsNullOrEmpty(value.Trim())) // 型号为空或占位符
            {
                _workState.ProductTypeName = string.Empty;
                _workState.ProductTypeId = 0;
                _deviceStatusManager.UpdateStatus(_context.StationId,
                    st => st.ProductTypeName = "--");
                _context.Log("<型号> PLC 读取型号为空", LogLevel.Warning);
                return;
            }

            var trimmed = value.Trim();
            _workState.ProductTypeName = trimmed;
            _workState.ProductTypeId = ResolveProductTypeId(trimmed);

            _deviceStatusManager.UpdateStatus(_context.StationId,
                st => st.ProductTypeName = trimmed);

            if (_workState.ProductTypeId <= 0)
                _context.Log($"<型号> PLC 读取型号「{trimmed}」未匹配到型号档案", LogLevel.Warning);
            else
                _context.Log($"<型号> PLC 读取型号={trimmed}，匹配 Id={_workState.ProductTypeId}");
        }

        /// <summary>处理 PLC 请求码：防重入门禁 + 调用 ProcessRequestAsync</summary>
        private async Task RequestCodeAsync(int value)
        {
            if (value == 0) // 请求码归零表示 PLC 已收到响应，释放处理锁
            {
                _workState.IsProcessing = false;
                return;
            }

            if (_workState.IsProcessing) // 上一指令尚未完成，跳过重复请求
            {
                _context.Log($"<指令处理> 跳过重复请求码={value}，上一指令仍在处理中", LogLevel.Warning);
                return;
            }

            _workState.IsProcessing = true;
            try
            {
                await ProcessRequestAsync(value);
            }
            finally
            {
                _workState.IsProcessing = false;
                _workState.LastProcessTime = DateTime.Now;
            }
        }

        #endregion

        #region ===================== 指令处理 =====================

        /// <summary>处理 PLC 请求指令：生命周期门禁 → 分发 Handler → 回写响应码</summary>
        private async Task ProcessRequestAsync(int requestCode)
        {
            _context.Log($"<指令处理> 开始，请求码={requestCode}，周期状态：握手={_workState.IsInteractionStarted}，流水码合格={_workState.IsFlowCodeQualified}，周期结束={_workState.IsBusinessCycleEnded}");

            _deviceStatusManager.UpdateStatus(_context.StationId,
               st =>
               {
                   st.DeviceLastCommand = requestCode.ToString();
                   st.DeviceLastCommandTime = DateTime.Now;
               });

            // ===== 生命周期门禁 =====
            if (!TryValidateRequestSequence(requestCode, out var rejectReason)) // 校验指令顺序与周期状态
            {
                _context.Log($"<指令处理> 拒绝，请求码={requestCode}，原因={rejectReason}", LogLevel.Warning);
                await WriteResponseAsync((int)SCADAResponseCode.SequenceNotAllowed); // 回写顺序不允许
                return;
            }

            int responseCode;
            string? dataPayload = null;

            try
            {
                _context.Log($"<指令处理> 执行中，分发请求码={requestCode}");

                switch (requestCode)
                {
                    case (int)PLCRequestCode.Handshake:
                        responseCode = await HandleHandshakeAsync();
                        break;

                    case (int)PLCRequestCode.FlowCodeVerify:
                        (responseCode, dataPayload) = await HandleFlowCodeVerifyAsync();
                        break;

                    case (int)PLCRequestCode.MaterialVerify:
                        (responseCode, dataPayload) = await HandleMaterialVerifyAsync();
                        break;

                    case (int)PLCRequestCode.SaveDataExtended:
                        responseCode = await HandleSaveDataExtendedAsync();
                        break;

                    case (int)PLCRequestCode.MotorCodeFetch:
                        responseCode = await HandleMotorCodeFetchAsync();
                        break;

                    default:
                        _context.Log($"未知指令码: {requestCode}", LogLevel.Warning);
                        responseCode = (int)SCADAResponseCode.GeneralError;
                        break;
                }

                _context.Log($"<指令处理> 执行中，写入响应码={responseCode}");
                await WriteResponseAsync(responseCode); // 回写 SCADA 响应码给 PLC

                if (dataPayload != null && requestCode == (int)PLCRequestCode.MaterialVerify) // 500 验证通过时回写物料码
                {
                    _context.Log($"<指令处理> 执行中，回写物料码={dataPayload}");
                    await WriteMaterialCodeAsync(dataPayload);
                }

                _context.Log($"<指令处理> 完成，请求码={requestCode}，响应码={responseCode}，周期状态：握手={_workState.IsInteractionStarted}，流水码合格={_workState.IsFlowCodeQualified}，周期结束={_workState.IsBusinessCycleEnded}");
            }
            catch (TimeoutException ex)
            {
                _context.Log($"<指令处理> 超时，请求码={requestCode}，{ex.Message}", LogLevel.Error);
                await WriteResponseAsync((int)SCADAResponseCode.NeedRetry); // 超时提示 PLC 重试
            }
            catch (Exception ex)
            {
                _context.Log($"<指令处理> 异常，请求码={requestCode}，{ex.Message}", LogLevel.Error);
                await WriteResponseAsync((int)SCADAResponseCode.GeneralError); // 通用错误响应
            }
        }

        /// <summary>校验指令是否允许在当前业务周期内执行（100/200/500/8000/900 顺序门禁）</summary>
        private bool TryValidateRequestSequence(int requestCode, out string rejectReason)
        {
            rejectReason = string.Empty;

            if (requestCode == (int)PLCRequestCode.Handshake) // 100 握手始终允许，用于开启新周期
                return true;

            if (_workState.IsBusinessCycleEnded) // 8000 完成后除 100 外均拒绝
            {
                rejectReason = "业务周期已结束，请先发送 100 重新握手";
                return false;
            }

            if (!_workState.IsInteractionStarted) // 未完成握手不允许其他指令
            {
                rejectReason = "尚未完成 100 握手，不允许执行其他指令";
                return false;
            }

            if ((requestCode == (int)PLCRequestCode.MaterialVerify
                 || requestCode == (int)PLCRequestCode.MotorCodeFetch)
                && !_workState.IsFlowCodeQualified) // 500/900 依赖 200 流水码验证通过
            {
                rejectReason = requestCode == (int)PLCRequestCode.MotorCodeFetch
                    ? "流水码尚未通过 200 验证，不允许获取电机码"
                    : "流水码尚未通过 200 验证，不允许执行物料验证";
                return false;
            }
            if (requestCode == (int)PLCRequestCode.MotorCodeFetch
                && string.IsNullOrEmpty(GetCurrentFlowCodeFromStatus())) // 900 需要有效流水码
            {
                rejectReason = "流水码为空，不允许获取电机码";
                return false;
            }

            if (requestCode == (int)PLCRequestCode.SaveDataExtended) // 8000 门禁
            {
                if (string.IsNullOrEmpty(GetCurrentFlowCodeFromStatus()))
                {
                    rejectReason = "流水码为空，不允许保存数据";
                    return false;
                }

                if (!_workState.IsFlowCodeQualified)
                {
                    rejectReason = "流水码尚未通过 200 验证，不允许保存数据";
                    return false;
                }
            }

            return true;
        }

        /// <summary>100 握手成功：重置工作态并开启新交互周期</summary>
        private void BeginInteractionCycle()
        {
            _workState.IsInteractionStarted = true;
            _workState.IsFlowCodeQualified = false;
            _workState.IsBusinessCycleEnded = false;
            _workState.IsRepairMode = false;
            _workState.IsCurrentRepairStation = false;
            _workState.StationRecordId = 0;
            _workState.AllowRepairSequence = 0;
            _workState.RepairCount = 0;
            _workState.RepairTargetStationId = 0;
            _workState.IsRepairProcess = false;
            _workState.ValidatedMaterials.Clear();
            _context.Log("<业务周期> 握手成功，开始新交互周期");
        }

        /// <summary>8000 保存成功：结束业务周期，复位标志并通知 PLC</summary>
        private async Task CompleteBusinessCycleAsync()
        {
            _workState.IsInteractionStarted = false;
            _workState.IsFlowCodeQualified = false;
            _workState.IsRepairMode = false;
            _workState.IsCurrentRepairStation = false;
            _workState.IsBusinessCycleEnded = true;
            _workState.StationRecordId = 0;
            _workState.AllowRepairSequence = 0;
            _workState.RepairCount = 0;
            _workState.RepairTargetStationId = 0;
            _workState.IsRepairProcess = false;
            _workState.ValidatedMaterials.Clear();

            await WriteBusinessCycleEndedAsync(true); // 通知 PLC 业务周期已结束
            _context.Log("<业务周期> 保存完成，周期结束（除 100 外不再处理业务，等待重新握手）");
        }

        /// <summary>写入业务周期结束标志到 PLC</summary>
        private async Task WriteBusinessCycleEndedAsync(bool ended)
        {
            await _context.WriteAsync(DataNameConstants.BusinessCycleEnded, ended);
        }

        /// <summary>回写 SCADA 响应码到 PLC，并同步 DeviceStatus 最近指令</summary>
        private async Task WriteResponseAsync(int responseCode)
        {
            await _context.WriteAsync(DataNameConstants.ResponseCode, responseCode); // 写入响应码地址
            _deviceStatusManager.UpdateStatus(_context.StationId,
               st =>
               {
                   st.LastCommand = responseCode.ToString();
                   st.LastCommandTime = DateTime.Now;
               });
        }

        #endregion

        #region ===================== 100 握手 =====================

        /// <summary>处理 100 握手：开启新周期、刷新型号、复位周期结束标志</summary>
        private async Task<int> HandleHandshakeAsync()
        {
            _context.Log("<握手> 开始");
            BeginInteractionCycle();
            RefreshProductTypeContext();
            await WriteBusinessCycleEndedAsync(false); // 复位周期结束标志

            _deviceStatusManager.UpdateStatus(_context.StationId,
               st => st.RunState = RunState.Running);
            await Task.Delay(5);
            _context.Log("<握手> 完成，IsInteractionStarted=true");
            return (int)SCADAResponseCode.HandshakeSuccess;
        }

        #endregion

        #region ===================== 200 流水码验证 =====================

        /// <summary>处理 200 流水码验证：按工位类型分流正常/返修路径</summary>
        private async Task<(int responseCode, string? dataPayload)> HandleFlowCodeVerifyAsync()
        {
            var (craftOk, craftReason) = await TryEnsureCraftContextAsync();
            if (!craftOk) // 型号/产线上下文未就绪
            {
                _context.Log($"<流水码验证> 中止：{craftReason}", LogLevel.Warning);
                return ((int)SCADAResponseCode.FlowCodeInvalid, null);
            }

            _workState.IsCurrentRepairStation = IsRepairStation();
            if (_workState.IsCurrentRepairStation) // 返修工位走独立验证逻辑
                return await HandleRepairFlowCodeVerifyAsync();

            return await HandleNormalFlowCodeVerifyAsync();
        }

        /// <summary>正常工位 200：调用 FlowCode Handler 验证流水码并执行工位传值</summary>
        private async Task<(int responseCode, string? dataPayload)> HandleNormalFlowCodeVerifyAsync()
        {
            _context.Log("<流水码验证> 开始（正常工位）");

            var flowCode = await ResolveFlowCodeAsync();
            if (string.IsNullOrEmpty(flowCode)) // 流水码为空则直接失败
            {
                _context.Log("<流水码验证> 失败：流水码为空", LogLevel.Warning);
                return ((int)SCADAResponseCode.FlowCodeInvalid, null);
            }

            _context.Log($"<流水码验证> 执行中，流水码={flowCode}，型号Id={_workState.ProductTypeId}，产线Id={_workState.LineId}，调用 FlowCode Handler");
            var flowPayload = new FlowCodeVerifyPayload
            {
                FlowCode = flowCode,
                ProductTypeId = _workState.ProductTypeId,
                LineId = _workState.LineId,
                TrayCode = _workState.TrayCode,
            };

            //本方法不查流水码是否合法
            //用键 "FlowCode" 找已注册的 Handler
            //真正校验在 FlowCodeService.HandleAsync
            var response = await InvokeHandlerAsync(DeviceHandlerKeys.FlowCode, flowPayload);
            if (response == null)
            {
                _context.Log("<流水码验证> 失败：Handler 未响应", LogLevel.Warning);
                return ((int)SCADAResponseCode.GeneralError, null);
            }

            if (response.Success)
            {
                _workState.IsFlowCodeQualified = true;
                if (response.Data is FlowCodeVerifyResult verifyResult)
                {
                    _workState.StationRecordId = verifyResult.StationRecordId;
                    _workState.IsRepairProcess = verifyResult.IsRepair;
                    _workState.RepairCount = verifyResult.RepairCount;
                    _workState.RepairTargetStationId = verifyResult.RepairTargetStationId;
                }
                _context.Log($"<流水码验证> 完成，通过，流水码={flowCode}，过站Id={_workState.StationRecordId}，IsRepairProcess={_workState.IsRepairProcess}，RepairCount={_workState.RepairCount}，IsFlowCodeQualified=true");
                await ApplyStationDataTransferAsync(flowCode); // 200 通过后按配置写入历史传值
                return ((int)SCADAResponseCode.FlowCodeValid, null);
            }

            _workState.IsFlowCodeQualified = false;
            _workState.StationRecordId = 0;
            _context.Log($"<流水码验证> 完成，失败，原因={response.Message}，IsFlowCodeQualified=false", LogLevel.Warning);
            return ((int)SCADAResponseCode.FlowCodeInvalid, null);
        }

        #endregion

        #region ===================== 700 返修 =====================

        /// <summary>返修工位 200：编码规则 + 过站历史查询，写 AllowRepairStation</summary>
        private async Task<(int responseCode, string? dataPayload)> HandleRepairFlowCodeVerifyAsync()
        {
            _context.Log("<返修流水码验证> 开始");

            var flowCode = await ResolveFlowCodeAsync();
            if (string.IsNullOrEmpty(flowCode))
            {
                _context.Log("<返修流水码验证> 失败：流水码为空", LogLevel.Warning);
                return ((int)SCADAResponseCode.FlowCodeInvalid, null);
            }

            var flowResponse = new FlowCodeVerifyPayload()
            {
                FlowCode = flowCode,
                ProductTypeId = _workState.ProductTypeId,
                LineId = _workState.LineId,
                TrayCode = _workState.TrayCode,
                Mode = FlowCodeVerifyMode.RulesOnly,
            };
            var response = await InvokeHandlerAsync(DeviceHandlerKeys.FlowCode, flowResponse);
            if (response == null)
            {
                _context.Log("<返修流水码验证> 失败：Handler 未响应", LogLevel.Warning);
                return ((int)SCADAResponseCode.GeneralError, null);
            }

            if (!response.Success)
            {
                _context.Log("<返修流水码验证> 失败：编码规则不通过", LogLevel.Warning);
                return ((int)SCADAResponseCode.FlowCodeInvalid, null);
            }

            var (queryOk, allowSequence, repairCount, queryMessage) =
                await _repairService.QueryAllowRepairSequenceAsync(
                    flowCode, _workState.ProductTypeId, _workState.LineId);

            if (!queryOk) // 过站历史查询失败
            {
                _context.Log($"<返修流水码验证> 失败：{queryMessage}", LogLevel.Warning);
                return ((int)SCADAResponseCode.FlowCodeInvalid, null);
            }

            if (!_context.HasMapping(DataNameConstants.AllowRepairStation)) // 缺少允许返修工位映射
            {
                _context.Log("<返修流水码验证> 失败：缺少「允许返修工位」地址映射", LogLevel.Warning);
                return ((int)SCADAResponseCode.FlowCodeInvalid, null);
            }

            await WriteAllowRepairSequenceAsync(allowSequence); // 写入允许返修顺序到 PLC

            _workState.IsFlowCodeQualified = true;
            _workState.IsRepairMode = true;
            _workState.StationRecordId = 0;
            _workState.AllowRepairSequence = allowSequence;
            _workState.RepairCount = repairCount;

            _context.Log($"<返修流水码验证> 完成，AllowSeq={allowSequence}，RepairCount={repairCount}，IsFlowCodeQualified=true");
            return ((int)SCADAResponseCode.FlowCodeValid, null);
        }

        /// <summary>返修工位 8000：确认返修目标工位后保存数据</summary>
        private async Task<int> HandleRepairSaveDataAsync()
        {
            _context.Log("<返修保存> 开始");

            var flowCode = GetCurrentFlowCodeFromStatus();
            if (string.IsNullOrEmpty(flowCode))
            {
                _context.Log("<返修保存> 失败：流水码为空", LogLevel.Warning);
                return (int)SCADAResponseCode.DatabaseError;
            }

            if (!_context.HasMapping(DataNameConstants.RequestRepairStation)) // 缺少请求返修工位映射
            {
                _context.Log("<返修保存> 失败：缺少「请求返修工位」地址映射", LogLevel.Warning);
                return (int)SCADAResponseCode.DatabaseError;
            }

            var requestSequence = await ReadRequestRepairSequenceAsync();
            if (requestSequence <= 0 || _workState.AllowRepairSequence <= 0 || requestSequence > _workState.AllowRepairSequence) // 返修顺序校验
            {
                _context.Log($"<返修保存> 失败：请求顺序={requestSequence}，允许顺序={_workState.AllowRepairSequence}", LogLevel.Warning);
                return (int)SCADAResponseCode.DatabaseError;
            }

            var (craftOk, craftReason) = await TryEnsureCraftContextAsync();
            if (!craftOk)
            {
                _context.Log($"<返修保存> 中止：{craftReason}", LogLevel.Warning);
                return (int)SCADAResponseCode.DatabaseError;
            }

            var repairPayload = new RepairConfirmPayload
            {
                FlowCode = flowCode,
                TrayCode = string.Empty,
                ProductTypeId = _workState.ProductTypeId,
                LineId = _workState.LineId,
                TargetRepairSequence = requestSequence,
                AllowRepairSequence = _workState.AllowRepairSequence,
                RepairCount = _workState.RepairCount
            };

            var repairResponse = await InvokeHandlerAsync(DeviceHandlerKeys.Repair, repairPayload);
            if (repairResponse?.Success != true || repairResponse.Data is not RepairConfirmResult confirmResult)
            {
                _context.Log($"<返修保存> 返修确认失败：{repairResponse?.Message ?? "Handler 未响应"}", LogLevel.Warning);
                return (int)SCADAResponseCode.DatabaseError;
            }

            _workState.StationRecordId = confirmResult.StationRecordId;
            _workState.RepairTargetStationId = confirmResult.TargetStationId;
            _workState.RepairCount = confirmResult.RepairCount;
            _context.Log($"<返修保存> 目标工位={confirmResult.TargetStationId}，过站Id={confirmResult.StationRecordId}（保持 Status=0 待加工），调用 DataSave Handler");
            var savePayload = new SaveDataPayload
            {
                FlowCode = flowCode,
                Status = 0,
                ProductTypeId = _workState.ProductTypeId,
                LineId = _workState.LineId,
                StationRecordId = confirmResult.StationRecordId,
                IsRepairSave = true,
                RepairTargetStationId = confirmResult.TargetStationId,
                RepairCount = confirmResult.RepairCount,
                Materials = _workState.ValidatedMaterials
                    .Select(v => new SaveMaterialBindItem
                    {
                        MaterialCode = v.MaterialCode,
                        MaterialName = v.MaterialName
                    })
                    .ToList()
            };

            var saveResponse = await InvokeHandlerAsync(DeviceHandlerKeys.DataSave, savePayload);
            if (saveResponse?.Success == true)
            {
                await TryWriteNextStationCodeAsync(confirmResult.TargetStationId); // 写入下一工位代号
                _context.Log($"<返修保存> 完成，流水码={flowCode}，结束业务周期");
                await CompleteBusinessCycleAsync();
                return (int)SCADAResponseCode.SaveSuccess;
            }

            _context.Log($"<返修保存> 失败，原因={saveResponse?.Message ?? "Handler 未响应"}", LogLevel.Warning);
            return (int)SCADAResponseCode.DatabaseError;
        }

        #endregion

        #region ===================== 500 物料验证 =====================

        /// <summary>处理 500 物料验证：读取物料码/类型，调用 Material Handler</summary>
        private async Task<(int responseCode, string? dataPayload)> HandleMaterialVerifyAsync()
        {
            _context.Log("<物料验证> 开始");

            if (!_context.HasMapping(DataNameConstants.MaterialCode) ||
                !_context.HasMapping(DataNameConstants.MaterialType)) // 地址映射缺失
            {
                _context.Log("<物料验证> 失败：缺少「物料码」或「物料类型」地址映射，终止零部件验证", LogLevel.Warning);
                return ((int)SCADAResponseCode.MaterialInvalid, null);
            }

            var materialCode = await ReadMaterialCodeAsync();
            if (string.IsNullOrEmpty(materialCode))
            {
                _context.Log("<物料验证> 失败：物料码为空", LogLevel.Warning);
                return ((int)SCADAResponseCode.MaterialInvalid, null);
            }

            var materialType = await ReadMaterialTypeAsync();
            if (string.IsNullOrEmpty(materialType))
            {
                _context.Log("<物料验证> 失败：物料类型为空", LogLevel.Warning);
                return ((int)SCADAResponseCode.MaterialInvalid, null);
            }

            var (craftOk, craftReason) = await TryEnsureCraftContextAsync();
            if (!craftOk)
            {
                _context.Log($"<物料验证> 中止：{craftReason}", LogLevel.Warning);
                return ((int)SCADAResponseCode.MaterialInvalid, null);
            }

            var rulesOnly = _workState.IsCurrentRepairStation; // 返修工位仅校验编码规则
            _context.Log($"<物料验证> 执行中，物料码={materialCode}，类型={materialType}，返修仅规则={rulesOnly}，调用 Material Handler");

            var payload = new MaterialVerifyPayload
            {
                MaterialCode = materialCode,
                MaterialType = materialType,
                ProductTypeId = _workState.ProductTypeId,
                LineId = _workState.LineId,
                ValidatedMaterialIds = _workState.ValidatedMaterials.Select(v => v.MaterialId).ToList(),
                RulesOnly = rulesOnly
            };

            // 本方法不判物料规则；键是 "Material"；真正校验在 MaterialService.HandleAsync。
            var response = await InvokeHandlerAsync(DeviceHandlerKeys.Material, payload);
            if (response == null)
            {
                _context.Log("<物料验证> 失败：Handler 未响应", LogLevel.Warning);
                return ((int)SCADAResponseCode.GeneralError, null);
            }

            if (response.Success && response.Data is MaterialVerifyResult verifyResult)
            {
                RecordValidatedMaterial(verifyResult);
                _context.Log($"<物料验证> 完成，通过，物料码={materialCode}，已验证 {_workState.ValidatedMaterials.Count} 种物料");
                return ((int)SCADAResponseCode.MaterialValid, materialCode);
            }

            if (response.Data is MaterialVerifyResult failedResult && failedResult.IsRuleFailure)
            {
                _context.Log($"<物料验证> 完成，编码规则不通过，原因={response.Message}", LogLevel.Warning);
                return ((int)SCADAResponseCode.MaterialRuleNotMatch, null);
            }

            _context.Log($"<物料验证> 完成，失败，原因={response.Message}", LogLevel.Warning);
            return ((int)SCADAResponseCode.MaterialInvalid, null);
        }

        #endregion

        #region ===================== 8000 保存数据 =====================

        /// <summary>处理 8000 保存数据：按工位类型分流正常/返修路径</summary>
        private async Task<int> HandleSaveDataExtendedAsync()
        {
            if (_workState.IsCurrentRepairStation)
                return await HandleRepairSaveDataAsync();

            return await HandleNormalSaveDataAsync();
        }

        /// <summary>正常工位 8000：校验物料就绪后调用 DataSave Handler</summary>
        private async Task<int> HandleNormalSaveDataAsync()
        {
            _context.Log("<保存数据> 开始");

            var flowCode = GetCurrentFlowCodeFromStatus();
            if (string.IsNullOrEmpty(flowCode))
            {
                _context.Log("<保存数据> 失败：流水码为空（DeviceStatus.CurrentFlowCode）", LogLevel.Warning);
                return (int)SCADAResponseCode.DatabaseError;
            }

            if (!_workState.IsFlowCodeQualified) // 流水码未通过 200 验证
            {
                _context.Log("<保存数据> 失败：流水码尚未通过 200 验证", LogLevel.Warning);
                return (int)SCADAResponseCode.DatabaseError;
            }

            if (_workState.StationRecordId <= 0) // 过站记录 Id 无效
            {
                _context.Log("<保存数据> 失败：过站记录 Id 无效", LogLevel.Warning);
                return (int)SCADAResponseCode.DatabaseError;
            }

            var (craftOk, craftReason) = await TryEnsureCraftContextAsync();
            if (!craftOk)
            {
                _context.Log($"<保存数据> 中止：{craftReason}", LogLevel.Warning);
                return (int)SCADAResponseCode.DatabaseError;
            }

            var (materialReady, materialReason) = TryEnsureMaterialsReadyForSave();
            if (!materialReady) // 工位物料未全部验证
            {
                _context.Log($"<保存数据> 失败：{materialReason}", LogLevel.Warning);
                return (int)SCADAResponseCode.DatabaseError;
            }

            _context.Log($"<保存数据> 执行中，流水码={flowCode}，型号Id={_workState.ProductTypeId}，过站Id={_workState.StationRecordId}，读取过站判定...");
            var status = await ReadSaveStatusAsync();

            _context.Log($"<保存数据> 执行中，过站状态={status}，已验证物料 {_workState.ValidatedMaterials.Count} 种，调用 DataSave Handler");

            var payload = new SaveDataPayload
            {
                FlowCode = flowCode,
                Status = status,
                ProductTypeId = _workState.ProductTypeId,
                LineId = _workState.LineId,
                StationRecordId = _workState.StationRecordId,
                TrayCode = _workState.TrayCode,
                Materials = _workState.ValidatedMaterials
                    .Select(v => new SaveMaterialBindItem
                    {
                        MaterialCode = v.MaterialCode,
                        MaterialName = v.MaterialName
                    })
                    .ToList(),
                IsRepairProcess = _workState.IsRepairProcess,
                RepairCount = _workState.RepairCount
            };

            // 键是 "DataSave"，和 DataSaveService.DataType 相同。
            //真正写库在 DataSaveService.HandleAsync。
            //本方法只做：检查物料是否齐、ReadSaveStatusAsync 读合格/不合格、组 SaveDataPayload、看成功后结束周期。
            var response = await InvokeHandlerAsync(DeviceHandlerKeys.DataSave, payload);
            if (response?.Success == true)
            {
                await TryWriteNextStationCodeAsync(ResolveNextStationIdFromProcessFlow()); // 写入工艺流程下一工位
                _context.Log($"<保存数据> 完成，流水码={flowCode}，结束业务周期");
                await CompleteBusinessCycleAsync();
                return (int)SCADAResponseCode.SaveSuccess;
            }

            _context.Log($"<保存数据> 失败，原因={response?.Message ?? "Handler 未响应"}", LogLevel.Warning);
            return (int)SCADAResponseCode.DatabaseError;
        }

        #endregion

        #region ===================== 900 电机码获取 =====================

        /// <summary>900：按当前流水码取最新绑定电机码，并下发平台代号</summary>
        private async Task<int> HandleMotorCodeFetchAsync()
        {
            var flowCode = GetCurrentFlowCodeFromStatus();
            if (string.IsNullOrWhiteSpace(flowCode))
            {
                _context.Log("<900> 失败：当前流水码为空", LogLevel.Warning);
                return (int)SCADAResponseCode.GeneralError;
            }
            _context.Log($"<900> 开始，流水码={flowCode}，型号Id={_workState.ProductTypeId}");
            var result = await _motorCodeDispatch.GetDispatchDataAsync(flowCode, _workState.ProductTypeId);
            if (!result.Success)
            {
                _context.Log($"<900> 失败：{result.ErrorMessage}", LogLevel.Warning);
                if (result.ErrorMessage.Contains("未绑定电机码", StringComparison.Ordinal))
                    return (int)SCADAResponseCode.MotorCodeNotFound;
                if (result.ErrorMessage.Contains("平台代号", StringComparison.Ordinal))
                    return (int)SCADAResponseCode.PlatformCodeNotConfigured;
                return (int)SCADAResponseCode.GeneralError;
            }
            await _context.WriteAsync(DataNameConstants.MotorCode, result.MotorCode); // 写入电机码
            await _context.WriteAsync(DataNameConstants.PlatformCode, result.PlatformCode); // 写入平台代号
            _context.Log($"<900> 成功，Motor={result.MotorCode}，Platform={result.PlatformCode}");
            return (int)SCADAResponseCode.MotorCodeFetchSuccess;
        }

        #endregion

        #region ===================== 辅助方法 =====================

        #region --------------------- 工位与返修判定 ---------------------

        /// <summary>判断当前工位是否为返修工位（craft_ProcessInfo.IsRepairStation）</summary>
        private bool IsRepairStation()
        {
            var processes = _cacheService.GetData<List<craft_ProcessInfo>>();
            if (processes == null || processes.Count == 0)
                return false;

            return processes.Any(p =>
                p.StationId == _context.StationId &&
                p.TypeId == _workState.ProductTypeId &&
                p.LineId == _workState.LineId &&
                p.IsRepairStation &&
                p.IsEnable);
        }

        /// <summary>写入允许返修工位顺序到 PLC</summary>
        private async Task WriteAllowRepairSequenceAsync(int allowSequence)
        {
            await _context.WriteAsync(DataNameConstants.AllowRepairStation, allowSequence);
        }

        /// <summary>读取 PLC 请求返修工位顺序</summary>
        private async Task<int> ReadRequestRepairSequenceAsync()
        {
            var result = await _context.ReadAsync(DataNameConstants.RequestRepairStation);
            if (result == null) return 0;
            return int.TryParse(result.ToString(), out var sequence) ? sequence : 0;
        }

        #endregion

        #region --------------------- 下一工位下发 ---------------------

        /// <summary>8000 保存成功后，若配置了 NextStation 映射则写入下一工位代号</summary>
        private async Task TryWriteNextStationCodeAsync(int nextStationId)
        {
            if (!_context.HasMapping(DataNameConstants.NextStation))
                return;

            if (nextStationId <= 0)
            {
                _context.Log("<下一工位> 工艺流程无下一工位，跳过下发", LogLevel.Info);
                return;
            }

            var stationCode = ResolveStationCode(nextStationId);
            if (string.IsNullOrEmpty(stationCode))
            {
                _context.Log($"<下一工位> 工位 Id={nextStationId} 未找到代号，跳过下发", LogLevel.Warning);
                return;
            }

            var ok = await _context.WriteAsync(DataNameConstants.NextStation, stationCode); // 写入下一工位代号
            if (ok)
                _context.Log($"<下一工位> 已写入代号={stationCode}（工位Id={nextStationId}）");
            else
                _context.Log($"<下一工位> 写入失败，代号={stationCode}（工位Id={nextStationId}）", LogLevel.Warning);
        }

        /// <summary>按工艺流程 Sequence 取当前工位下一工位 Id</summary>
        private int ResolveNextStationIdFromProcessFlow()
        {
            var allProcess = _cacheService.GetData<List<craft_ProcessInfo>>();
            if (allProcess == null || allProcess.Count == 0)
                return 0;

            var flow = allProcess
                .Where(p =>
                    p.TypeId == _workState.ProductTypeId &&
                    p.LineId == _workState.LineId &&
                    p.IsEnable)
                .OrderBy(p => p.Sequence)
                .ToList();

            var currentIndex = flow.FindIndex(p => p.StationId == _context.StationId);
            if (currentIndex < 0 || currentIndex + 1 >= flow.Count)
                return 0;

            return flow[currentIndex + 1].StationId;
        }

        /// <summary>按工位 Id 从 craft_StationInfo 解析工位代号</summary>
        private string ResolveStationCode(int stationId)
        {
            var stations = _cacheService.GetData<List<craft_StationInfo>>();
            if (stations == null || stations.Count == 0)
                return string.Empty;

            return stations.FirstOrDefault(s => s.Id == stationId)?.Code?.Trim() ?? string.Empty;
        }

        #endregion

        #region --------------------- 流水码与型号 ---------------------

        /// <summary>从 DeviceStatus 获取当前流水码（空或 UI 占位符视为无效）</summary>
        private string GetCurrentFlowCodeFromStatus()
        {
            var flowCode = _deviceStatusManager.GetStatus(_context.StationId)?.CurrentFlowCode;
            if (string.IsNullOrWhiteSpace(flowCode) || flowCode == "--")
                return string.Empty;
            return flowCode?.Trim()?? "" ;
        }

        /// <summary>优先 Status，空则主动读 PLC 并回写 Status</summary>
        private async Task<string> ResolveFlowCodeAsync()
        {
            var fromStatus = GetCurrentFlowCodeFromStatus();
            if (!string.IsNullOrEmpty(fromStatus))
                return fromStatus;

            if (_context.HasMapping(DataNameConstants.CurrentFlowCode))
            {
                var raw = await _context.ReadAsync(DataNameConstants.CurrentFlowCode);
                var code = NormalizeFlowCode(raw?.ToString());
                if (!string.IsNullOrEmpty(code))
                {
                    CurrentFlowCodeAsync(code);
                    return code;
                }
            }

            if (_context.HasMapping(DataNameConstants.DataPayload))
            {
                var raw = await _context.ReadAsync(DataNameConstants.DataPayload);
                return NormalizeFlowCode(raw?.ToString());
            }

            return string.Empty;
        }

        /// <summary>规范化流水码：空值或占位符返回空字符串</summary>
        private static string NormalizeFlowCode(string? value) =>
            string.IsNullOrWhiteSpace(value) || value == "--" ? string.Empty : value?.Trim() ?? "";

        /// <summary>刷新型号上下文：软件下发从缓存取；PLC 决定则依赖采集结果</summary>
        private void RefreshProductTypeContext()
        {
            _workState.LineId = _context.StationInfo.LineId;

            if (_context.IsIssueModel)
            {
                if (!_cacheService.HasData<craft_TypeInfo>())
                {
                    _workState.ProductTypeId = 0;
                    _workState.ProductTypeName = string.Empty;
                    _context.Log("<型号> 软件下发型号，但缓存中无已发布型号", LogLevel.Warning);
                    return;
                }

                var type = _cacheService.GetData<craft_TypeInfo>();
                _workState.ProductTypeId = type.Id;
                _workState.ProductTypeName = type.Name;
                _deviceStatusManager.UpdateStatus(_context.StationId,
                    st => st.ProductTypeName = type.Name);
                _context.Log($"<型号> 软件下发型号，Id={type.Id}，名称={type.Name}，代号={type.IssueCode}");
                return;
            }

            if (!_context.HasProductTypeReadMapping)
            {
                _workState.ProductTypeId = 0;
                _workState.ProductTypeName = string.Empty;
                _context.Log("<型号> 型号由 PLC 决定，但未配置「读取型号」地址映射，型号 Id 为空", LogLevel.Warning);
                return;
            }

            _context.Log("<型号> 型号由 PLC 决定，等待「读取型号」采集更新");
        }

        /// <summary>校验工位工艺上下文是否可用于业务（型号 Id、产线 Id）</summary>
        private async Task<(bool ok, string reason)> TryEnsureCraftContextAsync()
        {
            if (_workState.LineId <= 0)
                _workState.LineId = _context.StationInfo.LineId;

            if (_workState.LineId <= 0)
                return (false, "产线 Id 未就绪");

            if (_context.IsIssueModel)
            {
                RefreshProductTypeContext();
            }
            else if (!_context.HasProductTypeReadMapping)
            {
                _workState.ProductTypeId = 0;
                _workState.ProductTypeName = string.Empty;
                return (false, "型号由 PLC 决定，但未配置「读取型号」地址映射");
            }
            else
            {
                await RefreshProductTypeFromPlcAsync();
            }

            if (_workState.ProductTypeId <= 0)
            {
                var reason = _context.IsIssueModel
                    ? "软件下发型号但未获取到有效型号 Id"
                    : "PLC 型号为空或未匹配到型号档案";
                return (false, reason);
            }

            return (true, string.Empty);
        }

        /// <summary>PLC 决定型号时，主动读取「读取型号」并匹配 craft_TypeInfo</summary>
        private async Task RefreshProductTypeFromPlcAsync()
        {
            var raw = await _context.ReadAsync(DataNameConstants.ReadProductType);
            if (raw == null)
            {
                _workState.ProductTypeId = 0;
                _workState.ProductTypeName = string.Empty;
                _context.Log("<型号> 主动读取「读取型号」为空", LogLevel.Warning);
                return;
            }

            ProductTypeAsync(raw.ToString() ?? "--");
        }

        /// <summary>将 PLC 读取的型号文本匹配为 craft_TypeInfo.Id（支持名称或 IssueCode）</summary>
        private int ResolveProductTypeId(string rawValue)
        {
            var types = _cacheService.GetData<List<craft_TypeInfo>>();
            if (types == null || types.Count == 0)
                return 0;

            if (int.TryParse(rawValue, out var issueCode))
            {
                var byCode = types.FirstOrDefault(t => t.IssueCode == issueCode);
                if (byCode != null)
                    return byCode.Id;
            }

            var byName = types.FirstOrDefault(t =>
                string.Equals(t.Name, rawValue, StringComparison.OrdinalIgnoreCase));
            return byName?.Id ?? 0;
        }

        #endregion

        #region --------------------- Handler 调用与 PLC 读写 ---------------------

        /// <summary>调用已注册的业务处理器</summary>
        private async Task<BusinessResponse?> InvokeHandlerAsync(string handlerKey, object? payload)
        {
            if (!_handlers.TryGetValue(handlerKey, out var handler))
            {
                _context.Log($"<InvokeHandlerAsync> 失败：未注册 {handlerKey}", LogLevel.Warning);
                return null;
            }

            _context.Log($"<InvokeHandlerAsync> 开始调用 {handlerKey}");

            var message = new DeviceDataMessage
            {
                StationId = _context.StationId,
                DataType = handlerKey,
                Data = payload 
            };

            var response = await handler.HandleAsync(message, _context);
            _context.Log($"<InvokeHandlerAsync> 完成 {handlerKey}，Success={response.Success}，{response.Message}");
            return response;
        }

        /// <summary>读取过站判定：1=合格，2=不合格（见 SaveStatusHelper）</summary>
        private Task<int> ReadSaveStatusAsync() => SaveStatusHelper.ReadAsync(_context);

        /// <summary>从 PLC 读取物料码</summary>
        private async Task<string> ReadMaterialCodeAsync()
        {
            var result = await _context.ReadAsync(DataNameConstants.MaterialCode);
            if (result == null) return string.Empty;
            return result.ToString()?.Trim() ?? string.Empty;
        }

        /// <summary>从 PLC 读取物料类型</summary>
        private async Task<string> ReadMaterialTypeAsync()
        {
            var result = await _context.ReadAsync(DataNameConstants.MaterialType);
            if (result == null) return string.Empty;
            return result.ToString()?.Trim() ?? string.Empty;
        }

        /// <summary>500 验证通过后回写物料码到 PLC</summary>
        private async Task WriteMaterialCodeAsync(string data)
        {
            await _context.WriteAsync(DataNameConstants.MaterialCode, data);
        }

        /// <summary>200 流水码验证通过后：按 craft_StationDataTransfer 配置写入历史传值</summary>
        private async Task ApplyStationDataTransferAsync(string flowCode)
        {
            var items = await _stationDataTransfer.GetTransferWritesAsync(
                _context.StationId,
                _workState.ProductTypeId,
                _workState.LineId,
                flowCode);
            if (items.Count == 0)
                return;
            _context.Log($"<工位传值> 开始写入 PLC，共 {items.Count} 项，流水码={flowCode}");
            foreach (var item in items)
            {
                var value = ConvertTransferValue(item.DataValue, item.DataType);
                var ok = await _context.WriteAsync(item.TargetAddress, item.DataType, value, item.DataLength);
                if (ok)
                {
                    _context.Log(
                        $"<工位传值> 成功：{item.RequestDataName}={item.DataValue}，源工位Id={item.SourceStationId} → {item.TargetAddress} ({item.DataType})");
                }
                else
                {
                    _context.Log(
                        $"<工位传值> 写入失败：{item.RequestDataName} → {item.TargetAddress} ({item.DataType})",
                        LogLevel.Warning);
                }
            }
            _context.Log("<工位传值> 完成");
        }

        /// <summary>将历史表字符串按 PLC 数据类型转换为写入值</summary>
        private static object ConvertTransferValue(string dataValue, string dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
                return dataValue;
            return dataType.Trim() switch
            {
                "Bool" or "bool" or "Boolean" =>
                    bool.TryParse(dataValue, out var b) ? b
                    : dataValue is "1" or "OK" or "ok",
                "Int" or "int" or "Int16" or "Int32" or "Word" =>
                    int.TryParse(dataValue, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : 0,
                "Real" or "Float" or "Double" or "float" or "double" =>
                    double.TryParse(dataValue, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.0,
                _ => dataValue
            };
        }

        #endregion

        #region --------------------- 物料记录 ---------------------

        /// <summary>记录已验证物料，同 Id 去重后追加</summary>
        private void RecordValidatedMaterial(MaterialVerifyResult result)
        {
            var existing = _workState.ValidatedMaterials
                .FirstOrDefault(v => v.MaterialId == result.MaterialId);
            if (existing != null)
                _workState.ValidatedMaterials.Remove(existing);

            _workState.ValidatedMaterials.Add(new ValidatedMaterialItem
            {
                MaterialId = result.MaterialId,
                MaterialCode = result.MaterialCode,
                MaterialName = result.MaterialName,
                Sequence = result.Sequence
            });
        }

        /// <summary>校验工位所需物料是否均已通过 500 验证</summary>
        private (bool ok, string reason) TryEnsureMaterialsReadyForSave()
        {
            if (!_cacheService.HasData<List<material_Station>>())
                return (true, string.Empty);

            var required = _cacheService.GetData<List<material_Station>>()
             .Where(r => r.StationId == _context.StationId
                 && r.TypeId == _workState.ProductTypeId
                 && r.LineId == _workState.LineId);
            if (!required.Any())
                return (true, string.Empty);

            var validatedIds = _workState.ValidatedMaterials.Select(v => v.MaterialId).ToHashSet();
            var missing = required.Where(r => !validatedIds.Contains(r.MaterialId)).ToList();
            if (missing.Count == 0)
                return (true, string.Empty);

            return (false, $"尚有 {missing.Count} 种工位物料未完成 500 验证");
        }

        /// <summary>将 object 值转为布尔（支持 bool / "True" / "1"）</summary>
        private static bool IsTruthy(object? value) =>
            value is bool b ? b : value?.ToString() is "True" or "true" or "1";

        #endregion

        #endregion
    }

}
