using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Helpers;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;

namespace DeviceModule.ViewModels.Dialog
{
    /// <summary>
    /// 新增地址映射弹窗视图模型：选择产线/工位并录入 PLC 地址映射，写入 device_AddressMapping。
    /// </summary>
    public class AddAddressMappingViewModel : BindableBase, IDialogAware
    {
        #region ===================== 私有字段 =====================

        private readonly IRepository<device_AddressMapping> _mappingRepo; // 地址映射仓储
        private readonly IDataCacheService _cacheService;

        private List<craft_LineInfo> _allLines = new();
        private List<craft_StationInfo> _allStations = new();
        private List<device_AddressMapping> _allMappings = new();

        private List<craft_LineInfo> _lines = new();
        private List<craft_StationInfo> _stations = new();

        private int _lineSelectIndex = -1;
        private craft_LineInfo _lineSelectItem = new();
        private int _stationSelectIndex = -1;
        private craft_StationInfo _stationSelectItem = new();
        private bool _isStationEnabled;

        private List<string> _dataNames = new();
        private string _dataNameSelectedItem = string.Empty;
        private string _dataAddress = string.Empty;
        private List<string> _dataTypes = new();
        private string _dataTypeSelectedItem = string.Empty;
        private int _dataTypeIndex = -1;
        private string _stationProtocolType = string.Empty;
        private string _interactionType = string.Empty; 
        private bool _isDataTypeEnabled;
        private int _dataLen = 1;
        private List<string> _dataDirections = DeviceDataTypeConstants.DataDirection.ToList();
        private string _dataDirectionSelectedItem = DeviceDataTypeConstants.DataDirection[0];
        private int _dataDirectionIndex;
        private bool _isEnabled = true;
        private string _remarks = string.Empty;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入仓储与缓存，初始化命令 </summary>
        public AddAddressMappingViewModel(
            IRepository<device_AddressMapping> mappingRepo,
            IDataCacheService cacheService)
        {
            _mappingRepo = mappingRepo;
            _cacheService = cacheService;
            AddCommand = new DelegateCommand(OnAdd, CanAdd);
            CancelCommand = new DelegateCommand(OnCancel);
            PropertyChanged += (_, _) => AddCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region ===================== 公共属性 =====================

        public List<craft_LineInfo> Lines
        {
            get => _lines;
            set => SetProperty(ref _lines, value);
        }

        public int LineSelectIndex
        {
            get => _lineSelectIndex;
            set => SetProperty(ref _lineSelectIndex, value);
        }

        public craft_LineInfo LineSelectItem
        {
            get => _lineSelectItem;
            set
            {
                if (value == null) return;
                _lineSelectItem = value;
                LineSelectIndex = Lines.FindIndex(l => l.Id == value.Id);
                FilterStationsByLine();
                RaisePropertyChanged();
            }
        }

        public List<craft_StationInfo> Stations
        {
            get => _stations;
            set => SetProperty(ref _stations, value);
        }

        public int StationSelectIndex
        {
            get => _stationSelectIndex;
            set => SetProperty(ref _stationSelectIndex, value);
        }

        public craft_StationInfo StationSelectItem
        {
            get => _stationSelectItem;
            set
            {
                if (value == null) return;
                _stationSelectItem = value;
                StationSelectIndex = Stations.FindIndex(s => s.Id == value.Id);
                UpdateStationConnectionContext();
                RaisePropertyChanged();
            }
        }

        /// <summary>当前工位通讯协议（来自 device_ConnectInfo 缓存）</summary>
        public string StationProtocolType
        {
            get => _stationProtocolType;
            set => SetProperty(ref _stationProtocolType, value);
        }
        /// <summary>当前工位的交互类型（来自 device_ConnectInfo 缓存）</summary>
        public string InteractionType
        {
            get => _interactionType;
            set => SetProperty(ref _interactionType, value);
        }

        /// <summary>是否已配置工位连接且协议有对应数据类型</summary>
        public bool IsDataTypeEnabled
        {
            get => _isDataTypeEnabled;
            set => SetProperty(ref _isDataTypeEnabled, value);
        }

        public bool IsStationEnabled
        {
            get => _isStationEnabled;
            set => SetProperty(ref _isStationEnabled, value);
        }

        public List<string> DataNames
        {
            get => _dataNames;
            set => SetProperty(ref _dataNames, value);
        }

        public string DataNameSelectedItem
        {
            get => _dataNameSelectedItem;
            set => SetProperty(ref _dataNameSelectedItem, value);
        }

        public string DataAddress
        {
            get => _dataAddress;
            set
            {
                SetProperty(ref _dataAddress, value);
                AddCommand.RaiseCanExecuteChanged();
            }
        }

        public List<string> DataTypes
        {
            get => _dataTypes;
            set => SetProperty(ref _dataTypes, value);
        }

        public int DataTypeIndex
        {
            get => _dataTypeIndex;
            set => SetProperty(ref _dataTypeIndex, value);
        }

        public string DataTypeSelectedItem
        {
            get => _dataTypeSelectedItem;
            set
            {
                if (value == null) return;
                _dataTypeSelectedItem = value;
                DataTypeIndex = DataTypes.IndexOf(value);
                RaisePropertyChanged();
            }
        }

        public int DataLen
        {
            get => _dataLen;
            set => SetProperty(ref _dataLen, value);
        }

        public List<string> DataDirections
        {
            get => _dataDirections;
            set => SetProperty(ref _dataDirections, value);
        }

        public int DataDirectionIndex
        {
            get => _dataDirectionIndex;
            set => SetProperty(ref _dataDirectionIndex, value);
        }

        public string DataDirectionSelectedItem
        {
            get => _dataDirectionSelectedItem;
            set
            {
                if (value == null) return;
                _dataDirectionSelectedItem = value;
                DataDirectionIndex = DataDirections.IndexOf(value);
                RaisePropertyChanged();
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        #endregion

        #region ===================== 命令 =====================

        /// <summary> 确认新增映射 </summary>
        public DelegateCommand AddCommand { get; }

        /// <summary> 取消并关闭弹窗 </summary>
        public DelegateCommand CancelCommand { get; }

        #endregion

        #region ===================== IDialogAware =====================

        /// <summary> 弹窗标题 </summary>
        public string Title => "新增地址映射";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            _allLines = parameters.GetValue<List<craft_LineInfo>>("Lines") ?? new List<craft_LineInfo>();
            _allStations = parameters.GetValue<List<craft_StationInfo>>("Stations") ?? new List<craft_StationInfo>();
            _allMappings = parameters.GetValue<List<device_AddressMapping>>("AllMappings") ?? new List<device_AddressMapping>();

            Lines = _allLines;
            Stations = new List<craft_StationInfo>();
            IsStationEnabled = false;

            LineSelectIndex = -1;
            StationSelectIndex = -1;
            LineSelectItem = new craft_LineInfo();
            StationSelectItem = new craft_StationInfo();

            if (parameters.ContainsKey("DefaultLine"))
            {
                var line = parameters.GetValue<craft_LineInfo>("DefaultLine");
                if (line?.Id > 0)
                {
                    var matched = _allLines.FirstOrDefault(l => l.Id == line.Id);
                    if (matched != null)
                        LineSelectItem = matched;
                }
            }

            if (parameters.ContainsKey("DefaultStation"))
            {
                var station = parameters.GetValue<craft_StationInfo>("DefaultStation");
                if (station?.Id > 0)
                {
                    var matched = Stations.FirstOrDefault(s => s.Id == station.Id);
                    if (matched != null)
                        StationSelectItem = matched;
                }
            }
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 按选中产线过滤工位列表 </summary>
        private void FilterStationsByLine()
        {
            if (LineSelectItem?.Id > 0)
            {
                Stations = _allStations
                    .Where(s => s.LineId == LineSelectItem.Id)
                    .OrderBy(s => s.Code)
                    .ToList();
                IsStationEnabled = Stations.Any();
            }
            else
            {
                Stations = new List<craft_StationInfo>();
                IsStationEnabled = false;
            }

            StationSelectIndex = -1;
            StationSelectItem = new craft_StationInfo();
            UpdateStationConnectionContext();
        }

        /// <summary>工位变更后：从缓存连接信息刷新协议、交互类型、数据名称与数据类型</summary>
        private void UpdateStationConnectionContext(string? preserveDataType = null, string? preserveDataName = null)
        {
            if (StationSelectItem?.Id > 0)
            {
                StationProtocolType = StationConnectInfoHelper.GetProtocolType(_cacheService, StationSelectItem.Id) ?? string.Empty;
                InteractionType = StationConnectInfoHelper.GetInteractionType(_cacheService, StationSelectItem.Id) ?? string.Empty;
                DataNames = StationConnectInfoHelper.GetAddressMappingDataNames(_cacheService, StationSelectItem.Id);
                DataTypes = StationConnectInfoHelper.GetDataTypes(_cacheService, StationSelectItem.Id);
                IsDataTypeEnabled = DataTypes.Count > 0;

                var preferredName = preserveDataName ?? DataNameSelectedItem;
                if (!string.IsNullOrEmpty(preferredName) && DataNames.Contains(preferredName))
                    DataNameSelectedItem = preferredName;
                else
                    DataNameSelectedItem = DataNames.FirstOrDefault() ?? string.Empty;

                var preferredType = preserveDataType ?? DataTypeSelectedItem;
                if (!string.IsNullOrEmpty(preferredType) && DataTypes.Contains(preferredType))
                    DataTypeSelectedItem = preferredType;
                else
                    DataTypeSelectedItem = DataTypes.FirstOrDefault() ?? string.Empty;
            }
            else
            {
                StationProtocolType = string.Empty;
                InteractionType = string.Empty;
                DataNames = new List<string>();
                DataNameSelectedItem = string.Empty;
                DataTypes = new List<string>();
                DataTypeSelectedItem = string.Empty;
                DataTypeIndex = -1;
                IsDataTypeEnabled = false;
            }

            AddCommand.RaiseCanExecuteChanged();
        }

        private bool CanAdd() =>
            LineSelectIndex != -1 &&
            StationSelectIndex != -1 &&
            !string.IsNullOrWhiteSpace(DataNameSelectedItem) &&
            !string.IsNullOrWhiteSpace(DataAddress) &&
            IsDataTypeEnabled &&
            !string.IsNullOrWhiteSpace(DataTypeSelectedItem) &&
            !string.IsNullOrWhiteSpace(DataDirectionSelectedItem);

        private bool IsDuplicate() =>
            _allMappings.Any(m =>
                m.StationId == StationSelectItem.Id &&
                string.Equals(m.DataName, DataNameSelectedItem, StringComparison.Ordinal));

        private async void OnAdd()
        {
            if (LineSelectIndex == -1)
            {
                HandyControl.Controls.MessageBox.Show("请选择产线", "提示");
                return;
            }
            if (StationSelectIndex == -1)
            {
                HandyControl.Controls.MessageBox.Show("请选择工位", "提示");
                return;
            }
            if (IsDuplicate())
            {
                HandyControl.Controls.MessageBox.Show(
                    $"工位「{StationSelectItem.DisplayText}」下数据名称「{DataNameSelectedItem}」已存在",
                    "提示");
                return;
            }

            int newId = 0;
            try
            {
                var entity = new device_AddressMapping
                {
                    StationId = StationSelectItem.Id,
                    DataName = DataNameSelectedItem.Trim(),
                    DataAddress = DataAddress.Trim(),
                    DataType = DataTypeSelectedItem,
                    DataLen = DataLen,
                    DataDirection = DataDirectionSelectedItem,
                    IsEnabled = IsEnabled,
                    Remarks = Remarks?.Trim() ?? string.Empty,
                    CreateTime = DateTime.Now,
                    UpdateTime = DateTime.Now
                };

                newId = await _mappingRepo.InsertAsync(entity);
                if (newId <= 0)
                {
                    HandyControl.Controls.MessageBox.Show("新增失败", "提示");
                    return;
                }

                entity.Id = newId;
                HandyControl.Controls.MessageBox.Show("新增成功");

                var resultParams = new DialogParameters();
                resultParams.Add("AddressMapping", entity);
                RequestClose?.Invoke(new DialogResult(ButtonResult.OK, resultParams));
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"新增失败：{ex.Message}", "错误");
                if (newId > 0)
                    await _mappingRepo.DeleteAsync(newId);
            }
        }

        /// <summary> 取消并关闭弹窗 </summary>
        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)); // 返回 Cancel 结果
        }

        #endregion
    }
}
