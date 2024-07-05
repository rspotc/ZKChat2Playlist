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
    private static CommandsHelpPages commandsHelp;

    private void Awake()
    {
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        playlistChat = new PlaylistChatInterface(MyPluginInfo.PLUGIN_GUID);
        commandsHelp = new CommandsHelpPages();

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

    [HarmonyPatch(typeof(PhotonZeepkist), "OnConnectedToGame")]
    public class PhotonZeepkist_OnConnectedToGame
    {
        private static void Postfix()
        {
            playlistChat.setActive(true);
        }
    }

    [HarmonyPatch(typeof(OnlineGameplayUI), "Awake")]
    public class OnlineGameplayUI_Awake
    {
        private static void Postfix(GameObject ___tooltips)
        {
            GameObject commands = ___tooltips.transform.GetChild(0).gameObject;
            TextMeshProUGUI commandsText = commands.GetComponent<TextMeshProUGUI>();
            commandsHelp.addPage(commandsText.text);
            commandsHelp.addPage(playlistChat.addCommands());

            commandsText.text = commandsHelp.getPage();
        }
    }

    [HarmonyPatch(typeof(OnlineGameplayUI), "Update")]
    public class OnlineGameplayUI_Update
    {
        private static void Postfix(GameObject ___tooltips, bool ___showTooltip, OnlineTabLeaderboardUI ___OnlineTabLeaderboard)
        {
            if (___showTooltip && ___OnlineTabLeaderboard.SwitchAction.buttonDown) {
                GameObject commands = ___tooltips.transform.GetChild(0).gameObject;
                TextMeshProUGUI commandsText = commands.GetComponent<TextMeshProUGUI>();
                commandsHelp.pageChange();
                commandsText.text = commandsHelp.getPage();
            }
        }
    }
}
