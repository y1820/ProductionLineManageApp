using ProductionLineManage.Core.Constants;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ProductionLineManage.Shared.Controls
{
    /// <summary>
    /// 数据地址面板：按协议/数据类型展示地址录入 UI，支持新增（Compose）与编辑（Parse）双向同步。
    /// </summary>
    public partial class DataAddressPanel : UserControl, INotifyPropertyChanged
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化数据地址面板 </summary>
        public DataAddressPanel()
        {
            InitializeComponent(); // 加载 XAML 布局
            // 勿设置 UserControl.DataContext = this，否则外部 OutAddress="{Binding DataAddress}" 会绑定到控件自身而非 ViewModel
        }

        #endregion

        #region ===================== INotifyPropertyChanged =====================

        /// <summary> 属性变更通知事件 </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary> 触发属性变更通知 </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); // 通知 UI 刷新绑定
        }

        #endregion

        #region ===================== 依赖属性 =====================

        #region --------------------- 输入依赖属性 ---------------------

        /// <summary> 协议类型依赖属性 </summary>
        public static readonly DependencyProperty ProtocolTypeProperty =
            DependencyProperty.Register(
                nameof(ProtocolType),
                typeof(string),
                typeof(DataAddressPanel),
                new PropertyMetadata(string.Empty, OnProtocolTypeChanged));

        /// <summary> 协议类型变化回调 </summary>
        private static void OnProtocolTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataAddressPanel panel) // 确认目标为当前控件
            {
                panel.OnInputsReady(); // 协议就绪后刷新可见性并尝试解析外部地址
            }
        }

        /// <summary> 协议类型，决定显示 S7 / OPC UA 哪套录入 UI </summary>
        public string ProtocolType
        {
            get => (string)GetValue(ProtocolTypeProperty); // 读取依赖属性值
            set => SetValue(ProtocolTypeProperty, value); // 写入依赖属性值
        }

        /// <summary> 数据类型依赖属性 </summary>
        public static readonly DependencyProperty DataTypeProperty =
            DependencyProperty.Register(
                nameof(DataType),
                typeof(string),
                typeof(DataAddressPanel),
                new PropertyMetadata(string.Empty, OnDataTypeChanged));

        /// <summary> 数据类型变化回调 </summary>
        private static void OnDataTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataAddressPanel panel) // 确认目标为当前控件
            {
                panel.OnInputsReady(); // 数据类型就绪后刷新可见性并尝试解析外部地址
            }
        }

        /// <summary> 数据类型，决定区域内显示 bool / string / 其他类型控件 </summary>
        public string DataType
        {
            get => (string)GetValue(DataTypeProperty); // 读取依赖属性值
            set => SetValue(DataTypeProperty, value); // 写入依赖属性值
        }

        #endregion

        #region --------------------- 输出依赖属性 ---------------------

        /// <summary> 输出地址依赖属性，与外部 ViewModel 双向绑定 </summary>
        public static readonly DependencyProperty OutAddressProperty =
            DependencyProperty.Register(
                nameof(OutAddress),
                typeof(string),
                typeof(DataAddressPanel),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnOutAddressChanged));

        /// <summary> 外部地址推入时解析到内部字段（编辑模式入口） </summary>
        private static void OnOutAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataAddressPanel panel) return; // 目标类型不符则退出
            if (panel._isUpdating) return; // 内部 Compose 写回时跳过，避免 Parse ↔ Compose 死循环
            panel.TryApplyExternalAddress((string?)e.NewValue); // 尝试将外部地址解析到子控件
        }

        /// <summary> 输出地址字符串，Compose 后同步给外部 ViewModel </summary>
        public string OutAddress
        {
            get => (string)GetValue(OutAddressProperty); // 读取输出地址
            set => SetValue(OutAddressProperty, value); // 写入输出地址
        }

        /// <summary> 输出数据长度依赖属性 </summary>
        public static readonly DependencyProperty OutDataLengthProperty =
            DependencyProperty.Register(
                nameof(OutDataLength),
                typeof(int),
                typeof(DataAddressPanel),
                new FrameworkPropertyMetadata(
                    1,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnOutDataLengthChanged));

        /// <summary> 外部数据长度推入时同步到字符串长度 UI（编辑 S7String 场景） </summary>
        private static void OnOutDataLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataAddressPanel panel) return; // 目标类型不符则退出
            if (panel._isUpdating) return; // 内部写回时跳过
            if (panel.DataType != DeviceDataTypeConstants.SS7String) return; // 非 S7String 不需要同步长度 UI
            panel.ApplyStringLengthFromExternal(); // 将 DataLen 反映到长度文本框
        }

        /// <summary> 输出数据长度，Compose 后同步给外部 ViewModel </summary>
        public int OutDataLength
        {
            get => (int)GetValue(OutDataLengthProperty); // 读取输出长度
            set => SetValue(OutDataLengthProperty, value); // 写入输出长度
        }

        #endregion

        #endregion

        #region ===================== 私有字段 =====================

        #region --------------------- 同步控制 ---------------------

        /// <summary> 是否正在内部同步（Parse 或 Compose），用于阻断 Parse ↔ Compose 循环 </summary>
        private bool _isUpdating;

        /// <summary> 协议/数据类型未就绪时暂存的外部地址，待 OnInputsReady 后再 Parse </summary>
        private string? _pendingExternalAddress;

        #endregion

        #region --------------------- OPCUA协议 ---------------------

        private bool _isOPCUAVisible; // OPCUA协议整体可见性
        private bool _isOPCUANodeRegionNameVisible; // OPCUA协议节点区域名称可见性

        #endregion

        #region --------------------- S7协议 ---------------------

        private bool _isS7Visible; // S7协议整体可见性
        private bool _isS7StringVisible; // S7协议字符串数据类型可见性
        private bool _isS7OtherVisible; // S7协议其他数据类型可见性
        private bool _isS7OtherStartByteVisible; // S7协议其他数据类型起始字节的可见性
        private bool _isS7OtherStartBitVisible; // S7协议其他数据类型起始位的可见性
        private bool _isS7SerialNumberVisible; // S7协议编号可见性
        private bool _defaultLengthIsChecked; // S7协议字符串类型的默认长度勾选框

        private string _regionSelectedItem = "DB"; // 区域集合选择项
        private string _reagionSerialNumber = string.Empty; // S7协议区域编号
        private string _stringTypeStartAddress = string.Empty; // S7协议字符串类型的起始地址

        #endregion

        private string _addressPreview = string.Empty; // 预览地址
        private string _stringTypeLength = "1"; // S7协议字符串类型的长度
        private string _otherTypeStartByte = string.Empty; // S7协议其他类型的起始字节
        private int _otherTypeStartBit; // S7协议其他类型的起始位

        #endregion

        #region ===================== 公共属性 =====================

        #region --------------------- OPCUA协议 ---------------------

        /// <summary> OPC 协议整体可见性 </summary>
        public bool IsOPCUAVisible
        {
            get => _isOPCUAVisible;
            set
            {
                _isOPCUAVisible = value; // 赋值可见性
                OnPropertyChanged(nameof(IsOPCUAVisible)); // 触发通知
            }
        }

        /// <summary> OPC 协议节点区域名称可见性 </summary>
        public bool IsOPCUANodeRegionNameVisible
        {
            get => _isOPCUANodeRegionNameVisible;
            set
            {
                _isOPCUANodeRegionNameVisible = value; // 赋值可见性
                OnPropertyChanged(nameof(IsOPCUANodeRegionNameVisible)); // 触发通知
            }
        }

        /// <summary> OPC 协议节点区域名称 </summary>
        public string NodeRegionName { get; set; } = string.Empty;

        /// <summary> OPC 协议节点数据名称 </summary>
        public string NodeDataName { get; set; } = string.Empty;

        #endregion

        #region --------------------- S7协议 ---------------------

        /// <summary> S7 协议整体可见性 </summary>
        public bool IsS7Visible
        {
            get => _isS7Visible;
            set
            {
                _isS7Visible = value; // 赋值可见性
                OnPropertyChanged(nameof(IsS7Visible)); // 触发通知
            }
        }

        /// <summary> S7 协议字符串类型可见性 </summary>
        public bool IsS7StringVisible
        {
            get => _isS7StringVisible;
            set
            {
                _isS7StringVisible = value; // 赋值可见性
                OnPropertyChanged(nameof(IsS7StringVisible)); // 触发通知
            }
        }

        /// <summary> S7 协议其他类型可见性 </summary>
        public bool IsS7OtherVisible
        {
            get => _isS7OtherVisible;
            set
            {
                _isS7OtherVisible = value; // 赋值可见性
                OnPropertyChanged(nameof(IsS7OtherVisible)); // 触发通知
            }
        }

        /// <summary> S7 协议其他类型起始字节可见性 </summary>
        public bool IsS7OtherStartByteVisible
        {
            get => _isS7OtherStartByteVisible;
            set
            {
                _isS7OtherStartByteVisible = value; // 赋值可见性
                OnPropertyChanged(nameof(IsS7OtherStartByteVisible)); // 触发通知
            }
        }

        /// <summary> S7 协议其他类型起始位可见性 </summary>
        public bool IsS7OtherStartBitVisible
        {
            get => _isS7OtherStartBitVisible;
            set
            {
                _isS7OtherStartBitVisible = value; // 赋值可见性
                OnPropertyChanged(nameof(IsS7OtherStartBitVisible)); // 触发通知
            }
        }

        /// <summary> S7 协议编号可见性 </summary>
        public bool IsS7SerialNumberVisible
        {
            get => _isS7SerialNumberVisible;
            set
            {
                _isS7SerialNumberVisible = value; // 赋值可见性
                OnPropertyChanged(nameof(IsS7SerialNumberVisible)); // 触发通知
            }
        }

        /// <summary> S7 协议区域选择项，变更时刷新可见性并重新合并地址 </summary>
        public string RegionSelectedItem
        {
            get => _regionSelectedItem;
            set
            {
                _regionSelectedItem = value; // 赋值区域符号
                OnPropertyChanged(nameof(RegionSelectedItem)); // 通知 ComboBox 刷新
                ControlVisibility(); // 更新控件可见性
                if (!_isUpdating) UpdataAddressPreview(); // 非 Parse 期间才 Compose 输出
            }
        }

        /// <summary> S7 协议区域符号只读集合 </summary>
        public IReadOnlyList<string> RegionTypes { get; set; } = new List<string>()
        {
            "I",
            "Q",
            "M",
            "T",
            "DB",
        };

        /// <summary> S7 协议区域编号（DB 块号） </summary>
        public string ReagionSerialNumber
        {
            get => _reagionSerialNumber;
            set
            {
                _reagionSerialNumber = value; // 赋值 DB 编号
                OnPropertyChanged(nameof(ReagionSerialNumber)); // 通知文本框刷新
                if (!_isUpdating) UpdataAddressPreview(); // 非 Parse 期间才 Compose 输出
            }
        }

        /// <summary> S7 协议字符串类型的起始地址 </summary>
        public string StringTypeStartAddress
        {
            get => _stringTypeStartAddress;
            set
            {
                _stringTypeStartAddress = value; // 赋值起始地址
                OnPropertyChanged(nameof(StringTypeStartAddress)); // 通知文本框刷新
                if (!_isUpdating) UpdataAddressPreview(); // 非 Parse 期间才 Compose 输出
            }
        }

        /// <summary> S7 协议字符串类型的长度 </summary>
        public string StringTypeLength
        {
            get => _stringTypeLength;
            set
            {
                _stringTypeLength = value; // 赋值长度
                OnPropertyChanged(nameof(StringTypeLength)); // 通知文本框刷新
                if (!_isUpdating) UpdataAddressPreview(); // 非 Parse 期间才 Compose 输出
            }
        }

        /// <summary> S7 协议其他类型的起始字节 </summary>
        public string OtherTypeStartByte
        {
            get => _otherTypeStartByte;
            set
            {
                _otherTypeStartByte = value; // 赋值起始字节
                OnPropertyChanged(nameof(OtherTypeStartByte)); // 通知文本框刷新
                if (!_isUpdating) UpdataAddressPreview(); // 非 Parse 期间才 Compose 输出
            }
        }

        /// <summary> S7 协议其他类型的起始位（bool 专用） </summary>
        public int OtherTypeStartBit
        {
            get => _otherTypeStartBit;
            set
            {
                _otherTypeStartBit = value; // 赋值起始位
                OnPropertyChanged(nameof(OtherTypeStartBit)); // 通知 NumericUpDown 刷新
                if (!_isUpdating) UpdataAddressPreview(); // 非 Parse 期间才 Compose 输出
            }
        }

        /// <summary> S7 协议字符串类型长度文本框是否可用 </summary>
        public bool StringTypeLengthEnable { get; set; } = true;

        /// <summary> S7 协议字符串默认长度（256）勾选框 </summary>
        public bool DefaultLengthIsChecked
        {
            get => _defaultLengthIsChecked;
            set
            {
                _defaultLengthIsChecked = value; // 赋值勾选状态
                if (value) // 勾选默认长度
                {
                    _stringTypeLength = "256"; // 写入默认长度
                    StringTypeLengthEnable = false; // 禁用长度输入
                }
                else // 取消默认长度
                {
                    _stringTypeLength = string.Empty; // 清空长度
                    StringTypeLengthEnable = true; // 启用长度输入
                }
                OnPropertyChanged(nameof(StringTypeLength)); // 通知长度文本刷新
                OnPropertyChanged(nameof(StringTypeLengthEnable)); // 通知使能状态刷新
                if (!_isUpdating) UpdataAddressPreview(); // 非 Parse 期间才 Compose 输出
            }
        }

        #endregion

        /// <summary> 地址预览文本，仅用于 UI 展示 </summary>
        public string AddressPreview
        {
            get => _addressPreview;
            set
            {
                _addressPreview = value; // 赋值预览文本
                OnPropertyChanged(nameof(AddressPreview)); // 通知预览区刷新
            }
        }

        #endregion

        #region ===================== 私有方法 =====================

        #region --------------------- 外部地址同步（编辑） ---------------------

        /// <summary> 协议/数据类型就绪后：刷新可见性，处理暂存地址或 Compose </summary>
        private void OnInputsReady()
        {
            ControlVisibility(); // 按协议和数据类型刷新 UI 可见性
            if (_pendingExternalAddress != null) // 存在编辑时暂存的外部地址
            {
                TryApplyExternalAddress(_pendingExternalAddress); // 重新尝试 Parse
                return;
            }
            if (!_isUpdating) UpdataAddressPreview(); // 新增场景下按当前字段 Compose
        }

        /// <summary> 尝试将外部地址 Parse 到内部字段；协议/类型未就绪则暂存 </summary>
        private void TryApplyExternalAddress(string? address)
        {
            if (string.IsNullOrWhiteSpace(address)) return; // 空地址无需 Parse
            if (string.IsNullOrEmpty(ProtocolType) || string.IsNullOrEmpty(DataType)) // 编辑打开时协议/类型可能晚于地址绑定
            {
                _pendingExternalAddress = address; // 暂存，等 OnInputsReady 再 Parse
                return;
            }
            _pendingExternalAddress = null; // 清除暂存
            ApplyExternalAddress(address); // 执行 Parse
        }

        /// <summary> 将外部地址 Parse 到内部字段并刷新预览（不触发 Compose 回写 OutAddress） </summary>
        private void ApplyExternalAddress(string address)
        {
            _isUpdating = true; // 开启同步守卫，阻断 Parse ↔ Compose 循环
            try
            {
                ParseToFields(address); // 按协议解析地址到子字段
                ControlVisibility(); // Parse 后按新区域/类型刷新可见性
                _addressPreview = address; // 预览直接使用外部传入的完整地址
                OnPropertyChanged(nameof(AddressPreview)); // 通知预览区刷新
                ApplyStringLengthFromExternal(); // 编辑 S7String 时同步 DataLen 到长度 UI
            }
            finally
            {
                _isUpdating = false; // 关闭同步守卫
            }
        }

        /// <summary> 将 OutDataLength 同步到字符串长度 UI（编辑模式 DataLen 绑定晚于地址时使用） </summary>
        private void ApplyStringLengthFromExternal()
        {
            if (DataType != DeviceDataTypeConstants.SS7String) return; // 仅 S7String 需要
            if (OutDataLength <= 0) return; // 无效长度跳过
            if (OutDataLength == 256) // 默认长度
            {
                _defaultLengthIsChecked = true; // 勾选默认长度
                _stringTypeLength = "256"; // 写入 256
                StringTypeLengthEnable = false; // 禁用输入
            }
            else // 自定义长度
            {
                _defaultLengthIsChecked = false; // 取消默认长度
                _stringTypeLength = OutDataLength.ToString(); // 写入实际长度
                StringTypeLengthEnable = true; // 启用输入
            }
            OnPropertyChanged(nameof(DefaultLengthIsChecked)); // 通知勾选框刷新
            OnPropertyChanged(nameof(StringTypeLength)); // 通知长度文本刷新
            OnPropertyChanged(nameof(StringTypeLengthEnable)); // 通知使能状态刷新
        }

        /// <summary> 按协议将地址字符串 Parse 到内部字段 </summary>
        private void ParseToFields(string address)
        {
            if (ProtocolType == DeviceProtocolTypeConstants.S7) // S7 协议
            {
                ParseS7ToFields(address); // 解析 S7 地址
                return;
            }
            // OPC UA 等协议 Parse 待后续实现
        }

        #endregion

        #region --------------------- 内部地址输出（新增/编辑共用） ---------------------

        /// <summary> 将 Compose 结果统一写回 OutAddress、OutDataLength 和预览 </summary>
        private void PublishOutput(string address, int dataLength)
        {
            _isUpdating = true; // 开启同步守卫，避免写 OutAddress 时再次触发 Parse
            try
            {
                _addressPreview = address; // 更新预览字段
                OnPropertyChanged(nameof(AddressPreview)); // 通知预览区刷新
                OutAddress = address; // 写回外部绑定属性
                OutDataLength = dataLength; // 写回外部长度绑定
            }
            finally
            {
                _isUpdating = false; // 关闭同步守卫
            }
        }

        /// <summary> 根据内部字段 Compose 地址并输出到外部 </summary>
        private void UpdataAddressPreview()
        {
            if (_isUpdating) return; // Parse 期间不 Compose
            if (IsS7Visible) // S7 协议
            {
                var address = S7AddressMerge(out int dataLength); // 合并 S7 地址
                PublishOutput(address, dataLength); // 统一写回外部
                return;
            }
            // OPC UA 等协议 Compose 待后续实现
        }

        #endregion

        #region --------------------- 控件可见性控制 ---------------------

        /// <summary> 按 ProtocolType、DataType、RegionSelectedItem 控制各区域 UI 可见性 </summary>
        private void ControlVisibility()
        {
            if (string.IsNullOrEmpty(ProtocolType) || string.IsNullOrEmpty(DataType))// 无协议类型或无数据类型隐藏所有控件并返回
            {
                IsOPCUAVisible = false; // 隐藏 OPC UA 区域
                IsS7Visible = false;//隐藏S7协议
                return;
            }

            if (ProtocolType == DeviceProtocolTypeConstants.S7) // S7 协议
            {
                IsOPCUAVisible = false; // 隐藏 OPC UA 区域
                IsS7Visible = true; // 显示 S7 区域

                switch (RegionSelectedItem)
                {
                    case string r when r == "I" || r == "Q" || r == "M": // I/Q/M 区
                        IsS7SerialNumberVisible = false; // 隐藏 DB 编号
                        S7ControlVisibility(RegionSelectedItem); // 按数据类型显示子控件
                        break;

                    case "DB": // DB 区
                        IsS7SerialNumberVisible = true; // 显示 DB 编号
                        S7ControlVisibility(RegionSelectedItem); // 按数据类型显示子控件
                        break;

                    case "T": // Timer 区
                        IsS7SerialNumberVisible = false; // 隐藏 DB 编号
                        if (DataType == DeviceDataTypeConstants.STimer) // 仅 Timer 类型可用
                        {
                            IsS7OtherVisible = true; // 显示其他类型区
                            IsS7OtherStartByteVisible = true; // 显示起始字节
                            IsS7OtherStartBitVisible = false; // 隐藏起始位
                        }
                        else // 非 Timer 隐藏录入区
                        {
                            IsS7StringVisible = false; // 隐藏 String 区
                            IsS7OtherVisible = false; // 隐藏其他类型区
                        }
                        break;

                    default:
                        IsS7SerialNumberVisible = false; // 默认隐藏 DB 编号
                        break;
                }
            }

            if (ProtocolType == DeviceProtocolTypeConstants.OPCUA) // OPC UA 协议
            {

                IsOPCUANodeRegionNameVisible = RegionSelectedItem == "DB"; // DB 区显示节点区域名
            }
        }

        /// <summary> S7 协议下按数据类型和区域控制子控件可见性 </summary>
        private void S7ControlVisibility(string region)
        {
            switch (DataType)
            {
                case DeviceDataTypeConstants.SBool: // Bool
                    IsS7OtherVisible = true; // 显示其他类型区
                    IsS7OtherStartByteVisible = true; // 显示起始字节
                    IsS7OtherStartBitVisible = true; // 显示起始位
                    IsS7StringVisible = false; // 隐藏 String 区
                    break;

                case DeviceDataTypeConstants.SS7String: // S7String
                    if (region == "DB") // 仅 DB 区支持
                    {
                        IsS7OtherVisible = false; // 隐藏其他类型区
                        IsS7StringVisible = true; // 显示 String 区
                    }
                    else
                    {
                        IsS7StringVisible = false; // 隐藏 String 区
                        IsS7OtherVisible = false; // 隐藏其他类型区
                    }
                    break;

                case string s when s == DeviceDataTypeConstants.STimer ||
                                   s == DeviceDataTypeConstants.SDateTime: // Timer / DateTime 暂不支持录入
                    IsS7StringVisible = false; // 隐藏 String 区
                    IsS7OtherVisible = false; // 隐藏其他类型区
                    IsS7SerialNumberVisible = false; // 隐藏 DB 编号
                    break;

                case string s when s == DeviceDataTypeConstants.SByte ||
                                   s == DeviceDataTypeConstants.SInt ||
                                   s == DeviceDataTypeConstants.SDInt ||
                                   s == DeviceDataTypeConstants.SWord ||
                                   s == DeviceDataTypeConstants.SReal: // 字节/字/双字类型
                    IsS7OtherVisible = true; // 显示其他类型区
                    IsS7OtherStartByteVisible = true; // 显示起始字节
                    IsS7OtherStartBitVisible = false; // 隐藏起始位
                    IsS7StringVisible = false; // 隐藏 String 区
                    break;

                default:
                    IsS7StringVisible = false; // 隐藏 String 区
                    IsS7OtherVisible = false; // 隐藏其他类型区
                    IsS7SerialNumberVisible = false; // 隐藏 DB 编号
                    break;
            }
        }

        #endregion

        #region --------------------- S7 地址 Compose（内部 → 字符串） ---------------------

        /// <summary> 将 S7 内部字段合并为地址字符串 </summary>
        private string S7AddressMerge(out int dataLength)
        {
            dataLength = 1; // 默认长度为 1
            string address = string.Empty; // 合并结果

            switch (RegionSelectedItem)
            {
                case string r when r == "I" || r == "Q" || r == "M": // I/Q/M 区
                    address = AddSuffix(DataType, r); // 添加类型后缀（B/W/D 或 bool 无后缀）
                    if (string.IsNullOrEmpty(OtherTypeStartByte) || OtherTypeStartBit < 0 || OtherTypeStartBit > 7)
                        return address; // 偏移未填完整则返回前缀
                    return MergeOffsetAddress(DataType, address, OtherTypeStartByte, OtherTypeStartBit.ToString()); // 合并偏移

                case "T": // Timer 区
                    if (DataType != DeviceDataTypeConstants.STimer) return string.Empty; // 非 Timer 返回空
                    address = RegionSelectedItem; // 区域符号 T
                    if (string.IsNullOrEmpty(OtherTypeStartByte)) return address; // 无起始字节则只返回 T
                    return address + OtherTypeStartByte; // T + 起始字节

                case "DB": // DB 区
                    if (string.IsNullOrEmpty(ReagionSerialNumber)) return RegionSelectedItem; // 无 DB 号则只返回 DB
                    if (DataType == DeviceDataTypeConstants.SS7String) // S7String 专用格式
                    {
                        address = "DB=" + ReagionSerialNumber + ","; // DB=11,
                        if (IsS7StringVisible && string.IsNullOrEmpty(StringTypeStartAddress))
                            return address; // 无起始地址则返回前缀
                        dataLength = int.TryParse(StringTypeLength, out int len) ? len : 1; // 解析字符串长度
                        return address + "StartAddress=" + StringTypeStartAddress; // DB=11,StartAddress=54
                    }
                    else // DB 其他类型
                    {
                        address = "DB" + ReagionSerialNumber + "."; // DB1.
                        address += AddSuffix(DataType, "DB"); // DBX / DBB / DBW / DBD
                        if (IsS7OtherVisible && (string.IsNullOrEmpty(OtherTypeStartByte) ||
                            (IsS7OtherStartBitVisible && (OtherTypeStartBit > 7 || OtherTypeStartBit < 0))))
                            return address; // 偏移未填完整则返回前缀
                        return MergeOffsetAddress(DataType, address, OtherTypeStartByte, OtherTypeStartBit.ToString()); // 合并偏移
                    }

                default:
                    return string.Empty; // 未知区域
            }
        }

        /// <summary> 按数据类型返回 S7 地址后缀符号 </summary>
        private string AddSuffix(string dataType, string region)
        {
            switch (dataType)
            {
                case DeviceDataTypeConstants.SByte:
                    return region + "B"; // 字节：IB / DBB
                case DeviceDataTypeConstants.SInt:
                case DeviceDataTypeConstants.SWord:
                    return region + "W"; // 字：IW / DBW
                case DeviceDataTypeConstants.SDInt:
                case DeviceDataTypeConstants.SReal:
                    return region + "D"; // 双字：ID / DBD
                case DeviceDataTypeConstants.SBool:
                    if (region != "DB") return region; // I/Q/M 的 bool 无 X 后缀
                    return region + "X"; // DBX
                case DeviceDataTypeConstants.SS7String:
                    if (region != "DB") return string.Empty; // String 仅 DB
                    return region + "B"; // DBB（String 合并时用 DB= 格式，此处保留兼容）
                default:
                    return string.Empty; // 不支持的类型
            }
        }

        /// <summary> 将前缀地址与字节/位偏移合并 </summary>
        private string MergeOffsetAddress(string dataType, string address, string offsetByte, string offsetBit)
        {
            switch (dataType)
            {
                case DeviceDataTypeConstants.SBool:
                    return $"{address}{offsetByte}.{offsetBit}"; // M0.5 / DB1.DBX6.2
                case string s when s == DeviceDataTypeConstants.SByte ||
                                   s == DeviceDataTypeConstants.SInt ||
                                   s == DeviceDataTypeConstants.SDInt ||
                                   s == DeviceDataTypeConstants.SReal ||
                                   s == DeviceDataTypeConstants.SWord ||
                                   s == DeviceDataTypeConstants.STimer:
                    return $"{address}{offsetByte}"; // IB5 / DB1.DBB0
                case DeviceDataTypeConstants.SS7String:
                    return $"{address}{offsetByte}"; // 兼容路径
                default:
                    return string.Empty; // 不支持的类型
            }
        }

        #endregion

        #region --------------------- S7 地址 Parse（字符串 → 内部字段） ---------------------

        /// <summary> 将 S7 地址字符串 Parse 到内部字段（编辑模式） </summary>
        private void ParseS7ToFields(string address)
        {
            address = address.Trim(); // 去首尾空格
            if (string.IsNullOrEmpty(address)) return; // 空地址退出

            if (address.StartsWith("DB=", System.StringComparison.OrdinalIgnoreCase)) // S7String：DB=11,StartAddress=54
            {
                ParseS7StringAddress(address); // 解析 String 格式
                return;
            }

            if (address.StartsWith("DB", System.StringComparison.OrdinalIgnoreCase)) // DB 块：DB1.DBX6.2
            {
                ParseS7DbAddress(address); // 解析 DB 格式
                return;
            }

            if (address.StartsWith("T", System.StringComparison.OrdinalIgnoreCase)) // Timer：T5
            {
                ParseS7TimerAddress(address); // 解析 Timer 格式
                return;
            }

            if (address.StartsWith("I") || address.StartsWith("Q") || address.StartsWith("M")) // I/Q/M 区
            {
                ParseS7IqmAddress(address); // 解析 IQM 格式
            }
        }

        /// <summary> 解析 S7String 地址：DB=11,StartAddress=54 </summary>
        private void ParseS7StringAddress(string address)
        {
            string[] parts = address.Split(','); // 按逗号分组
            if (parts.Length != 2) return; // 格式不符则退出

            string dbPart = parts[0].Replace("DB=", string.Empty, System.StringComparison.OrdinalIgnoreCase).Trim(); // 提取 DB 号
            string startPart = parts[1].Replace("StartAddress=", string.Empty, System.StringComparison.OrdinalIgnoreCase).Trim(); // 提取起始地址

            _regionSelectedItem = "DB"; // 设置区域为 DB
            _reagionSerialNumber = dbPart; // 设置 DB 编号
            _stringTypeStartAddress = startPart; // 设置起始地址
            _otherTypeStartByte = string.Empty; // 清空其他类型字节
            _otherTypeStartBit = 0; // 清空起始位

            NotifyS7FieldProperties(); // 批量通知 UI 刷新
        }

        /// <summary> 解析 DB 块地址：DB1.DBX6.2 / DB1.DBB0 / DB1.DBW0 / DB1.DBD0 </summary>
        private void ParseS7DbAddress(string address)
        {
            int dotIndex = address.IndexOf('.'); // 找 DB 号与后缀分隔点
            if (dotIndex <= 2) return; // 至少 DB + 数字 + .

            string dbNumber = address.Substring(2, dotIndex - 2); // 提取 DB 号
            string remaining = address.Substring(dotIndex + 1); // DBX6.2 等

            _regionSelectedItem = "DB"; // 设置区域为 DB
            _reagionSerialNumber = dbNumber; // 设置 DB 编号

            if (remaining.StartsWith("DBX", System.StringComparison.OrdinalIgnoreCase)) // Bool：DBX6.2
            {
                string offsetPart = remaining.Substring(3); // 6.2
                int bitDot = offsetPart.IndexOf('.'); // 字节与位分隔点
                if (bitDot <= 0) return; // 格式不符
                _otherTypeStartByte = offsetPart.Substring(0, bitDot); // 起始字节
                _ = int.TryParse(offsetPart.Substring(bitDot + 1), out _otherTypeStartBit); // 起始位
            }
            else if (remaining.StartsWith("DBB", System.StringComparison.OrdinalIgnoreCase)) // Byte
            {
                _otherTypeStartByte = remaining.Substring(3); // 起始字节
                _otherTypeStartBit = 0; // 非 bool 位为 0
            }
            else if (remaining.StartsWith("DBW", System.StringComparison.OrdinalIgnoreCase)) // Word/Int
            {
                _otherTypeStartByte = remaining.Substring(3); // 起始字节
                _otherTypeStartBit = 0; // 非 bool 位为 0
            }
            else if (remaining.StartsWith("DBD", System.StringComparison.OrdinalIgnoreCase)) // DInt/Real
            {
                _otherTypeStartByte = remaining.Substring(3); // 起始字节
                _otherTypeStartBit = 0; // 非 bool 位为 0
            }

            _stringTypeStartAddress = string.Empty; // 清空 String 起始地址
            NotifyS7FieldProperties(); // 批量通知 UI 刷新
        }

        /// <summary> 解析 Timer 地址：T5 </summary>
        private void ParseS7TimerAddress(string address)
        {
            _regionSelectedItem = "T"; // 设置区域为 T
            _otherTypeStartByte = address.Substring(1); // 去掉 T 后的数字
            _otherTypeStartBit = 0; // Timer 无起始位
            _reagionSerialNumber = string.Empty; // 清空 DB 编号
            _stringTypeStartAddress = string.Empty; // 清空 String 起始地址
            NotifyS7FieldProperties(); // 批量通知 UI 刷新
        }

        /// <summary> 解析 I/Q/M 区地址：M0.5 / IB5 / IW10 / ID20 </summary>
        private void ParseS7IqmAddress(string address)
        {
            _regionSelectedItem = address.Substring(0, 1); // 首字符为区域 I/Q/M
            string rest = address.Substring(1); // 剩余部分

            if (rest.Contains('.')) // Bool：M0.5
            {
                int dotIndex = rest.IndexOf('.'); // 字节与位分隔点
                _otherTypeStartByte = rest.Substring(0, dotIndex); // 起始字节
                _ = int.TryParse(rest.Substring(dotIndex + 1), out _otherTypeStartBit); // 起始位
            }
            else if (rest.StartsWith("B", System.StringComparison.OrdinalIgnoreCase)) // Byte：IB5
            {
                _otherTypeStartByte = rest.Substring(1); // 起始字节
                _otherTypeStartBit = 0; // 非 bool 位为 0
            }
            else if (rest.StartsWith("W", System.StringComparison.OrdinalIgnoreCase)) // Word：IW10
            {
                _otherTypeStartByte = rest.Substring(1); // 起始字节
                _otherTypeStartBit = 0; // 非 bool 位为 0
            }
            else if (rest.StartsWith("D", System.StringComparison.OrdinalIgnoreCase)) // DInt/Real：ID20
            {
                _otherTypeStartByte = rest.Substring(1); // 起始字节
                _otherTypeStartBit = 0; // 非 bool 位为 0
            }

            _reagionSerialNumber = string.Empty; // I/Q/M 无 DB 编号
            _stringTypeStartAddress = string.Empty; // 清空 String 起始地址
            NotifyS7FieldProperties(); // 批量通知 UI 刷新
        }

        /// <summary> Parse 完成后批量通知 S7 子字段 UI 刷新 </summary>
        private void NotifyS7FieldProperties()
        {
            OnPropertyChanged(nameof(RegionSelectedItem)); // 通知区域 ComboBox
            OnPropertyChanged(nameof(ReagionSerialNumber)); // 通知 DB 编号
            OnPropertyChanged(nameof(StringTypeStartAddress)); // 通知 String 起始地址
            OnPropertyChanged(nameof(OtherTypeStartByte)); // 通知起始字节
            OnPropertyChanged(nameof(OtherTypeStartBit)); // 通知起始位
        }

        #endregion

        #endregion
    }
}
