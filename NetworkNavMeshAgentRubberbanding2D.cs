// Rubberband navmesh movement.
//
// How it works:
// - local player sends new position to server every 100ms
// - server validates the move
// - server broadcasts it to other clients
//   - clients apply it via agent.destination to get free interpolation
// - server also detects teleports to warp the client if needed
//
// The great part about this solution is that the client can move freely, but
// the server can still intercept with:
//   * agent.Warp()
//   * rubberbanding.ResetMovement()
// => all those calls are detected here and forced to the client.
//
// Note: no LookAtY needed because we move everything via .destination
// Note: only syncing .destination would save a lot of bandwidth, but it's way
//       too complicated to get right with both click AND wasd movement.
using UnityEngine;
using Mirror;

[RequireComponent(typeof(NavMeshAgent2D))]
public class NetworkNavMeshAgentRubberbanding2D : NetworkBehaviour
{
    public NavMeshAgent2D agent; // assign in Inspector (instead of GetComponent)
    public Entity entity;

    // remember last serialized values for dirty bit
    Vector2 lastSentPosition;
    double lastSentTime; // double for long term precision

    // check if a move is valid (the 'rubber' part)
    bool IsValidDestination(Vector2 position)
    {
        // there is virtually no way to cheat navmesh movement, since it will
        // never calcluate a path to a point that is not on the navmesh.
        // -> we only need to check if alive
        // -> and need to be IDLE or MOVING
        //    -> not while CASTING. the FSM resets path, but we don't event want
        //       to start it here. otherwise wasd movement could move a tiny bit
        //       while CASTING if Cmd sets destination and Player.UpateCASTING
        //       only resets it next frame etc.
        //    -> not while STUNNED.
        // -> maybe a distance check in case we get too far off from latency
        return entity.health.current > 0 &&
               (entity.state == "IDLE" || entity.state == "MOVING");
    }

    [Command]
    void CmdMoved(Vector2 position)
    {
        // rubberband (check if valid move)
        if (IsValidDestination(position))
        {
            // set position via .destination to get free interpolation
            agent.stoppingDistance = 0;
            agent.destination = position;

            // set dirty to trigger a OnSerialize next time, so that other clients
            // know about the new position too
            SetSyncVarDirtyBit(1);
        }
        else
        {
            // otherwise keep current position and set dirty so that OnSerialize
            // is trigger. it will warp eventually when getting too far away.
            SetSyncVarDirtyBit(1);
        }
    }

    void Update()
    {
        // NOTE: no automatic warp detection on server.
        //       Entity.Warp calls RpcWarped for 100% reliable detection.

        // local player can move freely. detect position changes.
        if (isLocalPlayer)
        {
            // send position every send interval.
            // only if changed though. no need to send same position twice!
            //
            // NOTE: epsilon comparison can cause agent positions to get
            //       slightly out of sync at times. it's not worth it.
            if (NetworkTime.time >= lastSentTime + syncInterval &&
                (Vector2)transform.position != lastSentPosition)
            {
                // host sets dirty without cmd/overwriting destination/etc.
                if (isServer)
                    SetSyncVarDirtyBit(1);
                // client sends to server to broadcast/set destination/etc.
                else
                    CmdMoved(transform.position);

                lastSentTime = NetworkTime.time;
                lastSentPosition = transform.position;
            }
        }
    }

    // 100% reliable warp. instead of trying to detect it based on speed etc.
    [ClientRpc]
    public void RpcWarp(Vector2 position)
    {
        // need to only Warp if the destination is on NavMesh.
        // for example:
        // - if we are a connected client
        // - someone next to us with NavMesh movement walks into a portal
        // - we get the RpcWarp for the other player in new instance position
        // - we aren't in the instance, so we never loaded the NavMesh
        // - we can't move the other player there, and we don't have to either.
        // => best to simply do nothing and wait for AoI to remove other player!
        if (NavMesh2D.SamplePosition(position, out NavMeshHit2D _, 0.1f, NavMesh2D.AllAreas))
        {
            agent.Warp(position);
        }
        else Debug.Log($"RpcWarp for {name} ignored because destination is not on NavMesh: {position}. This can happen if a NavMesh player next to us walked into an instance.");
    }

    // force reset movement on localplayer
    // => always call rubberbanding.ResetMovement instead of agent.ResetMovement
    //    when using Rubberbanding.
    // => there is no decent way to detect .ResetMovement on server while doing
    //    rubberband movement on client. it would always lead to false positives
    //    and accidental resets. this is the 100% safe way to do it here.
    [Server]
    public void ResetMovement()
    {
        // force reset on target
        TargetResetMovement(transform.position);

        // set dirty so onserialize notifies others
        SetSyncVarDirtyBit(1);
    }

    // force reset movement on localplayer
    // note: with rubberband movement, the server's player position always lags
    //       behind a bit. if server resets movement and then tells client to
    //       reset it, client will reset it while already behind ahead.
    // => solution: include reset position so we don't get out of sync.
    // -> if local player moves to B then player position on server is always
    //    a bit behind. if server resets movement then the player will stop
    //    abruptly where it is on server and on client, which is not the same
    //    yet. we need to stay in sync.
    [TargetRpc]
    void TargetResetMovement(Vector2 resetPosition)
    {
        // reset path and velocity
        //Debug.LogWarning(name + "(local=" + isLocalPlayer + ") TargetResetMovement @ " + resetPosition);
        agent.ResetMovement();
        agent.Warp(resetPosition);
    }

    // server-side serialization
    // used for the server to broadcast positions to other clients too
    public override void OnSerialize(NetworkWriter writer, bool initialState)
    {
        // always send position so client knows if he's too far off and needs warp
        // we also need it for wasd movement anyway
        writer.WriteVector2(transform.position);

        // always send speed in case it's modified by something
        writer.WriteFloat(agent.speed);
    }

    // client-side deserialization
    public override void OnDeserialize(NetworkReader reader, bool initialState)
    {
        // read position, speed, movement type in any case, so that we read
        // exactly what we write
        Vector2 position = reader.ReadVector2();
        float speed = reader.ReadFloat();

        // IMPORTANT: when spawning (=initialState), always warp to position!
        //            respawning a scene object might otherwise stay at the
        //            previous position on the client, causing movement desync.
        //            => fixes https://github.com/vis2k/uMMORPG2D/issues/4
        if (initialState)
        {
            agent.Warp(position);
        }

        // we can only apply the position if the agent is on the navmesh
        // (might not be while falling from the sky after joining, etc.)
        if (agent.isOnNavMesh)
        {
            // we can only move the agent to a position that is on the navmesh.
            // (might not if the agent walked into an instance, server
            //  broadcasted the new position to us, and proximity checker hasn't
            //  yet realized that the agent is out of sight. so it's not
            //  destroyed yet)
            // => 0.1f distance for network imprecision that might happen.
            // => if it happens when we simply do nothing and hope that the next
            //    update will be on a navmesh again.
            //    (if we were to Destroy it then we might get out of sync if
            //     the agent comes back out of the instance and was in proximity
            //     range the whole time)
            // NOTE: we *could* also call agent.proxchecker.Hide() and later
            //       Show() if agent is on a valid navmesh again. but let's keep
            //       the agents in front of the portal instead so we see what's
            //       happening. it's highly unlikely that an instance will be in
            //       proximity range of a player not in that instance anyway.
            if (NavMesh2D.SamplePosition(position, out NavMeshHit2D _, 0.1f, NavMesh2D.AllAreas))
            {
                // ignore for local player since he can move freely
                if (!isLocalPlayer)
                {
                    agent.stoppingDistance = 0;
                    agent.speed = speed;
                    agent.destination = position;
                }

                // rubberbanding: if we are too far off because of a rapid position
                // change or latency or server side teleport, then warp
                // -> agent moves 'speed' meter per seconds
                // -> if we are speed * 2 units behind, then we teleport
                //    (using speed is better than using a hardcoded value)
                // -> we use speed * 2 for update/network latency tolerance. player
                //    might have moved quit a bit already before OnSerialize was called
                //    on the server.
                if (Vector2.Distance(transform.position, position) > agent.speed * 2 && agent.isOnNavMesh)
                {
                    agent.Warp(position);
                    //Debug.LogWarning(name + "(local=" + isLocalPlayer + ") rubberbanding to " + position);
                }
            }
            else Debug.Log("NetworkNavMeshAgent.OnDeserialize: new position not on NavMesh, name=" + name + " new position=" + position + ". This could happen if the agent was warped to a dungeon instance that isn't on the local player.");
        }
        else Debug.LogWarning("NetworkNavMeshAgent.OnSerialize: agent not on NavMesh, name=" + name + " position=" + transform.position + " new position=" + position);
    }
}
