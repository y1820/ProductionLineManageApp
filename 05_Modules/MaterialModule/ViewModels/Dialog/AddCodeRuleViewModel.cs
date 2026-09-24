using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.RepositoryGrop;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace MaterialModule.ViewModels.Dialog
{
    /// <summary>新增条码规则弹窗：配置物料扫码校验规则并写入 material_CodeRules。</summary>
    public class AddCodeRuleViewModel : BindableBase, IDialogAware
    {
        private readonly IRepository<material_CodeRules> _repo;

        // 全量数据（从主界面传入）
        private List<craft_TypeInfo> _allTypes = new List<craft_TypeInfo>();
        private List<material_Info> _allMaterials = new List<material_Info>();
        private List<material_CodeRules> _allCodeRules = new List<material_CodeRules>();

        // 型号物料相关
        private List<craft_TypeInfo> _types = new List<craft_TypeInfo>();
        private int _typeSelectIndex = -1;
        private craft_TypeInfo _typeSelectItem = new craft_TypeInfo();
        private List<material_Info> _materials = new List<material_Info>();
        private int _materialSelectIndex = -1;
        private material_Info _materialSelectItem = new material_Info();
        private bool _isMaterialEnabled = false;

        // 规则类型相关
        private List<KeyValuePair<int, string>> _ruleTypes = new List<KeyValuePair<int, string>>();
        private int _ruleTypeSelectIndex = -1;
        private KeyValuePair<int, string> _ruleTypeSelectItem;

        // 规则字段
        private int _groupNo = 1;
        private string _ruleName = string.Empty;
        private int _startBit = 1;
        private int _length = 0;
        private string _ruleContent = string.Empty;
        private string _dynamicMappingText = string.Empty;
        private string _exampleCode = string.Empty;
        private bool _isActive = true;
        private string _remarks = string.Empty;

        public AddCodeRuleViewModel(IRepository<material_CodeRules> repo)
        {
            _repo = repo;
            AddCommand = new DelegateCommand(OnAdd, CanAdd);
            CancelCommand = new DelegateCommand(OnCancel);

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TypeSelectItem))
                {
                    OnTypeChanged();
                }
                if (e.PropertyName == nameof(RuleTypeSelectItem))
                {
                    OnRuleTypeChanged();
                }
                AddCommand.RaiseCanExecuteChanged();
            };
        }

        #region ===================== 公共属性 =====================

        // 型号物料
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
                FilterMaterialsByType(value.Id);
                RaisePropertyChanged();
            }
        }

        public List<material_Info> Materials
        {
            get => _materials;
            set => SetProperty(ref _materials, value);
        }

        public int MaterialSelectIndex
        {
            get => _materialSelectIndex;
            set => SetProperty(ref _materialSelectIndex, value);
        }

        public material_Info MaterialSelectItem
        {
            get => _materialSelectItem;
            set
            {
                if (value == null) return;
                _materialSelectItem = value;
                MaterialSelectIndex = Materials.FindIndex(m => m.Id == value.Id);
                RaisePropertyChanged();
            }
        }

        public bool IsMaterialEnabled
        {
            get => _isMaterialEnabled;
            set => SetProperty(ref _isMaterialEnabled, value);
        }

        // 规则类型
        public List<KeyValuePair<int, string>> RuleTypes
        {
            get => _ruleTypes;
            set => SetProperty(ref _ruleTypes, value);
        }

        public int RuleTypeSelectIndex
        {
            get => _ruleTypeSelectIndex;
            set => SetProperty(ref _ruleTypeSelectIndex, value);
        }

        public KeyValuePair<int, string> RuleTypeSelectItem
        {
            get => _ruleTypeSelectItem;
            set
            {
                _ruleTypeSelectItem = value;
                RuleTypeSelectIndex = RuleTypes.FindIndex(t => t.Key == value.Key);
                RaisePropertyChanged();
            }
        }

        // 规则字段
        public int GroupNo
        {
            get => _groupNo;
            set => SetProperty(ref _groupNo, value);
        }

        public string RuleName
        {
            get => _ruleName;
            set => SetProperty(ref _ruleName, value);
        }

        public int StartBit
        {
            get => _startBit;
            set => SetProperty(ref _startBit, value);
        }

        public int Length
        {
            get => _length;
            set => SetProperty(ref _length, value);
        }

        public string RuleContent
        {
            get => _ruleContent;
            set => SetProperty(ref _ruleContent, value);
        }

        public string DynamicMappingText
        {
            get => _dynamicMappingText;
            set => SetProperty(ref _dynamicMappingText, value);
        }

        public string ExampleCode
        {
            get => _exampleCode;
            set => SetProperty(ref _exampleCode, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        // 界面联动属性
        public bool IsMiddleRule => RuleTypeSelectItem.Key == (int)CodeRuleType.Middle;
        public bool IsLengthRequired => RuleTypeSelectItem.Key == (int)CodeRuleType.TotalLength;
        public bool IsFixedContent => RuleTypeSelectItem.Key == (int)CodeRuleType.Left ||
                                      RuleTypeSelectItem.Key == (int)CodeRuleType.Right ||
                                      RuleTypeSelectItem.Key == (int)CodeRuleType.Middle;
        public bool IsDynamicContent => RuleTypeSelectItem.Key == (int)CodeRuleType.Year ||
                                        RuleTypeSelectItem.Key == (int)CodeRuleType.Month ||
                                        RuleTypeSelectItem.Key == (int)CodeRuleType.Day;

        #endregion

        #region ===================== 命令 =====================

        public DelegateCommand AddCommand { get; set; }
        public DelegateCommand CancelCommand { get; set; }

        #endregion

        #region ===================== IDialogAware =====================

        public string Title => "新增编码规则";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // 接收全量数据
            _allTypes = parameters.GetValue<List<craft_TypeInfo>>("Types") ?? new List<craft_TypeInfo>();
            _allMaterials = parameters.GetValue<List<material_Info>>("Materials") ?? new List<material_Info>();
            _allCodeRules = parameters.GetValue<List<material_CodeRules>>("AllCodeRules") ?? new List<material_CodeRules>();

            // 初始化型号下拉框
            Types = _allTypes;

            // 初始化规则类型
            RuleTypes = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>((int)CodeRuleType.Left, "左侧内容验证"),
                new KeyValuePair<int, string>((int)CodeRuleType.Right, "右侧内容验证"),
                new KeyValuePair<int, string>((int)CodeRuleType.Middle, "中间内容验证"),
                new KeyValuePair<int, string>((int)CodeRuleType.Year, "年代码验证"),
                new KeyValuePair<int, string>((int)CodeRuleType.Month, "月代码验证"),
                new KeyValuePair<int, string>((int)CodeRuleType.Day, "日代码验证"),
                new KeyValuePair<int, string>((int)CodeRuleType.TotalLength, "总长度验证")
            };

            // 默认选中左侧验证
            RuleTypeSelectItem = RuleTypes.First();
        }

        #endregion

        #region ===================== 私有方法 =====================

        private void FilterMaterialsByType(int typeId)
        {
            if (typeId <= 0)
            {
                Materials = new List<material_Info>();
                IsMaterialEnabled = false;
                return;
            }

            var filtered = _allMaterials.Where(m => m.TypeId == typeId).ToList();
            Materials = filtered;
            IsMaterialEnabled = filtered.Any();
            MaterialSelectIndex = -1;
            MaterialSelectItem = new material_Info();

            if (!filtered.Any())
            {
                HandyControl.Controls.MessageBox.Show("该型号下没有物料，请先创建物料", "提示");
            }
        }

        private void OnTypeChanged()
        {
            // 重置物料选择
            MaterialSelectIndex = -1;
            MaterialSelectItem = new material_Info();
        }

        private void OnRuleTypeChanged()
        {
            StartBit = 1;
            Length = 0;
            RuleContent = string.Empty;
            DynamicMappingText = string.Empty;

            RaisePropertyChanged(nameof(IsMiddleRule));
            RaisePropertyChanged(nameof(IsLengthRequired));
            RaisePropertyChanged(nameof(IsFixedContent));
            RaisePropertyChanged(nameof(IsDynamicContent));
        }

        private bool CanAdd()
        {
            // 型号物料必选
            if (TypeSelectIndex == -1) return false;
            if (MaterialSelectIndex == -1) return false;
            if (RuleTypeSelectIndex == -1) return false;

            int ruleType = RuleTypeSelectItem.Key;

            if (ruleType == (int)CodeRuleType.Middle && StartBit <= 0) return false;
            if (ruleType == (int)CodeRuleType.TotalLength && Length <= 0) return false;
            if (IsFixedContent && string.IsNullOrWhiteSpace(RuleContent)) return false;
            if (IsDynamicContent && string.IsNullOrWhiteSpace(DynamicMappingText)) return false;

            return true;
        }

        private bool IsDuplicate()
        {
            return _allCodeRules.Any(r =>
                r.TypeId == TypeSelectItem.Id &&
                r.MaterialId == MaterialSelectItem.Id &&
                r.GroupNo == GroupNo &&
                r.RuleType == RuleTypeSelectItem.Key &&
                r.RuleName == RuleName);
        }

        private string GetRuleContentFromDynamicMapping()
        {
            if (string.IsNullOrWhiteSpace(DynamicMappingText))
                return string.Empty;

            var trimmed = DynamicMappingText.Trim();
            if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}"))
                return trimmed;

            return trimmed;
        }

        private void OnAdd()
        {
            if (IsDuplicate())
            {
                HandyControl.Controls.MessageBox.Show($"组 {GroupNo} 中已存在规则 \"{RuleName}\"", "提示");
                return;
            }

            // 检查物料是否已选择
            if (MaterialSelectIndex == -1)
            {
                HandyControl.Controls.MessageBox.Show("请选择物料", "提示");
                return;
            }

            try
            {
                int ruleType = RuleTypeSelectItem.Key;
                string finalRuleContent = IsDynamicContent ? GetRuleContentFromDynamicMapping() : RuleContent;

                if (IsDynamicContent && !string.IsNullOrEmpty(finalRuleContent))
                {
                    if (!Regex.IsMatch(finalRuleContent, @"^\{.*\}$"))
                    {
                        HandyControl.Controls.MessageBox.Show("动态映射格式错误，应为 {2025:8,2026:9} 格式", "提示");
                        return;
                    }
                }

                var entity = new material_CodeRules
                {
                    TypeId = TypeSelectItem.Id,
                    MaterialId = MaterialSelectItem.Id,
                    GroupNo = GroupNo,
                    RuleType = ruleType,
                    RuleName = RuleName,
                    StartBit = ruleType == (int)CodeRuleType.Middle ? StartBit : -1,
                    Length = (ruleType == (int)CodeRuleType.Middle || ruleType == (int)CodeRuleType.TotalLength) ? Length : -1,
                    RuleContent = finalRuleContent,
                    ExampleCode = ExampleCode,
                    IsEnabled = IsActive,
                    Remarks = Remarks,
                    CreateTime = DateTime.Now,
                    UpdateTime = DateTime.Now
                };

                var result = _repo.Insert(entity);
                if (result > 0)
                {
                    entity.Id = result;
                    HandyControl.Controls.MessageBox.Show("新增成功");

                    var parameters = new DialogParameters();
                    parameters.Add("CodeRule", entity);
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
