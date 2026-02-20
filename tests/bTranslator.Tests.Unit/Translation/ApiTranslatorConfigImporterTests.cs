using bTranslator.Infrastructure.Translation.Services;
using FluentAssertions;

namespace bTranslator.Tests.Unit.Translation;

public class ApiTranslatorConfigImporterTests
{
    [Fact]
    public void ImportFromIniLikeContent_ShouldMapLegacyKeysToProviderOptions()
    {
        const string content =
            """
            MsTranslate_ApiUrl=https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&from=%s&to=%s
            MsTranslate_AzureClientSecret=azure-key
            MsTranslate_ArrayLimit=49
            MsTranslate_english=en
            DeepL_Key=deepl-key
            DeepL_Model0=ignored-model
            DeepL_ApiUrl=https://api-free.deepl.com/v2/translate?auth_key=%s&source_lang=%s&target_lang=%s{text}
            DeepL_ProApiUrl=https://api.deepl.com/v2/translate?auth_key=%s&source_lang=%s&target_lang=%s{text}
            OpenAI_ApiUrl=https://api.openai.com/v1/chat/completions
            OpenAI_Model0=gpt-4o-mini
            OpenAI_DefaultQuery=Translate to %lang_dest%
            Baidu_AppId=baidu-app
            Baidu_Key=baidu-secret
            Google_ApiUrl=https://translate.googleapis.com/translate_a/single?client=gtx&sl=%s&tl=%s&dt=t&q=%s
            Tencent_Region=ap-shanghai
            Tencent_AppId=tencent-secret-id
            Tencent_Secret=tencent-secret-key
            Tencent_zhhans=zh
            """;

        var options = ApiTranslatorConfigImporter.ImportFromIniLikeContent(content);

        options.Providers.Should().ContainKey("azure-translator");
        var azure = options.Providers["azure-translator"];
        azure.BaseUrl.Should().Contain("microsofttranslator");
        azure.ApiKey.Should().Be("azure-key");
        azure.Capabilities.MaxItemsPerBatch.Should().Be(49);
        azure.LanguageMap.Should().ContainKey("english");
        azure.LanguageMap["english"].Should().Be("en");

        options.Providers.Should().ContainKey("deepl");
        var deepl = options.Providers["deepl"];
        deepl.ApiKey.Should().Be("deepl-key");
        deepl.BaseUrl.Should().Be("https://api.deepl.com/v2/translate");

        options.Providers.Should().ContainKey("openai-compatible");
        var openAi = options.Providers["openai-compatible"];
        openAi.Model.Should().Be("gpt-4o-mini");
        openAi.PromptTemplate.Should().Be("Translate to %lang_dest%");

        options.Providers.Should().ContainKey("baidu");
        var baidu = options.Providers["baidu"];
        baidu.ApiKey.Should().Be("baidu-app");
        baidu.ApiSecret.Should().Be("baidu-secret");

        options.Providers.Should().ContainKey("google-cloud-translate");
        var google = options.Providers["google-cloud-translate"];
        google.BaseUrl.Should().Be("https://translation.googleapis.com/language/translate/v2");

        options.Providers.Should().ContainKey("tencent-tmt");
        var tencent = options.Providers["tencent-tmt"];
        tencent.Region.Should().Be("ap-shanghai");
        tencent.ApiKey.Should().Be("tencent-secret-id");
        tencent.ApiSecret.Should().Be("tencent-secret-key");
        tencent.LanguageMap.Should().ContainKey("zhhans");
        tencent.LanguageMap["zhhans"].Should().Be("zh");
    }
}
