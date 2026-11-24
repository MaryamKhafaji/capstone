using UnityEngine;

// Converts the saved pin's live bearing into nearby AR cues. GPS remains Android-owned.
internal static class HuntLocationGuide
{
    private static Transform player;
    private static EmbeddedHunt.State state;
    private static float nextRead;
    private static float northYaw;
    private static Vector3 calibrationPosition;
    private static double calibrationLatitude, calibrationLongitude;
    internal static float InitialDistance { get; private set; }
    internal static bool Active => EmbeddedHunt.Active && state != null && !state.closeRangeDebug;
    internal static bool HasFix => Current.status == "en_route" || Current.status == "arrived";
    internal static bool AtDestination => Current.status == "arrived";

    internal static void Begin(Transform camera, EmbeddedHunt.State initial)
    {
        player = camera;
        state = initial;
        InitialDistance = initial.distanceMeters;
        nextRead = Time.unscaledTime + 0.5f;
        northYaw = camera.eulerAngles.y - (initial.hasHeading ? initial.headingDegrees : 0f);
        calibrationPosition = camera.position;
        calibrationLatitude = initial.playerLatitude;
        calibrationLongitude = initial.playerLongitude;
    }

    internal static EmbeddedHunt.State Current
    {
        get
        {
            if (state == null) return new EmbeddedHunt.State();
            if (Time.unscaledTime >= nextRead && EmbeddedHunt.Active)
            {
                nextRead = Time.unscaledTime + 0.5f;
                state = EmbeddedHunt.ReadState();
                if (player != null && (state.status == "en_route" || state.status == "arrived"))
                {
                    double north = (state.playerLatitude - calibrationLatitude) * 111320;
                    double east = (state.playerLongitude - calibrationLongitude) * 111320 *
                        System.Math.Cos(state.playerLatitude * System.Math.PI / 180);
                    Vector3 movement = Vector3.ProjectOnPlane(player.position - calibrationPosition, Vector3.up);
                    // Walking calibrates the phone compass against the glasses' tracking space.
                    if (north * north + east * east >= 225 && movement.magnitude >= 8f)
                    {
                        northYaw = Mathf.Atan2(movement.x, movement.z) * Mathf.Rad2Deg -
                            (float)(System.Math.Atan2(east, north) * 180 / System.Math.PI);
                        calibrationPosition = player.position;
                        calibrationLatitude = state.playerLatitude;
                        calibrationLongitude = state.playerLongitude;
                    }
                }
            }
            return state;
        }
    }

    internal static Vector3 Direction => Quaternion.Euler(0, northYaw + Current.bearingDegrees, 0) * Vector3.forward;
}
