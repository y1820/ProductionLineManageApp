using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Windows;

namespace WorkmanshipModule.ViewModels.Dialog
{
    /// <summary>新增工艺流程弹窗：创建 craft_ProcessInfo 并关联产品型号。</summary>
    public class NewAddProcessViewModel : BindableBase, IDialogAware
    {
        public NewAddProcessViewModel(IRepository<craft_ProcessInfo> repo)
        {
            NewAddCommand = new DelegateCommand(NewAdd);
            //关闭命令
            CloseCommand = new DelegateCommand(OnDialogClosed);
            _repo = repo;
        }
        #region ============================== 新增工艺流程 ==============================
        private void NewAdd()
        {
            // 输入验证
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
            if (!HaveUpStation && UpStationSelectIndex == -1)
            {
                HandyControl.Controls.MessageBox.Show("请选择前工位", "提示");
                return;
            }
            try
            {
                var info = new craft_ProcessInfo()
                {
                    TypeId = TypeSelectItem.Id,
                    LineId = LineSelectItem.Id,
                    StationId = StationSelectItem.Id,
                    UpperWorkstationId = HaveUpStation ? 0 : (UpStationSelectItem?.Id ?? 0),
                    Sequence = Model.Sequence,
                    IsRepeatWork = Model.IsRepeatWork,
                    IsRepairStation = Model.IsRepairStation,
                    IsEnable = Model.IsEnable,
                    Remarks = Model.Remarks ?? string.Empty,
                    CreateTime = DateTime.Now,
                    UpdateTime = DateTime.Now
                };
                //验证与现有的工艺流程是否重复
                if (IsDuplicateProcess(info))
                {
                    HandyControl.Controls.MessageBox.Show("该工艺流程已存在，请勿重复添加", "提示");
                    return;
                }
                var result = _repo.Insert(info);
                if (result > 0)
                {
                    info.Id = result;
                    HandyControl.Controls.MessageBox.Show("添加成功");

                    var parameters = new DialogParameters();
                    parameters.Add("Process", info);
                    RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
                }
                else
                {
                    HandyControl.Controls.MessageBox.Show("添加失败", "提示");
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"添加失败：{ex.Message}", "错误");
            }

        }
        #endregion


        #region ============================== 弹窗接口实现 ==============================
        public string Title => "新增工艺流程";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            try
            {
                Types = parameters.GetValue<List<craft_TypeInfo>>("Types");
                Lines = parameters.GetValue<List<craft_LineInfo>>("Lines");
                _stations = parameters.GetValue<List<craft_StationInfo>>("Stations");
                _allProcess = parameters.GetValue<List<craft_ProcessInfo>>("AllProcess");
                TypeSelectIndex = LineSelectIndex = StationSelectIndex = UpStationSelectIndex = -1;
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"加载数据失败：{ex.Message}", "错误");
            }
        }

        #endregion ----------------------------------

        #region ============================== 属性 字段 命令 定义 ==============================

        #region 属性
        //型号集合下拉框
        public List<craft_TypeInfo> Types { get; set; } = new List<craft_TypeInfo>();
        //产线集合下拉框
        public List<craft_LineInfo> Lines { get; set; } = new List<craft_LineInfo>();
        public List<craft_StationInfo> LineWorkstation
        {
            get { return _lineWorkstation; }
            set { SetProperty(ref _lineWorkstation, value); }
        }
        public List<craft_StationInfo> UpStations
        {
            get { return _upStations; }
            set { SetProperty(ref _upStations, value); }
        }
        public bool IsRepeatWork { get; set; } = false;
        public bool IsEnable { get; set; } = true;
        public int Sequence { get; set; }
        public craft_TypeInfo TypeSelectItem
        {
            get => _typeSelectItem;
            set
            {
                if (value == null) return;
                _typeSelectItem = value;
                TypeSelectIndex = Types.FindIndex(t => t.Id == value.Id);
                //如果选择了产线，则筛选出型号和产线下所有工艺流程中未存在的工位传给界面选择
                if (LineSelectItem?.Id != 0)
                    _ = UpdateStation();
                RaisePropertyChanged();
            }
        }
        public craft_LineInfo LineSelectItem
        {
            get => _lineSelectItem;
            set
            {
                if (value == null) return;
                _lineSelectItem = value;
                LineSelectIndex = Lines.FindIndex(l => l.Id == value.Id);
                if (TypeSelectItem?.Id != 0)
                {
                    _ = UpdateStation();
                }
                RaisePropertyChanged();
            }
        }
        public craft_StationInfo StationSelectItem { get; set; } = new craft_StationInfo();
        public craft_StationInfo UpStationSelectItem { get; set; } = new craft_StationInfo();
        public int TypeSelectIndex { get; set; }
        public int LineSelectIndex { get; set; }
        public int StationSelectIndex { get; set; }
        public int UpStationSelectIndex { get; set; }

        public craft_ProcessInfo Model { get; set; } = new craft_ProcessInfo();
        //有无前工位
        public bool HaveUpStation { get; set; }

        #endregion

        #region 字段
        //工位集合
        private List<craft_StationInfo> _stations = new List<craft_StationInfo>();
        private List<craft_StationInfo> _upStations = new List<craft_StationInfo>();
        private List<craft_ProcessInfo> _allProcess = new List<craft_ProcessInfo>();
        private List<craft_StationInfo> _lineWorkstation = new List<craft_StationInfo>();
        private craft_LineInfo _lineSelectItem = new craft_LineInfo();
        private craft_TypeInfo _typeSelectItem = new craft_TypeInfo();
        //数据库操作单例
        private IRepository<craft_ProcessInfo> _repo;
        #endregion

        #region 命令
        public DelegateCommand NewAddCommand { get; set; }
        public DelegateCommand CloseCommand { get; set; }
        #endregion

        #endregion ----------------------------------

        #region 集中方法调用

        //更新界面
        private async Task UpdateStation()
        {
            if (TypeSelectItem == null || LineSelectItem == null)
                return;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (TypeSelectItem == null || LineSelectItem == null) return;

                    // 获取该产线下的所有工位
                    var allLineStations = _stations
                        .Where(s => s.LineId == LineSelectItem.Id)
                        .ToList();

                    if (allLineStations.Count == 0)
                    {
                        HandyControl.Controls.MessageBox.Show("该产线下没有工位可选，请先去创建工位", "提示");
                        LineWorkstation = new List<craft_StationInfo>();
                        return;
                    }

                    // 筛选该型号+该产线下已有的工艺流程
                    var existingProcess = _allProcess
                        .Where(p => p.TypeId == TypeSelectItem.Id && p.LineId == LineSelectItem.Id)
                        .ToList();

                    // 移除已被使用的工位
                    var availableStations = allLineStations
                        .Where(s => !existingProcess.Any(p => p.StationId == s.Id))
                        .ToList();

                    if (availableStations.Count == 0)
                    {
                        HandyControl.Controls.MessageBox.Show(
                            "该产线和型号下的工艺流程已创建完，请先去修改或删除一些工位的工艺流程",
                            "提示");
                        LineWorkstation = new List<craft_StationInfo>();
                        return;
                    }

                    LineWorkstation = availableStations;

                    // 更新前工位列表（同一产线下的所有工位）
                    UpStations = allLineStations;

                    // 重置工位选择索引
                    StationSelectIndex = -1;
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show($"更新工位列表失败：{ex.Message}", "错误");
                }
            });
        }
        // 在 NewAdd 方法中添加重复验证
        private bool IsDuplicateProcess(craft_ProcessInfo info)
        {
            return _allProcess.Any(p =>
                p.TypeId == info.TypeId &&
                p.LineId == info.LineId &&
                p.StationId == info.StationId);
        }

        #endregion
    }
}
