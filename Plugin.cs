using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;

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

    [HarmonyPatch(typeof(PhotonZeepkist), "OnDisconnectedFromGame")]
    public class PhotonZeepkist_OnDisconnectedFromGame
    {
        private static void Prefix()
        {
            playlistChat.resetBackups();
            playlistChat.setActive(false);
        }
    }

    [HarmonyPatch(typeof(OnlineGameplayUI), "OnOpen")]
    public class OnlineGameplayUI_OnOpen
    {
        private static void Postfix(GameObject ___tooltips)
        {
            GameObject commands = ___tooltips.transform.GetChild(0).gameObject;
            TextMeshProUGUI commandsText = commands.GetComponent<TextMeshProUGUI>();
            commandsText.text = playlistChat.addCommands(commandsText.text);
        }
    }

    [HarmonyPatch(typeof(PhotonZeepkist), "OnConnectedToGame")]
    public class PhotonZeepkist_OnConnectedToGame
    {
        private static void Postfix()
        {
            playlistChat.setActive(true);
        }
    }
}
