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
    /// <summary>编辑条码规则弹窗：修改已有 material_CodeRules 记录。</summary>
    public class EditCodeRuleViewModel : BindableBase, IDialogAware
    {
        private readonly IRepository<material_CodeRules> _repo;

        private material_CodeRules _originalData = new material_CodeRules();
        private List<material_CodeRules> _allCodeRules = new List<material_CodeRules>();

        private List<KeyValuePair<int, string>> _ruleTypes = new List<KeyValuePair<int, string>>();
        private int _ruleTypeSelectIndex = -1;
        private KeyValuePair<int, string> _ruleTypeSelectItem;

        private int _id;
        private DateTime? _createTime;
        private int _groupNo = 1;
        private string _ruleName = string.Empty;
        private int _startBit = 1;
        private int _length = 0;
        private string _ruleContent = string.Empty;
        private string _dynamicMappingText = string.Empty;
        private string _exampleCode = string.Empty;
        private bool _isActive = true;
        private string _remarks = string.Empty;

        public EditCodeRuleViewModel(IRepository<material_CodeRules> repo)
        {
            _repo = repo;
            EditCommand = new DelegateCommand(OnEdit, CanEdit);
            CancelCommand = new DelegateCommand(OnCancel);

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RuleTypeSelectItem))
                {
                    OnRuleTypeChanged();
                }
                EditCommand.RaiseCanExecuteChanged();
            };
        }

        #region ===================== 公共属性 =====================

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public DateTime? CreateTime
        {
            get => _createTime;
            set => SetProperty(ref _createTime, value);
        }

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

        public DelegateCommand EditCommand { get; set; }
        public DelegateCommand CancelCommand { get; set; }

        #endregion

        #region ===================== IDialogAware =====================

        public string Title => "编辑编码规则";

        public event Action<IDialogResult>? RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            _originalData = parameters.GetValue<material_CodeRules>("CodeRule") ?? new material_CodeRules();
            _allCodeRules = parameters.GetValue<List<material_CodeRules>>("AllCodeRules") ?? new List<material_CodeRules>();

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

            Id = _originalData.Id;
            _createTime = _originalData.CreateTime;
            GroupNo = _originalData.GroupNo;
            RuleName = _originalData.RuleName;
            _startBit = _originalData.StartBit == -1 ? 1 : _originalData.StartBit;
            _length = _originalData.Length == -1 ? 0 : _originalData.Length;
            _exampleCode = _originalData.ExampleCode;
            IsActive = _originalData.IsEnabled;
            Remarks = _originalData.Remarks;

            // 规则类型选中
            var ruleType = RuleTypes.FirstOrDefault(t => t.Key == _originalData.RuleType);
            if (ruleType.Key != 0)
            {
                RuleTypeSelectItem = ruleType;
            }

            // 规则内容处理
            if (IsDynamicContent)
            {
                DynamicMappingText = _originalData.RuleContent;
            }
            else
            {
                RuleContent = _originalData.RuleContent;
            }
        }

        #endregion

        #region ===================== 私有方法 =====================

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

        private bool CanEdit()
        {
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
                r.Id != Id &&
                r.TypeId == _originalData.TypeId &&
                r.MaterialId == _originalData.MaterialId &&
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

        private void OnEdit()
        {
            if (IsDuplicate())
            {
                HandyControl.Controls.MessageBox.Show($"组 {GroupNo} 中已存在规则 \"{RuleName}\"", "提示");
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
                    Id = Id,
                    TypeId = _originalData.TypeId,
                    MaterialId = _originalData.MaterialId,
                    GroupNo = GroupNo,
                    RuleType = ruleType,
                    RuleName = RuleName,
                    StartBit = ruleType == (int)CodeRuleType.Middle ? StartBit : -1,
                    Length = (ruleType == (int)CodeRuleType.Middle || ruleType == (int)CodeRuleType.TotalLength) ? Length : -1,
                    RuleContent = finalRuleContent,
                    ExampleCode = ExampleCode,
                    IsEnabled = IsActive,
                    Remarks = Remarks,
                    CreateTime = CreateTime,
                    UpdateTime = DateTime.Now
                };

                var result = _repo.Update(entity);
                if (result > 0)
                {
                    HandyControl.Controls.MessageBox.Show("修改成功");

                    var parameters = new DialogParameters();
                    parameters.Add("CodeRule", entity);
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
