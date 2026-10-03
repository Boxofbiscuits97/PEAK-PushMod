using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace PushMod;

public class ConfigurationHandler {

    private ConfigEntry<KeyCode> _configPushKey;
    private ConfigEntry<KeyCode> _configSelfPushKey;
    private ConfigEntry<bool> _configcanCharge;
    private ConfigEntry<float> _configPushForce;

    public KeyCode SelfPushKey => _configSelfPushKey.Value;
    public KeyCode PushKey => _configPushKey.Value;
    public bool CanCharge => _configcanCharge.Value;
    public float PushForce => _configPushForce.Value;

    public ConfigurationHandler(Plugin instance) {
        Plugin.Log.LogInfo("PushMod ConfigurationHandler initialising");
        _configPushKey = instance.Config.Bind(
            section: "Push Settings",
            key: "PushKey",
            defaultValue: KeyCode.F,
            description: "The keyboard key used to push. Example: F, E, G, etc."
        );
        _configSelfPushKey = instance.Config.Bind(
            section: "Push Settings",
            key: "SelfPushKey",
            defaultValue: KeyCode.G,
            description: "The keyboard key used to push yourself. Example: F, E, G, etc."
        );
        _configcanCharge = instance.Config.Bind(
            section: "Push Settings",
            key: "CanCharge",
            defaultValue: true,
            description: "The setting includes charging force when pushed"
        );
        _configPushForce = instance.Config.Bind(
            section: "Push Settigns",
            key: "PushForce",
            defaultValue: 500f,
            description: "The setting used to determine how powerful the push will be"
        );

        Plugin.Log.LogInfo("PushMod Configuration loaded:");
        Plugin.Log.LogInfo($"  PushKey: {PushKey}");
        Plugin.Log.LogInfo($"  CanCharge: {CanCharge}");

        Plugin.Log.LogInfo("PushMod ConfigurationHandler initialised");
    }
}
