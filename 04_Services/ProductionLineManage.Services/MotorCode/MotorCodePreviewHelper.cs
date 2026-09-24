using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.MotorCode;

namespace ProductionLineManage.Services.MotorCode
{
    /// <summary>
    /// 电机码规则片段数据值预览：UI 配置页与生成服务共用，
    /// 按片段类型解析年月日/序列/固定信息的展示值。
    /// </summary>
    public static class MotorCodePreviewHelper
    {
        #region ===================== 片段预览 =====================

        /// <summary>
        /// 预览单个片段在当前参考时间下的取值（不分配真实序列号）。
        /// </summary>
        public static string PreviewSegmentValue(
            MotorCodeCacheSnapshot snapshot,
            int productTypeId,
            craft_MotorCodeSegmentBind bind,
            DateTime referenceTime,
            int simulateSequenceValue,
            int sequenceDigitLength)
        {
            var type = (MotorCodeSegmentType)bind.SegmentType;
            var dateMaps = snapshot.DateMaps.Where(m => m.ProductTypeId == productTypeId).ToList(); // 该型号日期映射
            var fixedSegments = snapshot.FixedSegments.Where(f => f.ProductTypeId == productTypeId).ToList(); // 该型号固定片段

            return type switch
            {
                MotorCodeSegmentType.Year => PreviewDate(dateMaps, MotorCodeMapType.Year, referenceTime.Year),
                MotorCodeSegmentType.Month => PreviewDate(dateMaps, MotorCodeMapType.Month, referenceTime.Month),
                MotorCodeSegmentType.Day => PreviewDate(dateMaps, MotorCodeMapType.Day, referenceTime.Day),
                MotorCodeSegmentType.Sequence => FormatSequence(simulateSequenceValue, sequenceDigitLength), // 模拟序列值
                MotorCodeSegmentType.Fixed => PreviewFixed(fixedSegments, bind.FixedSegmentId),
                _ => "-" // 未知片段类型
            };
        }

        #endregion

        #region ===================== 内部辅助 =====================

        /// <summary> 预览日期映射片段 </summary>
        private static string PreviewDate(List<craft_MotorCodeDateMap> maps, MotorCodeMapType mapType, int key)
        {
            var item = maps.FirstOrDefault(m => m.MapType == (int)mapType && m.MapKey == key);
            return string.IsNullOrWhiteSpace(item?.MapCode) ? "(未配置)" : item!.MapCode;
        }

        /// <summary> 预览固定信息片段 </summary>
        private static string PreviewFixed(List<craft_MotorCodeFixedSegment> segments, int fixedSegmentId)
        {
            if (fixedSegmentId <= 0)
                return "(请选择固定信息)";

            var seg = segments.FirstOrDefault(s => s.Id == fixedSegmentId);
            return string.IsNullOrWhiteSpace(seg?.FixedValue) ? "(未配置)" : seg!.FixedValue;
        }

        /// <summary> 格式化序列号为固定位数 </summary>
        private static string FormatSequence(int value, int digitLength)
        {
            if (digitLength <= 0) digitLength = 4; // 默认 4 位
            return value.ToString($"D{digitLength}");
        }

        #endregion
    }
}
