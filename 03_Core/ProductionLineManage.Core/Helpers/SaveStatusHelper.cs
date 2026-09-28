using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Services.DeviceManager.Connection;

namespace ProductionLineManage.Core.Helpers
{
    /// <summary>
    /// 8000 保存时的产品工位状态解析：1=合格，2=不合格。
    /// 优先读地址映射「产品状态」，否则回退工件判定合格/不合格；均未配置或为假时默认合格。
    /// </summary>
    public static class SaveStatusHelper
    {
        #region ===================== 对外入口 =====================

        /// <summary> 从 PLC 地址映射读取过站 Status（1/2），供 8000 保存写入工位状态表 </summary>
        public static async Task<int> ReadAsync(IDeviceTaskContext context)
        {
            if (context.HasMapping(DataNameConstants.PrductStatus)) // 优先：映射名「产品状态」
            {
                var productStatus = await context.ReadAsync(DataNameConstants.PrductStatus);
                if (TryParseProductStatus(productStatus) is int parsed)
                    return parsed;
            }

            var nok = await context.ReadAsync(DataNameConstants.JudgeNOK); // 回退：工件判定不合格
            if (IsTruthy(nok))
                return 2;

            var ok = await context.ReadAsync(DataNameConstants.JudgeOK); // 回退：工件判定合格
            if (IsTruthy(ok))
                return 1;

            return 1; // 均未配置时默认合格
        }

        /// <summary> 工艺历史 IsQualified：优先采集项/映射「产品状态」，否则用过站 Status </summary>
        public static bool ResolveIsQualifiedForHistory(
            IEnumerable<(string DataName, string DataValue)> processRows,
            int fallbackStationStatus)
        {
            var productStatusValue = processRows
                .FirstOrDefault(r => string.Equals(r.DataName, DataNameConstants.PrductStatus, StringComparison.OrdinalIgnoreCase))
                .DataValue;

            if (!string.IsNullOrWhiteSpace(productStatusValue))
                return ToIsQualified(TryParseProductStatus(productStatusValue) ?? fallbackStationStatus);

            return ToIsQualified(fallbackStationStatus);
        }

        /// <summary> 过站 Status → IsQualified（1→true，其余→false） </summary>
        public static bool ToIsQualified(int stationStatus) => stationStatus == 1;

        #endregion

        #region ===================== 解析与判定 =====================

        /// <summary> PLC 产品状态：1=合格，2=不合格；其他数值默认合格 </summary>
        internal static int? TryParseProductStatus(object? value)
        {
            if (value == null)
                return null;

            if (value is bool b)
                return b ? 1 : 2;

            if (value is sbyte or byte or short or ushort or int or uint or long or ulong)
            {
                return Convert.ToInt32(value) switch
                {
                    2 => 2,
                    1 => 1,
                    _ => 1
                };
            }

            var text = value.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(text))
                return null;

            if (int.TryParse(text, out var number))
            {
                return number switch
                {
                    2 => 2,
                    1 => 1,
                    _ => 1
                };
            }

            if (text.Equals("不合格", StringComparison.OrdinalIgnoreCase)
                || text.Equals("NOK", StringComparison.OrdinalIgnoreCase))
                return 2;

            if (text.Equals("合格", StringComparison.OrdinalIgnoreCase)
                || text.Equals("OK", StringComparison.OrdinalIgnoreCase))
                return 1;

            return IsTruthy(value) ? 1 : null;
        }

        /// <summary> 将 PLC 布尔/字符串读数视为真 </summary>
        private static bool IsTruthy(object? value) =>
            value is bool b ? b : value?.ToString() is "True" or "true" or "1";

        #endregion
    }
}
