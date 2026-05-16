namespace Content.Server._Goobstation.SpaceWhale.Admin;

public static class SpaceWhaleAdminLog
{
    public static void Info(string message)
    {
        Logger.InfoS("whale", $"[Whale] {message}");
    }
}
