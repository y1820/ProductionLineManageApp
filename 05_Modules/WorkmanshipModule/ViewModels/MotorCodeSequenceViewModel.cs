using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.MotorCode;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.MotorCode;
using Prism.Commands;
using Prism.Mvvm;
using System.Windows;

namespace WorkmanshipModule.ViewModels
{
    /// <summary>电机码 - 按型号序列号设置</summary>
    public class MotorCodeSequenceViewModel : BindableBase
    {
        #region ===================== 私有字段 =====================

        private readonly IMotorCodeService _motorCodeService; // 电机码生成服务
        private readonly IMotorCodeCacheService _motorCodeCache;
        private readonly IDataCacheService _cache;

        private List<craft_TypeInfo> _types = new();
        private craft_TypeInfo? _selectedType;
        private int _digitLength = 4;
        private int _startValue = 1;
        private int _step = 1;
        private MotorCodeResetCycle _resetCycle = MotorCodeResetCycle.Daily;
        private bool _isEnabled = true;
        private string _bucketKey = string.Empty;
        private int _currentValue;
        private int _nextValue;
        private string _manualValueText = string.Empty;
        private string _adjustReason = string.Empty;
        private string _operatorName = "Admin";

        public MotorCodeSequenceViewModel(
            IMotorCodeService motorCodeService,
            IMotorCodeCacheService motorCodeCache,
            IDataCacheService cache)
        {
            _motorCodeService = motorCodeService;
            _motorCodeCache = motorCodeCache;
            _cache = cache;

            SaveCommand = new DelegateCommand(async () => await SaveAsync());
            RefreshCommand = new DelegateCommand(async () => await LoadAsync());
            AdjustCommand = new DelegateCommand(async () => await AdjustAsync());
            ResetCommand = new DelegateCommand(async () => await ResetAsync());

            LoadTypes();
        }

        public List<craft_TypeInfo> Types
        {
            get => _types;
            set => SetProperty(ref _types, value);
        }

        public craft_TypeInfo? SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetProperty(ref _selectedType, value) && value != null)
                    _ = LoadAsync();
            }
        }

        public int DigitLength
        {
            get => _digitLength;
            set => SetProperty(ref _digitLength, value);
        }

        public int StartValue
        {
            get => _startValue;
            set => SetProperty(ref _startValue, value);
        }

        /// <summary>自增系数</summary>
        public int Step
        {
            get => _step;
            set => SetProperty(ref _step, value);
        }

        public MotorCodeResetCycle ResetCycle
        {
            get => _resetCycle;
            set => SetProperty(ref _resetCycle, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public string BucketKey
        {
            get => _bucketKey;
            set => SetProperty(ref _bucketKey, value);
        }

        public int CurrentValue
        {
            get => _currentValue;
            set => SetProperty(ref _currentValue, value);
        }

        public int NextValue
        {
            get => _nextValue;
            set => SetProperty(ref _nextValue, value);
        }

        public string ManualValueText
        {
            get => _manualValueText;
            set => SetProperty(ref _manualValueText, value);
        }

        public string AdjustReason
        {
            get => _adjustReason;
            set => SetProperty(ref _adjustReason, value);
        }

        public string OperatorName
        {
            get => _operatorName;
            set => SetProperty(ref _operatorName, value);
        }

        public Array ResetCycleOptions => Enum.GetValues(typeof(MotorCodeResetCycle));

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand AdjustCommand { get; }
        public DelegateCommand ResetCommand { get; }

        private void LoadTypes()
        {
            Types = _cache.GetData<List<craft_TypeInfo>>() ?? new List<craft_TypeInfo>();
            SelectedType = Types.FirstOrDefault();
        }

        private async Task LoadAsync()
        {
            if (SelectedType == null)
                return;

            var status = await _motorCodeService.GetSequenceStatusAsync(SelectedType.Id);
            if (status == null)
            {
                DigitLength = 4;
                StartValue = 1;
                Step = 1;
                ResetCycle = MotorCodeResetCycle.Daily;
                IsEnabled = true;
                BucketKey = string.Empty;
                CurrentValue = 0;
                NextValue = 1;
                return;
            }

            DigitLength = status.DigitLength;
            StartValue = status.StartValue;
            Step = status.Step;
            ResetCycle = (MotorCodeResetCycle)status.ResetCycle;
            IsEnabled = status.IsEnabled;
            BucketKey = status.BucketKey;
            CurrentValue = status.CurrentValue;
            NextValue = status.NextValue;
        }

        private async Task SaveAsync()
        {
            if (SelectedType == null)
            {
                MessageBox.Show("请选择型号", "校验");
                return;
            }

            if (DigitLength < 1 || DigitLength > 10)
            {
                MessageBox.Show("位数应在 1~10 之间", "校验");
                return;
            }

            if (Step < 1)
            {
                MessageBox.Show("自增系数应 >= 1", "校验");
                return;
            }

            var config = new craft_MotorCodeSequenceConfig
            {
                ProductTypeId = SelectedType.Id,
                DigitLength = DigitLength,
                StartValue = StartValue,
                Step = Step,
                ResetCycle = (int)ResetCycle,
                IsEnabled = IsEnabled
            };

            await _motorCodeService.SaveSequenceConfigAsync(config);
            await _motorCodeCache.RefreshAsync();
            await LoadAsync();
            MessageBox.Show("序列号配置已保存", "提示");
        }

        private async Task AdjustAsync()
        {
            if (SelectedType == null)
            {
                MessageBox.Show("请选择型号", "校验");
                return;
            }

            if (!int.TryParse(ManualValueText, out var newValue))
            {
                MessageBox.Show("请输入有效的当前序号", "校验");
                return;
            }

            if (string.IsNullOrWhiteSpace(AdjustReason))
            {
                MessageBox.Show("请填写调整原因", "校验");
                return;
            }

            if (newValue < CurrentValue &&
                MessageBox.Show("新值小于当前值，可能产生重复电机码，是否继续？", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            await _motorCodeService.AdjustSequenceAsync(SelectedType.Id, newValue, OperatorName, AdjustReason);
            AdjustReason = string.Empty;
            await LoadAsync();
            MessageBox.Show("序号已调整", "提示");
        }

        private async Task ResetAsync()
        {
            if (SelectedType == null)
            {
                MessageBox.Show("请选择型号", "校验");
                return;
            }

            if (MessageBox.Show("将当前型号序号复位到起始前状态，是否继续？", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            await _motorCodeService.ResetSequenceToStartAsync(SelectedType.Id, OperatorName, "手动复位");
            await LoadAsync();
            MessageBox.Show("已复位", "提示");
        }

        #endregion
    }
}
