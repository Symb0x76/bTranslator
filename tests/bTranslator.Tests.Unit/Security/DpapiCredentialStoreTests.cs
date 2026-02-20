using bTranslator.Infrastructure.Security.Options;
using bTranslator.Infrastructure.Security.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace bTranslator.Tests.Unit.Security;

public class DpapiCredentialStoreTests
{
    [Fact]
    public async Task SetAndGetSecret_ShouldRoundTrip()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "bTranslator-tests", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new CredentialStoreOptions
        {
            RootDirectory = tempRoot
        });
        var store = new DpapiCredentialStore(options);

        await store.SetSecretAsync("openai", "api-key", "secret-value");
        var value = await store.GetSecretAsync("openai", "api-key");

        value.Should().Be("secret-value");
    }
}

