using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace PushMod;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;

        Log.LogInfo($"Plugin {Name} is loaded!");
        Harmony.CreateAndPatchAll(typeof(PushPatch));
    }
}

public static class PushPatch {
    [HarmonyPostfix, HarmonyPatch(typeof(Character), nameof(Character.Awake))]
    public static void AwakePatch(Character __instance)
    {
        __instance.gameObject.AddComponent<PushManager>();
        Plugin.Log.LogInfo($"Added Component to character: {__instance.characterName}");
    }
}

public class PushManager : MonoBehaviour {
    private Character localCharacter = null!;
    private float coolDownLeft;
    private float animationCoolDown;
    private float animationTime = 0.25f;
    private float forceMultiplier = 1f;
    private bool bingBong;

    private void Awake() {
        Character foundCharacter = GetComponent<Character>();
        if (foundCharacter.IsLocal) localCharacter = foundCharacter;
    }

    private void Update() {
        coolDownLeft -= Time.deltaTime;
        animationCoolDown -= Time.deltaTime;
        if (animationCoolDown > 0f) {
            Character foundCharacter = gameObject.GetComponent<Character>();
            PlayPushAnimation(foundCharacter);
        }

        if (coolDownLeft > 0f) return;

        if (localCharacter == null) return;
        if (!localCharacter.view.IsMine) return;
        if (!localCharacter.data.fullyConscious) return;
        if (localCharacter.data.isCarried) return;
        if (localCharacter.data.isClimbing) return;

        Item currentItem = localCharacter.data.currentItem;
        if (currentItem != null) {
            bingBong = currentItem.GetItemName().ToLowerInvariant().Contains("bing bong");
            if (!bingBong) return;
        }
        else bingBong = false;

        if (Input.GetKeyDown(KeyCode.F)) {
            Character pushedCharacter;

            RaycastHit HitInfo;
            Transform cameraTransform = Camera.main.transform;

            if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out HitInfo, 2.5f)) return;

            pushedCharacter = GetCharacter(HitInfo.transform.gameObject);
            if (pushedCharacter == null) return;

            if (bingBong) forceMultiplier = 10f;
            else forceMultiplier = 1f;

            Vector3 forceDirection = Camera.main.transform.forward * 500 * forceMultiplier;

            Transform sfx = pushedCharacter.gameObject.transform.Find("Scout").Find("SFX").Find("Movement").Find("SFX Jump");
            if (sfx == null) {
                Plugin.Log.LogError("Could not find sound effect for pushed character.");
            }
            else {
                sfx.gameObject.SetActive(true);
            }

            coolDownLeft = 1f;
            float staminaUsed = 0.1f * forceMultiplier;
            localCharacter.UseStamina(staminaUsed, true);

            Plugin.Log.LogInfo("Sending Push RPC Event");
            localCharacter.view.RPC("PushPlayer_Rpc", RpcTarget.All, new object[]
            {
                pushedCharacter.view.ViewID,
                forceDirection,
                localCharacter.view.ViewID
            });
        }
    }

    private Character GetCharacter(GameObject obj) {
        if (!obj.TryGetComponent(out Character character)) {
            if (obj.transform.parent == null) return character;
            return GetCharacter(obj.transform.parent.gameObject);
        }

        return character;
    }

    private void PlayPushAnimation(Character character) {
        CharacterAnimations characterAnimations = character.gameObject.GetComponent<CharacterAnimations>();
        Animator animator = characterAnimations.character.refs.animator;

        animator.Play("A_Scout_Reach_Straight");
    }

    [PunRPC]
    private void PushPlayer_Rpc(int viewID, Vector3 force, int senderID) {
        Plugin.Log.LogInfo($"Received Event for ID: {viewID} with Force: {force} from ID: {senderID}");

        foreach (Character character in Character.AllCharacters) {
            if (character.IsLocal) localCharacter = character;
        }

        if (localCharacter == null) {
            Plugin.Log.LogError("Local Character is null!");
            return;
        }

        Character senderCharacter = null!;
        Character.GetCharacterWithPhotonID(senderID, out senderCharacter);

        if (senderCharacter == null) {
            Plugin.Log.LogWarning($"Could not find character with photon ID: {senderID}");
        }
        else {
            PushManager senderPushManager = senderCharacter.GetComponent<PushManager>();
            senderPushManager.animationCoolDown = animationTime;
        }

        int localViewID = localCharacter.view.ViewID;
        if (viewID != localViewID) {
            Plugin.Log.LogInfo($"Local Player ID: {localViewID} is not pushed ID: {viewID}");
            return;
        }

        Transform sfx = localCharacter.gameObject.transform.Find("Scout").Find("SFX").Find("Movement").Find("SFX Jump");
        if (sfx == null) {
            Plugin.Log.LogError("Could not find sound effect for local character.");
        }
        else {
            sfx.gameObject.SetActive(true);
        }

        localCharacter.AddForce(force);
    }
}
