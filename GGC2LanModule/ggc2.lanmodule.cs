using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[BepInPlugin("ggc2.lanmodule", "GGC2LanModule", "1.6.1")]
public class GGC2LanModule : BaseUnityPlugin
{
    // ===== Colors =====
    private static readonly Color ColorWhite = Color.white;
    private static readonly Color ColorBlack = Color.black;
    private static readonly Color ColorRed = Color.red;
    private static readonly Color ColorToggleButtonHover = new Color(30f / 255f, 30f / 255f, 30f / 255f);
    private static readonly Color ColorMenuButtonHover = new Color(225f / 255f, 225f / 255f, 225f / 255f);
    private static readonly Color ColorMenuButtonBlock = new Color(150f / 255f, 150f / 255f, 150f / 255f);

    // ===== Layout, at reference resolution 1920x1080 =====
    private const float RefWidth = 1920f;
    private const float RefHeight = 1080f;

    private const float ScreenMargin = 30f;

    private const float ToggleBtnW = 86f;
    private const float ToggleBtnH = 42f;
    private const float ToggleBtnFontSize = 18f;
    private const float ToggleBtnMargin = 10f;

    private const float MenuW = 340f;
    private const float MenuGap = 20f;
    private const float MenuPadding = 20f;

    private const float MenuHeaderFontSize = 16f;

    private const float MenuBtnW = 300f;
    private const float MenuBtnH = 42f;
    private const float MenuBtnGap = 16f;
    private const float MenuBtnFontSize = 18f;

    private const float PingW = 90f;
    private const float PingFontSize = 16f;

    // ===== LobbyManagerGGC.State / DisconnectType values used by the game
    // (named here since the underlying enums aren't accessible by name from
    // outside the game's assembly in a clean way) =====
    private const LobbyManagerGGC.State StateMainMenu = (LobbyManagerGGC.State)0;
    private const LobbyManagerGGC.State StateLobby = (LobbyManagerGGC.State)1;
    private const LobbyManagerGGC.State StateInGame = (LobbyManagerGGC.State)3;
    private const LobbyManagerGGC.DisconnectType DisconnectVoluntary = (LobbyManagerGGC.DisconnectType)3;

    // ===== Runtime state =====
    private bool listening = false;
    private bool isHosting = false;
    private bool show = false;

    // While true, the game's TestConnection() coroutine is suppressed (see
    // the Harmony patch below). That coroutine pings the online matchmaker
    // relay every 10s and kicks the player to the main menu with a fake
    // "no network" error when it fails - which it always does in a LAN
    // game, since there's no relay to ping.
    public static bool suppressDisconnectKick = false;

    // Bumped on every new search; a pending FIND callback only acts on its
    // result if this still matches the token it started with - that's how
    // a search gets "cancelled" without a direct stop-and-forget API.
    private int searchToken = 0;

    // Latched version of NetworkCommunicator.lobbyIsLockedForGameStart (see
    // Update()) - tracks whether a game start is in progress, independently
    // of whether the menu panel is currently open.
    private bool gameStartLatched = false;

    private Font arialFont;
    private GUIStyle toggleBtnStyle;
    private GUIStyle menuHeaderStyle;
    private GUIStyle menuHeaderLeftStyle;
    private GUIStyle menuHeaderRightStyle;
    private GUIStyle menuBtnStyle;
    private GUIStyle pingStyle;
    private float lastScale = -1f;

    private void Awake()
    {
        Logger.LogInfo("GGC2LanModule: Awake() called, plugin is active.");

        bool arialInstalled = false;
        foreach (string name in Font.GetOSInstalledFontNames())
        {
            if (name == "Arial") { arialInstalled = true; break; }
        }

        if (arialInstalled)
        {
            arialFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
        }
        else
        {
            Logger.LogWarning("GGC2LanModule: Arial not found on this system, falling back to the game's own (English) menu font.");
            arialFont = Resources.Load<Font>("Fonts/HEADTH_");

            if (arialFont == null)
            {
                Logger.LogError("GGC2LanModule: fallback font 'Fonts/HEADTH_' also not found - text may not render.");
            }
        }

        try
        {
            new Harmony("ggc2.lanmodule").PatchAll();
        }
        catch (System.Exception e)
        {
            Logger.LogError("Harmony PatchAll failed: " + e);
        }
    }

    private void Update()
    {
        // If the game itself returned to the main menu (e.g. the player used
        // the game's own "Leave Lobby" button), our HOST button needs to
        // reset too, otherwise it stays stuck showing "STOP HOSTING" for a
        // session that no longer exists. Not applied to "listening": while
        // searching, the game legitimately stays in the MainMenu state until
        // a host is actually found.
        if (isHosting && LobbyManagerGGC.Instance != null && LobbyManagerGGC.Instance.state == StateMainMenu)
        {
            isHosting = false;
            SetOnlineSession(false);
        }
        // If we're still searching for a LAN host but the player left the
        // main menu some other way (e.g. started a regular offline game),
        // the search must be stopped - otherwise the discovery listener
        // keeps running in the background and could still join a LAN match
        // mid-way through an unrelated solo session.
        if (listening && LobbyManagerGGC.Instance != null && LobbyManagerGGC.Instance.state != StateMainMenu)
        {
            CancelSearch();
        }
        // Tracked independently of the menu panel's visibility (show), so the
        // HOST/FIND buttons are correctly blocked/unblocked even if the panel
        // was closed and reopened while a game start was in progress.
        if (NetworkCommunicator.lobbyIsLockedForGameStart) gameStartLatched = true;
        if (LobbyManagerGGC.Instance != null && LobbyManagerGGC.Instance.state == StateMainMenu) gameStartLatched = false;
    }

    // Same check the game itself uses (GameLauncher.Update()) to know the
    // main menu scene is actually loaded and showing - more reliable than
    // LobbyManagerGGC.state, which already defaults to "MainMenu" before the
    // menu is really up (e.g. during the boot/loading screen).
    private bool IsMainMenuSceneLoaded()
    {
        if (LevelTypeData.mainMenuScene == null) return false;
        string sceneName = LevelTypeData.mainMenuScene.SceneName;
        return UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName).isLoaded;
    }

    private float GetScale()
    {
        float sx = Screen.width / RefWidth;
        float sy = Screen.height / RefHeight;
        return Mathf.Min(sx, sy);
    }

    private void EnsureStyles(float scale)
    {
        bool scaleChanged = !Mathf.Approximately(scale, lastScale);

        if (toggleBtnStyle == null)
        {
            toggleBtnStyle = new GUIStyle();
            toggleBtnStyle.font = arialFont;
            toggleBtnStyle.fontStyle = FontStyle.Normal;
            toggleBtnStyle.normal.textColor = ColorWhite;
            toggleBtnStyle.alignment = TextAnchor.MiddleCenter;
        }

        if (menuHeaderStyle == null)
        {
            menuHeaderStyle = new GUIStyle();
            menuHeaderStyle.font = arialFont;
            menuHeaderStyle.fontStyle = FontStyle.Normal;
            menuHeaderStyle.normal.textColor = ColorWhite;
        }

        if (menuHeaderLeftStyle == null)
        {
            menuHeaderLeftStyle = new GUIStyle(menuHeaderStyle) { alignment = TextAnchor.MiddleLeft };
        }

        if (menuHeaderRightStyle == null)
        {
            menuHeaderRightStyle = new GUIStyle(menuHeaderStyle) { alignment = TextAnchor.MiddleRight };
        }

        if (menuBtnStyle == null)
        {
            menuBtnStyle = new GUIStyle();
            menuBtnStyle.font = arialFont;
            menuBtnStyle.fontStyle = FontStyle.Normal;
            menuBtnStyle.normal.textColor = ColorBlack;
            menuBtnStyle.alignment = TextAnchor.MiddleCenter;
        }

        if (pingStyle == null)
        {
            pingStyle = new GUIStyle();
            pingStyle.font = arialFont;
            pingStyle.fontStyle = FontStyle.Normal;
            pingStyle.normal.textColor = ColorRed;
            pingStyle.alignment = TextAnchor.MiddleRight;
        }

        // Font sizes scale with the screen resolution, so only recompute
        // them when the scale factor actually changes, not every frame.
        if (scaleChanged)
        {
            toggleBtnStyle.fontSize = Mathf.RoundToInt(ToggleBtnFontSize * scale);
            menuHeaderStyle.fontSize = Mathf.RoundToInt(MenuHeaderFontSize * scale);
            menuHeaderLeftStyle.fontSize = menuHeaderStyle.fontSize;
            menuHeaderRightStyle.fontSize = menuHeaderStyle.fontSize;
            menuBtnStyle.fontSize = Mathf.RoundToInt(MenuBtnFontSize * scale);
            pingStyle.fontSize = Mathf.RoundToInt(PingFontSize * scale);
            lastScale = scale;
        }
    }

    private void DrawRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = ColorWhite;
    }

    // Keeps our own flag and the game's own (otherwise unused) pingVisible
    // field in sync - purely cosmetic, nothing reads pingVisible today, but
    // it's the field the devs clearly intended for this exact purpose.
    private void SetOnlineSession(bool active)
    {
        suppressDisconnectKick = active;
        if (LobbyManagerGGC.Instance != null) LobbyManagerGGC.Instance.pingVisible = active;
    }

    private void CancelSearch()
    {
        NetworkDiscoveryManagerGGC.StopListeningForNetworkMatches();
        searchToken++;
        listening = false;
    }

    private void StopHosting()
    {
        SetOnlineSession(false);
        isHosting = false;

        if (LobbyManagerGGC.Instance != null)
        {
            LobbyManagerGGC.Instance.DestroyMatch(DisconnectVoluntary);
        }
        // If Instance is already gone (e.g. the game tore down the session
        // on its own between the click and this call), there's nothing left
        // to destroy - our own state is already reset above, which is all
        // that matters for the UI.
    }

    private void StartHosting()
    {
        if (LobbyManagerGGC.Instance == null || WindowManager.Instance == null)
        {
            Logger.LogWarning("StartHosting: LobbyManagerGGC.Instance or WindowManager.Instance is null, aborting.");
            return;
        }

        try
        {
            // Same call path as the game's own (hidden) "Local Network Game" option.
            LobbyManagerGGC.Instance.CreateMatch(false);
            LobbyManagerGGC.Instance.ChangeState(StateLobby);
            WindowManager.Instance.OpenWindow(new LobbyWindow(false));
        }
        catch (System.Exception e)
        {
            Logger.LogError("StartHosting failed: " + e);
            // Best-effort rollback so the button doesn't get stuck on
            // "STOP HOSTING" for a session that never actually started.
            SetOnlineSession(false);
            isHosting = false;
            return;
        }

        SetOnlineSession(true);
        isHosting = true;
    }

    private void StartSearching()
    {
        listening = true;
        int thisSearch = ++searchToken;

        NetworkDiscoveryManagerGGC.StartListeningForNetworkMatches(broadcast =>
        {
            // Stale if the search was cancelled (or restarted) since it began.
            if (thisSearch != searchToken) return;

            if (LobbyManagerGGC.Instance == null || WindowManager.Instance == null)
            {
                Logger.LogWarning("StartSearching callback: LobbyManagerGGC.Instance or WindowManager.Instance is null, aborting join.");
                NetworkDiscoveryManagerGGC.StopListeningForNetworkMatches();
                listening = false;
                return;
            }

            try
            {
                // Same call path as the game's own LAN client "join" callback.
                LobbyManagerGGC.ConnectViaIP(broadcast.ip);
                LobbyManagerGGC.Instance.ChangeState(StateLobby);
                WindowManager.Instance.OpenWindow(new LobbyWindow(false));
            }
            catch (System.Exception e)
            {
                Logger.LogError("StartSearching join failed: " + e);
                // Don't call SetOnlineSession(true) below - session never
                // actually started, so leave suppressDisconnectKick off.
                NetworkDiscoveryManagerGGC.StopListeningForNetworkMatches();
                listening = false;
                return;
            }

            NetworkDiscoveryManagerGGC.StopListeningForNetworkMatches();
            SetOnlineSession(true);
            listening = false;
        });
    }

    // Ping (RTT) display - just your own ping, as a plain "XXms" number.
    // Works the same whether you're hosting or connected as a client:
    // NetworkClient.GetRTT() reports each player's own connection.
    private string BuildPingText(LobbyManagerGGC.State state)
    {
        if (LobbyManagerGGC.Instance == null) return null;
        if (state != StateLobby && state != StateInGame) return null;

        // state == InGame also covers regular offline/local play, since the
        // game reuses the same networking manager for that too. Only show
        // ping while we're actually in a session started via this mod.
        if (!suppressDisconnectKick) return null;

        NetworkClient client = LobbyManagerGGC.Instance.client;
        if (client == null || !client.isConnected) return null;

        int rtt = client.GetRTT();
        return (rtt > 999 ? "999+" : rtt.ToString()) + "ms";
    }

    private void OnGUI()
    {
        // LobbyManagerGGC itself draws a small debug HUD ("hide" /
        // "Initialize Broadcast" etc.) via its networkManagerHud field
        // whenever that field's own showGUI is true. Not our UI - keep it off.
        if (LobbyManagerGGC.Instance != null && LobbyManagerGGC.Instance.networkManagerHud != null
            && LobbyManagerGGC.Instance.networkManagerHud.showGUI)
        {
            LobbyManagerGGC.Instance.networkManagerHud.showGUI = false;
        }

        float scale = GetScale();
        EnsureStyles(scale);
        float margin = ScreenMargin * scale;
        float gap = ToggleBtnMargin * scale;

        bool mainMenuOpen = IsMainMenuSceneLoaded() && !MainMenuGameState.MainMenuIsTransitional;

        float toggleW = ToggleBtnW * scale;
        float toggleH = ToggleBtnH * scale;
        Rect toggleRect = new Rect(Screen.width - toggleW - margin, margin, toggleW, toggleH);

        // Ping sits top-right too, to the left of the LAN button with the
        // same gap the button uses toward the menu below it. When the LAN
        // button isn't shown (outside the main menu), the ping shifts over
        // into the button's own spot instead of leaving a gap there.
        float pingW = PingW * scale;
        Rect pingRect = mainMenuOpen
            ? new Rect(toggleRect.x - gap - pingW, margin, pingW, toggleH)
            : new Rect(toggleRect.x, margin, pingW, toggleH);

        LobbyManagerGGC.State currentState = LobbyManagerGGC.Instance != null
            ? LobbyManagerGGC.Instance.state
            : StateMainMenu;
        string pingText = BuildPingText(currentState);
        if (pingText != null)
        {
            GUI.Label(pingRect, pingText, pingStyle);
        }

        // Nothing else outside the main menu - not during boot/loading,
        // not during gameplay, not anywhere else.
        if (!mainMenuOpen) return;

        DrawRect(toggleRect, toggleRect.Contains(Event.current.mousePosition) ? ColorToggleButtonHover : ColorBlack);
        if (GUI.Button(toggleRect, GUIContent.none, GUIStyle.none))
        {
            show = !show;
        }
        GUI.Label(toggleRect, show ? "X" : "LAN", toggleBtnStyle);

        if (!show) return;

        float menuW = MenuW * scale;
        float menuX = Screen.width - menuW - margin;
        float menuY = toggleRect.yMax + ToggleBtnMargin * scale;
        float pad = MenuPadding * scale;

        // Header row: title on the left, version on the right.
        Rect headerRect = new Rect(menuX + pad, menuY + pad, menuW - pad * 2, MenuHeaderFontSize * scale + 4 * scale);

        float btnW = MenuBtnW * scale;
        float btnH = MenuBtnH * scale;
        float btnX = menuX + (menuW - btnW) / 2f;
        float firstBtnY = headerRect.yMax + MenuGap * scale;

        Rect hostRect = new Rect(btnX, firstBtnY, btnW, btnH);
        Rect findRect = new Rect(btnX, hostRect.yMax + MenuBtnGap * scale, btnW, btnH);

        float menuH = (findRect.yMax - menuY) + pad;
        Rect menuRect = new Rect(menuX, menuY, menuW, menuH);

        DrawRect(menuRect, ColorBlack);

        GUI.Label(headerRect, "GGC2LanModule", menuHeaderLeftStyle);
        GUI.Label(headerRect, "v1.6.1", menuHeaderRightStyle);

        // ----- HOST LAN SERVER -----
        // Synced from the server to every player via UNET's SyncVar system,
        // true during the ~3s countdown after "START THE GAME" is clicked.
        // Works identically for the host and for joined clients - unlike a
        // locally-tracked flag, which would only ever see the host's click.
        // Also blocked once actually in-game, as a safety net - the menu
        // isn't drawn there anyway, but better safe than sorry.
        // (gameStartLatched itself is maintained in Update() - see comment there.)
        bool gameStarting = gameStartLatched
            || (LobbyManagerGGC.Instance != null && LobbyManagerGGC.Instance.state == StateInGame);

        DrawRect(hostRect, gameStarting ? ColorMenuButtonBlock : (hostRect.Contains(Event.current.mousePosition) ? ColorMenuButtonHover : ColorWhite));
        if (!gameStarting && GUI.Button(hostRect, GUIContent.none, GUIStyle.none))
        {
            if (isHosting)
            {
                StopHosting();
            }
            else
            {
                if (listening) CancelSearch(); // player meant to host instead
                StartHosting();
            }
        }
        GUI.Label(hostRect, isHosting ? "STOP HOSTING" : "HOST LAN SERVER", menuBtnStyle);

        // ----- FIND LAN SERVER -----
        DrawRect(findRect, gameStarting
            ? ColorMenuButtonBlock
            : (findRect.Contains(Event.current.mousePosition) ? ColorMenuButtonHover : ColorWhite));
        if (!gameStarting && GUI.Button(findRect, GUIContent.none, GUIStyle.none))
        {
            if (listening)
            {
                CancelSearch();
            }
            else
            {
                if (isHosting) StopHosting(); // player meant to search instead
                StartSearching();
            }
        }
        GUI.Label(findRect, listening ? "SEARCHING..." : "FIND LAN SERVER", menuBtnStyle);
    }
}

// The real bug this whole mod works around: LobbyPlayerGGC.OnStartLocalPlayer()
// always starts a TestConnection() coroutine that pings NetworkManager.matchHost
// (the online matchmaker relay address) every 10 seconds, regardless of LAN or
// online mode. In a LAN game matchHost is empty, the ping fails, and the game
// concludes "no network available" and kicks back to the main menu.
//
// We simply stop that coroutine from ever running during our LAN sessions.
// Nothing else needs to be touched - Leave Lobby, disconnect handling, etc.
// all keep working exactly as the game intends.
[HarmonyPatch(typeof(LobbyPlayerGGC), "TestConnection")]
class Patch_TestConnection
{
    static bool Prefix(ref System.Collections.IEnumerator __result)
    {
        if (!GGC2LanModule.suppressDisconnectKick) return true;
        __result = Empty();
        return false;
    }

    // A plain (non-yield) empty enumerator. Deliberately not written with
    // "yield break": the C# compiler decorates yield-based iterators with
    // System.Runtime.CompilerServices.IteratorStateMachineAttribute, which
    // doesn't fully resolve under this game's old Mono runtime and made
    // Harmony's PatchAll() throw a TypeLoadException while scanning the
    // assembly.
    static System.Collections.IEnumerator Empty()
    {
        return new object[0].GetEnumerator();
    }
}