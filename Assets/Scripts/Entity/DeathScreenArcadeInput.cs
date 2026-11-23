using System;
using System.Collections;

/// Lets the death screens be used without a mouse, through the arcade controls (ArcadeControls.inputactions):
/// A (or Start) = Retry, B (or Exit) = Main Menu. Same buttons the player already uses in-game.
public static class DeathScreenArcadeInput
{
    private static ArcadeControls controls;

    private static ArcadeControls Controls
    {
        get
        {
            if (controls == null) controls = new ArcadeControls();
            controls.Enable();
            return controls;
        }
    }

    /// Waits (unscaled, works while the game is paused) for a fresh arcade press, then calls retry or mainMenu.
    public static IEnumerator WaitForChoice(Action retry, Action mainMenu)
    {
        var arcade = Controls.Arcade;
        yield return null;   // ignore a press that was already happening on the frame the buttons appeared

        while (true)
        {
            if (retry != null && (arcade.ButtonA.WasPressedThisFrame() || arcade.ButtonStart.WasPressedThisFrame()))
            {
                retry();
                yield break;
            }
            if (mainMenu != null && (arcade.ButtonB.WasPressedThisFrame() || arcade.ButtonExit.WasPressedThisFrame()))
            {
                mainMenu();
                yield break;
            }
            yield return null;
        }
    }
}
