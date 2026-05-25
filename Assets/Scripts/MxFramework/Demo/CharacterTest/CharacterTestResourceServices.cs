using MxFramework.Resources;
using MxFramework.Runtime;

namespace MxFramework.Demo.CharacterTest
{
    public sealed class CharacterTestResourceServices
    {
        private readonly IRuntimeLogger _logger;
        private readonly RuntimeLogBuffer _logBuffer = new RuntimeLogBuffer(96);
        private bool _baseResourcesLoaded;

        public CharacterTestResourceServices(
            ResourceManager resourceManager,
            ResourcePreloadService preloadService,
            IRuntimeLogger logger = null)
        {
            ResourceManager = resourceManager ?? new ResourceManager();
            PreloadService = preloadService ?? new ResourcePreloadService(ResourceManager);
            _logger = logger ?? NullRuntimeLogger.Instance;
            _logger.Info("Resources", "Resource services initialized");
        }

        public ResourceManager ResourceManager { get; }
        public ResourcePreloadService PreloadService { get; }
        public bool BaseResourcesLoaded => _baseResourcesLoaded;

        public static CharacterTestResourceServices CreateDefault(IRuntimeLogger logger = null)
        {
            var resourceManager = new ResourceManager();
            var preloadService = new ResourcePreloadService(resourceManager);
            return new CharacterTestResourceServices(resourceManager, preloadService, logger);
        }

        public bool LoadBaseResources()
        {
            if (_baseResourcesLoaded)
            {
                _logger.Info("Resources", "Base resources already loaded");
                return true;
            }

            // The concrete catalog/preload plan is intentionally deferred until the demo has real UI/character assets.
            _baseResourcesLoaded = true;
            _logBuffer.Clear()
                .Append("Base resource preload completed. catalogs=")
                .Append(ResourceManager.CreateDebugSnapshot().CatalogCount);
            _logger.Info("Resources", _logBuffer);
            return true;
        }
    }
}
