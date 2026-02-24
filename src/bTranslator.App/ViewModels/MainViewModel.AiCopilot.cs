using System.Collections.ObjectModel;
using System.Text;
using bTranslator.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace bTranslator.App.ViewModels;

public partial class MainViewModel
{
    private const string McpCommandPrefix = "/mcp";
    private const int MaxAiMessageCount = 120;

    public ObservableCollection<AiChatMessageViewModel> AiChatMessages { get; } = [];

    [ObservableProperty]
    public partial string AiChatInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAiChatBusy { get; set; }

    partial void OnAiChatInputChanged(string value)
    {
        SendAiChatMessageCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAiChatBusyChanged(bool value)
    {
        SendAiChatMessageCommand.NotifyCanExecuteChanged();
    }

    private void InitializeAiCopilot()
    {
        if (AiChatMessages.Count > 0)
        {
            return;
        }

        AppendAiMessage(
            AiChatMessageViewModel.AssistantRole,
            L(
                "AiCopilot.WelcomeMessage",
                "Copilot is ready. Use /mcp help to view tool commands for direct mod localization."));
    }

    [RelayCommand(CanExecute = nameof(CanSendAiChatMessage))]
    private async Task SendAiChatMessageAsync()
    {
        var userText = AiChatInput.Trim();
        if (userText.Length == 0)
        {
            return;
        }

        AiChatInput = string.Empty;
        AppendAiMessage(AiChatMessageViewModel.UserRole, userText);

        IsAiChatBusy = true;
        try
        {
            var mcpResponse = await TryExecuteMcpCommandAsync(userText);
            if (!string.IsNullOrWhiteSpace(mcpResponse))
            {
                AppendAiMessage(AiChatMessageViewModel.AssistantRole, mcpResponse);
                return;
            }

            var llmResponse = await GenerateLlmChatReplyAsync(userText);
            AppendAiMessage(AiChatMessageViewModel.AssistantRole, llmResponse);
        }
        catch (Exception ex)
        {
            var message = Lf("Status.AiCopilotError", "Copilot request failed: {0}", ex.Message);
            StatusText = message;
            AddLog(message);
            AppendAiMessage(AiChatMessageViewModel.AssistantRole, message);
        }
        finally
        {
            IsAiChatBusy = false;
        }
    }

    private bool CanSendAiChatMessage()
    {
        return !IsAiChatBusy && !string.IsNullOrWhiteSpace(AiChatInput);
    }

    [RelayCommand]
    private void ClearAiChatHistory()
    {
        AiChatMessages.Clear();
        InitializeAiCopilot();
    }

    private async Task<string?> TryExecuteMcpCommandAsync(string rawInput)
    {
        var input = rawInput.Trim();
        var isExplicit = input.StartsWith(McpCommandPrefix, StringComparison.OrdinalIgnoreCase);
        if (!isExplicit && !LooksLikeImplicitMcpIntent(input))
        {
            return null;
        }

        var commandText = isExplicit
            ? input[McpCommandPrefix.Length..].Trim()
            : input;

        if (string.IsNullOrWhiteSpace(commandText) ||
            IsAnyAlias(commandText, "help", "h", "?", "帮助"))
        {
            return BuildMcpHelpText();
        }

        if (IsAnyAlias(commandText, "status", "状态"))
        {
            return BuildMcpStatusText();
        }

        if (StartsWithAnyAlias(commandText, "open ", "打开 "))
        {
            return await ExecuteMcpOpenWorkspaceAsync(commandText);
        }

        if (StartsWithAnyAlias(commandText, "translate-row", "row", "当前行", "选中行"))
        {
            var ok = await TranslateSelectedRowWithProvidersAsync();
            return ok
                ? StatusText
                : L(
                    "AiCopilot.McpTranslateRowFailed",
                    "MCP translate-row failed. Check status panel for details.");
        }

        if (StartsWithAnyAlias(commandText, "next", "next-pending", "下一条", "下一个"))
        {
            SelectNextPendingRow();
            return L(
                "AiCopilot.McpNextPendingDone",
                "Moved selection to next pending row.");
        }

        if (StartsWithAnyAlias(
                commandText,
                "translate-mod",
                "localize-mod",
                "汉化mod",
                "批量汉化",
                "翻译mod"))
        {
            var saveAfter = commandText.Contains("--save", StringComparison.OrdinalIgnoreCase) ||
                            commandText.Contains("保存", StringComparison.OrdinalIgnoreCase);

            var beforePending = PendingEntries;
            await RunBatchTranslation();
            var translatedCount = Math.Max(beforePending - PendingEntries, 0);

            if (saveAfter)
            {
                await SaveWorkspaceAsync();
            }

            return saveAfter
                ? Lf(
                    "AiCopilot.McpLocalizeAndSaveDone",
                    "MCP localized {0} rows and saved workspace.",
                    translatedCount)
                : Lf(
                    "AiCopilot.McpLocalizeDone",
                    "MCP localized {0} rows. Run /mcp save to persist output.",
                    translatedCount);
        }

        if (StartsWithAnyAlias(commandText, "save", "保存"))
        {
            await SaveWorkspaceAsync();
            return StatusText;
        }

        return Lf(
            "AiCopilot.McpUnknownCommand",
            "Unknown MCP command: '{0}'. Use /mcp help.",
            commandText);
    }

    private async Task<string> ExecuteMcpOpenWorkspaceAsync(string commandText)
    {
        var index = commandText.IndexOf(' ');
        if (index < 0 || index == commandText.Length - 1)
        {
            return L(
                "AiCopilot.McpOpenUsage",
                "Usage: /mcp open <plugin-path>");
        }

        var candidatePath = commandText[(index + 1)..].Trim().Trim('"');
        if (candidatePath.Length == 0 || !File.Exists(candidatePath))
        {
            return Lf(
                "AiCopilot.McpOpenPathMissing",
                "Plugin path not found: {0}",
                candidatePath);
        }

        PluginPath = candidatePath;
        if (string.IsNullOrWhiteSpace(OutputPluginPath))
        {
            OutputPluginPath = candidatePath;
        }

        await OpenWorkspaceAsync();
        return StatusText;
    }

    private async Task<bool> TranslateSelectedRowWithProvidersAsync()
    {
        if (SelectedRow is null)
        {
            StatusText = L("Status.NoSelectedRow", "No selected row.");
            AddLog(StatusText);
            return false;
        }

        if (SelectedRow.IsLocked)
        {
            StatusText = L(
                "Status.RowLockedCannotTranslate",
                "Selected row is locked. Unlock it before AI translation.");
            AddLog(StatusText);
            return false;
        }

        var chain = BuildProviderChain();
        if (chain.Count == 0)
        {
            StatusText = L("Status.NoAvailableProvider", "No model selected or provider unavailable.");
            AddLog(StatusText);
            return false;
        }

        try
        {
            var row = SelectedRow;
            var source = row.SourceText;
            IReadOnlyList<TranslationDictionaryTokenReplacement> replacements = [];
            if (IsDictionaryPreReplaceEnabled && DictionaryEntryCount > 0)
            {
                var prepared = PrepareSourceWithDictionary(source, row.EditorId, row.FieldSignature);
                source = prepared.PreparedSource;
                replacements = prepared.Replacements;
            }

            var result = await _translationOrchestrator.ExecuteAsync(
                    new TranslationJob
                    {
                        SourceLanguage = SourceLanguage,
                        TargetLanguage = TargetLanguage,
                        ProviderChain = chain,
                        NormalizePlaceholders = true,
                        Items =
                        [
                            new TranslationItem
                            {
                                Id = row.RowKey,
                                SourceText = source,
                                TranslatedText = row.TranslatedText,
                                IsLocked = false,
                                IsValidated = row.IsValidated
                            }
                        ]
                    },
                    new OrchestratorPolicy
                    {
                        FailOnAuthenticationError = false
                    })
                .ConfigureAwait(true);

            var translated = result.Items.FirstOrDefault()?.TranslatedText ?? row.SourceText;
            if (replacements.Count > 0)
            {
                translated = RestoreDictionaryTokens(translated, replacements);
            }

            row.TranslatedText = translated;
            row.IsValidated = false;

            RecalculateMetrics();
            ApplyFilters();

            StatusText = Lf(
                "Status.RowTranslationDone",
                "Translated selected row '{0}' via '{1}'.",
                row.EditorId,
                result.ProviderId);
            AddLog(StatusText);
            return true;
        }
        catch (Exception ex)
        {
            StatusText = Lf("Status.RowTranslationFailed", "Selected row translation failed: {0}", ex.Message);
            AddLog(StatusText);
            return false;
        }
    }

    private async Task<string> GenerateLlmChatReplyAsync(string userText)
    {
        var chain = BuildProviderChain();
        if (chain.Count == 0)
        {
            return L(
                "AiCopilot.NoProviderAvailable",
                "No model selected. Choose one from the Model selector.");
        }

        var prompt = BuildChatPrompt(userText);
        try
        {
            var result = await _translationOrchestrator.ExecuteAsync(
                    new TranslationJob
                    {
                        SourceLanguage = LanguageEnglish,
                        TargetLanguage = LanguageEnglish,
                        ProviderChain = chain,
                        NormalizePlaceholders = false,
                        Items =
                        [
                            new TranslationItem
                            {
                                Id = $"chat:{Guid.NewGuid():N}",
                                SourceText = prompt,
                                TranslatedText = string.Empty,
                                IsLocked = false,
                                IsValidated = false
                            }
                        ]
                    },
                    new OrchestratorPolicy
                    {
                        FailOnAuthenticationError = false
                    })
                .ConfigureAwait(true);

            var reply = result.Items.FirstOrDefault()?.TranslatedText?.Trim();
            if (string.IsNullOrWhiteSpace(reply))
            {
                return L(
                    "AiCopilot.EmptyReply",
                    "Provider returned an empty reply. Check provider model/prompt settings.");
            }

            AddLog(Lf("Log.AiCopilotProviderReply", "Copilot response generated via '{0}'.", result.ProviderId));
            return reply;
        }
        catch (Exception ex)
        {
            var message = Lf("Status.AiCopilotLlmFailed", "LLM chat failed: {0}", ex.Message);
            AddLog(message);
            return message;
        }
    }

    private string BuildChatPrompt(string userText)
    {
        var recent = AiChatMessages
            .TakeLast(8)
            .Select(message => $"{message.Role}: {message.MessageText}")
            .ToList();

        var selected = SelectedRow is null
            ? "none"
            : $"{SelectedRow.EditorId} / {SelectedRow.FieldSignature}";
        var builder = new StringBuilder();
        builder.AppendLine("You are bTranslator Copilot for Bethesda mod localization.");
        builder.AppendLine("Respond concisely in the same language as the latest user message.");
        builder.AppendLine("If user asks for direct actions, suggest /mcp commands.");
        builder.AppendLine("Available MCP commands:");
        builder.AppendLine("/mcp status");
        builder.AppendLine("/mcp open <plugin-path>");
        builder.AppendLine("/mcp translate-row");
        builder.AppendLine("/mcp translate-mod [--save]");
        builder.AppendLine("/mcp save");
        builder.AppendLine("/mcp next");
        builder.AppendLine();
        builder.AppendLine($"Current selected model: {ProviderChainPreview}");
        builder.AppendLine($"Workspace rows: total={TotalEntries}, pending={PendingEntries}, translated={TranslatedEntries}");
        builder.AppendLine($"Current selected row: {selected}");
        builder.AppendLine();
        builder.AppendLine("Recent conversation:");
        foreach (var line in recent)
        {
            builder.AppendLine(line);
        }

        builder.AppendLine($"user: {userText}");
        builder.Append("assistant:");
        return builder.ToString();
    }

    private string BuildMcpStatusText()
    {
        var selected = SelectedRow is null
            ? "-"
            : $"{SelectedRow.EditorId}/{SelectedRow.FieldSignature}";
        var workspace = string.IsNullOrWhiteSpace(PluginPath) ? "-" : Path.GetFileName(PluginPath);
        var selectedModel = string.IsNullOrWhiteSpace(ProviderChainPreview) ? "-" : ProviderChainPreview;

        return Lf(
            "AiCopilot.McpStatusSummary",
            "Workspace={0}; Rows total={1}, pending={2}; Selected={3}; Model={4}.",
            workspace,
            TotalEntries,
            PendingEntries,
            selected,
            selectedModel);
    }

    private string BuildMcpHelpText()
    {
        return L(
            "AiCopilot.McpHelpText",
            "MCP commands:\n" +
            "/mcp status\n" +
            "/mcp open <plugin-path>\n" +
            "/mcp translate-row\n" +
            "/mcp translate-mod [--save]\n" +
            "/mcp save\n" +
            "/mcp next");
    }

    private void AppendAiMessage(string role, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var roleLabel = string.Equals(role, AiChatMessageViewModel.UserRole, StringComparison.OrdinalIgnoreCase)
            ? L("AiCopilot.UserRoleLabelText", "You")
            : L("AiCopilot.AssistantRoleLabelText", "Copilot");

        AiChatMessages.Add(
            new AiChatMessageViewModel(
                role,
                roleLabel,
                message.Trim(),
                DateTimeOffset.Now));

        while (AiChatMessages.Count > MaxAiMessageCount)
        {
            AiChatMessages.RemoveAt(0);
        }
    }

    private static bool LooksLikeImplicitMcpIntent(string text)
    {
        return text.Contains("汉化mod", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("批量汉化", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("translate mod", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("localize mod", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAnyAlias(string text, params string[] aliases)
    {
        return aliases.Any(alias =>
            string.Equals(text, alias, StringComparison.OrdinalIgnoreCase));
    }

    private static bool StartsWithAnyAlias(string text, params string[] aliases)
    {
        return aliases.Any(alias =>
            text.StartsWith(alias, StringComparison.OrdinalIgnoreCase));
    }
}
