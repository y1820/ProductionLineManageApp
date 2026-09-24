using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Helpers;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;

namespace WorkmanshipModule.ViewModels.Dialog
{
    /// <summary>
    /// 编辑加工数据采集配置弹窗视图模型：修改已有 craft_DataCollectConfig 记录。
    /// </summary>
    public class EditDataCollectConfigViewModel : BindableBase, IDialogAware
    {
        #region ===================== 私有字段 =====================

        private readonly IRepository<craft_DataCollectConfig> _configRepo; // 采集配置仓储
        private readonly IDataCacheService _cacheService;

        private craft_DataCollectConfig _originalData = new();
        private List<craft_TypeInfo> _allTypes = new();
        private List<craft_LineInfo> _allLines = new();
        private List<craft_StationInfo> _allStations = new();
        private List<craft_DataCollectConfig> _allConfigs = new();

        private List<craft_TypeInfo> _types = new();
        private List<craft_LineInfo> _lines = new();
        private List<craft_StationInfo> _stations = new();

        private int _id;
        private int _typeSelectIndex = -1;
        private craft_TypeInfo _typeSelectItem = new();
        private int _lineSelectIndex = -1;
        private craft_LineInfo _lineSelectItem = new();
        private int _stationSelectIndex = -1;
        private craft_StationInfo _stationSelectItem = new();
        private bool _isStationEnabled;

        private string _dataName = string.Empty;
        private string _address = string.Empty;
        private List<string> _dataTypes = new();
        private string _dataTypeSelectedItem = string.Empty;
        private int _dataTypeIndex = -1;
        private string _stationProtocolType = string.Empty;
        private bool _isDataTypeEnabled;
        private int _dataLength = 1;
        private string _dataUnit = string.Empty;
        private bool _isEnabled = true;
        private string _remarks = string.Empty;

        public EditDataCollectConfigViewModel(
            IRepository<craft_DataCollectConfig> configRepo,
            IDataCacheService cacheService)
        {
            _configRepo = configRepo;
            _cacheService = cacheService;
            EditCommand = new DelegateCommand(OnEdit, CanEdit);
            CancelCommand = new DelegateCommand(OnCancel);
            PropertyChanged += (_, _) => EditCommand.RaiseCanExecuteChanged();
        }

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public List<craft_TypeInfo> Types
        {
            get => _types;
            set => SetProperty(ref _types, value);
        }

        public int TypeSelectIndex
        {
            get => _typeSelectIndex;
            set => SetProperty(ref _typeSelectIndex, value);
        }

        public craft_TypeInfo TypeSelectItem
        {
            get => _typeSelectItem;
            set
            {
                if (value == null) return;
                _typeSelectItem = value;
                TypeSelectIndex = Types.FindIndex(t => t.Id == value.Id);
                RaisePropertyChanged();
            }
        }

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
                FilterStationsByLine(preserveStation: false);
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
                UpdateDataTypesForSelectedStation();
                RaisePropertyChanged();
            }
        }

        /// <summary>当前工位通讯协议（来自 device_ConnectInfo 缓存）</summary>
        public string StationProtocolType
        {
            get => _stationProtocolType;
            set => SetProperty(ref _stationProtocolType, value);
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

        public string DataName
        {
            get => _dataName;
            set => SetProperty(ref _dataName, value);
        }

        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
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

        public int DataLength
        {
            get => _dataLength;
            set => SetProperty(ref _dataLength, value);
        }

        public string DataUnit
        {
            get => _dataUnit;
            set => SetProperty(ref _dataUnit, value);
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

        public DelegateCommand EditCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public string Title => "编辑加工数据采集配置";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            _originalData = parameters.GetValue<craft_DataCollectConfig>("DataCollectConfig") ?? new craft_DataCollectConfig();
            _allTypes = parameters.GetValue<List<craft_TypeInfo>>("Types") ?? new List<craft_TypeInfo>();
            _allLines = parameters.GetValue<List<craft_LineInfo>>("Lines") ?? new List<craft_LineInfo>();
            _allStations = parameters.GetValue<List<craft_StationInfo>>("Stations") ?? new List<craft_StationInfo>();
            _allConfigs = parameters.GetValue<List<craft_DataCollectConfig>>("AllConfigs") ?? new List<craft_DataCollectConfig>();

            Types = _allTypes;
            Lines = _allLines;

            Id = _originalData.Id;
            DataName = _originalData.DataName;
            Address = _originalData.Address;
            DataTypeSelectedItem = _originalData.DataType;
            DataLength = _originalData.DataLength;
            DataUnit = _originalData.DataUnit ?? string.Empty;
            IsEnabled = _originalData.IsEnabled;
            Remarks = _originalData.Remarks ?? string.Empty;

            TypeSelectItem = _allTypes.FirstOrDefault(t => t.Id == _originalData.ProductTypeId) ?? new craft_TypeInfo();

            var line = _allLines.FirstOrDefault(l => l.Id == _originalData.LineId);
            if (line != null)
            {
                LineSelectItem = line;
                FilterStationsByLine(preserveStation: true);
                var station = Stations.FirstOrDefault(s => s.Id == _originalData.StationId)
                              ?? _allStations.FirstOrDefault(s => s.Id == _originalData.StationId);
                if (station != null)
                {
                    StationSelectItem = station;
                    UpdateDataTypesForSelectedStation(_originalData.DataType);
                }
            }
            else
            {
                Stations = new List<craft_StationInfo>();
                IsStationEnabled = false;
                UpdateDataTypesForSelectedStation();
            }
        }

        private void FilterStationsByLine(bool preserveStation)
        {
            var previousStationId = preserveStation ? _originalData.StationId : 0;

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

            if (preserveStation && previousStationId > 0)
            {
                var matched = Stations.FirstOrDefault(s => s.Id == previousStationId);
                if (matched != null)
                {
                    StationSelectItem = matched;
                    return;
                }
            }

            StationSelectIndex = -1;
            StationSelectItem = new craft_StationInfo();
            UpdateDataTypesForSelectedStation();
        }

        /// <summary>工位变更后：按协议刷新可选数据类型（数据采集名称仍自由输入）</summary>
        private void UpdateDataTypesForSelectedStation(string? preserveDataType = null)
        {
            if (StationSelectItem?.Id > 0)
            {
                StationProtocolType = StationConnectInfoHelper.GetProtocolType(_cacheService, StationSelectItem.Id) ?? string.Empty;
                DataTypes = StationConnectInfoHelper.GetDataTypes(_cacheService, StationSelectItem.Id);
                IsDataTypeEnabled = DataTypes.Count > 0;

                var preferred = preserveDataType ?? DataTypeSelectedItem;
                if (!string.IsNullOrEmpty(preferred) && DataTypes.Contains(preferred))
                    DataTypeSelectedItem = preferred;
                else
                    DataTypeSelectedItem = DataTypes.FirstOrDefault() ?? string.Empty;
            }
            else
            {
                StationProtocolType = string.Empty;
                DataTypes = new List<string>();
                DataTypeSelectedItem = string.Empty;
                DataTypeIndex = -1;
                IsDataTypeEnabled = false;
            }

            EditCommand.RaiseCanExecuteChanged();
        }

        private bool CanEdit() =>
            TypeSelectIndex != -1 &&
            LineSelectIndex != -1 &&
            StationSelectIndex != -1 &&
            !string.IsNullOrWhiteSpace(DataName) &&
            !string.IsNullOrWhiteSpace(Address) &&
            IsDataTypeEnabled &&
            !string.IsNullOrWhiteSpace(DataTypeSelectedItem);

        private bool IsDuplicate() =>
            _allConfigs.Any(c =>
                c.Id != _originalData.Id &&
                c.StationId == StationSelectItem.Id &&
                c.ProductTypeId == TypeSelectItem.Id &&
                string.Equals(c.DataName, DataName.Trim(), StringComparison.Ordinal));

        private async void OnEdit()
        {
            if (TypeSelectIndex == -1)
            {
                HandyControl.Controls.MessageBox.Show("请选择型号", "提示");
                return;
            }
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
            if (DataLength < 0)
            {
                HandyControl.Controls.MessageBox.Show("数据长度不能为负数", "提示");
                return;
            }
            if (IsDuplicate())
            {
                HandyControl.Controls.MessageBox.Show(
                    $"工位「{StationSelectItem.DisplayText}」型号「{TypeSelectItem.Name}」下数据名称「{DataName}」已存在",
                    "提示");
                return;
            }

            try
            {
                _originalData.ProductTypeId = TypeSelectItem.Id;
                _originalData.LineId = LineSelectItem.Id;
                _originalData.StationId = StationSelectItem.Id;
                _originalData.DataName = DataName.Trim();
                _originalData.Address = Address.Trim();
                _originalData.DataType = DataTypeSelectedItem;
                _originalData.DataLength = DataLength;
                _originalData.DataUnit = DataUnit?.Trim() ?? string.Empty;
                _originalData.IsEnabled = IsEnabled;
                _originalData.Remarks = Remarks?.Trim() ?? string.Empty;
                _originalData.UpdateTime = DateTime.Now;

                await _configRepo.UpdateAsync(_originalData);
                HandyControl.Controls.MessageBox.Show("保存成功");

                var resultParams = new DialogParameters();
                resultParams.Add("DataCollectConfig", _originalData);
                RequestClose?.Invoke(new DialogResult(ButtonResult.OK, resultParams));
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"保存失败：{ex.Message}", "错误");
            }
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        #endregion
    }
}
