// We implemented a chat system that works directly with UNET. The chat supports
// different channels that can be used to communicate with other players:
//
// - **Local Chat:** by default, all messages that don't start with a **/** are
// addressed to the local chat. If one player writes a local message, then all
// players around him _(all observers)_ will be able to see the message.
// - **Whisper Chat:** a player can write a private message to another player by
// using the **/ name message** format.
// - **Guild Chat:** we implemented guild chat support with the **/g message**
// - **Info Chat:** the info chat can be used by the server to notify all
// players about important news. The clients won't be able to write any info
// messages.
//
// _Note: the channel names, colors and commands can be edited in the Inspector_
using System;
using UnityEngine;
using Mirror;

[Serializable]
public class ChannelInfo
{
    public string command; // /w etc.
    public string identifierOut; // for sending
    public string identifierIn; // for receiving
    public GameObject textPrefab;

    public ChannelInfo(string command, string identifierOut, string identifierIn, GameObject textPrefab)
    {
        this.command = command;
        this.identifierOut = identifierOut;
        this.identifierIn = identifierIn;
        this.textPrefab = textPrefab;
    }
}

[Serializable]
public struct ChatMessage
{
    public string sender;
    public string identifier;
    public string message;
    public string replyPrefix; // copied to input when clicking the message
    public GameObject textPrefab;

    public ChatMessage(string sender, string identifier, string message, string replyPrefix, GameObject textPrefab)
    {
        this.sender = sender;
        this.identifier = identifier;
        this.message = message;
        this.replyPrefix = replyPrefix;
        this.textPrefab = textPrefab;
    }

    // construct the message
    public string Construct()
    {
        return "<b>" + sender + identifier + ":</b> " + message;
    }
}

[RequireComponent(typeof(PlayerGuild))]
[RequireComponent(typeof(PlayerParty))]
[DisallowMultipleComponent]
public class PlayerChat : NetworkBehaviour
{
    [Header("Components")] // to be assigned in inspector
    public PlayerGuild guild;
    public PlayerParty party;
    public Player player;

    [Header("Channels")]
    public ChannelInfo whisperChannel = new ChannelInfo("/w", "(TO)", "(FROM)", null);
    public ChannelInfo localChannel = new ChannelInfo("", "", "", null);
    public ChannelInfo partyChannel = new ChannelInfo("/p", "(Party)", "(Party)", null);
    public ChannelInfo guildChannel = new ChannelInfo("/g", "(Guild)", "(Guild)", null);
    public ChannelInfo infoChannel = new ChannelInfo("", "(Info)", "(Info)", null);
    public ChannelInfo broadcastChannel = new ChannelInfo("'", "(Broadcast)", "(Broadcast)", null);
    public ChannelInfo deathtxtChannel = new ChannelInfo("", "(Death)", "(Death)", null);


    [Header("Other")]
    public int maxLength = 70;

    [Header("Events")]
    public UnityEventString onSubmit;

    public override void OnStartLocalPlayer()
    {
        // test messages
        UIChat.singleton.AddMessage(new ChatMessage("", infoChannel.identifierIn, "Use /w NAME to whisper", "",  infoChannel.textPrefab));
        UIChat.singleton.AddMessage(new ChatMessage("", infoChannel.identifierIn, "Use /p for party chat", "",  infoChannel.textPrefab));
        UIChat.singleton.AddMessage(new ChatMessage("", infoChannel.identifierIn, "Use /g for guild chat", "",  infoChannel.textPrefab));
        UIChat.singleton.AddMessage(new ChatMessage("", infoChannel.identifierIn, "Use ' to broadcast to everybody", "",  infoChannel.textPrefab));
        UIChat.singleton.AddMessage(new ChatMessage("", infoChannel.identifierIn, "Or click on a message to reply", "", infoChannel.textPrefab));
    }

    public bool IsAdmin()
    {
        return player != null && Database.singleton.IsAdminAccount(player.account);
    }

    // submit tries to send the string and then returns the new input text
    [Client]
    public string OnSubmit(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            string lastCommand = "";

            if (text.StartsWith("'"))
            {
                // Broadcast chat
                string message = text.Substring(1); // Remove the `'` character
                CmdMsgBroadcast(message);
            }
            else if (text.StartsWith(whisperChannel.command))
            {
                // Whisper
                (string user, string message) = ParsePM(whisperChannel.command, text);
                if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(message))
                {
                    if (user != name)
                    {
                        lastCommand = whisperChannel.command + " " + user + " ";
                        CmdMsgWhisper(user, message);
                    }
                    else Debug.Log("Cannot whisper to self");
                }
                else Debug.Log("Invalid whisper format: " + user + "/" + message);
            }
            else if (!text.StartsWith("/"))
            {
                // Local chat
                lastCommand = "";
                CmdMsgLocal(text);
            }
            else if (text.StartsWith("/drop "))
            {
                // Admin item drop
                string itemName = text.Substring(6).Trim();
                CmdAdminDropItem(itemName);
            }
            else if (text.StartsWith("/warpmeto "))
            {
                // Warp player to another player
                string targetPlayer = text.Substring(10).Trim();
                CmdAdminWarpMeTo(targetPlayer);
            }
            else if (text.StartsWith("/warptome "))
            {
                // Warp another player to the admin
                string targetPlayer = text.Substring(10).Trim();
                CmdAdminWarpToMe(targetPlayer);
            }
            else if (text.StartsWith(partyChannel.command))
            {
                // Party
                string msg = ParseGeneral(partyChannel.command, text);
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    lastCommand = partyChannel.command + " ";
                    CmdMsgParty(msg);
                }
            }
            else if (text.StartsWith(guildChannel.command))
            {
                // Guild
                string msg = ParseGeneral(guildChannel.command, text);
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    lastCommand = guildChannel.command + " ";
                    CmdMsgGuild(msg);
                }
            }

            // Addon system hooks
            onSubmit.Invoke(text);

            return lastCommand;
        }

        return "";
    }



    // parse a message of form "/command message"
    internal static string ParseGeneral(string command, string msg)
    {
        // return message without command prefix (if any)
        return msg.StartsWith(command + " ") ? msg.Substring(command.Length + 1) : "";
    }

    // parse a private message
    internal static (string user, string message) ParsePM(string command, string pm)
    {
        // parse to /w content
        string content = ParseGeneral(command, pm);

        // now split the content in "user msg"
        if (content != "")
        {
            // find the first space that separates the name and the message
            int i = content.IndexOf(" ");
            if (i >= 0)
            {
                string user = content.Substring(0, i);
                string msg = content.Substring(i+1);
                return (user, msg);
            }
        }
        return ("", "");
    }

    // networking //////////////////////////////////////////////////////////////
    [Command]
    void CmdMsgLocal(string message)
    {
        if (message.Length > maxLength) return;

        // Define the radius for local chat
        float radius = 15f;

        // Iterate over all online players
        foreach (var player in Player.onlinePlayers.Values)
        {
            if (Vector2.Distance(player.transform.position, transform.position) <= radius)
            {
                // Send the message to players within the radius
                player.chat.TargetMsgLocal(name, message);
            }
        }

        // Show the chat bubble above the sender's head for all clients
        player.RpcShowChatBubble(message);
    }

    [TargetRpc]
    public void TargetMsgDeath(string message)
    {
        UIChat.singleton.AddMessage(new ChatMessage("", deathtxtChannel.identifierIn, message, "", deathtxtChannel.textPrefab));
    }


    [TargetRpc]
    void TargetShowChatBubble(NetworkConnection target, string message)
    {
        // Get the Entity component to display the bubble
        GetComponent<Entity>().ShowChatBubble(message);
    }



    [Command]
    void CmdMsgBroadcast(string message)
    {
        if (message.Length > maxLength) return;

        // Send the broadcast message to all online players
        foreach (var player in Player.onlinePlayers.Values)
        {
            player.chat.TargetMsgBroadcast(name, message);
        }
    }

    [TargetRpc]
    public void TargetMsgBroadcast(string sender, string message)
    {
        string broadcastMessage = $"{message}";
        UIChat.singleton.AddMessage(new ChatMessage(sender, broadcastChannel.identifierIn, $"<color=#FFFF00>{broadcastMessage}</color>", "", broadcastChannel.textPrefab));
    }




    [TargetRpc]
    public void TargetMsgLocal(string sender, string message)
    {
        string identifier = sender != name ? localChannel.identifierIn : localChannel.identifierOut;
        string reply = whisperChannel.command + " " + sender + " "; // Prepare reply prefix
        UIChat.singleton.AddMessage(new ChatMessage(sender, identifier, message, reply, localChannel.textPrefab));
    }



    [Command]
    void CmdMsgParty(string message)
    {
        if (message.Length > maxLength) return;

        // send message to all online party members
        if (party.InParty())
        {
            foreach (string member in party.party.members)
            {
                if (Player.onlinePlayers.TryGetValue(member, out Player onlinePlayer))
                {
                    // call TargetRpc on that GameObject for that connection
                    onlinePlayer.chat.TargetMsgParty(name, message);
                }
            }
        }
    }

    [Command]
    void CmdMsgGuild(string message)
    {
        if (message.Length > maxLength) return;

        // send message to all online guild members
        if (guild.InGuild())
        {
            foreach (GuildMember member in guild.guild.members)
            {
                if (Player.onlinePlayers.TryGetValue(member.name, out Player onlinePlayer))
                {
                    // call TargetRpc on that GameObject for that connection
                    onlinePlayer.chat.TargetMsgGuild(name, message);
                }
            }
        }
    }

    [Command]
    void CmdAdminDropItem(string itemName)
    {
        if (!IsAdmin())
        {
            string AdminMessage = "You do not have permission to perform this action.";
            TargetMsgLocal(name, AdminMessage);
            return;
        }

        // Use GetStableHashCode to get the correct key for the item lookup
        int itemHash = itemName.GetStableHashCode();
        if (ScriptableItem.All.TryGetValue(itemHash, out ScriptableItem itemData))
        {
            if (AddonItemDrop.RandomPoint2D(transform.position, out Vector2 dropPoint))
            {
                ItemDrop loot = AddonItemDrop.GenerateLoot(itemData.name, false, 0, transform.position, dropPoint);
                NetworkServer.Spawn(loot.gameObject);

                string AdminMessage = $"Dropped 1 {itemData.name} at your location.";
                TargetMsgLocal(name, AdminMessage);
            }
            else
            {
                string AdminMessage = "Could not find a valid drop location.";
                TargetMsgLocal(name, AdminMessage);
            }
        }
        else
        {
            string AdminMessage = $"Item '{itemName}' not found in the database.";
            TargetMsgLocal(name, AdminMessage);
        }
    }



    [Command]
    void CmdAdminWarpMeTo(string targetPlayerName)
    {
        if (!IsAdmin())
        {
            TargetMsgInfo("You do not have permission to perform this action.");
            return;
        }

        if (Player.onlinePlayers.TryGetValue(targetPlayerName, out Player targetPlayer))
        {
            transform.position = targetPlayer.transform.position;
            TargetMsgInfo($"You have been warped to {targetPlayerName}.");
        }
        else
        {
            TargetMsgInfo($"Player '{targetPlayerName}' not found.");
        }
    }


    [Command]
    void CmdAdminWarpToMe(string targetPlayerName)
    {
        if (!IsAdmin())
        {
            TargetMsgInfo("You do not have permission to perform this action.");
            return;
        }

        if (Player.onlinePlayers.TryGetValue(targetPlayerName, out Player targetPlayer))
        {
            targetPlayer.transform.position = transform.position;
            TargetMsgInfo($"{targetPlayerName} has been warped to you.");
        }
        else
        {
            TargetMsgInfo($"Player '{targetPlayerName}' not found.");
        }
    }


    [Command]
    public void CmdSetAdmin(string targetAccount, bool isAdmin)
    {
        if (IsAdmin())
        {
            Database.singleton.SetAdminStatus(targetAccount, isAdmin);
            TargetMsgInfo($"{targetAccount} admin status set to {isAdmin}.");
        }
        else
        {
            TargetMsgInfo("You do not have permission to perform this action.");
        }
    }


    [Command]
    void CmdMsgWhisper(string playerName, string message)
    {
        if (message.Length > maxLength) return;

        // find the player with that name
        if (Player.onlinePlayers.TryGetValue(playerName, out Player onlinePlayer))
        {
            // receiver gets a 'from' message, sender gets a 'to' message
            // (call TargetRpc on that GameObject for that connection)
            onlinePlayer.chat.TargetMsgWhisperFrom(name, message);
            TargetMsgWhisperTo(playerName, message);
        }
    }

    // send a global info message to everyone
    [Server]
    public void SendGlobalMessage(string message)
    {
        foreach (Player player in Player.onlinePlayers.Values)
            player.chat.TargetMsgInfo(message);
    }

    // message handlers ////////////////////////////////////////////////////////
    [TargetRpc]
    public void TargetMsgWhisperFrom(string sender, string message)
    {
        // add message with identifierIn
        string identifier = whisperChannel.identifierIn;
        string reply = whisperChannel.command + " " + sender + " "; // whisper
        UIChat.singleton.AddMessage(new ChatMessage(sender, identifier, message, reply, whisperChannel.textPrefab));
    }

    [TargetRpc]
    public void TargetMsgWhisperTo(string receiver, string message)
    {
        // add message with identifierOut
        string identifier = whisperChannel.identifierOut;
        string reply = whisperChannel.command + " " + receiver + " "; // whisper
        UIChat.singleton.AddMessage(new ChatMessage(receiver, identifier, message, reply, whisperChannel.textPrefab));
    }

    [ClientRpc]
    public void RpcMsgLocal(string sender, string message)
    {
        // Only show the message for players within a certain radius
        float radius = 10f; // Adjust radius as needed
        foreach (var player in Player.onlinePlayers.Values)
        {
            if (Vector2.Distance(player.transform.position, transform.position) <= radius)
            {
                string identifier = sender != name ? localChannel.identifierIn : localChannel.identifierOut;
                string reply = whisperChannel.command + " " + sender + " "; // whisper
                UIChat.singleton.AddMessage(new ChatMessage(sender, identifier, message, reply, localChannel.textPrefab));
            }
        }
    }


    [TargetRpc]
    public void TargetMsgGuild(string sender, string message)
    {
        string reply = whisperChannel.command + " " + sender + " "; // whisper
        UIChat.singleton.AddMessage(new ChatMessage(sender, guildChannel.identifierIn, message, reply, guildChannel.textPrefab));
    }

    [TargetRpc]
    public void TargetMsgParty(string sender, string message)
    {
        string reply = whisperChannel.command + " " + sender + " "; // whisper
        UIChat.singleton.AddMessage(new ChatMessage(sender, partyChannel.identifierIn, message, reply, partyChannel.textPrefab));
    }

    [TargetRpc]
    public void TargetMsgInfo(string message)
    {
        AddMsgInfo(message);
    }



    // info message can be added from client too
    public void AddMsgInfo(string message)
    {
        UIChat.singleton.AddMessage(new ChatMessage("", infoChannel.identifierIn, message, "", infoChannel.textPrefab));
    }
}
