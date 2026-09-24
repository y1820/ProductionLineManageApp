using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.IO;
using System.Windows;

namespace WorkmanshipModule.ViewModels.Dialog
{
    /// <summary>
    /// 新增工位视图模型类
    /// </summary>
    public class NewAddWorkStationViewModel : BindableBase, IDialogAware
    {
        /// <summary>
        /// 构造方法
        /// </summary>
        public NewAddWorkStationViewModel(IRepository<craft_StationInfo> repo)
        {
            //实例化新增命令
            NewAddCommand = new DelegateCommand(NewAdd);
            //关闭命令
            CloseCommand = new DelegateCommand(OnDialogClosed);
            _repo = repo;

        }

        #region ============================== 属性 命令 字段 定义 ==============================
        //新增
        public DelegateCommand NewAddCommand { get; set; }
        //关闭
        public DelegateCommand CloseCommand { get; set; }
        //产线下拉框选择的对象
        private craft_LineInfo _lineSelectedItem = new craft_LineInfo();
        public craft_LineInfo LineSelectedItem
        {
            get { return _lineSelectedItem; }
            set
            {
                if (value == null) return;
                _lineSelectedItem = value;
                StationInfo.LineId = value.Id;
                RaisePropertyChanged();
            }
        }
        //产线集合,界面显示
        private List<craft_LineInfo> _lines = new List<craft_LineInfo>();
        public List<craft_LineInfo> Lines
        {
            get { return _lines; }
            set { SetProperty(ref _lines, value); }
        }
        //工位信息
        public craft_StationInfo StationInfo { get; set; } = new craft_StationInfo();
        //数据库操作
        private IRepository<craft_StationInfo> _repo;

        #endregion ---------------------------------------------------------------

        #region ============================== 弹窗接口实现 ==============================
        public string Title => "新增工位";

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
            Lines = parameters.GetValue<List<craft_LineInfo>>("Lines") ?? new List<craft_LineInfo>();
            RaisePropertyChanged(nameof(Lines));

            if (parameters.ContainsKey("DefaultLine"))
            {
                var line = parameters.GetValue<craft_LineInfo>("DefaultLine");
                if (line?.Id > 0)
                {
                    LineSelectedItem = Lines.FirstOrDefault(l => l.Id == line.Id) ?? line;
                }
            }
        }

        #endregion ---------------------------------------------------------------

        #region ============================== 新增工位 ==============================
        /// <summary>
        /// 新增工位
        /// </summary>
        private void NewAdd()
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
            if (HandyControl.Controls.MessageBox.Show($"确认新增{StationInfo.DisplayText}",
                "提示", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    //写入数据库
                    StationInfo.Id = _repo.Insert(StationInfo);
                    HandyControl.Controls.MessageBox.Show("新增成功");
                    //回调新增的数据
                    var parameters = new DialogParameters();
                    parameters.Add("StationInfo", StationInfo);
                    RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Show("写入工位到数据库异常：" + ex.Message);
                }

            }
        }

        #endregion ---------------------------------------------------------------




    }

}
