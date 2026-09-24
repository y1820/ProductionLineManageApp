using ProductionLineManage.Core.Enums;
using System.Globalization;

namespace ProductionLineManage.Services.MotorCode
{
    /// <summary>
    /// 电机码序列复位桶 Key 计算：按复位周期（日/周/月/年/永不）生成唯一标识，
    /// 用于 craft_MotorCodeSequenceState.BucketKey 判断是否跨周期重置序列。
    /// </summary>
    internal static class MotorCodeBucketHelper
    {
        #region ===================== 桶 Key 生成 =====================

        /// <summary> 根据复位周期与参考时间生成桶 Key </summary>
        public static string BuildBucketKey(MotorCodeResetCycle cycle, DateTime time)
        {
            return cycle switch
            {
                MotorCodeResetCycle.Daily => $"D:{time:yyyyMMdd}", // 按自然日
                MotorCodeResetCycle.Weekly => $"W:{GetWeekMonday(time):yyyyMMdd}", // 按所在周周一
                MotorCodeResetCycle.Monthly => $"M:{time:yyyyMM}", // 按自然月
                MotorCodeResetCycle.Yearly => $"Y:{time:yyyy}", // 按自然年
                MotorCodeResetCycle.Never => "GLOBAL", // 永不复位
                _ => "GLOBAL" // 未知周期兜底
            };
        }

        #endregion

        #region ===================== 内部辅助 =====================

        /// <summary> 所在自然周的周一日期（按本地日历） </summary>
        private static DateTime GetWeekMonday(DateTime time)
        {
            var day = time.Date; // 取日期部分，忽略时分秒
            int diff = (7 + (day.DayOfWeek - DayOfWeek.Monday)) % 7; // 距周一的天数
            return day.AddDays(-diff); // 回退到本周一
        }

        #endregion
    }
}
