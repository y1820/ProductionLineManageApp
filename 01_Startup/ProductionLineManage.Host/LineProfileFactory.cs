using ProductionLineManage.Core.Abstractions;
using ProductionLineManage.Line.RLD19145.Profile;


namespace ProductionLineManage.Host
{
    /// <summary>
    /// 按 ActiveLine 创建协议包。未知代号先回退到 RLD19145，避免现场起不来。
    /// </summary>
    public static class LineProfileFactory
    {
        public static ILineProfile Create(string? activeLine)
        {
            var key = activeLine?.Trim() ?? string.Empty;
            if (string.Equals(key, "RLD19145", StringComparison.OrdinalIgnoreCase))
                return new Rld19145LineProfile();
            return new Rld19145LineProfile();
        }

        public static IRequestCodes CreateRequestCodes(string? activeLine)
        {
            var key = activeLine?.Trim() ?? string.Empty;
            if (string.Equals(key, "RLD19145", StringComparison.OrdinalIgnoreCase))
                return new Rld19145RequestCodes();

            return new Rld19145RequestCodes();
        }
    }

}
