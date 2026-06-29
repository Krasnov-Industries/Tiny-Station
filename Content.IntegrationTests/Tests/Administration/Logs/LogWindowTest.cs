using System.Collections.Generic;
using System.Linq;
using Content.Client.Administration.UI;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Administration.UI.Logs;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.IntegrationTests;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Administration.Commands;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests.Administration.Logs;

public sealed class LogWindowTest : InteractionTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, AdminLogsEnabled = true, DummyTicker = false };

    [Test]
    public async Task TestAdminLogsWindow()
    {
        // First, generate a new log
        var log = Server.Resolve<IAdminLogManager>();
        var guid = Guid.NewGuid();
        await Server.WaitPost(() => log.Add(LogType.Unknown, $"{SPlayer} test log 1: {guid}"));
        // Tinystation edit start - wait for async admin log persistence before querying the UI
        await WaitForLog(log, guid);
        // Tinystation edit end

        // Click the admin button in the menu bar
        await ClickWidgetControl<GameTopMenuBar, MenuButton>(nameof(GameTopMenuBar.AdminButton));
        var adminWindow = GetWindow<AdminMenuWindow>();

        // Find and click the "open logs" button.
        Assert.That(TryGetControlFromChildren<CommandButton>(x => x.Command == OpenAdminLogsCommand.Cmd, adminWindow, out var btn));
        await ClickControl(btn!);
        var logWindow = GetWindow<AdminLogsWindow>();

        // Find the log search field and refresh buttons
        var search = logWindow.Logs.LogSearch;
        var refresh = logWindow.Logs.RefreshButton;
        var cont = logWindow.Logs.LogsContainer;

        // Search for the log we added earlier.
        await Client.WaitPost(() => search.Text = guid.ToString());
        await ClickControl(refresh);
        // Tinystation edit start - wait for async EUI response instead of a fixed tick delay
        await WaitForSearchResult(cont, $" test log 1: {guid}");
        // Tinystation edit end

        // Add a new log
        guid = Guid.NewGuid();
        await Server.WaitPost(() => log.Add(LogType.Unknown, $"{SPlayer} test log 2: {guid}"));
        // Tinystation edit start - wait for async admin log persistence before querying the UI
        await WaitForLog(log, guid);
        // Tinystation edit end

        // Update the search and refresh
        await Client.WaitPost(() => search.Text = guid.ToString());
        await ClickControl(refresh);
        // Tinystation edit start - wait for async EUI response instead of a fixed tick delay
        await WaitForSearchResult(cont, $" test log 2: {guid}");
        // Tinystation edit end
    }

    // Tinystation added start - shared waits for async admin log persistence and EUI updates
    private async Task WaitForLog(IAdminLogManager log, Guid guid)
    {
        var filter = new LogFilter
        {
            Search = guid.ToString(),
            Types = new HashSet<LogType> { LogType.Unknown },
        };

        await PoolManager.WaitUntil(Server, async () =>
        {
            foreach (var _ in await log.All(filter))
            {
                return true;
            }

            return false;
        });
    }

    private async Task WaitForSearchResult(BoxContainer container, string expectedMessage)
    {
        AdminLogLabel[] searchResult = [];

        for (var i = 0; i < 600; i++)
        {
            await Client.WaitPost(() => searchResult = VisibleLogLabels(container));

            if (searchResult.Length == 1 && searchResult[0].Log.Message.Contains(expectedMessage))
                return;

            await RunTicks(1);
        }

        Assert.That(searchResult, Has.Length.EqualTo(1));
        Assert.That(searchResult[0].Log.Message, Contains.Substring(expectedMessage));
    }

    private static AdminLogLabel[] VisibleLogLabels(BoxContainer container)
    {
        return container.Children
            .Where(x => x.Visible && x is AdminLogLabel)
            .Cast<AdminLogLabel>()
            .ToArray();
    }
    // Tinystation added end
}
