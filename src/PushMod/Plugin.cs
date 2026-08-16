using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PushMod;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin {
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigurationHandler PConfig { get; private set; } = null!;

    private void Awake() {
        Log = Logger;
        PConfig = new ConfigurationHandler(this);
        Log.LogInfo($"Plugin {Name} is loaded!");
        Harmony.CreateAndPatchAll(typeof(PushPatch));
    }
}

public static class PushPatch {
    /// <summary>
    /// Postfix patch on Character.Awake to attach the PushManager component.
    /// Ensures every character in the game gets a PushManager when initialized.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Character), nameof(Character.Awake))]
    public static void AwakePatch(Character __instance) {
        __instance.gameObject.AddComponent<PushManager>();
        Plugin.Log.LogInfo($"Added PushManager component to character: {__instance.characterName}");
    }


    /// <summary>
    /// Prefix patch on GUIManager.UpdateReticle to change the reticle whilst pushing.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(GUIManager), nameof(GUIManager.UpdateReticle))]
    public static bool ReticlePatch(GUIManager __instance) {
        PushManager pushManager = (PushManager)Character.localCharacter.GetComponent(typeof(PushManager));
        if (pushManager is null) return true;

        if (pushManager.animationCoolDown > 0f) {
            __instance.SetReticle(__instance.reticleReach);
            return false;
        }

        // Show reticle when raycast hits other player
        if (pushManager.isCharging && !pushManager.isPushingSelf && pushManager.hitCharacter != null && pushManager.hitCharacter != Character.localCharacter) {
            __instance.SetReticle(__instance.reticleShoot);
            return false;
        }

        return true;
    }
}

public class PushManager : MonoBehaviour {
    // ============================== Configuration Constants ===================================================
    private const float PUSH_RANGE = 2.5f;                      // Maximum distance for push interaction
    private const float PUSH_COOLDOWN = 1f;                     // Cooldown time between successful pushes
    private const float PUSH_FORCE_BASE = 500f;                 // Base push force applied
    private const float BINGBONG_MULTIPLIER = 10f;              // Force multiplier when holding "BingBong" item
    private const float STAMINA_COST = 0.1f;                    // Stamina consumed per push

    private const float MAX_CHARGE = 1f;                        // Maximum charge duration (seconds)
    private const float CHARGE_FORCE_MULTIPLIER = 1.5f;         // Additional force multiplier based on charge level
    private const float ANIMATION_TIME = 0.25f;                 // Fixed animation playback time
    private const float MAX_STAMINA_COST_MULTIPLIER = 3f;       // Maximum stamina cost multiplier.
    // ==========================================================================================================

    // ====================================== Debug & UI ========================================================
    private Color chargeBarMinColor = new Color(0.3483f, 0.7843f, 0f);             // Color of the charge bar at min charge
    private Color chargeBarMaxColor = new Color(0.749f, 0.9255f, 0.1098f);         // Color of the charge bar at max charge

    private Character localCharacter = null!;
    private Character? pushedCharacter;
    public Character? hitCharacter;
    
    private float coolDownLeft;                                 // Remaining cooldown time before next push
    public float animationCoolDown;                             // Duration of active push animation

    private bool bingBong;                                      // True if player is holding the "BingBong" item

    // Charging system
    public bool isCharging;                                    // Whether the player is currently charging a push
    public bool isPushingSelf;                                 // Whether the player is currently trying to push themselves
    public float currentCharge;                                // Current charge level (0 to MAX_CHARGE)

    private GameObject? chargeBarObject;
    private Image? chargeBar;

    // ====================== Cached components for performance optimization ====================================
    private Character cachedCharacter = null!;
    private Camera mainCamera = null!;
    // ==========================================================================================================

    private void Awake() {
        // Cache the Character component on this GameObject
        cachedCharacter = GetComponent<Character>();
        if (cachedCharacter is null) {
            Debug.LogError("[PushManager] Character component not found on GameObject!", gameObject);
            enabled = false;
            return;
        }

        // Store reference to local player's character
        if (cachedCharacter.IsLocal) {
            localCharacter = cachedCharacter;

            // Cache main camera for raycasting
            mainCamera = Camera.main;
            if (mainCamera is null) {
                Debug.LogError("[PushManager] Main camera not found!");
                enabled = false;
            }
        }
    }

    private void Update() {
        // Update cooldown timers
        if (coolDownLeft > 0f) coolDownLeft -= Time.deltaTime;
        if (animationCoolDown > 0f) animationCoolDown -= Time.deltaTime;

        // Play push animation
        if (animationCoolDown > 0f && cachedCharacter is not null) {
            PlayPushAnimation(cachedCharacter);
        }

        if (coolDownLeft > 0f) return;
        if (localCharacter is null) return;
        if (!localCharacter.view.IsMine) return;

        if (!localCharacter.data.fullyConscious || localCharacter.data.isCarried || localCharacter.data.isClimbingAnything) {
            if (chargeBarObject) chargeBarObject.SetActive(false);
            return;
        }

        // Check if the current held item is "BingBong"
            Item? currentItem = localCharacter.data.currentItem;
        bingBong = currentItem is not null && currentItem.itemTags is Item.ItemTags.BingBong; // Multi-language support 🤡

        // Handle input for charge-based pushing
        HandleChargeInput();

        // Create Charge bar UI element
        if (chargeBar is null || chargeBarObject is null) {
            GameObject throwUIObject = GameObject.Find("GAME/GUIManager/Canvas_HUD/Throw/");
            chargeBarObject = Instantiate(throwUIObject);
            chargeBarObject.name = "PushCharge";
            chargeBarObject.transform.parent = throwUIObject.transform.parent;
            chargeBarObject.transform.localPosition = Vector3.zero;
            chargeBarObject.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 180f));

            GameObject barFillGameObject = chargeBarObject.transform.Find("BarMask/BarFill/").gameObject;
            barFillGameObject.transform.localScale = new Vector3(1f, -1f, 1f);
            chargeBar = barFillGameObject.GetComponent<Image>();
        }

        chargeBarObject.SetActive(isCharging);

        // Charging is active — we update the visualization
        if (isCharging) {
            currentCharge += Time.deltaTime;
            currentCharge = Mathf.Clamp(currentCharge, 0f, MAX_CHARGE);

            float fillAmount = Mathf.Lerp(0.672f, 0.808f, currentCharge);
            Color fillColor = Color.Lerp(chargeBarMinColor, chargeBarMaxColor, currentCharge);
            chargeBar.fillAmount = fillAmount;
            chargeBar.color = fillColor;

            // Perform raycast from camera forward within push range
            if (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out RaycastHit hitInfo, PUSH_RANGE, LayerMask.GetMask("Character"))) {
                // Retrieve the Character component from the hit object
                hitCharacter = GetCharacter(hitInfo.transform.gameObject);
            }
            else hitCharacter = null;
            
        }
    }

    /// <summary>
    /// Handles input for charging and releasing the push action.
    /// Charging begins on key down and applies the push on key up.
    /// </summary>
    private void HandleChargeInput() {
        if (Plugin.PConfig.CanCharge) {
            if ((Input.GetKey(Plugin.PConfig.PushKey) || Input.GetKey(Plugin.PConfig.SelfPushKey)) && !isCharging && coolDownLeft <= 0f) {
                isCharging = true;
                currentCharge = 0f;
                Plugin.Log.LogInfo("Started charging push...");
            }
            if (Input.GetKeyUp(Plugin.PConfig.PushKey) && !Input.GetKey(Plugin.PConfig.SelfPushKey) && isCharging) {
                isCharging = false;
                isPushingSelf = false;
                TryPushTarget(false);
            }
            if (Input.GetKeyUp(Plugin.PConfig.SelfPushKey) && !Input.GetKey(Plugin.PConfig.PushKey) && isCharging) {
                isCharging = false;
                isPushingSelf = true;
                TryPushTarget(true);
            }
        }
        else {
            if (Input.GetKeyDown(Plugin.PConfig.PushKey) && coolDownLeft <= 0f) {
                TryPushTarget(false);
            }
            if (Input.GetKeyDown(Plugin.PConfig.SelfPushKey) && coolDownLeft <= 0f) {
                TryPushTarget(true);
            }
        }
    }

    /// <summary>
    /// Attempts to perform a push using a forward raycast from the main camera.
    /// If a valid character is hit, applies force via RPC.
    /// </summary>
    private void TryPushTarget(bool self) {
        if (mainCamera is null) return;


        if (self) {
            pushedCharacter = localCharacter;
        }
        else {
            pushedCharacter = hitCharacter;
            if (pushedCharacter == null || pushedCharacter == localCharacter) return;
        }

        // Calculate final push force with multipliers
        float chargeMultiplier = 1f + ((currentCharge / MAX_CHARGE) * CHARGE_FORCE_MULTIPLIER);
        float bingBongMultiplier = bingBong ? BINGBONG_MULTIPLIER : 1f;
        float totalMultiplier = bingBongMultiplier * chargeMultiplier;
        Vector3 forceDirection = mainCamera.transform.forward * PUSH_FORCE_BASE * totalMultiplier;

        Plugin.Log.LogInfo($"Push force direction: {forceDirection}");

        // Trigger jump SFX on the target (temporary feedback)
        if (!self) {
            PlayPushSFX(pushedCharacter);
        }

        // Apply cooldown and stamina cost
        coolDownLeft = PUSH_COOLDOWN;
        float usedStamina = bingBong ? 1f : STAMINA_COST * ((currentCharge / MAX_CHARGE) * MAX_STAMINA_COST_MULTIPLIER);
        localCharacter.UseStamina(usedStamina, true, true);

        // Send RPC to all clients to synchronize the push
        Plugin.Log.LogInfo("Sending Push RPC Event");
        localCharacter.view.RPC("PushPlayer_Rpc", RpcTarget.All, pushedCharacter.view.ViewID, forceDirection, localCharacter.view.ViewID);
    }

    /// <summary>
    /// Recursively searches for a Character component on the given GameObject or any of its parents.
    /// Useful for raycast hits that may not directly hit the root character object.
    /// </summary>
    /// <param name="obj">The GameObject to start searching from</param>
    /// <returns>The Character component if found; otherwise null</returns>
    private Character? GetCharacter(GameObject? obj) {
        if (obj is null) return null;

        // Check current object first
        if (obj.TryGetComponent(out Character character))
            return character;

        // Traverse up the hierarchy
        Transform? parent = obj.transform.parent;
        if (parent is not null) {
            return GetCharacter(parent.gameObject);
        }

        // No Character found in hierarchy
        return null;
    }

    /// <summary>
    /// Plays the push animation on the specified character.
    /// Uses the character's animator to trigger the reach animation.
    /// </summary>
    /// <param name="character">The character to animate</param>
    private void PlayPushAnimation(Character? character) {
        if (character is null) return;
        character.refs.animator.Play("A_Scout_Reach_Straight");
    }

    /// <summary>
    /// Activates the jump sound effect on the target character.
    /// Used as audio feedback when a push is applied.
    /// </summary>
    /// <param name="character">The character to play SFX on</param>
    private void PlayPushSFX(Character character) {
        Transform sfx = character.gameObject.transform.Find("Scout").Find("SFX").Find("Movement").Find("SFX Jump");
        if (sfx is null) {
            Plugin.Log.LogError("Could not find sound effect for pushed character.");
        }
        else {
            sfx.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// RPC callback to apply a push force to a specific character.
    /// Called on all clients to ensure synchronized behavior.
    /// </summary>
    /// <param name="viewID">Photon View ID of the target character</param>
    /// <param name="force">Force vector to apply</param>
    /// <param name="senderID">Photon View ID of the pushing character</param>
    [PunRPC]
    private void PushPlayer_Rpc(int viewID, Vector3 force, int senderID) {
        Plugin.Log.LogInfo($"Received Push RPC Event for ID: {viewID}, Force: {force}, from SenderID: {senderID}");

        // Trigger push animation on the sender (if visible on this client)
        if (Character.GetCharacterWithPhotonID(senderID, out Character senderCharacter)) {
            if (senderCharacter.TryGetComponent<PushManager>(out var senderPushManager)) {
                senderPushManager.animationCoolDown = ANIMATION_TIME;
            }
        }
        else {
            Plugin.Log.LogWarning($"Could not find character with photon ID: {senderID}");
        }

        // Ensure local character is identified
        // Ensure we have a reference to the local character
        if (localCharacter is null) {
            localCharacter = Character.AllCharacters.First(c => c.IsLocal);
            if (localCharacter is null) {
                Plugin.Log.LogError("Failed to find local character in PushPlayer_Rpc.");
                return;
            }
        }

        // Only apply force if this client controls the target character
        int localViewID = localCharacter.view.ViewID;
        if (viewID != localViewID) {
            Plugin.Log.LogInfo($"Local Player ID: {localViewID} is not the pushed ID: {viewID}");
            return;
        }

        // Play SFX locally for feedback
        PlayPushSFX(localCharacter);

        // Apply physical force to the character
        localCharacter.AddForce(force);
    }
}
