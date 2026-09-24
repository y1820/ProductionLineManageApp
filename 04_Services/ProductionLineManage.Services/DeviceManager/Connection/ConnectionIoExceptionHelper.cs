namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 连接层 I/O 异常分类：供 StationConnection、StationAcquisition 统一判断超时与传输断线。
    /// </summary>
    internal static class ConnectionIoExceptionHelper
    {
        #region ===================== 超时异常 =====================

        /// <summary> 判断异常链中是否包含 TimeoutException（读/写/心跳超时） </summary>
        public static bool IsIoTimeout(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is TimeoutException)
                    return true; // 命中超时
            }

            return false;
        }

        #endregion

        #region ===================== 传输异常 =====================

        /// <summary> 判断异常链中是否包含 IOException / SocketException（物理连接已断） </summary>
        public static bool IsTransport(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is System.IO.IOException or System.Net.Sockets.SocketException)
                    return true; // 命中传输层断线
            }

            return false;
        }

        #endregion
    }
}
