using BepInEx;
using HarmonyLib;

namespace Zeepkist;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("ZeepSDK")]
public class Plugin : BaseUnityPlugin
{
    private Harmony harmony;
    private static PlaylistChatInterface playlistChat;

    private void Awake()
    {
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        playlistChat = new PlaylistChatInterface(MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        harmony = null;
    }

    [HarmonyPatch(typeof(SetupGame), "FinishLoading")]
    public class SetupGame_FinishLoading
    {
        private static void Postfix(LevelScriptableObject ___GlobalLevel)
        {
            playlistChat.setLevel(___GlobalLevel);
        }
    }

    [HarmonyPatch(typeof(OnlineChatUI), "SendChatMessage")]
    public class OnlineChatUI_SendChatMessage
    {
        private static bool Prefix(string message)
        {
            return !(playlistChat.isPlaylistCommand(message));
        }
    }

    [HarmonyPatch(typeof(PhotonZeepkist), "OnDisconnectedFromGame")]
    public class PauseMenuUI_OnQuit
    {
        private static void Prefix()
        {
//            playlistChat.backupChangedPlaylists();
            playlistChat.resetBackups();
            playlistChat.setActive(false);
        }
    }

    [HarmonyPatch(typeof(PhotonZeepkist), "OnConnectedToGame")]
    public class LobbyManager_GoToLobby
    {
        private static void Postfix()
        {
            playlistChat.setActive(true);
        }
    }
}

//    <PackageReference Include="Zeepkist.GameLibs" Version="16.0.1392" />
//    <PackageReference Include="ZeepSDK" Version="1.*" />
