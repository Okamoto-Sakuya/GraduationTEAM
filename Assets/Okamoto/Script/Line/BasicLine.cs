using UnityEngine;

public class BasicLine : TrackEdge
{
    [Header("出現確率")]
    [Min(0.01f)]
    public float spawnWeight = 1f;

    [Header("基本線の識別名")]
    public string lineName;

    private void Start()
    {
        RegisterPoints();
    }

    private void OnDestroy()
    {
        UnregisterPoints();
    }

    // 基本線はAからBの方向に進む
    public override bool CanDepartFrom(LinePoint point)
    {
        return point == pointA && pointB != null;
    }
}