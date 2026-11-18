using System;
using UnityEngine;

internal static class EmbeddedHunt
{
    [Serializable]
    internal class State
    {
        public string status;
        public string monsterId;
        public string siteName;
        public int distanceMeters;
        public float bearingDegrees;
        public bool accuracyLimited;
        public bool closeRangeDebug;
    }

    internal static bool Active
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var activity = CurrentActivity();
            return activity != null && activity.Call<string>("getPackageName") == "com.example.masar_plus";
#else
            return false;
#endif
        }
    }

    internal static State ReadState()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var bridge = new AndroidJavaClass("com.example.masar_plus.EmbeddedHuntBridge");
            using var activity = CurrentActivity();
            return JsonUtility.FromJson<State>(bridge.CallStatic<string>("getHuntState", activity));
        }
        catch (AndroidJavaException exception)
        {
            Debug.LogWarning("Could not read Android hunt state: " + exception.Message);
        }
#endif
        return new State { status = "unavailable" };
    }

    internal static void RequestNextHunt()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var bridge = new AndroidJavaClass("com.example.masar_plus.EmbeddedHuntBridge");
        bridge.CallStatic("requestNextHunt");
#endif
    }

    internal static void QueueResult(string json)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var bridge = new AndroidJavaClass("com.example.masar_plus.EmbeddedHuntBridge");
        using var activity = CurrentActivity();
        bridge.CallStatic("queueResult", activity, json);
#endif
    }

    internal static int InitialCrystals()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var bridge = new AndroidJavaClass("com.example.masar_plus.EmbeddedHuntBridge");
            using var activity = CurrentActivity();
            return bridge.CallStatic<int>("getInitialCrystals", activity);
        }
        catch (AndroidJavaException exception)
        {
            Debug.LogError("Could not load crystal balance: " + exception.Message);
        }
#endif
        return 0;
    }

    internal static void ReturnToApp()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var bridge = new AndroidJavaClass("com.example.masar_plus.EmbeddedHuntBridge");
        using var activity = CurrentActivity();
        bridge.CallStatic("returnToApp", activity);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject CurrentActivity()
    {
        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        return player.GetStatic<AndroidJavaObject>("currentActivity");
    }
#endif
}
