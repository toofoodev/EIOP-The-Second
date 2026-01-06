using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using EIOP.Anti_Cheat;
using EIOP.Core;
using EIOP.Tools;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking; // Required for the fix

namespace EIOP;

[BepInPlugin(Constants.PluginGuid, Constants.PluginName, Constants.PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    private const string GorillaInfoEndPointURL =
            "https://raw.githubusercontent.com/toofoodev/gorinf/main/";

    public static Dictionary<string, string> KnownCheats;
    public static Dictionary<string, string> KnownMods;

    public static AssetBundle EIOPBundle;
    public static Shader      UberShader;
    public static AudioClip   ButtonPressSound;

    public static Color MainColour;
    public static Color SecondaryColour;

    public static AudioSource PluginAudioSource;

    private void Start()
    {
        new Harmony(Constants.PluginGuid).PatchAll();
        GorillaTagger.OnPlayerSpawned(OnGameInitialized);
    }

    public static void PlaySound(AudioClip audioClip)
    {
        if (audioClip != null && PluginAudioSource != null)
            PluginAudioSource.PlayOneShot(audioClip);
    }

    private void OnGameInitialized()
    {
        PCHandler.ThirdPersonCameraTransform = GorillaTagger.Instance.thirdPersonCamera.transform.GetChild(0);
        PCHandler.ThirdPersonCamera          = PCHandler.ThirdPersonCameraTransform.GetComponent<Camera>();

        // FIX: Check if resource exists before loading to prevent crash
        Stream bundleStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EIOP.Resources.eiopbundle");
        if (bundleStream != null)
        {
            EIOPBundle = AssetBundle.LoadFromStream(bundleStream);
            bundleStream.Close();
        }
        else
        {
            Logger.LogError("EIOP: Could not find embedded resource 'EIOP.Resources.eiopbundle'. Make sure Build Action is set to Embedded Resource.");
        }

        UberShader = Shader.Find("GorillaTag/UberShader");

        ButtonPressSound = LoadWavFromResource("EIOP.Resources.ButtonPressWood.wav");

        PluginAudioSource              = new GameObject("LocalAudioSource").AddComponent<AudioSource>();
        PluginAudioSource.spatialBlend = 0f;
        PluginAudioSource.playOnAwake  = false;

        Type[] antiCheatHandlers = Assembly.GetExecutingAssembly().GetTypes()
                                           .Where(t => t.IsClass && !t.IsAbstract &&
                                                       typeof(AntiCheatHandlerBase).IsAssignableFrom(t)).ToArray();

        foreach (Type antiCheatHandlerType in antiCheatHandlers)
            gameObject.AddComponent(antiCheatHandlerType);

        gameObject.AddComponent<CoroutineManager>();
        gameObject.AddComponent<EIOPUtils>();
        gameObject.AddComponent<Notifications>();
        gameObject.AddComponent<MenuHandler>();

        // FIX: Start Coroutine instead of blocking the thread
        StartCoroutine(FetchModsAndCheatsCoroutine());
    }

    // FIX: Replaced synchronous HttpClient with UnityWebRequest Coroutine
    private IEnumerator FetchModsAndCheatsCoroutine()
    {
        // Fetch Known Cheats
        using (UnityWebRequest www = UnityWebRequest.Get(GorillaInfoEndPointURL + "KnownCheats.txt"))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Logger.LogError($"EIOP: Failed to fetch cheats list. {www.error}");
            }
            else
            {
                try 
                {
                    KnownCheats = JsonConvert.DeserializeObject<Dictionary<string, string>>(www.downloadHandler.text);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"EIOP: Error parsing KnownCheats JSON: {ex.Message}");
                }
            }
        }

        // Fetch Known Mods
        using (UnityWebRequest www = UnityWebRequest.Get(GorillaInfoEndPointURL + "KnownMods.txt"))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Logger.LogError($"EIOP: Failed to fetch mods list. {www.error}");
            }
            else
            {
                try
                {
                    KnownMods = JsonConvert.DeserializeObject<Dictionary<string, string>>(www.downloadHandler.text);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"EIOP: Error parsing KnownMods JSON: {ex.Message}");
                }
            }
        }
    }

    private AudioClip LoadWavFromResource(string resourcePath)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);

        if (stream == null)
        {
            Logger.LogError($"EIOP: Failed to load audio resource {resourcePath}");
            return null;
        }

        // FIX: Ensure complete read of the stream
        byte[] buffer;
        using (MemoryStream ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            buffer = ms.ToArray();
        }

        WAV     wav = new(buffer);
        float[] samples;

        if (wav.ChannelCount == 2)
        {
            samples = new float[wav.SampleCount];
            for (int i = 0; i < wav.SampleCount; i++)
                samples[i] = (wav.LeftChannel[i] + wav.RightChannel[i]) * 0.5f;
        }
        else
        {
            samples = wav.LeftChannel;
        }

        AudioClip audioClip = AudioClip.Create(resourcePath, wav.SampleCount, 1, wav.Frequency, false);
        audioClip.SetData(samples, 0);

        return audioClip;
    }
}
