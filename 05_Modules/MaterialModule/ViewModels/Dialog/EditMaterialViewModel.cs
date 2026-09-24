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
    /// <summary>编辑物料弹窗：修改 material_Info 基础信息。</summary>
    public class EditMaterialViewModel : BindableBase, IDialogAware
    {
        private readonly IRepository<material_Info> _repo;

        private List<craft_TypeInfo> _types = new List<craft_TypeInfo>();
        private List<material_Info> _allMaterials = new List<material_Info>();
        private int _typeSelectIndex = -1;
        private craft_TypeInfo _typeSelectItem = new craft_TypeInfo();
        private material_Info _material = new material_Info();
        private material_Info _originalMaterial = new material_Info();

        public EditMaterialViewModel(IRepository<material_Info> repo)
        {
            _repo = repo;
            EditCommand = new DelegateCommand(OnEdit);
            CancelCommand = new DelegateCommand(OnCancel);
        }

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

        public DelegateCommand EditCommand { get; set; }
        public DelegateCommand CancelCommand { get; set; }

        #endregion

        #region ===================== IDialogAware =====================

        public string Title => "编辑物料";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            _originalMaterial = parameters.GetValue<material_Info>("Material") ?? new material_Info();
            Types = parameters.GetValue<List<craft_TypeInfo>>("Types") ?? new List<craft_TypeInfo>();
            _allMaterials = parameters.GetValue<List<material_Info>>("AllMaterials") ?? new List<material_Info>();

            // 创建副本进行编辑
            Material = new material_Info
            {
                Id = _originalMaterial.Id,
                TypeId = _originalMaterial.TypeId,
                Code = _originalMaterial.Code,
                Name = _originalMaterial.Name,
                Remarks = _originalMaterial.Remarks,
                CreateTime = _originalMaterial.CreateTime,
                UpdateTime = _originalMaterial.UpdateTime
            };

            // 设置型号选中项
            var selectedType = Types.FirstOrDefault(t => t.Id == Material.TypeId);
            if (selectedType != null)
            {
                TypeSelectItem = selectedType;
            }
        }

        #endregion

        #region ===================== 私有方法 =====================

        private void OnEdit()
        {
            // 验证
            if (TypeSelectIndex == -1)
            {
                HandyControl.Controls.MessageBox.Show("请选择所属型号", "提示");
                return;
            }

            if (string.IsNullOrWhiteSpace(Material.Name))
            {
                HandyControl.Controls.MessageBox.Show("请输入物料名称", "提示");
                return;
            }

            // 更新型号ID
            Material.TypeId = TypeSelectItem.Id;

            try
            {
                var result = _repo.Update(Material);
                if (result > 0)
                {
                    HandyControl.Controls.MessageBox.Show("修改成功");

                    var parameters = new DialogParameters();
                    parameters.Add("Material", Material);
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

        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        #endregion
    }
}
