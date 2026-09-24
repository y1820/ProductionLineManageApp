using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Windows;

namespace WorkmanshipModule.ViewModels.Dialog
{
    /// <summary>
    /// 编辑工位视图模型类 ViewModel
    /// </summary>
    public class EditWorkStationViewModel : BindableBase, IDialogAware
    {
        public EditWorkStationViewModel(IRepository<craft_StationInfo> repo)
        {
            EditCommand = new DelegateCommand(Edit);
            CloseCommand = new DelegateCommand(OnDialogClosed);
            _repo = repo;
        }

        #region ============================== 属性 命令 字段 定义 ==============================
        //工位信息，界面显示
        private craft_StationInfo _stationInfo = new craft_StationInfo();
        public craft_StationInfo StationInfo
        {
            get { return _stationInfo; }
            set
            {
                SetProperty(ref _stationInfo, value);
            }
        }
        //产线信息，产线选择下拉框
        public List<craft_LineInfo> Lines { get; set; } = new List<craft_LineInfo>();
        //产线下拉框选择索引
        private int _cbSelectedIndex;
        public int LineSelectedIndex
        {
            get { return _cbSelectedIndex; }
            set
            {
                SetProperty(ref _cbSelectedIndex, value);
            }
        }
        //产线下拉框选择对象
        private craft_LineInfo _lineSelectedItem = new craft_LineInfo();
        public craft_LineInfo LineSelectedItem
        {
            get { return _lineSelectedItem; }
            set
            {
                _lineSelectedItem = value;
                //将产线Id传给工位信息
                StationInfo.LineId = value.Id;
            }
        }
        //编辑
        public DelegateCommand EditCommand { get; set; }
        //关闭
        public DelegateCommand CloseCommand { get; set; }
        //数据库操作
        private IRepository<craft_StationInfo> _repo;

        #endregion ---------------------------------------------------------------

        #region ============================== 弹窗接口实现 ==============================

        public string Title => "编辑工位";

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
        /// 打开弹窗时 加载要修改的数据到界面上
        /// </summary>
        /// <param name="parameters">打开弹窗时传入的工位信息参数</param>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            //获取打开弹窗方所传入的工位数据
            craft_StationInfo stationInfo = parameters.GetValue<craft_StationInfo>("StationInfo");
            List<craft_LineInfo> lines = parameters.GetValue<List<craft_LineInfo>>("Lines");
            //填充界面 
            StationInfo = new craft_StationInfo()
            {
                Id = stationInfo.Id,
                Code = stationInfo.Code,
                Name = stationInfo.Name,
                Remarks = stationInfo.Remarks,
                LineId = stationInfo.LineId,
                CreateTime = stationInfo.CreateTime,
                UpdateTime = stationInfo.UpdateTime,
            };
            //填充产线集合到界面
            Lines = lines;
            //将产线下拉框选到与传入参数的工位信息对应的产线上
            LineSelectedIndex = Lines.FindIndex(s => s.Id == stationInfo.LineId);
        }

        #endregion ---------------------------------------------------------------

        private void Edit()
        {
            //输入验证
            if (string.IsNullOrWhiteSpace(StationInfo.Code))
            {
                HandyControl.Controls.MessageBox.Show("工位代号不能为空", "提示");
                return;
            }
            if (string.IsNullOrWhiteSpace(StationInfo.Name))
            {
                HandyControl.Controls.MessageBox.Show("工位名称不能为空", "提示");
                return;
            }
            if (StationInfo.LineId <= 0)
            {
                HandyControl.Controls.MessageBox.Show("所属产线不能为空", "提示");
                return;
            }
            //提示 
            if (HandyControl.Controls.MessageBox.Show($"确认修改:{StationInfo.DisplayText}", "提示",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    //修改数据到数据库
                    if (_repo.Update(StationInfo) > 0)
                    {
                        HandyControl.Controls.MessageBox.Show("修改成功");
                        //创建回调工位参数给上级
                        var parameters = new DialogParameters();
                        parameters.Add("StationInfo", StationInfo);
                        RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
                    }
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show("修改工位到数据库异常：" + ex.Message);
                }
            }

        }
    }
}
