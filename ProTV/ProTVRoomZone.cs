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
    [Space(10)]
    [Header("Fade Settings")]
    [Tooltip("Wie lange soll das Ein-/Ausblenden in Sekunden dauern?")]
    public float fadeDuration = 1.5f;

    [Tooltip("Ziehe hier die AudioSources des ProTVs rein - damit die Lautstärke smooth gefadet wird.")]
    public AudioSource[] audioSources;

    private BoxCollider roomCollider;
    private float fadeProgress = 0f;
    private int fadeState = 0; 
    
    // Hier speichern wir die echte Lautstärke, bevor sie verfälscht wird
    private float[] savedVolumes; 

    void Start()
    {
        roomCollider = GetComponent<BoxCollider>();

        // Arrays initialisieren und Basis-Lautstärke beim Start sichern
        if (audioSources != null && audioSources.Length > 0)
        {
            savedVolumes = new float[audioSources.Length];
            for (int i = 0; i < audioSources.Length; i++)
            {
                if (audioSources[i] != null) savedVolumes[i] = audioSources[i].volume;
            }
        }

        SendCustomEventDelayedSeconds(nameof(CheckSpawnPosition), 2.0f);
    }

    public void CheckSpawnPosition()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) return;

        if (roomCollider != null && roomCollider.bounds.Contains(player.GetPosition()))
        {
            if (localVideoPlayer != null) localVideoPlayer.SetActive(true);
            fadeProgress = 1f;
        }
        else
        {
            // Update Saved Volumes bevor wir beim Spawn abschalten
            UpdateSavedVolumes();
            
            if (localVideoPlayer != null) localVideoPlayer.SetActive(false);
            fadeProgress = 0f;
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;

        if (localVideoPlayer != null) localVideoPlayer.SetActive(true);
        
        // Starte Fade In von 0
        fadeProgress = 0f;
        fadeState = 1; 
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;

        // GANZ WICHTIG: Echte Lautstärke sichern, BEVOR wir faden
        UpdateSavedVolumes();

        fadeProgress = 1f;
        fadeState = -1; // Starte Fade Out
    }

    void Update()
    {
        if (fadeState == 0) return;

        if (fadeState == 1) // FADE IN
        {
            fadeProgress += Time.deltaTime / fadeDuration;
            if (fadeProgress >= 1f)
            {
                fadeProgress = 1f;
                fadeState = 0; 
            }
            ApplyFadedVolume();
        }
        else if (fadeState == -1) // FADE OUT
        {
            fadeProgress -= Time.deltaTime / fadeDuration;
            
            if (fadeProgress <= 0f)
            {
                fadeProgress = 0f;
                fadeState = 0; 
                
                // DER PRO-TV FIX:
                // Wir zwingen die Original-Lautstärke in exakt diesem Frame zurück,
                // direkt bevor wir das Objekt deaktivieren. ProTV speichert so den echten Wert!
                RestoreOriginalVolume();

                if (localVideoPlayer != null) localVideoPlayer.SetActive(false);
            }
            else
            {
                ApplyFadedVolume();
            }
        }
    }

    // --- Hilfsfunktionen für saubereren Code ---

    private void UpdateSavedVolumes()
    {
        if (audioSources == null) return;
        for (int i = 0; i < audioSources.Length; i++)
        {
            // Wir aktualisieren die gespeicherte Lautstärke nur, 
            // wenn sie gerade nicht auf 0 runtergefadet ist.
            if (audioSources[i] != null && audioSources[i].volume > 0.05f) 
            {
                savedVolumes[i] = audioSources[i].volume;
            }
        }
    }

    private void ApplyFadedVolume()
    {
        if (audioSources == null || savedVolumes == null) return;
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null)
            {
                audioSources[i].volume = savedVolumes[i] * fadeProgress;
            }
        }
    }

    private void RestoreOriginalVolume()
    {
        if (audioSources == null || savedVolumes == null) return;
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null)
            {
                audioSources[i].volume = savedVolumes[i];
            }
        }
    }
}