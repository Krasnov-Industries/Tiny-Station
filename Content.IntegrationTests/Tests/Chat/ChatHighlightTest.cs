#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Client.CharacterInfo;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chat;

public sealed class ChatHighlightTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly IConfigurationManager _configManager = null!;
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _uiManager = null!;
    private static readonly ProtoId<JobPrototype> Captain = "Captain";

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestCustomHighlightsPreserved()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();

        // 1. Enable auto-fill highlights
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        // 2. Set custom highlights
        var customHighlights = "ling\nrev";
        chatController.UpdateHighlights(customHighlights);

        // Verify they are saved
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // 3. Simulate character update
        var characterData = new CharacterInfoSystem.CharacterData(
            default,
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            Captain,
            "John Doe"
        );

        var method = chatController.GetType().GetMethod(
            "OnCharacterUpdated",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.That(method, Is.Not.Null);

        // Set internal state to allow character update processing
        var attachField = chatController.GetType().GetField(
            "_charInfoIsAttach",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(attachField, Is.Not.Null);
        attachField.SetValue(chatController, true);

        // Invoke update
        method.Invoke(chatController, new object[] { characterData });

        // 4. Assertions:
        // - Custom highlights in config must remain unchanged
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // - Internal active regex highlights must contain both custom & auto-filled highlights
        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        // Check that custom and auto highlights are loaded
        // Custom:
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        // Auto:
        // Tinystation edit start - auto-fill keywords come from the active client locale.
        var expectedJobHighlights = Client.ResolveDependency<ILocalizationManager>().GetString("highlights-captain")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(keyword => keyword.StartsWith('"') && keyword.EndsWith('"')
                ? $@"(?<!\w){Regex.Escape(keyword[1..^1])}(?!\w)"
                : Regex.Escape(keyword))
            .ToArray();
        Assert.That(expectedJobHighlights, Is.Not.Empty);
        foreach (var keyword in expectedJobHighlights)
            Assert.That(activeHighlights, Contains.Item(keyword));
        // Tinystation edit end

        // 5. Disable auto-fill highlights and verify auto-filled highlights are removed
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, false);

        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        // Tinystation edit start - all localized auto-fill keywords must be removed.
        foreach (var keyword in expectedJobHighlights)
            Assert.That(activeHighlights, Is.Not.Contains(keyword));
        // Tinystation edit end
    }

    [Test]
    [RunOnSide(Side.Client)]
    public async Task TestEnablingAutoFillPreservesCustomHighlights()
    {
        var chatController = _uiManager.GetUIController<ChatUIController>();

        // 1. Start with auto-fill disabled
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, false);

        // 2. Set custom highlights
        var customHighlights = "ling\nrev";
        chatController.UpdateHighlights(customHighlights);

        // Verify active matches are ONLY custom highlights
        var highlightsField = chatController.GetType().GetField(
            "_highlights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(highlightsField, Is.Not.Null);
        var activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;

        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        Assert.That(activeHighlights.Count, Is.EqualTo(2));

        // 3. Enable auto-fill highlights
        _configManager.SetCVar(CCVars.ChatAutoFillHighlights, true);

        // 4. Simulate character update (spawning into round)
        var characterData = new CharacterInfoSystem.CharacterData(
            default,
            new Dictionary<string, List<Shared.Objectives.ObjectiveInfo>>(),
            null,
            Captain,
            "John Doe"
        );

        var method = chatController.GetType().GetMethod(
            "OnCharacterUpdated",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        Assert.That(method, Is.Not.Null);

        var attachField = chatController.GetType().GetField(
            "_charInfoIsAttach",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.That(attachField, Is.Not.Null);
        attachField.SetValue(chatController, true);

        // Invoke character update
        method.Invoke(chatController, new object[] { characterData });

        // 5. Assertions:
        // - Config highlights MUST NOT be wiped and remain as custom highlights
        Assert.That(_configManager.GetCVar(CCVars.ChatHighlights), Is.EqualTo(customHighlights));

        // - Active highlights list must now merge both custom and auto-filled ones
        activeHighlights = (List<string>)highlightsField.GetValue(chatController)!;
        Assert.That(activeHighlights, Contains.Item("ling"));
        Assert.That(activeHighlights, Contains.Item("rev"));
        // Tinystation edit start - auto-fill keywords come from the active client locale.
        var expectedJobHighlights = Client.ResolveDependency<ILocalizationManager>().GetString("highlights-captain")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(keyword => keyword.StartsWith('"') && keyword.EndsWith('"')
                ? $@"(?<!\w){Regex.Escape(keyword[1..^1])}(?!\w)"
                : Regex.Escape(keyword))
            .ToArray();
        Assert.That(expectedJobHighlights, Is.Not.Empty);
        foreach (var keyword in expectedJobHighlights)
            Assert.That(activeHighlights, Contains.Item(keyword));
        // Tinystation edit end
    }
}
