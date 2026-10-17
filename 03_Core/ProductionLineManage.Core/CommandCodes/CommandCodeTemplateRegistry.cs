namespace ProductionLineManage.Core.CommandCodes
{
    /// <summary> 指令码模板登记表。新增模板：实现 ICommandCodeTemplate 并加入 All。 </summary>
    public static class CommandCodeTemplateRegistry
    {
        public static ICommandCodeTemplate Code100 { get; } = new Code100CommandTemplate();
        public static ICommandCodeTemplate Code12 { get; } = new Code12CommandTemplate();

        public static IReadOnlyList<ICommandCodeTemplate> All { get; } = new[]
        {
            Code100,
            Code12
        };

        public static ICommandCodeTemplate Default => Code100;

        /// <summary> 按工位配置键解析；空或未知则回退 Code100，兼容旧库行。 </summary>
        public static ICommandCodeTemplate Resolve(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Default;

            var match = All.FirstOrDefault(t =>
                string.Equals(t.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));
            return match ?? Default;
        }

        public static string GetDisplayName(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Default.DisplayName;
            return Resolve(key).DisplayName;
        }
    }
}
