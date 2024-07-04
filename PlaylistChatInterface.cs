using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.ChatCommands;
using ZeepSDK.Chat;

public class PlaylistChatInterface
{
    private HashSet<string> backupsSaved;
    private OnlineZeeplevel currentLevel;
    private DirectoryInfo playlistDir;
    private FileInfo[] playlistFiles;
    private string lastUpdated;
    private bool activeStatus;

    private static Color colorText = new Color32(0xF1, 0xE6, 0xD9, 0xFF);
    private static Color colorSuccess = new Color32(0x58, 0x7B, 0x4B, 0xFF);
    private static Color colorFailure = new Color32(0xA8, 0x3E, 0x48, 0xFF);
    private static ILocalChatCommandExtension playlistChatCommand = null;

    public ManualLogSource Logger;
    
    public PlaylistChatInterface(string logName)
    {
        playlistChatCommand ??= setupChat();

        Logger = BepInEx.Logging.Logger.CreateLogSource(logName);
        backupsSaved = new HashSet<string>();
        setActive(false);
        lastUpdated = "";
    }

    public void setActive(bool active)
    {
        Logger.LogDebug($"Setting setLevel to {active}");
        activeStatus = active;
    }

    public void setLevel(LevelScriptableObject level)
    {
        if (!activeStatus || !(ZeepkistClient.ZeepkistNetwork.IsConnected)) return;

        currentLevel = new OnlineZeeplevel()
        {
            Name = level.Name,
            UID = level.UID,
            WorkshopID = level.WorkshopID == 0 ? ZeepkistNetwork.CurrentLobby.WorkshopID : level.WorkshopID,
            Author = level.Author
        };
    }

    public void resetBackups()
    {
        backupsSaved.Clear();
        lastUpdated = "";
    }

    public string addCommands(string currentCommands) { return currentCommands + "\n\n\n" + playlistChatCommand.repr(); }

    private string isPlaylistCommand(string playlistName)
    {
        // Update playlists because the user may have added one, or updated one through host controls
        playlistDir = new DirectoryInfo(Directory.CreateDirectory(Path.Combine(PlayerManager.GetTargetFolder(), "Zeepkist", "Playlists")).FullName);
        playlistFiles = playlistDir.GetFiles("*.zeeplist", SearchOption.TopDirectoryOnly);
        Array.Sort(playlistFiles, (f1, f2) => { return f1.Name.CompareTo(f2.Name); });

        string resultName = lastUpdated;
        if (playlistName != "") resultName = playlistName;
        return resultName;
    }

    private bool checkLevelLoaded()
    {
        Logger.LogDebug($"currentLevel is Name: {currentLevel.Name}, Author: {currentLevel.Author}, UID: {currentLevel.UID}, WorkshopID: {currentLevel.WorkshopID}");
        if (currentLevel.WorkshopID == 0 && currentLevel.Author != "Yannic")
        {
            displayLog("Unable to execute command. Level data failed to load into Chat2Playlist Mod.", false);
            return false;
        }
        return true;
    }

    private void displayLog(string message, bool goodStatus)
    {
        if (goodStatus)
        {
            PlayerManager.Instance.messenger.LogCustomColor(message, 2.5f, colorText, colorSuccess);
        }
        else
        {
            PlayerManager.Instance.messenger.LogCustomColor(message, 2.5f, colorText, colorFailure);
        }
    }

    private int checkPlaylistExists(string playlistName, bool wantExist=true)
    {
        if (playlistName == "")
        {
            displayLog("You must specify a playlist name.", false);
            return -2;
        }

        for (int idx = 0; idx < playlistFiles.Length; ++idx)
        {
            string currentFile = playlistFiles[idx].Name;
            // Remove ".zeeplist" when comparing name to file name
            if (playlistName == currentFile.Remove(currentFile.Length - 9))
            {
                if (wantExist) lastUpdated = playlistName;
                return idx;
            }
        }

        if (wantExist)
        {
            displayLog($"No \"{playlistName}\" playlist exists.", false);
        }
        return -1;
    }

    private PlaylistSaveJSON loadPlaylist(string filename)
    {
        return JsonUtility.FromJson<PlaylistSaveJSON>(File.ReadAllText(filename));
    }

    private int findLevel(ref PlaylistSaveJSON playlistJSON)
    {
        return playlistJSON.levels.FindIndex(x => (x.UID == currentLevel.UID && x.WorkshopID == currentLevel.WorkshopID));
    }

    private void writePlaylist(PlaylistSaveJSON playlist, bool backup = false)
    {
        backup = (backup || backupsSaved.Add(playlist.name));
        DirectoryInfo backupDir = new DirectoryInfo(Directory.CreateDirectory(PlayerManager.GetTargetFolder() + "\\Zeepkist\\Playlists\\Backups").FullName);

        PlayerManager.Instance.safeSaving.SafeWriteAllText(
            playlistDir.FullName,
            playlist.name,
            JsonUtility.ToJson(playlist, true),
            backupDir.FullName,
            playlist.name + "_bckp",
            SafeFileSaving.ZeepFileExtension.zeeplist,
            backup,
            true
        );
    }

    private void addDuplicateLevelToExistingPlaylist(string playlistName)
    {
        addLevelToExistingPlaylist(playlistName, true);
    }

    private void addUniqueLevelToExistingPlaylist(string playlistName)
    {
        addLevelToExistingPlaylist(playlistName);
    }

    private void addLevelToExistingPlaylist(string playlistName, bool duplicate = false)
    {
        if (!checkLevelLoaded()) return;
        playlistName = isPlaylistCommand(playlistName);

        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx <= -1)
        {
            Logger.LogInfo($"Unable to add \"{currentLevel.Name}\" to playlist \"{playlistName}\". Playlist does not exist.");
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);
        if (!duplicate && findLevel(ref playlistJSON) >= 0)
        {
            Logger.LogInfo($"Unable to add \"{currentLevel.Name}\" to playlist \"{playlistName}\". Level already exists in playlist. Try duplicate insert (\"dadd\", \"dinsert\", or \"din\") instead.");
            displayLog($"\"{currentLevel.Name}\" already in playlist \"{playlistName}\". Try dinsert for duplicates.", false);
            return;
        }

        playlistJSON.amountOfLevels++;
        playlistJSON.levels.Add(currentLevel);
        writePlaylist(playlistJSON);

        Logger.LogInfo($"Added \"{currentLevel.Name}\" to playlist \"{playlistName}\".");
        displayLog($"Added \"{currentLevel.Name}\" to playlist \"{playlistName}\".", true);
    }

    private void fullDeleteLevelFromExistingPlaylist(string playlistName)
    {
        deleteLevelFromExistingPlaylist(playlistName, true);
    }
    private void singleDeleteLevelFromExistingPlaylist(string playlistName)
    {
        deleteLevelFromExistingPlaylist(playlistName);
    }

    private void deleteLevelFromExistingPlaylist(string playlistName, bool all = false)
    {
        if (!checkLevelLoaded()) return;
        playlistName = isPlaylistCommand(playlistName);

        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx <= -1)
        {
            Logger.LogInfo($"Unable to remove \"{currentLevel.Name}\" from playlist \"{playlistName}\". Playlist does not exist.");
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);

        int removedCount = 0;
        int zeeplevelIdx;
        do
        {
            zeeplevelIdx = playlistJSON.levels.FindIndex(x => (x.UID == currentLevel.UID && x.WorkshopID == currentLevel.WorkshopID));
            if (zeeplevelIdx >= 0)
            {
                playlistJSON.levels.RemoveAt(zeeplevelIdx);
                playlistJSON.amountOfLevels--;
                writePlaylist(playlistJSON);
                removedCount++;
            }
        } while (all && zeeplevelIdx >= 0);

        if (removedCount == 0)
        {
            Logger.LogInfo($"Unable to remove \"{currentLevel.Name}\" from playlist \"{playlistName}\". Does not exist in playlist.");
            displayLog($"\"{currentLevel.Name}\" was not in playlist \"{playlistName}\".", false);
        }
        else
        {
            Logger.LogInfo($"Removed {removedCount} of \"{currentLevel.Name}\" from playlist \"{playlistName}\".");
            displayLog($"Removed {removedCount} of \"{currentLevel.Name}\" from playlist \"{playlistName}\".", true);
        }
    }

    private void createNewPlaylist(string playlistName)
    {
        isPlaylistCommand(playlistName);

        int playlistFileIdx = checkPlaylistExists(playlistName, false);
        if (playlistFileIdx >= 0)
        {
            Logger.LogInfo($"Unable to create new playlist named \"{playlistName}\". Playlist already exists.");
            displayLog($"Playlist \"{playlistName}\" already exists.", false);
            return;
        }
        else if (playlistFileIdx == -2) return;

        PlaylistSaveJSON playlistJSON = new PlaylistSaveJSON() { name = playlistName };
        backupsSaved.Add(playlistJSON.name);
        writePlaylist(playlistJSON);
        lastUpdated = playlistName;

        Logger.LogInfo($"Created new playlist named \"{playlistName}\".");
        displayLog($"Playlist \"{playlistName}\" created.", true);
    }

    private void wipePlaylist(string playlistName)
    {
        playlistName = isPlaylistCommand(playlistName);

        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx <= -1)
        {
            Logger.LogInfo($"Unable to wipe playlist named \"{playlistName}\". Playlist does not exist.");
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);
        playlistJSON.levels.Clear();
        playlistJSON.amountOfLevels = 0;
        writePlaylist(playlistJSON);

        Logger.LogInfo($"Wiped all levels from playlist named \"{playlistName}\".");
        displayLog($"Removed all levels from playlist \"{playlistName}\".", true);
    }
    
    private void deletePlaylist(string playlistName)
    {
        playlistName = isPlaylistCommand(playlistName);

        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx <= -1)
        {
            Logger.LogInfo($"Unable to delete playlist named \"{playlistName}\". Playlist does not exist.");
            return;
        }

        // Force a back up first
        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);
        writePlaylist(playlistJSON, true);
        
        // Delete the file
        playlistFiles[playlistFileIdx].Delete();
        lastUpdated = "";
        
        Logger.LogInfo($"Deleted playlist named \"{playlistName}\".");
        displayLog($"Deleted playlist \"{playlistName}\".", true);
    }

    private void countLevelsInPlaylist(string playlistName)
    {
        playlistName = isPlaylistCommand(playlistName);

        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx <= -1)
        {
            Logger.LogInfo($"Unable to count levels in playlist named \"{playlistName}\". Playlist does not exist.");
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);

        Logger.LogInfo($"There are {playlistJSON.amountOfLevels} levels in playlist named \"{playlistName}\".");
        displayLog($"\"{playlistName}\" level count: {playlistJSON.amountOfLevels}.", true);
    }

    private void backupPlaylist(string playlistName)
    {
        playlistName = isPlaylistCommand(playlistName);

        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx <= -1)
        {
            Logger.LogInfo($"Unable to backup playlist named \"{playlistName}\". Playlist does not exist.");
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);
        writePlaylist(playlistJSON, true);

        Logger.LogInfo($"Backed up playlist named \"{playlistName}\".");
        displayLog($"\"{playlistName}\" backed up.", true);
    }

    private void toggleShuffle(string playlistName)
    {
        playlistName = isPlaylistCommand(playlistName);

        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx <= -1)
        {
            Logger.LogInfo($"Unable to backup playlist named \"{playlistName}\". Playlist does not exist.");
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);
        playlistJSON.shufflePlaylist = !playlistJSON.shufflePlaylist;
        writePlaylist(playlistJSON);

        Logger.LogInfo($"Shuffle {(playlistJSON.shufflePlaylist ? "en" : "dis")}abled for \"{playlistName}\".");
        displayLog($"\"{playlistName}\" shuffle {(playlistJSON.shufflePlaylist ? "en" : "dis")}abled.", true);
    }

    private void changeRoundLength(string tokensString)
    {
        string[] tokens = tokensString.Split(' ', 2);
        string playlistName = isPlaylistCommand(tokens.Length == 2 ? tokens[1] : "");
        int roundLength;
        if (int.TryParse(tokens[0], out roundLength))
        {
            if (roundLength >= 60 && roundLength <= 86400)
            {
                changeRoundLengthOnPlaylist(playlistName, roundLength);
            }
            else
            {
                displayLog($"Round timer must be from 60 to 86400.", false);
            }
        }
        else
        {
            Logger.LogInfo($"Unrecognized option \"{tokens[0]}\" with playlist command");
            displayLog($"\"{tokens[0]}\" is not a playlist function.", false);
        }
    }

    private void changeRoundLengthOnPlaylist(string playlistName, int roundLength)
    {
        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx <= -1)
        {
            Logger.LogInfo($"Unable to backup playlist named \"{playlistName}\". Playlist does not exist.");
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);
        playlistJSON.roundLength = roundLength;
        writePlaylist(playlistJSON);

        Logger.LogInfo($"Round length changed to {playlistJSON.roundLength} for \"{playlistName}\".");
        displayLog($"\"{playlistName}\" timer is {playlistJSON.roundLength}.", true);
    }

    /*
    private string getHelpMessage(string[] tokens)
    {
        string message;
        if (tokens.Length == 3 && (tokens[1] == "help" || tokens[1] == "hlp"))
        {
            if (tokens[2] == "create")
            {
                message = "create - Create a new empty playlist with the specified name. (aliases; new)";
            }
            else if (tokens[2] == "insert")
            {
                message = "insert - Add track to the specified playlist. Will not add if the track is already in the playlist. (aliases; add, in)";
            }
            else if (tokens[2] == "dinsert")
            {
                message = "dinsert - Add the current track to the specified playlist, with duplicates allowed. (aliases; dadd, din)";
            }
            else if (tokens[2] == "remove")
            {
                message = "remove - Remove one instance of the current track from the specified playlist. (aliases; rm, delete, del)";
            }
            else if (tokens[2] == "fremove")
            {
                message = "fremove - Remove all instances (full remove) of the current track from the specified playlist. (aliases; frm, fdelete, fdel)";
            }
            else if (tokens[2] == "wipe")
            {
                message = "wipe - Remove all tracks from the specified playlist, leaving an empty playlist. (aliases; clear, clr, empty)";
            }
            else if (tokens[2] == "erase")
            {
                message = "erase - Erase the specified playlist entirely. (aliases; drop)";
            }
            else if (tokens[2] == "count")
            {
                message = "count - Count the number of tracks in the specified playlist. (aliases; cnt)";
            }
            else if (tokens[2] == "backup")
            {
                message = "backup - Force a backup of the specified playlist. Will auto backup before first edit of the playlist in and upon leaving each lobby. (aliases; bu)";
            }
            else if (tokens[2] == "shuffle")
            {
                message = "shuffle - Toggle the shuffle option for the specified playlist. (aliases; sh)";
            }
            else if (tokens[2] == "list")
            {
                message = "list - List existing playlists, displaying 10 per page. Substitute the standard <playlist-name> with <page-number> defaulting to 1.";
            }
            else
            {
                message = "60-86400 - Set the lobby timer for the specified playlist to the provided number of seconds.";
            }
        }
        else
        {
            message = "/playlist (/pl) commands are of the form<br>/pl <function> <playlist-name><br>";
            message += "Example:<br>\"/pl add Test Playlist\"<br>This will add the currently loaded level to the playlist \"Test Playlist\".<br><br>";
            message += "Supported functions are:<br>";
            message += "insert<br>";
            message += "dinsert<br>";
            message += "remove<br>";
            message += "fremove<br>";
            message += "create<br>";
            message += "wipe<br>";
            message += "erase<br>";
            message += "count<br>";
            message += "backup<br>";
            message += "shuffle<br>";
            message += "list<br>";
            message += "<60-86400><br><br>";
            message += "Type \"/pl help <function>\" for more info.";
        }
        return message;
    }
    */

    private void listPlaylists(string pageNumStr)
    {
        isPlaylistCommand("");
        
        int pageNum = 1;
        if (!(int.TryParse(pageNumStr, out pageNum))) {
            pageNum = 1;
        }

        if ((pageNum - 1) * 10 >= playlistFiles.Length) pageNum = 1;

        string playlistMessage = $"Playlists Page {pageNum}";
        int playlistIndex = (pageNum - 1) * 10;
        while (playlistIndex < Math.Min(pageNum*10, playlistFiles.Length))
        {
            string playlistName = playlistFiles[playlistIndex].Name;
            playlistMessage += $"<br>{playlistIndex+1}. {playlistName.Remove(playlistName.Length-9)}";
            playlistIndex++;
        }

        ChatApi.AddLocalMessage($"<#F1E6D9><i>{playlistMessage}</i></color>");
    }

    private ILocalChatCommandExtension setupChat()
    {
        ILocalChatCommandExtension chatCommand = new ILocalChatCommandExtension("/", "pl", "Set the lobby timer for the specified playlist to the provided number of seconds.", changeRoundLength, false, new string[] { "0-86400", "playlist name" });
        chatCommand.addAlias("playlist");
        chatCommand.registerSubcommand("create", "Create a new empty playlist with the specified name.", ["new"], createNewPlaylist, ["playlist name"]);
        chatCommand.registerSubcommand("insert", "Add the current track to a specified playlist. Will not add if the track is already in the playlist.", ["add", "in"], addUniqueLevelToExistingPlaylist, ["playlist name"]);
        chatCommand.registerSubcommand("dinsert", "Add the current track to a specified playlist, with duplicates allowed.", ["dadd", "din"], addDuplicateLevelToExistingPlaylist, ["playlist name"]);
        chatCommand.registerSubcommand("remove", "Remove one instance of the current track from the specified playlist.", ["rm", "delete", "del"], singleDeleteLevelFromExistingPlaylist, ["playlist name"]);
        chatCommand.registerSubcommand("fremove", "Remove all instances (full remove) of the current track from the specified playlist.", ["frm", "fdelete", "fdel"], fullDeleteLevelFromExistingPlaylist, ["playlist name"]);
        chatCommand.registerSubcommand("wipe", "Remove all tracks from the specified playlist, leaving an empty playlist.", ["clear", "clr", "empty"], wipePlaylist, ["playlist name"]);
        chatCommand.registerSubcommand("erase", "Erase the specified playlist entirely.", ["drop"], deletePlaylist, ["playlist name"]);
        chatCommand.registerSubcommand("count", "Count the number of tracks in the specified playlist.", ["cnt"], countLevelsInPlaylist, ["playlist name"]);
        chatCommand.registerSubcommand("backup", "Force a backup of the specified playlist. (Will auto backup before first edit of the playlist in and upon leaving each lobby.)", ["bu"], backupPlaylist, ["playlist name"]);
        chatCommand.registerSubcommand("shuffle", "Toggle the shuffle option for the specified playlist.", ["sh"], toggleShuffle, ["playlist name"]);
        chatCommand.registerSubcommand("list", "List existing playlists, displaying 10 per page.", [], listPlaylists, ["page number"]);

        return chatCommand;
    }
}
