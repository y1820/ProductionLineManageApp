using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;

namespace WorkmanshipModule.ViewModels.Dialog
{
    /// <summary>
    /// 新增工位传值配置弹窗视图模型：配置跨工位历史数据写入 PLC 的规则。
    /// </summary>
    public class AddStationDataTransferViewModel : BindableBase, IDialogAware
    {
        #region ===================== 私有字段 =====================

        private readonly IRepository<craft_StationDataTransfer> _transferRepo; // 传值配置仓储

        private List<craft_TypeInfo> _allTypes = new();
        private List<craft_LineInfo> _allLines = new();
        private List<craft_StationInfo> _allStations = new();
        private List<craft_StationDataTransfer> _allConfigs = new();

        private List<craft_TypeInfo> _types = new();
        private List<craft_LineInfo> _lines = new();
        private List<craft_StationInfo> _requestStations = new();
        private List<craft_StationInfo> _sourceStations = new();

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
        private string _dataTypeSelectedItem = DeviceDataTypeConstants.DataTypeConstants[0];
        private int _dataTypeIndex;
        private int _dataLength;
        private bool _isEnabled = true;
        private string _remarks = string.Empty;

        public AddStationDataTransferViewModel(IRepository<craft_StationDataTransfer> transferRepo)
        {
            _transferRepo = transferRepo;
            AddCommand = new DelegateCommand(OnAdd, CanAdd);
            CancelCommand = new DelegateCommand(OnCancel);
            PropertyChanged += (_, _) => AddCommand.RaiseCanExecuteChanged();
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
                FilterRequestStationsByLine();
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

        public DelegateCommand AddCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public string Title => "新增工位传值";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            _allTypes = parameters.GetValue<List<craft_TypeInfo>>("Types") ?? new List<craft_TypeInfo>();
            _allLines = parameters.GetValue<List<craft_LineInfo>>("Lines") ?? new List<craft_LineInfo>();
            _allStations = parameters.GetValue<List<craft_StationInfo>>("Stations") ?? new List<craft_StationInfo>();
            _allConfigs = parameters.GetValue<List<craft_StationDataTransfer>>("AllConfigs") ?? new List<craft_StationDataTransfer>();

            Types = _allTypes;
            Lines = _allLines;
            SourceStations = _allStations.OrderBy(s => s.Code).ToList();
            RequestStations = new List<craft_StationInfo>();
            IsRequestStationEnabled = false;

            TypeSelectIndex = -1;
            LineSelectIndex = -1;
            RequestStationSelectIndex = -1;
            SourceStationSelectIndex = -1;
            TypeSelectItem = new craft_TypeInfo();
            LineSelectItem = new craft_LineInfo();
            RequestStationSelectItem = new craft_StationInfo();
            SourceStationSelectItem = new craft_StationInfo();

            if (parameters.ContainsKey("DefaultType"))
            {
                var type = parameters.GetValue<craft_TypeInfo>("DefaultType");
                if (type?.Id > 0)
                    TypeSelectItem = _allTypes.FirstOrDefault(t => t.Id == type.Id) ?? type;
            }

            if (parameters.ContainsKey("DefaultLine"))
            {
                var line = parameters.GetValue<craft_LineInfo>("DefaultLine");
                if (line?.Id > 0)
                    LineSelectItem = _allLines.FirstOrDefault(l => l.Id == line.Id) ?? line;
            }

            if (parameters.ContainsKey("DefaultRequestStation"))
            {
                var station = parameters.GetValue<craft_StationInfo>("DefaultRequestStation");
                if (station?.Id > 0)
                {
                    var matched = RequestStations.FirstOrDefault(s => s.Id == station.Id);
                    if (matched != null)
                        RequestStationSelectItem = matched;
                }
            }
        }

        private void FilterRequestStationsByLine()
        {
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

            RequestStationSelectIndex = -1;
            RequestStationSelectItem = new craft_StationInfo();
        }

        private bool CanAdd() =>
            TypeSelectIndex != -1 &&
            LineSelectIndex != -1 &&
            RequestStationSelectIndex != -1 &&
            SourceStationSelectIndex != -1 &&
            !string.IsNullOrWhiteSpace(RequestDataName) &&
            !string.IsNullOrWhiteSpace(SourceAddress) &&
            !string.IsNullOrWhiteSpace(DataTypeSelectedItem);

        private bool IsDuplicate() =>
            _allConfigs.Any(c =>
                c.RequestStationId == RequestStationSelectItem.Id &&
                c.SourceStationId == SourceStationSelectItem.Id &&
                c.ProductTypeId == TypeSelectItem.Id &&
                c.LineId == LineSelectItem.Id &&
                string.Equals(c.RequestDataName, RequestDataName.Trim(), StringComparison.Ordinal));

        private async void OnAdd()
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

            int newId = 0;
            try
            {
                var entity = new craft_StationDataTransfer
                {
                    ProductTypeId = TypeSelectItem.Id,
                    LineId = LineSelectItem.Id,
                    RequestStationId = RequestStationSelectItem.Id,
                    RequestDataName = RequestDataName.Trim(),
                    SourceStationId = SourceStationSelectItem.Id,
                    SourceAddress = SourceAddress.Trim(),
                    DataType = DataTypeSelectedItem,
                    DataLength = DataLength,
                    IsEnabled = IsEnabled,
                    Remarks = Remarks?.Trim() ?? string.Empty,
                    CreateTime = DateTime.Now,
                    UpdateTime = DateTime.Now
                };

                newId = await _transferRepo.InsertAsync(entity);
                if (newId <= 0)
                {
                    HandyControl.Controls.MessageBox.Show("新增失败", "提示");
                    return;
                }

                entity.Id = newId;
                HandyControl.Controls.MessageBox.Show("新增成功");

                var resultParams = new DialogParameters();
                resultParams.Add("StationDataTransfer", entity);
                RequestClose?.Invoke(new DialogResult(ButtonResult.OK, resultParams));
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"新增失败：{ex.Message}", "错误");
                if (newId > 0)
                    await _transferRepo.DeleteAsync(newId);
            }
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        #endregion
    }
}

