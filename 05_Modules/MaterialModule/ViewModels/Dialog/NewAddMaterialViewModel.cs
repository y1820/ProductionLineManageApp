using MaterialModule.Views.Dialog;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MaterialModule.ViewModels.Dialog
{
    /// <summary>
    /// 新增物料弹窗视图模型：录入物料信息并写入 material_Info。
    /// </summary>
    public class NewAddMaterialViewModel : BindableBase, IDialogAware
    {
        #region ===================== 私有字段 =====================

        private readonly IRepository<material_Info> _repo; // 物料仓储

        private List<craft_TypeInfo> _types = new List<craft_TypeInfo>();
        private List<material_Info> _allMaterials = new List<material_Info>();
        private int _typeSelectIndex = -1;
        private craft_TypeInfo _typeSelectItem = new craft_TypeInfo();
        private material_Info _material = new material_Info();

        public NewAddMaterialViewModel(IRepository<material_Info> repo)
        {
            _repo = repo;
            AddCommand = new DelegateCommand(OnAdd);
            CancelCommand = new DelegateCommand(OnCancel);
        }

        #endregion

        #region ===================== 公共属性 =====================

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

        public material_Info Material
        {
            get => _material;
            set => SetProperty(ref _material, value);
        }

        #endregion

        #region ===================== 命令 =====================

        public DelegateCommand AddCommand { get; set; }
        public DelegateCommand CancelCommand { get; set; }

        #endregion

        #region ===================== IDialogAware =====================

        public string Title => "新增物料";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            Types = parameters.GetValue<List<craft_TypeInfo>>("Types") ?? new List<craft_TypeInfo>();
            _allMaterials = parameters.GetValue<List<material_Info>>("AllMaterials") ?? new List<material_Info>();

            Material = new material_Info
            {
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };
        }

        #endregion

        #region ===================== 私有方法 =====================

        private void OnAdd()
        {
            // 验证
            if (TypeSelectIndex == -1)
            {
                HandyControl.Controls.MessageBox.Show("请选择所属型号", "提示");
                return;
            }

            if (string.IsNullOrWhiteSpace(Material.Code))
            {
                HandyControl.Controls.MessageBox.Show("请输入物料编码", "提示");
                return;
            }

            if (string.IsNullOrWhiteSpace(Material.Name))
            {
                HandyControl.Controls.MessageBox.Show("请输入物料名称", "提示");
                return;
            }

            // 检查编码是否重复
            if (_allMaterials.Any(m => m.Code == Material.Code))
            {
                HandyControl.Controls.MessageBox.Show($"物料编码 \"{Material.Code}\" 已存在", "提示");
                return;
            }

            // 设置型号ID
            Material.TypeId = TypeSelectItem.Id;

            try
            {
                var result = _repo.Insert(Material);
                if (result > 0)
                {
                    Material.Id = result;
                    HandyControl.Controls.MessageBox.Show("新增成功");

                    var parameters = new DialogParameters();
                    parameters.Add("Material", Material);
                    RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
                }
                else
                {
                    HandyControl.Controls.MessageBox.Show("新增失败", "提示");
                }
            }
            catch (Exception ex)
            {
                HandyControl.Controls.MessageBox.Show($"新增失败：{ex.Message}", "错误");
            }
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        #endregion
    }
}
