using BTFX.Common;
using BTFX.Services.Implementations;
using BTFX.Services.Interfaces;
using Xunit;

namespace BTFX.Tests;

public sealed class LanguageChangeSubscriptionTests
{
    [Fact]
    public void Dispose_UnsubscribesHandler()
    {
        var localization = new FakeLocalizationService();
        var notificationCount = 0;
        var subscription = new LanguageChangeSubscription(
            localization,
            (_, _) => notificationCount++);

        localization.ApplyLanguage(AppLanguage.English);
        subscription.Dispose();
        localization.ApplyLanguage(AppLanguage.ChineseSimplified);

        Assert.Equal(1, notificationCount);
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public AppLanguage CurrentLanguage { get; private set; }

        public event EventHandler<AppLanguage>? LanguageChanged;

        public void ApplyLanguage(AppLanguage language)
        {
            CurrentLanguage = language;
            LanguageChanged?.Invoke(this, language);
        }

        public string GetString(string key) => key;

        public string GetString(string key, params object[] args) => string.Format(key, args);
    }
}
