using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Windows;
namespace WorkmanshipModule.ViewModels.Dialog
{
    /// <summary>
    /// 工艺流程 - 弹窗 - 编辑工艺流程
    /// </summary>
    public class EditProcessViewModel : BindableBase, IDialogAware
    {

        #region ============================== 字段 属性 命令 ==============================
        #region 字段
        //所有工位
        private List<craft_StationInfo> _allStations = new List<craft_StationInfo>();
        //当前编辑的工艺流程信息
        private ProcessFlowItem _process = new ProcessFlowItem();
        //所有工艺流程
        private List<craft_ProcessInfo> _allProcess = new List<craft_ProcessInfo>();
        //数据库操作单例
        private IRepository<craft_ProcessInfo> _repo;

        //界面产线集合
        private List<craft_LineInfo> _lines = new List<craft_LineInfo>();
        //界面型号集合
        private List<craft_TypeInfo> _types = new List<craft_TypeInfo>();
        //界面工位集合
        private List<craft_StationInfo> _workStationInfos = new List<craft_StationInfo>();
        //界面型号和产线下能选择的工位
        private List<craft_StationInfo> _canSelectStations = new List<craft_StationInfo>();
        //界面型号选择索引
        private int _typeSelectedIndex;
        //界面产线选择索引
        private int _lineSelectedIndex;
        //界面工位选择索引
        private int _stationSelectedIndex;
        //界面前工位选择索引
        private int _upStationSelectedIndex;
        //界面无前工位勾选
        private bool _haveUpStation;
        //界面型号下拉框选择后的对象
        private craft_TypeInfo _typesSelectItem = new craft_TypeInfo();
        //界面产线下拉框选择后的对象
        private craft_LineInfo _linesSelectItem = new craft_LineInfo();
        #endregion

        #region 属性
        //界面无前工位勾选
        public bool HaveUpStation
        {
            get { return _haveUpStation; }
            set
            {
                SetProperty(ref _haveUpStation, value);
            }
        }
        //界面产线集合
        public List<craft_LineInfo> Lines
        {
            get { return _lines; }
            set { SetProperty(ref _lines, value); }
        }
        //界面型号集合
        public List<craft_TypeInfo> Types
        {
            get { return _types; }
            set { SetProperty(ref _types, value); }
        }
        //界面工位集合
        public List<craft_StationInfo> WorkStationInfos
        {
            get { return _workStationInfos; }
            set { SetProperty(ref _workStationInfos, value); }
        }

        public ProcessFlowItem Process
        {
            get => _process;
            set
            {
                SetProperty(ref _process, value);
            }
        }
        //界面型号选择索引
        public int TypeSelectIndex
        {
            get { return _typeSelectedIndex; }
            set { SetProperty(ref _typeSelectedIndex, value); }
        }
        //界面产线选择索引
        public int LineSelectIndex
        {
            get { return _lineSelectedIndex; }
            set { SetProperty(ref _lineSelectedIndex, value); }
        }
        //界面工位选择索引
        public int StationSelectIndex
        {
            get { return _stationSelectedIndex; }
            set { SetProperty(ref _stationSelectedIndex, value); }
        }
        //界面前工位选择索引
        public int UpStationSelectIndex
        {
            get { return _upStationSelectedIndex; }
            set { SetProperty(ref _upStationSelectedIndex, value); }
        }
        //界面型号和产线下能选择的工位
        public List<craft_StationInfo> CanSelectStations
        {
            get { return _canSelectStations; }
            set { SetProperty(ref _canSelectStations, value); }
        }
        //界面型号下拉框选择后的对象
        public craft_TypeInfo TypesSelectItem
        {
            get => _typesSelectItem;
            set
            {
                if (value == null) return;
                _typesSelectItem = value;
                // 更新索引
                TypeSelectIndex = Types.FindIndex(t => t.Id == value.Id);
                //更新界面工位集合
                CanSelectStations = FilterStation() ?? new List<craft_StationInfo>();
                RaisePropertyChanged();
            }
        }
        //界面产线下拉框选择后的对象
        public craft_LineInfo LinesSelectItem
        {
            get => _linesSelectItem;
            set
            {
                if (value == null) return;
                _linesSelectItem = value;
                // 更新索引
                LineSelectIndex = Lines.FindIndex(l => l.Id == value.Id);
                //更新界面当前工位集合
                CanSelectStations = FilterStation() ?? new List<craft_StationInfo>();
                //更新界面前工位集合
                WorkStationInfos = _allStations.Where(c => c.LineId == value.Id).ToList();
                RaisePropertyChanged();
            }
        }
        //界面工位下拉框选择后的对象
        public craft_StationInfo StationsSelectItem { get; set; } = new craft_StationInfo();
        //界面前工位下拉框选择后的对象
        public craft_StationInfo UpStationsSelectItem { get; set; } = new craft_StationInfo();
        #endregion
        #region 命令
        //编辑按钮
        public DelegateCommand EditCommand { get; set; }
        //取消和退出按钮
        public DelegateCommand CloseCommand { get; set; }
        #endregion

        #endregion ----------------------------------

        public EditProcessViewModel(IRepository<craft_ProcessInfo> repo)
        {
            //命令初始化
            EditCommand = new DelegateCommand(Edit);
            CloseCommand = new DelegateCommand(OnDialogClosed);
            _repo = repo;
        }

        #region ============================== 弹窗接口实现 ==============================

        public string Title => "编辑工艺流程";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        /// <summary>
        /// 当本弹窗打开完成时
        /// </summary>
        /// <param name="parameters"></param>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            //修改的工艺流程对象
            var process = parameters.GetValue<ProcessFlowItem>("Process");
            Process = new ProcessFlowItem()
            {
                RawData = process.RawData,
                TypeInfo = process.TypeInfo,
                LineInfo = process.LineInfo,
                StationInfo = process.StationInfo,
                UpperWorkstation = process.UpperWorkstation,
            };
            //所有产线信息
            Lines = parameters.GetValue<List<craft_LineInfo>>("Lines");
            //所有型号信息
            Types = parameters.GetValue<List<craft_TypeInfo>>("Types");
            //所有工位信息 赋值给缓存，这个需要根据产线来决定工位下拉框数据
            _allStations = parameters.GetValue<List<craft_StationInfo>>("Stations");
            //所有已存在的工艺流程，防止重复工艺流程的元素为 产线 下的 工位 存在于这个集合中则当产线选择时候不显示该工位
            _allProcess = parameters.GetValue<List<craft_ProcessInfo>>("AllProcess");
            ///将所有下拉框选择当前的信息
            //型号
            TypeSelectIndex = Types.FindIndex(t => t.Id == _process.RawData.TypeId);
            //产线
            LineSelectIndex = Lines.FindIndex(l => l.Id == _process.RawData.LineId);
            //筛选型号和产线下的工艺流程的工位并更新界面
            CanSelectStations = FilterStation() ?? new List<craft_StationInfo>();
            //选择正在编辑的对象
            StationSelectIndex = CanSelectStations.FindIndex(l => l.Id == _process.RawData.StationId);
            //上工位,只需筛选产线下的工位并选择正在编辑的对象
            WorkStationInfos = _allStations.Where(c => c.LineId == Lines[LineSelectIndex].Id).ToList();
            //确认所编辑的工艺流程上工位的id
            if (_process.RawData.UpperWorkstationId != 0)
            {
                UpStationSelectIndex = WorkStationInfos.FindIndex(w => w.Id == _process.RawData.UpperWorkstationId);
            }
            else
            {
                //界面勾选无前工位连带触发前工位下拉框选择-1
                HaveUpStation = true;
            }

        }

        #endregion ----------------------------------
        //筛选当前型号和产线下能选择的工位
        private List<craft_StationInfo>? FilterStation()
        {
            // 检查索引有效性
            if (TypeSelectIndex < 0 || LineSelectIndex < 0) return null;
            // 筛选当前型号 + 当前产线下的工艺流程
            var filteredProcess = _allProcess.Where(p =>
                p.TypeId == Types[TypeSelectIndex].Id &&
                p.LineId == Lines[LineSelectIndex].Id);
            // 获取该产线下的所有工位
            var listStation = _allStations.Where(c => c.LineId == Lines[LineSelectIndex].Id).ToList();
            foreach (var item in filteredProcess)
            {
                listStation.RemoveAll(s => s.Id == item.StationId);
            }
            //在选择的产线下
            bool isInLine = _process.RawData.LineId == Lines[LineSelectIndex].Id;
            //得到最终筛选完的能选择的工位后再添加正在编辑的工位进去
            if (_process?.StationInfo != null && isInLine)
            {
                listStation.Add(_process.StationInfo!);
            }
            return listStation.OrderBy(s => s.Code).ToList();
        }

        //编辑按钮方法
        private void Edit()
        {
            // 输入验证
            if (TypeSelectIndex < 0)
            {
                HandyControl.Controls.MessageBox.Show("请选择型号", "提示");
                return;
            }
            if (LineSelectIndex < 0)
            {
                HandyControl.Controls.MessageBox.Show("请选择产线", "提示");
                return;
            }
            if (StationSelectIndex < 0 || StationSelectIndex >= CanSelectStations.Count)
            {
                HandyControl.Controls.MessageBox.Show("请选择工位", "提示");
                return;
            }

            if (HandyControl.Controls.MessageBox.Show("确认修改", "提示",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            try
            {
                craft_ProcessInfo info = new craft_ProcessInfo()
                {
                    Id = _process.Id,
                    TypeId = Types[TypeSelectIndex].Id,
                    LineId = Lines[LineSelectIndex].Id,
                    StationId = CanSelectStations[StationSelectIndex].Id,
                    UpperWorkstationId = HaveUpStation ? 0 : WorkStationInfos[UpStationSelectIndex].Id,
                    Sequence = Process.Sequence,
                    IsRepeatWork = Process.IsRepeatWork,
                    IsRepairStation = Process.IsRepairStation,
                    IsEnable = Process.IsEnable,
                    CreateTime = Process.CreateTime,
                    UpdateTime = Process.UpdateTime,
                    Remarks = Process.Remarks,
                };
                var result = _repo.Update(info);
                if (result > 0)
                {
                    HandyControl.Controls.MessageBox.Show("修改成功");
                    var parameters = new DialogParameters();
                    parameters.Add("Process", info);
                    RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
                }
                else
                {
                    HandyControl.Controls.MessageBox.Show("修改失败", "提示");
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"修改失败：{ex.Message}", "错误");
            }
        }
    }
}
