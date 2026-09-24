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
    public class AddStationMaterialViewModel : BindableBase, IDialogAware
    {
        private readonly IRepository<material_Station> _repo;

        // 全量数据（从主界面传入）
        private List<craft_TypeInfo> _allTypes = new List<craft_TypeInfo>();
        private List<craft_LineInfo> _allLines = new List<craft_LineInfo>();
        private List<craft_StationInfo> _allStations = new List<craft_StationInfo>();
        private List<material_Info> _allMaterials = new List<material_Info>();
        private List<material_Station> _allStationMaterials = new List<material_Station>();


        // 界面绑定数据
        private List<craft_TypeInfo> _types = new List<craft_TypeInfo>();
        private List<craft_LineInfo> _lines = new List<craft_LineInfo>();
        private List<craft_StationInfo> _stations = new List<craft_StationInfo>();
        private List<material_Info> _materials = new List<material_Info>();
        private List<craft_StationInfo> _checkStations = new List<craft_StationInfo>();
        private List<material_Info> _parentMaterials = new List<material_Info>();

        // 选中项
        private int _typeSelectIndex = -1;
        private craft_TypeInfo _typeSelectItem = new craft_TypeInfo();
        private int _lineSelectIndex = -1;
        private craft_LineInfo _lineSelectItem = new craft_LineInfo();
        private int _stationSelectIndex = -1;
        private craft_StationInfo _stationSelectItem = new craft_StationInfo();
        private int _materialSelectIndex = -1;
        private material_Info _materialSelectItem = new material_Info();
        private int _checkStationSelectIndex = -1;
        private craft_StationInfo _checkStationSelectItem = new craft_StationInfo();
        private int _parentMaterialSelectIndex = -1;
        private material_Info _parentMaterialSelectItem = new material_Info();

        // 表单字段
        private int _sequence = 0;
        private string _remarks = string.Empty;

        public AddStationMaterialViewModel(IRepository<material_Station> repo)
        {
            _repo = repo;
            AddCommand = new DelegateCommand(OnAdd);
            CancelCommand = new DelegateCommand(OnCancel);
        }

        #region ===================== 公共属性 =====================

        public List<craft_TypeInfo> Types
        {
            get => _types;
            set => SetProperty(ref _types, value);
        }

        public List<craft_LineInfo> Lines
        {
            get => _lines;
            set => SetProperty(ref _lines, value);
        }

        public List<craft_StationInfo> Stations
        {
            get => _stations;
            set => SetProperty(ref _stations, value);
        }

        public List<material_Info> Materials
        {
            get => _materials;
            set => SetProperty(ref _materials, value);
        }

        public List<craft_StationInfo> CheckStations
        {
            get => _checkStations;
            set => SetProperty(ref _checkStations, value);
        }

        public List<material_Info> ParentMaterials
        {
            get => _parentMaterials;
            set => SetProperty(ref _parentMaterials, value);
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

                // 型号变化时，筛选物料和父物料
                FilterMaterialsByType(value.Id);
                RaisePropertyChanged();
            }
        }

        public int LineSelectIndex
        {
            get => _lineSelectIndex;
            set => SetProperty(ref _lineSelectIndex, value);
        }

        public craft_LineInfo LineSelectItem
        {
            get => _lineSelectItem;
            set
            {
                if (value == null) return;
                _lineSelectItem = value;
                LineSelectIndex = Lines.FindIndex(l => l.Id == value.Id);

                // 产线变化时，筛选工位和合格检验工位
                FilterStationsByLine(value.Id);
                RaisePropertyChanged();
            }
        }

        public int StationSelectIndex
        {
            get => _stationSelectIndex;
            set => SetProperty(ref _stationSelectIndex, value);
        }

        public craft_StationInfo StationSelectItem
        {
            get => _stationSelectItem;
            set
            {
                if (value == null) return;
                _stationSelectItem = value;
                StationSelectIndex = Stations.FindIndex(s => s.Id == value.Id);
                RaisePropertyChanged();
            }
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

        public int CheckStationSelectIndex
        {
            get => _checkStationSelectIndex;
            set => SetProperty(ref _checkStationSelectIndex, value);
        }

        public craft_StationInfo CheckStationSelectItem
        {
            get => _checkStationSelectItem;
            set
            {
                if (value == null) return;
                _checkStationSelectItem = value;
                CheckStationSelectIndex = CheckStations.FindIndex(s => s.Id == value.Id);
                RaisePropertyChanged();
            }
        }

        public int ParentMaterialSelectIndex
        {
            get => _parentMaterialSelectIndex;
            set => SetProperty(ref _parentMaterialSelectIndex, value);
        }

        public material_Info ParentMaterialSelectItem
        {
            get => _parentMaterialSelectItem;
            set
            {
                if (value == null) return;
                _parentMaterialSelectItem = value;
                ParentMaterialSelectIndex = ParentMaterials.FindIndex(m => m.Id == value.Id);
                RaisePropertyChanged();
            }
        }

        public int Sequence
        {
            get => _sequence;
            set => SetProperty(ref _sequence, value);
        }

        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        #endregion

        #region ===================== 命令 =====================

        public DelegateCommand AddCommand { get; set; }
        public DelegateCommand CancelCommand { get; set; }

        #endregion

        #region ===================== IDialogAware =====================

        public string Title => "新增工位物料";

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
            _allLines = parameters.GetValue<List<craft_LineInfo>>("Lines") ?? new List<craft_LineInfo>();
            _allStations = parameters.GetValue<List<craft_StationInfo>>("Stations") ?? new List<craft_StationInfo>();
            _allMaterials = parameters.GetValue<List<material_Info>>("Materials") ?? new List<material_Info>();
            _allStationMaterials = parameters.GetValue<List<material_Station>>("AllStationMaterials") ?? new List<material_Station>();
            // 初始化下拉框（全量显示）
            Types = _allTypes;
            Lines = _allLines;

            // 工位、物料、合格检验工位、父物料初始为空，等待用户选择型号/产线后填充
            Stations = new List<craft_StationInfo>();
            Materials = new List<material_Info>();
            CheckStations = new List<craft_StationInfo>();
            ParentMaterials = new List<material_Info>();

            // 重置选中状态
            TypeSelectIndex = -1;
            LineSelectIndex = -1;
            StationSelectIndex = -1;
            MaterialSelectIndex = -1;
            CheckStationSelectIndex = -1;
            ParentMaterialSelectIndex = -1;
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary>
        /// 根据型号筛选物料和父物料
        /// </summary>
        private void FilterMaterialsByType(int typeId)
        {
            if (typeId <= 0)
            {
                Materials = new List<material_Info>();
                ParentMaterials = new List<material_Info>();
                return;
            }

            var filtered = _allMaterials.Where(m => m.TypeId == typeId).ToList();
            Materials = filtered;
            ParentMaterials = filtered;

            // 重置物料选中状态
            MaterialSelectIndex = -1;
            ParentMaterialSelectIndex = -1;
        }

        /// <summary>
        /// 根据产线筛选工位和合格检验工位
        /// </summary>
        private void FilterStationsByLine(int lineId)
        {
            if (lineId <= 0)
            {
                Stations = new List<craft_StationInfo>();
                CheckStations = new List<craft_StationInfo>();
                return;
            }

            var filtered = _allStations.Where(s => s.LineId == lineId).ToList();
            Stations = filtered;
            CheckStations = filtered;

            // 重置工位选中状态
            StationSelectIndex = -1;
            CheckStationSelectIndex = -1;
        }

        private bool IsDuplicate()
        {
            return _allStationMaterials.Any(s =>
                s.TypeId == TypeSelectItem.Id &&
                s.LineId == LineSelectItem.Id &&
                s.StationId == StationSelectItem.Id &&
                s.MaterialId == MaterialSelectItem.Id);
        }

        private void OnAdd()
        {
            // 验证必填项
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
            if (MaterialSelectIndex == -1)
            {
                HandyControl.Controls.MessageBox.Show("请选择物料", "提示");
                return;
            }
            if (IsDuplicate())
            {
                HandyControl.Controls.MessageBox.Show($"该工位已存在 \"{MaterialSelectItem.Name}\" 这个物料了", "提示");
                return;
            }

            try
            {
                var entity = new material_Station
                {
                    TypeId = TypeSelectItem.Id,
                    LineId = LineSelectItem.Id,
                    StationId = StationSelectItem.Id,
                    MaterialId = MaterialSelectItem.Id,
                    CheckMaterialStationId = CheckStationSelectItem?.Id ?? 0,
                    Sequence = Sequence,
                    ParentMaterialId = ParentMaterialSelectItem?.Id ?? 0,
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
                    parameters.Add("StationMaterial", entity);
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
