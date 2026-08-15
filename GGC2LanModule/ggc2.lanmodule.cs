using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[BepInPlugin("ggc2.lanmodule", "GGC2LanModule", "1.5.0")]
public class GGC2LanModule : BaseUnityPlugin
{
    // ===== Colors =====
    private static readonly Color ColorWhite = Color.white;
    private static readonly Color ColorBlack = Color.black;
    private static readonly Color ColorButtonMainHover = new Color(30f / 255f, 30f / 255f, 30f / 255f);
    private static readonly Color ColorButtonMenuHover = new Color(225f / 255f, 225f / 255f, 225f / 255f);
    private static readonly Color ColorButtonBlockHover = new Color(150f / 255f, 150f / 255f, 150f / 255f);

    // ===== Layout, at reference resolution 1920x1080 =====
    private const float RefWidth = 1920f;
    private const float RefHeight = 1080f;

    private const float ScreenMargin = 30f;

    private const float ToggleBtnW = 86f;
    private const float ToggleBtnH = 42f;
    private const float ToggleFontSize = 18f;
    private const float PanelToggleGap = 10f;

    private const float PanelW = 340f;
    private const float PanelPadding = 20f;

    private const float HeaderFontSize = 16f;
    private const float HeaderToButtonsGap = 20f;

    private const float BtnW = 300f;
    private const float BtnH = 42f;
    private const float BtnGap = 16f;
    private const float BtnFontSize = 18f;
    private const float PingFontSize = 16f;
    private const float PingW = 90f;

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

    private Font arialFont;
    private GUIStyle headerStyle;
    private GUIStyle whiteBtnStyle;
    private GUIStyle toggleBtnStyle;
    private GUIStyle leftHeaderStyle;
    private GUIStyle rightHeaderStyle;
    private GUIStyle pingStyle;
    private float lastScale = -1f;

    private void Awake()
    {
        Logger.LogInfo("GGC2LanModule: Awake() called, plugin is active.");
        arialFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

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

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle();
            headerStyle.font = arialFont;
            headerStyle.fontStyle = FontStyle.Normal;
            headerStyle.normal.textColor = ColorWhite;
        }

        if (whiteBtnStyle == null)
        {
            whiteBtnStyle = new GUIStyle();
            whiteBtnStyle.font = arialFont;
            whiteBtnStyle.fontStyle = FontStyle.Normal;
            whiteBtnStyle.normal.textColor = ColorBlack;
            whiteBtnStyle.alignment = TextAnchor.MiddleCenter;
        }

        if (toggleBtnStyle == null)
        {
            toggleBtnStyle = new GUIStyle();
            toggleBtnStyle.font = arialFont;
            toggleBtnStyle.fontStyle = FontStyle.Normal;
            toggleBtnStyle.normal.textColor = ColorWhite;
            toggleBtnStyle.alignment = TextAnchor.MiddleCenter;
        }

        if (leftHeaderStyle == null)
        {
            leftHeaderStyle = new GUIStyle(headerStyle) { alignment = TextAnchor.MiddleLeft };
        }

        if (rightHeaderStyle == null)
        {
            rightHeaderStyle = new GUIStyle(headerStyle) { alignment = TextAnchor.MiddleRight };
        }

        if (pingStyle == null)
        {
            pingStyle = new GUIStyle();
            pingStyle.font = arialFont;
            pingStyle.fontStyle = FontStyle.Normal;
            pingStyle.normal.textColor = Color.red;
            pingStyle.alignment = TextAnchor.MiddleRight;
        }

        // Font sizes scale with the screen resolution, so only recompute
        // them when the scale factor actually changes, not every frame.
        if (scaleChanged)
        {
            headerStyle.fontSize = Mathf.RoundToInt(HeaderFontSize * scale);
            whiteBtnStyle.fontSize = Mathf.RoundToInt(BtnFontSize * scale);
            toggleBtnStyle.fontSize = Mathf.RoundToInt(ToggleFontSize * scale);
            leftHeaderStyle.fontSize = headerStyle.fontSize;
            rightHeaderStyle.fontSize = headerStyle.fontSize;
            pingStyle.fontSize = Mathf.RoundToInt(PingFontSize * scale);
            lastScale = scale;
        }
    }

    private void DrawRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
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
                listening = false;
                return;
            }

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
        if (rtt > 999) rtt = 999;
        return rtt + "ms";
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
        float gap = PanelToggleGap * scale;

        bool mainMenuOpen = IsMainMenuSceneLoaded() && !MainMenuGameState.MainMenuIsTransitional;

        float toggleW = ToggleBtnW * scale;
        float toggleH = ToggleBtnH * scale;
        Rect toggleRect = new Rect(Screen.width - toggleW - margin, margin, toggleW, toggleH);

        // Ping sits top-right too, to the left of the LAN button with the
        // same gap the button uses toward the panel below it. When the LAN
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

        DrawRect(toggleRect, toggleRect.Contains(Event.current.mousePosition) ? ColorButtonMainHover : ColorBlack);
        if (GUI.Button(toggleRect, GUIContent.none, GUIStyle.none))
        {
            show = !show;
        }
        GUI.Label(toggleRect, show ? "X" : "LAN", toggleBtnStyle);

        if (!show) return;

        float panelW = PanelW * scale;
        float panelX = Screen.width - panelW - margin;
        float panelY = toggleRect.yMax + PanelToggleGap * scale;
        float pad = PanelPadding * scale;

        // Header row: title on the left, version on the right.
        Rect headerRect = new Rect(panelX + pad, panelY + pad, panelW - pad * 2, HeaderFontSize * scale + 4 * scale);

        float btnW = BtnW * scale;
        float btnH = BtnH * scale;
        float btnX = panelX + (panelW - btnW) / 2f;
        float firstBtnY = headerRect.yMax + HeaderToButtonsGap * scale;

        Rect hostRect = new Rect(btnX, firstBtnY, btnW, btnH);
        Rect findRect = new Rect(btnX, hostRect.yMax + BtnGap * scale, btnW, btnH);

        float panelH = (findRect.yMax - panelY) + pad;
        Rect panelRect = new Rect(panelX, panelY, panelW, panelH);

        DrawRect(panelRect, ColorBlack);

        GUI.Label(headerRect, "GGC2LanModule", leftHeaderStyle);
        GUI.Label(headerRect, "v1.5.0", rightHeaderStyle);

        // ----- HOST LAN SERVER -----
        // Synced from the server to every player via UNET's SyncVar system,
        // true during the ~3s countdown after "START THE GAME" is clicked.
        // Works identically for the host and for joined clients - unlike a
        // locally-tracked flag, which would only ever see the host's click.
        // Also blocked once actually in-game, as a safety net - the panel
        // isn't drawn there anyway, but better safe than sorry.
        bool gameStarting = NetworkCommunicator.lobbyIsLockedForGameStart
            || (LobbyManagerGGC.Instance != null && LobbyManagerGGC.Instance.state == StateInGame);

        DrawRect(hostRect, gameStarting ? ColorButtonBlockHover : (hostRect.Contains(Event.current.mousePosition) ? ColorButtonMenuHover : ColorWhite));
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
        GUI.Label(hostRect, isHosting ? "STOP HOSTING" : "HOST LAN SERVER", whiteBtnStyle);

        // ----- FIND LAN SERVER -----
        DrawRect(findRect, gameStarting
            ? ColorButtonBlockHover
            : (findRect.Contains(Event.current.mousePosition) ? ColorButtonMenuHover : ColorWhite));
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
        GUI.Label(findRect, listening ? "SEARCHING..." : "FIND LAN SERVER", whiteBtnStyle);
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