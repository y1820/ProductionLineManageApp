using System.ServiceModel;
using System.ServiceModel.Description;
using ProductionLineManage.Core.Configuration;
using ProductionLineManage.Core.Services.ExternalWeb;
using ProductionLineManage.Infrastructure.Logging;
using ProductionLineManage.Line.RLD19145.ExternalWeb;

namespace ProductionLineManage.Host.ExternalWeb
{
    /// <summary>
    /// 在 Host 进程内用 WCF ServiceHost 对外提供 Win7 兼容 SOAP 服务。
    /// 替代原 net9 Kestrel + SoapCore 方案，契约与路径保持不变。
    /// </summary>
    public sealed class ExternalWebStationHost : IDisposable
    {
        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

        private readonly SemaphoreSlim _lifecycleGate = new SemaphoreSlim(1, 1);
        private readonly ExternalWebStationOptions _options;
        private readonly IExternalWebStationService _service;
        private readonly ILogger _logger;
        private ServiceHost? _host;
        private volatile bool _isStopped;

        public ExternalWebStationHost(
            ExternalWebStationOptions options,
            IExternalWebStationService service,
            ILogger logger)
        {
            _options = options;
            _service = service;
            _logger = logger;
        }

        public bool IsRunning => _host != null && _host.State == CommunicationState.Opened;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                _logger.Info("ExternalWebStation 未启用（ExternalWebStation:Enabled=false）", "ExternalWebStation");
                return;
            }

            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsRunning)
                    return;

                var endpoint = new RLD19145WebInterfaceSoapEndpoint(_service);
                var baseAddress = ToWcfBaseAddress(_options.Urls);
                var soapPath = (_options.SoapPath ?? "/RLD19145WebInterface.asmx").TrimStart('/');
                var soapUri = new Uri(baseAddress, soapPath);

                // 基地址就是 .asmx 本身：避免再挂一层相对路径，也避免帮助页/WSDL 抢同一个 URL
                var host = new ServiceHost(endpoint, soapUri);
                var binding = new BasicHttpBinding
                {
                    MaxReceivedMessageSize = int.MaxValue,
                    MaxBufferSize = int.MaxValue,
                    ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max,
                    // Exact：注册 http://127.0.0.1:8090/... 而不是 http://+:8090/，避免非管理员无法监听
                    HostNameComparisonMode = HostNameComparisonMode.Exact
                };

                host.AddServiceEndpoint(typeof(IRLD19145WebInterfaceSoap), binding, string.Empty);

                // HttpGet/帮助页默认会按 StrongWildcard 注册 http://+:8090/，非管理员会失败；
                // 且与 SOAP POST 共用同一地址时，客户端会收到 404。
                var metadata = host.Description.Behaviors.Find<ServiceMetadataBehavior>();
                if (metadata != null)
                    host.Description.Behaviors.Remove(metadata);

                var debug = host.Description.Behaviors.Find<ServiceDebugBehavior>();
                if (debug == null)
                {
                    debug = new ServiceDebugBehavior();
                    host.Description.Behaviors.Add(debug);
                }
                debug.HttpHelpPageEnabled = false;
                debug.HttpsHelpPageEnabled = false;
                debug.IncludeExceptionDetailInFaults = true;

                host.Open();
                _host = host;
                _isStopped = false;
                _logger.Info($"ExternalWebStation 已启动，监听 {soapUri.AbsoluteUri}", "ExternalWebStation");
            }
            catch (AddressAccessDeniedException ex)
            {
                _logger.Error(ex,
                    "ExternalWebStation 启动失败：当前用户没有 HTTP.sys URL 权限（不是防火墙）。可在管理员 PowerShell 执行：netsh http add urlacl url=http://127.0.0.1:8090/ user=Everyone",
                    "ExternalWebStation");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ExternalWebStation 启动失败", "ExternalWebStation");
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_isStopped)
                return;

            await _lifecycleGate.WaitAsync(cancellationToken);
            try
            {
                if (_isStopped)
                    return;

                var host = _host;
                _host = null;
                if (host != null)
                {
                    try
                    {
                        if (host.State == CommunicationState.Opened)
                            host.Close(StopTimeout);
                        else
                            host.Abort();
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning($"ExternalWebStation StopAsync 异常: {ex.Message}", "ExternalWebStation");
                        try { host.Abort(); } catch { }
                    }
                }

                _isStopped = true;
                _logger.Info("ExternalWebStation 已停止", "ExternalWebStation");
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }

        public void Dispose()
        {
            try { StopAsync().GetAwaiter().GetResult(); } catch { }
            _lifecycleGate.Dispose();
        }

        /// <summary>
        /// WCF 不能使用 http://*:8090。星号/加号改为 127.0.0.1，避免 IPv6 localhost 连不上。
        /// 现场 Win7 工位请把 Urls 配成 http://本机局域网IP:8090。
        /// </summary>
        private static Uri ToWcfBaseAddress(string urls)
        {
            var text = string.IsNullOrWhiteSpace(urls) ? "http://127.0.0.1:8090" : urls.Trim().TrimEnd('/');
            text = text.Replace("://*", "://127.0.0.1").Replace("://+", "://127.0.0.1");
            if (text.IndexOf("://localhost", StringComparison.OrdinalIgnoreCase) >= 0)
                text = text.Replace("localhost", "127.0.0.1").Replace("Localhost", "127.0.0.1").Replace("LOCALHOST", "127.0.0.1");
            if (text.EndsWith("://0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("://0.0.0.0:", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Replace("0.0.0.0", "127.0.0.1");
            }
            return new Uri(text);
        }
    }
}
