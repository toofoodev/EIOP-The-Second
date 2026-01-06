using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EIOP.Tools; // Assumes your WAV parser is here
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace EIOP.Core;

public class Notifications : MonoBehaviour
{
    private static   Notifications            Instance;
    private readonly Dictionary<Guid, string> notifications = new();
    private          GameObject               canvas;
    
    private Text      notificationText;
    private AudioClip notificationSound; // Variable to hold the sound

    private void Awake() => Instance = this;

    private void Start()
    {
        GameObject canvasPrefab = Plugin.EIOPBundle.LoadAsset<GameObject>("EIOPNotifications");
        canvas = Instantiate(canvasPrefab,
                XRSettings.isDeviceActive
                        ? GTPlayer.Instance.headCollider.transform
                        : PCHandler.ThirdPersonCameraTransform);

        Destroy(canvasPrefab);
        canvas.name = "EIOPNotifications";

        canvas.transform.localPosition = XRSettings.isDeviceActive
                                                 ? new Vector3(0.1f,     0.2f,    0.6f)
                                                 : new Vector3(-0.6793f, 0.5705f, 0.6f);

        canvas.transform.localRotation =
                XRSettings.isDeviceActive ? Quaternion.Euler(345f, 0f, 0f) : Quaternion.identity;

        notificationText = canvas.GetComponentInChildren<Text>();
        canvas.SetLayer(XRSettings.isDeviceActive ? UnityLayer.FirstPersonOnly : UnityLayer.MirrorOnly);

        // ----------------------------------------------------
        // LOAD THE SOUND HERE
        // ----------------------------------------------------
        notificationSound = LoadWavFromResource("EIOP.Resources.NotificationSound.wav");
        // ----------------------------------------------------

        ApplyNotificationText();
    }

    public static void SendNotification(string message)
    {
        Guid notificationId = Guid.NewGuid();
        message                                = message.InsertNewlinesWithRichText(40);
        Instance.notifications[notificationId] = message;
        Instance.ApplyNotificationText();
        CoroutineManager.Instance.StartCoroutine(Instance.RemoveNotificationAfterTime(notificationId));

        // ----------------------------------------------------
        // PLAY THE SOUND HERE
        // ----------------------------------------------------
        if (Instance.notificationSound != null)
        {
            // We use the AudioSource from Plugin to prevent creating too many sources
            Plugin.PlaySound(Instance.notificationSound);
        }
    }

    private IEnumerator RemoveNotificationAfterTime(Guid notificationId)
    {
        yield return new WaitForSeconds(10f);
        notifications.Remove(notificationId);
        ApplyNotificationText();
    }

    private void ApplyNotificationText()
    {
        const int BaseSize = 32;
        const int MinSize  = 16;
        const int Step     = 4;

        string[] ordered = notifications.Values.ToArray();
        string   text    = string.Empty;

        for (int i = ordered.Length - 1; i > -1; i--)
        {
            int size = Mathf.Max(MinSize, BaseSize - Step * (ordered.Length - i - 1));
            text += $"<size={size}>{ordered[i]}</size>\n\n";
        }

        notificationText.supportRichText = true;
        notificationText.text            = text.Trim();
    }

    // Helper method to load the WAV file from the DLL
    private AudioClip LoadWavFromResource(string resourcePath)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);

        if (stream == null)
        {
            Debug.LogError($"EIOP: Could not find notification sound at '{resourcePath}'. Check Build Action.");
            return null;
        }

        byte[] buffer;
        using (MemoryStream ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            buffer = ms.ToArray();
        }

        // Assumes you have the WAV parser in EIOP.Tools (same as used in Plugin.cs)
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
