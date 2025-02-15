using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class HighScoreStatue : NetworkBehaviour
{
    private void OnMouseDown()
    {
        if (isClient)
        {
            // Send a command to the server to request high scores
            CmdRequestHighScores();
        }
    }

    [Command(requiresAuthority = false)] // Allow the command to be called without authority
    private void CmdRequestHighScores(NetworkConnectionToClient sender = null)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[HighScoreStatue] Cannot retrieve high scores because the server is not active.");
            return;
        }

        if (Database.singleton == null)
        {
            Debug.LogError("[HighScoreStatue] Database instance is null. Make sure the database is initialized.");
            return;
        }

        var highScores = Database.singleton.GetHighScores(10);
        if (highScores != null)
        {
            // Send the high scores back to the client that requested them
            TargetShowHighScores(sender, highScores);
        }
        else
        {
            Debug.LogWarning("[HighScoreStatue] No high scores to display.");
        }
    }

    [TargetRpc]
    private void TargetShowHighScores(NetworkConnection target, List<KeyValuePair<string, int>> highScores)
    {
        if (UIHighScores.singleton != null)
        {
            UIHighScores.singleton.ShowHighScores(highScores);
        }
        else
        {
            Debug.LogError("[HighScoreStatue] UIHighScores singleton not found.");
        }
    }
}
