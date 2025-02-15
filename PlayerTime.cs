using UnityEngine;
using Mirror;
using System;

public class PlayerTime : NetworkBehaviour
{
    [SyncVar] public long playTime = 0;  // Stored time in seconds

    private DateTime loginTime;

    void Start()
    {
        if (isServer)
        {
            // Initialize login time on the server
            loginTime = DateTime.UtcNow;
        }
    }

    void Update()
    {
        if (isServer)
        {
            long elapsedSeconds = (long)(DateTime.UtcNow - loginTime).TotalSeconds;

            if (elapsedSeconds >= 0 && elapsedSeconds < 86400)
            {
                playTime += elapsedSeconds;
                loginTime = DateTime.UtcNow;
            }
        }
    }


    [ClientRpc]
    public void RpcUpdatePlayTime(long newPlayTime)
    {
        // Update play time on clients
        playTime = newPlayTime;
    }

    public string GetFormattedPlayTime()
    {
        TimeSpan time = TimeSpan.FromSeconds(playTime);
        return $"{time.Days} Days - {time.Hours} Hours - {time.Minutes} Minutes";
    }
}
