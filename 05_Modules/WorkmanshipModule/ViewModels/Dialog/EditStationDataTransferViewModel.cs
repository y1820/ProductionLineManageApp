using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;

namespace WorkmanshipModule.ViewModels.Dialog
{
    /// <summary>
    /// 编辑工位传值配置弹窗视图模型：修改已有 craft_StationDataTransfer 记录。
    /// </summary>
    public class EditStationDataTransferViewModel : BindableBase, IDialogAware
    {
        #region ===================== 私有字段 =====================

        private readonly IRepository<craft_StationDataTransfer> _transferRepo; // 传值配置仓储

        private craft_StationDataTransfer _originalData = new();
        private List<craft_TypeInfo> _allTypes = new();
        private List<craft_LineInfo> _allLines = new();
        private List<craft_StationInfo> _allStations = new();
        private List<craft_StationDataTransfer> _allConfigs = new();

        private List<craft_TypeInfo> _types = new();
        private List<craft_LineInfo> _lines = new();
        private List<craft_StationInfo> _requestStations = new();
        private List<craft_StationInfo> _sourceStations = new();

        private int _id;
        private int _typeSelectIndex = -1;
        private craft_TypeInfo _typeSelectItem = new();
        private int _lineSelectIndex = -1;
        private craft_LineInfo _lineSelectItem = new();
        private int _requestStationSelectIndex = -1;
        private craft_StationInfo _requestStationSelectItem = new();
        private int _sourceStationSelectIndex = -1;
        private craft_StationInfo _sourceStationSelectItem = new();
        private bool _isRequestStationEnabled;

        private string _requestDataName = string.Empty;
        private string _sourceAddress = string.Empty;
        private List<string> _dataTypes = DeviceDataTypeConstants.DataTypeConstants.ToList();
        private string _dataTypeSelectedItem = string.Empty;
        private int _dataTypeIndex = -1;
        private int _dataLength;
        private bool _isEnabled = true;
        private string _remarks = string.Empty;

        public EditStationDataTransferViewModel(IRepository<craft_StationDataTransfer> transferRepo)
        {
            _transferRepo = transferRepo;
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
                FilterRequestStationsByLine(preserveRequestStation: false);
                RaisePropertyChanged();
            }
        }

        public List<craft_StationInfo> RequestStations
        {
            get => _requestStations;
            set => SetProperty(ref _requestStations, value);
        }

        public int RequestStationSelectIndex
        {
            get => _requestStationSelectIndex;
            set => SetProperty(ref _requestStationSelectIndex, value);
        }

        public craft_StationInfo RequestStationSelectItem
        {
            get => _requestStationSelectItem;
            set
            {
                if (value == null) return;
                _requestStationSelectItem = value;
                RequestStationSelectIndex = RequestStations.FindIndex(s => s.Id == value.Id);
                RaisePropertyChanged();
            }
        }

        public List<craft_StationInfo> SourceStations
        {
            get => _sourceStations;
            set => SetProperty(ref _sourceStations, value);
        }

        public int SourceStationSelectIndex
        {
            get => _sourceStationSelectIndex;
            set => SetProperty(ref _sourceStationSelectIndex, value);
        }

        public craft_StationInfo SourceStationSelectItem
        {
            get => _sourceStationSelectItem;
            set
            {
                if (value == null) return;
                _sourceStationSelectItem = value;
                SourceStationSelectIndex = SourceStations.FindIndex(s => s.Id == value.Id);
                RaisePropertyChanged();
            }
        }

        public bool IsRequestStationEnabled
        {
            get => _isRequestStationEnabled;
            set => SetProperty(ref _isRequestStationEnabled, value);
        }

        public string RequestDataName
        {
            get => _requestDataName;
            set => SetProperty(ref _requestDataName, value);
        }

        public string SourceAddress
        {
            get => _sourceAddress;
            set => SetProperty(ref _sourceAddress, value);
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

        public string Title => "编辑工位传值";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            _originalData = parameters.GetValue<craft_StationDataTransfer>("StationDataTransfer") ?? new craft_StationDataTransfer();
            _allTypes = parameters.GetValue<List<craft_TypeInfo>>("Types") ?? new List<craft_TypeInfo>();
            _allLines = parameters.GetValue<List<craft_LineInfo>>("Lines") ?? new List<craft_LineInfo>();
            _allStations = parameters.GetValue<List<craft_StationInfo>>("Stations") ?? new List<craft_StationInfo>();
            _allConfigs = parameters.GetValue<List<craft_StationDataTransfer>>("AllConfigs") ?? new List<craft_StationDataTransfer>();

            Types = _allTypes;
            Lines = _allLines;
            SourceStations = _allStations.OrderBy(s => s.Code).ToList();

            Id = _originalData.Id;
            RequestDataName = _originalData.RequestDataName;
            SourceAddress = _originalData.SourceAddress;
            DataTypeSelectedItem = _originalData.DataType;
            DataLength = _originalData.DataLength;
            IsEnabled = _originalData.IsEnabled;
            Remarks = _originalData.Remarks ?? string.Empty;

            TypeSelectItem = _allTypes.FirstOrDefault(t => t.Id == _originalData.ProductTypeId) ?? new craft_TypeInfo();

            var sourceStation = _allStations.FirstOrDefault(s => s.Id == _originalData.SourceStationId);
            if (sourceStation != null)
                SourceStationSelectItem = sourceStation;

            var line = _allLines.FirstOrDefault(l => l.Id == _originalData.LineId);
            if (line != null)
            {
                LineSelectItem = line;
                FilterRequestStationsByLine(preserveRequestStation: true);
            }
            else
            {
                RequestStations = new List<craft_StationInfo>();
                IsRequestStationEnabled = false;
            }
        }

        private void FilterRequestStationsByLine(bool preserveRequestStation)
        {
            var previousRequestStationId = preserveRequestStation ? _originalData.RequestStationId : 0;

            if (LineSelectItem?.Id > 0)
            {
                RequestStations = _allStations
                    .Where(s => s.LineId == LineSelectItem.Id)
                    .OrderBy(s => s.Code)
                    .ToList();
                IsRequestStationEnabled = RequestStations.Any();
            }
            else
            {
                RequestStations = new List<craft_StationInfo>();
                IsRequestStationEnabled = false;
            }

            if (preserveRequestStation && previousRequestStationId > 0)
            {
                var matched = RequestStations.FirstOrDefault(s => s.Id == previousRequestStationId)
                              ?? _allStations.FirstOrDefault(s => s.Id == previousRequestStationId);
                if (matched != null)
                {
                    RequestStationSelectItem = matched;
                    return;
                }
            }

            RequestStationSelectIndex = -1;
            RequestStationSelectItem = new craft_StationInfo();
        }

        private bool CanEdit() =>
            TypeSelectIndex != -1 &&
            LineSelectIndex != -1 &&
            RequestStationSelectIndex != -1 &&
            SourceStationSelectIndex != -1 &&
            !string.IsNullOrWhiteSpace(RequestDataName) &&
            !string.IsNullOrWhiteSpace(SourceAddress) &&
            !string.IsNullOrWhiteSpace(DataTypeSelectedItem);

        private bool IsDuplicate() =>
            _allConfigs.Any(c =>
                c.Id != _originalData.Id &&
                c.RequestStationId == RequestStationSelectItem.Id &&
                c.SourceStationId == SourceStationSelectItem.Id &&
                c.ProductTypeId == TypeSelectItem.Id &&
                c.LineId == LineSelectItem.Id &&
                string.Equals(c.RequestDataName, RequestDataName.Trim(), StringComparison.Ordinal));

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
            if (RequestStationSelectIndex == -1)
            {
                HandyControl.Controls.MessageBox.Show("请选择请求工位", "提示");
                return;
            }
            if (SourceStationSelectIndex == -1)
            {
                HandyControl.Controls.MessageBox.Show("请选择数据源工位", "提示");
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
                    $"请求工位「{RequestStationSelectItem.DisplayText}」数据源工位「{SourceStationSelectItem.DisplayText}」型号「{TypeSelectItem.Name}」下数据名称「{RequestDataName}」已存在",
                    "提示");
                return;
            }

            try
            {
                _originalData.ProductTypeId = TypeSelectItem.Id;
                _originalData.LineId = LineSelectItem.Id;
                _originalData.RequestStationId = RequestStationSelectItem.Id;
                _originalData.RequestDataName = RequestDataName.Trim();
                _originalData.SourceStationId = SourceStationSelectItem.Id;
                _originalData.SourceAddress = SourceAddress.Trim();
                _originalData.DataType = DataTypeSelectedItem;
                _originalData.DataLength = DataLength;
                _originalData.IsEnabled = IsEnabled;
                _originalData.Remarks = Remarks?.Trim() ?? string.Empty;
                _originalData.UpdateTime = DateTime.Now;

                await _transferRepo.UpdateAsync(_originalData);
                HandyControl.Controls.MessageBox.Show("保存成功");

                var resultParams = new DialogParameters();
                resultParams.Add("StationDataTransfer", _originalData);
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

