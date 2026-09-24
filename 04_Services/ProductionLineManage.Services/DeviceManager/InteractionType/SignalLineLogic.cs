using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.DeviceManager.InteractionType
{
    /// <summary>
    /// 信号型交互逻辑：处理 Bool 触发型 PLC 信号（开始工作、流水码完成、物料码完成、保存完成等）。
    /// </summary>
    public class SignalLineLogic : IInteractionType
    {
        #region ===================== 字段与构造 =====================

        private readonly IDeviceTaskContext _context;
        private readonly IDeviceStatusManager _deviceStatusManager;
        private readonly IReadOnlyDictionary<string, IDeviceDataHandler> _handlers;
        private readonly IDataCacheService _cacheService;
        private readonly IMaterialService _materialService;

        /// <summary>本实例私有的交互编排状态，不交给 Mediator 管理</summary>
        private readonly StationWorkState _workState;

        /// <summary>工位 Id</summary>
        public int StationId => _context.StationId;

        /// <summary>交互类型标识：Signal</summary>
        public string LogicType => InteractionTypeConstants.Signal;

        /// <summary>注入工位上下文、状态管理器及业务服务</summary>
        public SignalLineLogic(
            IDeviceTaskContext context,
            IDeviceStatusManager statusManager,
            IReadOnlyDictionary<string, IDeviceDataHandler> handlers,
            IDataCacheService cacheService,
            IMaterialService materialService)
        {
            _context = context;
            _deviceStatusManager = statusManager;
            _handlers = handlers;
            _cacheService = cacheService;
            _materialService = materialService;
            _workState = new StationWorkState
            {
                StationId = context.StationId,
                LineId = context.StationInfo.LineId
            };
        }

        #endregion

        #region ===================== 消息分发 =====================

        /// <summary>业务中介入口：按信号数据名称分发到对应处理方法</summary>
        public async Task HandleMessageAsync(DeviceDataMessage message, IDeviceTaskContext context)
        {
            try
            {
                switch (message.DataType)
                {
                    case DataNameConstants.RunState:
                        UpdateRunState(Convert.ToInt32(message.Data ?? 0)); // 更新运行状态
                        break;

                    case DataNameConstants.CurrentFlowCode:
                        UpdateFlowCode(message.Data?.ToString() ?? string.Empty); // 同步流水码
                        break;

                    case DataNameConstants.StartWork:
                        await HandleStartWorkAsync(message.Data); // 开始工作信号
                        break;

                    case DataNameConstants.FlowCodeDone:
                        await HandleFlowCodeDoneAsync(message.Data); // 流水码完成信号
                        break;

                    case DataNameConstants.MaterialCodeDone:
                        await HandleMaterialCodeDoneAsync(message.Data); // 物料码完成信号
                        break;

                    case DataNameConstants.SaveDone:
                        await HandleSaveDataSignalAsync(message.Data); // 保存完成信号
                        break;

                    default:
                        _context.Log($"{message.DataType} 该信号名称未设置处理方式", LogLevel.Warning);
                        break;
                }
            }
            catch (Exception ex)
            {
                _context.Log($"信号处理异常，数据名称：{message.DataType}，值：{message.Data}，{ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>将 PLC 运行状态码映射为 RunState 枚举并写入 DeviceStatus</summary>
        private void UpdateRunState(int state)
        {
            _deviceStatusManager.UpdateStatus(_context.StationId, st =>
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
        private void UpdateFlowCode(string code)
        {
            _deviceStatusManager.UpdateStatus(_context.StationId, st => st.CurrentFlowCode = code);
        }

        #endregion

        #region ===================== 信号处理 =====================

        /// <summary>处理「开始工作」信号：重置本周期流水码/物料验证状态</summary>
        private async Task HandleStartWorkAsync(object? value)
        {
            if (!ConvertToBool(value)) // 信号为 false 时释放处理锁
            {
                _workState.IsProcessing = false;
                return;
            }

            _deviceStatusManager.UpdateStatus(_context.StationId, st =>
            {
                st.DeviceLastCommand = DataNameConstants.StartWork;
                st.DeviceLastCommandTime = DateTime.Now;
            });
            _context.Log("<开始工作> 收到信号，重置本周期处理标记");
            _workState.IsFlowCodeQualified = false;
            _workState.StationRecordId = 0;
            _workState.ValidatedMaterials.Clear();
            await Task.CompletedTask;
        }

        /// <summary>处理「流水码完成」信号：验证流水码并回写通过/不通过</summary>
        private async Task HandleFlowCodeDoneAsync(object? value)
        {
            if (!ConvertToBool(value) || _workState.IsProcessing) // 非 true 或正在处理则跳过
                return;

            _workState.IsProcessing = true;
            try
            {
                _deviceStatusManager.UpdateStatus(_context.StationId, st =>
                {
                    st.DeviceLastCommand = DataNameConstants.FlowCodeDone;
                    st.DeviceLastCommandTime = DateTime.Now;
                });

                _context.Log("<流水码信号> 开始");
                var flowCode = await ReadFlowCodeAsync();
                if (string.IsNullOrWhiteSpace(flowCode) || flowCode == "--") // 流水码为空
                {
                    _context.Log("<流水码信号> 失败：流水码为空", LogLevel.Warning);
                    await WriteFlowCodeResultAsync(false); // 回写不通过
                    return;
                }

                var (craftOk, craftReason) = await TryEnsureCraftContextAsync();
                if (!craftOk) // 型号/产线上下文未就绪
                {
                    _context.Log($"<流水码信号> 中止：{craftReason}", LogLevel.Warning);
                    await WriteFlowCodeResultAsync(false);
                    return;
                }

                _context.Log($"<流水码信号> 执行中，流水码={flowCode}，型号Id={_workState.ProductTypeId}，调用 FlowCode Handler");
                var flowPayload = new FlowCodeVerifyPayload
                {
                    FlowCode = flowCode,
                    ProductTypeId = _workState.ProductTypeId,
                    LineId = _workState.LineId
                };
                var response = await InvokeHandlerAsync(DeviceHandlerKeys.FlowCode, flowPayload);
                var success = response?.Success == true;
                if (success && response?.Data is FlowCodeVerifyResult verifyResult)
                {
                    _workState.IsFlowCodeQualified = true;
                    _workState.StationRecordId = verifyResult.StationRecordId;
                    _workState.IsRepairProcess = verifyResult.IsRepair;
                    _workState.RepairCount = verifyResult.RepairCount;
                    _workState.RepairTargetStationId = verifyResult.RepairTargetStationId;
                }
                else
                {
                    _workState.IsFlowCodeQualified = false;
                    _workState.StationRecordId = 0;
                }

                _context.Log($"<流水码信号> 完成，结果={success}，过站Id={_workState.StationRecordId}");
                await WriteFlowCodeResultAsync(success); // 回写通过/不通过到 PLC
            }
            finally
            {
                _workState.IsProcessing = false;
            }
        }

        /// <summary>处理「物料码完成」信号：验证物料并回写通过/不通过</summary>
        private async Task HandleMaterialCodeDoneAsync(object? value)
        {
            if (!ConvertToBool(value) || _workState.IsProcessing) // 非 true 或正在处理则跳过
                return;

            _workState.IsProcessing = true;
            try
            {
                _deviceStatusManager.UpdateStatus(_context.StationId, st =>
                {
                    st.DeviceLastCommand = DataNameConstants.MaterialCodeDone;
                    st.DeviceLastCommandTime = DateTime.Now;
                });

                _context.Log("<物料码信号> 开始");

                if (!_context.HasMapping(DataNameConstants.MaterialCode) ||
                    !_context.HasMapping(DataNameConstants.MaterialType)) // 地址映射缺失
                {
                    _context.Log("<物料码信号> 失败：缺少「物料码」或「物料类型」地址映射，终止零部件验证", LogLevel.Warning);
                    await WriteMaterialResultAsync(false); // 回写不通过
                    return;
                }

                var materialCode = await ReadStringAsync(DataNameConstants.MaterialCode);
                var materialType = await ReadStringAsync(DataNameConstants.MaterialType);
                if (string.IsNullOrWhiteSpace(materialCode))
                {
                    _context.Log("<物料码信号> 失败：物料码为空", LogLevel.Warning);
                    await WriteMaterialResultAsync(false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(materialType))
                {
                    _context.Log("<物料码信号> 失败：物料类型为空", LogLevel.Warning);
                    await WriteMaterialResultAsync(false);
                    return;
                }

                var (craftOk, craftReason) = await TryEnsureCraftContextAsync();
                if (!craftOk)
                {
                    _context.Log($"<物料码信号> 中止：{craftReason}", LogLevel.Warning);
                    await WriteMaterialResultAsync(false);
                    return;
                }

                _context.Log($"<物料码信号> 执行中，物料码={materialCode}，类型={materialType}，调用 Material Handler");

                var payload = new MaterialVerifyPayload
                {
                    MaterialCode = materialCode.Trim(),
                    MaterialType = materialType.Trim(),
                    ProductTypeId = _workState.ProductTypeId,
                    LineId = _workState.LineId,
                    ValidatedMaterialIds = _workState.ValidatedMaterials.Select(v => v.MaterialId).ToList()
                };

                var response = await InvokeHandlerAsync(DeviceHandlerKeys.Material, payload);
                var success = response?.Success == true;
                if (success && response?.Data is MaterialVerifyResult verifyResult)
                {
                    RecordValidatedMaterial(verifyResult);
                    _context.Log($"<物料码信号> 完成，通过，已验证 {_workState.ValidatedMaterials.Count} 种物料");
                }
                else
                {
                    _context.Log($"<物料码信号> 完成，失败，原因={response?.Message ?? "Handler 未响应"}", LogLevel.Warning);
                }

                await WriteMaterialResultAsync(success); // 回写通过/不通过到 PLC
            }
            finally
            {
                _workState.IsProcessing = false;
            }
        }

        /// <summary>处理「保存完成」信号：校验前置条件后保存数据并回写结果</summary>
        private async Task HandleSaveDataSignalAsync(object? value)
        {
            if (!ConvertToBool(value) || _workState.IsProcessing) // 非 true 或正在处理则跳过
                return;

            _workState.IsProcessing = true;
            try
            {
                _deviceStatusManager.UpdateStatus(_context.StationId, st =>
                {
                    st.DeviceLastCommand = DataNameConstants.SaveData;
                    st.DeviceLastCommandTime = DateTime.Now;
                });

                _context.Log("<保存信号> 开始");
                var flowCode = await ReadFlowCodeAsync();
                if (string.IsNullOrWhiteSpace(flowCode) || flowCode == "--") // 流水码为空
                {
                    _context.Log("<保存信号> 失败：流水码为空", LogLevel.Warning);
                    await _context.WriteAsync(DataNameConstants.SaveDone, false); // 回写保存失败
                    return;
                }

                if (!_workState.IsFlowCodeQualified || _workState.StationRecordId <= 0) // 流水码未验证或过站 Id 无效
                {
                    _context.Log("<保存信号> 失败：流水码尚未通过验证或过站 Id 无效", LogLevel.Warning);
                    await _context.WriteAsync(DataNameConstants.SaveDone, false);
                    return;
                }

                var (craftOk, craftReason) = await TryEnsureCraftContextAsync();
                if (!craftOk)
                {
                    _context.Log($"<保存信号> 中止：{craftReason}", LogLevel.Warning);
                    await _context.WriteAsync(DataNameConstants.SaveDone, false);
                    return;
                }

                var (materialReady, materialReason) = await TryEnsureMaterialsReadyForSaveAsync();
                if (!materialReady) // 工位物料未全部验证
                {
                    _context.Log($"<保存信号> 失败：{materialReason}", LogLevel.Warning);
                    await _context.WriteAsync(DataNameConstants.SaveDone, false);
                    return;
                }

                var status = await ReadSaveStatusAsync();

                var payload = new SaveDataPayload
                {
                    FlowCode = flowCode,
                    Status = status,
                    ProductTypeId = _workState.ProductTypeId,
                    LineId = _workState.LineId,
                    StationRecordId = _workState.StationRecordId,
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

                _context.Log($"<保存信号> 执行中，流水码={flowCode}，过站Id={_workState.StationRecordId}，已验证物料 {_workState.ValidatedMaterials.Count} 种，调用 DataSave Handler");
                var response = await InvokeHandlerAsync(DeviceHandlerKeys.DataSave, payload);
                var success = response?.Success == true;
                if (success)
                {
                    _workState.IsFlowCodeQualified = false;
                    _workState.StationRecordId = 0;
                    _workState.ValidatedMaterials.Clear(); // 保存成功后重置本周期状态
                }

                _context.Log($"<保存信号> 完成，结果={success}，回写保存完成信号");
                await _context.WriteAsync(DataNameConstants.SaveDone, success); // 回写保存结果到 PLC
            }
            finally
            {
                _workState.IsProcessing = false;
            }
        }

        #endregion

        #region ===================== PLC 响应回写 =====================

        /// <summary>回写流水码验证结果：通过写 FlowCodePass，不通过写 FlowCodeNotPass</summary>
        private async Task WriteFlowCodeResultAsync(bool success)
        {
            await _context.WriteAsync(DataNameConstants.FlowCodePass, success);
            await _context.WriteAsync(DataNameConstants.FlowCodeNotPass, !success);
        }

        /// <summary>回写物料验证结果：通过写 MaterialPass，不通过写 MaterialNotPass</summary>
        private async Task WriteMaterialResultAsync(bool success)
        {
            await _context.WriteAsync(DataNameConstants.MaterialPass, success);
            await _context.WriteAsync(DataNameConstants.MaterialNotPass, !success);
        }

        #endregion

        #region ===================== 辅助方法 =====================

        #region --------------------- Handler 调用 ---------------------

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
            _context.Log($"<InvokeHandlerAsync> 完成 {handlerKey}，Success={response.Success}");
            return response;
        }

        #endregion

        #region --------------------- PLC 读取 ---------------------

        /// <summary>读取流水码：优先 CurrentFlowCode，否则读 DataPayload</summary>
        private async Task<string> ReadFlowCodeAsync()
        {
            var fromMapping = await ReadStringAsync(DataNameConstants.CurrentFlowCode);
            if (!string.IsNullOrWhiteSpace(fromMapping) && fromMapping != "--")
                return fromMapping;

            return await ReadStringAsync(DataNameConstants.DataPayload);
        }

        /// <summary>从 PLC 读取指定数据名称的字符串值</summary>
        private async Task<string> ReadStringAsync(string dataName)
        {
            var result = await _context.ReadAsync(dataName);
            return result?.ToString() ?? string.Empty;
        }

        /// <summary>读取过站判定：1=合格，2=不合格（见 SaveStatusHelper）</summary>
        private Task<int> ReadSaveStatusAsync() => SaveStatusHelper.ReadAsync(_context);

        #endregion

        #region --------------------- 型号上下文 ---------------------

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
                return;
            }

            if (!_context.HasProductTypeReadMapping)
            {
                _workState.ProductTypeId = 0;
                _workState.ProductTypeName = string.Empty;
                _context.Log("<型号> 型号由 PLC 决定，但未配置「读取型号」地址映射", LogLevel.Warning);
            }
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

            ApplyPlcProductType(raw.ToString() ?? "--");
        }

        /// <summary>解析 PLC 读取的型号文本并更新工作态</summary>
        private void ApplyPlcProductType(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "--") // 型号为空或占位符
            {
                _workState.ProductTypeName = string.Empty;
                _workState.ProductTypeId = 0;
                return;
            }

            var trimmed = value.Trim();
            _workState.ProductTypeName = trimmed;
            _workState.ProductTypeId = ResolveProductTypeId(trimmed);

            if (_workState.ProductTypeId <= 0)
                _context.Log($"<型号> PLC 读取型号「{trimmed}」未匹配到型号档案", LogLevel.Warning);
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

        /// <summary>校验工位所需物料是否均已通过验证</summary>
        private async Task<(bool ok, string reason)> TryEnsureMaterialsReadyForSaveAsync()
        {
            var required = await _materialService.GetStationMaterialsAsync(
                _context.StationId, _workState.ProductTypeId, _workState.LineId);
            if (required.Count == 0)
                return (true, string.Empty);

            var validatedIds = _workState.ValidatedMaterials.Select(v => v.MaterialId).ToHashSet();
            var missing = required.Where(r => !validatedIds.Contains(r.MaterialId)).ToList();
            if (missing.Count == 0)
                return (true, string.Empty);

            return (false, $"尚有 {missing.Count} 种工位物料未完成验证");
        }

        #endregion

        #region --------------------- 工具方法 ---------------------

        /// <summary>将 object 值转为布尔（支持 bool / "True" / "1"）</summary>
        private static bool ConvertToBool(object? value)
        {
            if (value is bool b)
                return b;

            return value?.ToString() is "True" or "true" or "1";
        }

        #endregion

        #endregion
    }
}
