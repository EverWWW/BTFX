using BTFX.Common;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

internal sealed class LanguageChangeSubscription : IDisposable
{
    private readonly ILocalizationService _localizationService;
    private readonly EventHandler<AppLanguage> _handler;
    private bool _disposed;

    public LanguageChangeSubscription(
        ILocalizationService localizationService,
        EventHandler<AppLanguage> handler)
    {
        _localizationService = localizationService;
        _handler = handler;
        _localizationService.LanguageChanged += _handler;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _localizationService.LanguageChanged -= _handler;
        _disposed = true;
    }
}
