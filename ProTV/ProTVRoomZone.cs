/*
 * ============================================================================
 * ProTV Room Zone Manager
 * ============================================================================
 * Ein autarkes Trigger-Modul zur ressourcenschonenden Steuerung von 
 * ProTV / AVPro Instanzen in VRChat
 *
 * written by MrUnknownDE
 * https://mrunknown.de
 * ============================================================================
 */

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ProTVRoomZone : UdonSharpBehaviour
{
    [Header("Der Videoplayer für diesen Raum")]
    public GameObject localVideoPlayer;
    
    private BoxCollider roomCollider;

    void Start()
    {
        roomCollider = GetComponent<BoxCollider>();
        SendCustomEventDelayedSeconds(nameof(CheckSpawnPosition), 2.0f);
    }

    public void CheckSpawnPosition()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) return;
        if (roomCollider != null && roomCollider.bounds.Contains(player.GetPosition()))
        {
            if (localVideoPlayer != null) localVideoPlayer.SetActive(true);
        }
        else
        {
            if (localVideoPlayer != null) localVideoPlayer.SetActive(false);
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;
        if (localVideoPlayer != null) localVideoPlayer.SetActive(true);
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;
        if (localVideoPlayer != null) localVideoPlayer.SetActive(false);
    }
}