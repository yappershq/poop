namespace Prefix.Poop.Modules;

/// <summary>
/// Workaround for a ModSharp/CS2 bug where the HTML HUD flickers during map restarts.
/// Each game frame we force <c>IsGameRestart</c> to <c>true</c> only while
/// <c>RestartRoundTime</c> is non-zero, preventing the engine from oscillating the flag.
/// </summary>
internal sealed class FlashingHtmlHudFix : IModule
{
    private readonly InterfaceBridge _bridge;

    public FlashingHtmlHudFix(InterfaceBridge bridge)
        => _bridge = bridge;

    public bool Init()
    {
        _bridge.ModSharp.InstallGameFrameHook(pre: null, post: OnGameFrame);
        return true;
    }

    public void Shutdown()
        => _bridge.ModSharp.RemoveGameFrameHook(pre: null, post: OnGameFrame);

    private void OnGameFrame(bool simulating, bool firstTick, bool lastTick)
    {
        var rules = _bridge.ModSharp.GetGameRules();
        rules.IsGameRestart = rules.RestartRoundTime == 0.0f;
    }
}
