﻿using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using Mono.Cecil;
using UnityEngine;
using ZeepkistClient;
using ZeepkistNetworking;

public class PlaylistChatInterface
{
    private HashSet<string> backupsSaved;
    private OnlineZeeplevel currentLevel;
    private DirectoryInfo playlistDir;
    private DirectoryInfo backupDir;
    private FileInfo[] playlistFiles;
    private static Color colorText = new Color32(0xF1, 0xE6, 0xD9, 0xFF);
    private static Color colorSuccess = new Color32(0x58, 0x7B, 0x4B, 0xFF);
    private static Color colorFailure = new Color32(0xA8, 0x3E, 0x48, 0xFF);

    public ManualLogSource Logger;

    public PlaylistChatInterface()
    {
        Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(PlaylistChatInterface));
        backupsSaved = new HashSet<string>();
    }

    public void setLevel(LevelScriptableObject level)
    {
        currentLevel = new OnlineZeeplevel()
        {
            Name = level.Name,
            UID = level.UID,
            WorkshopID = level.WorkshopID,
            Author = level.Author
        };
    }

    public bool isPlaylistCommand(string message)
    {
        string[] tokens = message.Split(' ', 3);
        if (tokens[0] == "/pl" || tokens[0] == "/playlist")
        {
            if (tokens.Length == 3 && !(tokens[1] == "hlp" || tokens[1] == "help"))
            {
                // Update playlists because the user may have added one, or updated one through host controls
                playlistDir = new DirectoryInfo(Directory.CreateDirectory(PlayerManager.GetTargetFolder() + "\\Zeepkist\\Playlists").FullName);
                backupDir = new DirectoryInfo(Directory.CreateDirectory(PlayerManager.GetTargetFolder() + "\\Zeepkist\\Playlists\\Backups").FullName);
                playlistFiles = playlistDir.GetFiles("*.zeeplist", SearchOption.AllDirectories);

                executePlaylistCommand(tokens);
            }
            else
            {
                ZeepkistChatMessage zeepkistChatMessage = new ZeepkistChatMessage();
                zeepkistChatMessage.Message = getHelpMessage(tokens);
                zeepkistChatMessage.Message = $"<#F1E6D9><i>{zeepkistChatMessage.Message}</i></color>";
                addNewChatMessage(zeepkistChatMessage);
            }

            return true;
        }
        return false;
    }

    public void backupChangedPlaylists()
    {
        Logger.LogInfo("Backing up all playlists that were modified through chat.");
        foreach (string playlistName in backupsSaved)
        {
            backupPlaylist(playlistName, false);
        }
        backupsSaved.Clear();
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

    private int checkPlaylistExists(string playlistName)
    {
        for (int idx = 0; idx < playlistFiles.Length; ++idx)
        {
            string currentFile = playlistFiles[idx].Name;
            // Remove ".zeeplist" when comparing name to file name
            if (playlistName == currentFile.Remove(currentFile.Length - 9))
            {
                return idx;
            }
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

    private void addLevelToExistingPlaylist(string playlistName, bool duplicate = false)
    {
        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx == -1)
        {
            Logger.LogInfo($"Unable to add \"{currentLevel.Name}\" to playlist \"{playlistName}\". Playlist does not exist.");
            displayLog($"No \"{playlistName}\" playlist exists.", false);
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

    private void deleteLevelFromExistingPlaylist(string playlistName, bool all = false)
    {
        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx == -1)
        {
            Logger.LogInfo($"Unable to remove \"{currentLevel.Name}\" from playlist \"{playlistName}\". Playlist does not exist.");
            displayLog($"No \"{playlistName}\" playlist exists.", false);
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
        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx >= 0)
        {
            Logger.LogInfo($"Unable to create new playlist named \"{playlistName}\". Playlist already exists.");
            displayLog($"Playlist \"{playlistName}\" already exists.", false);
            return;
        }

        PlaylistSaveJSON playlistJSON = new PlaylistSaveJSON() { name = playlistName };
        writePlaylist(playlistJSON);

        Logger.LogInfo($"Created new playlist named \"{playlistName}\".");
        displayLog($"Playlist \"{playlistName}\" created.", true);
    }

    private void wipePlaylist(string playlistName)
    {
        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx == -1)
        {
            Logger.LogInfo($"Unable to wipe playlist named \"{playlistName}\". Playlist does not exist.");
            displayLog($"No \"{playlistName}\" playlist exists.", false);
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
        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx == -1)
        {
            Logger.LogInfo($"Unable to delete playlist named \"{playlistName}\". Playlist does not exist.");
            displayLog($"No \"{playlistName}\" playlist exists.", false);
            return;
        }

        // Force a back up first
        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);
        writePlaylist(playlistJSON, true);
        
        // Delete the file
        playlistFiles[playlistFileIdx].Delete();
        
        // Remove the backup status in case this playlist name is reused
        backupsSaved.Remove(playlistName);

        Logger.LogInfo($"Deleted playlist named \"{playlistName}\".");
        displayLog($"Deleted playlist \"{playlistName}\".", true);
    }

    private void countLevelsInPlaylist(string playlistName)
    {
        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx == -1)
        {
            Logger.LogInfo($"Unable to count levels in playlist named \"{playlistName}\". Playlist does not exist.");
            displayLog($"No \"{playlistName}\" playlist exists.", false);
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);

        Logger.LogInfo($"There are {playlistJSON.amountOfLevels} levels in playlist named \"{playlistName}\".");
        displayLog($"\"{playlistName}\" level count: {playlistJSON.amountOfLevels}.", true);
    }

    private void backupPlaylist(string playlistName, bool report=true)
    {
        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx == -1)
        {
            Logger.LogInfo($"Unable to backup playlist named \"{playlistName}\". Playlist does not exist.");
            if (report) displayLog($"No \"{playlistName}\" playlist exists.", false);
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);
        writePlaylist(playlistJSON, true);

        Logger.LogInfo($"Backed up playlist named \"{playlistName}\".");
        if (report) displayLog($"\"{playlistName}\" backed up.", true);
    }

    private void toggleShuffle(string playlistName)
    {
        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx == -1)
        {
            Logger.LogInfo($"Unable to backup playlist named \"{playlistName}\". Playlist does not exist.");
            displayLog($"No \"{playlistName}\" playlist exists.", false);
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);
        playlistJSON.shufflePlaylist = !playlistJSON.shufflePlaylist;
        writePlaylist(playlistJSON);

        Logger.LogInfo($"Shuffle {(playlistJSON.shufflePlaylist ? "en" : "dis")}abled for \"{playlistName}\".");
        displayLog($"\"{playlistName}\" shuffle {(playlistJSON.shufflePlaylist ? "en" : "dis")}abled.", true);
    }

    private void changeRoundLength(string playlistName, int roundLength)
    {
        int playlistFileIdx = checkPlaylistExists(playlistName);
        if (playlistFileIdx == -1)
        {
            Logger.LogInfo($"Unable to backup playlist named \"{playlistName}\". Playlist does not exist.");
            displayLog($"No \"{playlistName}\" playlist exists.", false);
            return;
        }

        PlaylistSaveJSON playlistJSON = loadPlaylist(playlistFiles[playlistFileIdx].FullName);
        playlistJSON.roundLength = roundLength;
        writePlaylist(playlistJSON);

        Logger.LogInfo($"Round length changed to {playlistJSON.roundLength} for \"{playlistName}\".");
        displayLog($"\"{playlistName}\" timer is {playlistJSON.roundLength}.", true);
    }

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
            message += "wipe<br>";
            message += "erase<br>";
            message += "count<br>";
            message += "backup<br>";
            message += "shuffle<br>";
            message += "<60-86400><br><br>";
            message += "Type \"/pl help <function>\" for more info.";
        }
        return message;
    }

    private void executePlaylistCommand(string[] tokens)
    {
        if (tokens[1] == "new" || tokens[1] == "create")
        {
            createNewPlaylist(tokens[2]);
        }
        else if (tokens[1] == "add" || tokens[1] == "insert" || tokens[1] == "in")
        {
            addLevelToExistingPlaylist(tokens[2]);
        }
        else if (tokens[1] == "dadd" || tokens[1] == "dinsert" || tokens[1] == "din")
        {
            addLevelToExistingPlaylist(tokens[2], true);
        }
        else if (tokens[1] == "remove" || tokens[1] == "rm" || tokens[1] == "delete" || tokens[1] == "del")
        {
            deleteLevelFromExistingPlaylist(tokens[2]);
        }
        else if (tokens[1] == "fremove" || tokens[1] == "frm" || tokens[1] == "fdelete" || tokens[1] == "fdel")
        {
            deleteLevelFromExistingPlaylist(tokens[2], true);
        }
        else if (tokens[1] == "wipe" || tokens[1] == "clear" || tokens[1] == "clr" || tokens[1] == "empty")
        {
            wipePlaylist(tokens[2]);
        }
        else if (tokens[1] == "erase" || tokens[1] == "drop")
        {
            deletePlaylist(tokens[2]);
        }
        else if (tokens[1] == "count" || tokens[1] == "cnt")
        {
            countLevelsInPlaylist(tokens[2]);
        }
        else if (tokens[1] == "backup" || tokens[1] == "bu")
        {
            backupPlaylist(tokens[2]);
        }
        else if (tokens[1] == "shuffle" || tokens[1] == "sh")
        {
            toggleShuffle(tokens[2]);
        }
        else
        {
            int roundLength;
            if (int.TryParse(tokens[1], out roundLength))
            {
                if (roundLength >= 60 && roundLength <= 86400)
                {
                    changeRoundLength(tokens[2], roundLength);
                }
                else
                {
                    displayLog($"Round timer must be from 60 to 86400.", false);
                }
            }
            else
            {
                Logger.LogInfo($"Unrecognized option \"{tokens[1]}\" with playlist command");
                displayLog($"\"{tokens[1]}\" is not a playlist function.", false);
            }
        }
    }

    private void addNewChatMessage(ZeepkistChatMessage message)
    {
        ZeepkistClient.ZeepkistNetwork.ChatMessages.Add(message);
        if (ZeepkistClient.ZeepkistNetwork.ChatMessages.Count > 20)
            ZeepkistClient.ZeepkistNetwork.ChatMessages.RemoveAt(0);
        Action<ZeepkistClient.ZeepkistChatMessage> chatMessageReceived = ZeepkistClient.ZeepkistNetwork.ChatMessageReceived;
        if (chatMessageReceived != null)
            chatMessageReceived(message);
    }
}