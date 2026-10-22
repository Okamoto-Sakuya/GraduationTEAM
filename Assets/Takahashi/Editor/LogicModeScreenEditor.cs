// Assets/…/Editor/ フォルダに置いてください
using UnityEditor;
using UnityEngine;

/// <summary>
/// LogicModeScreen をシーンビューで編集するためのハンドル。
///   黄色の四角 : 円の中心（ドラッグで移動）
///   水色の丸   : 半径（円の真上。ドラッグで拡大・縮小）
///   橙の丸     : 腕の位置（左右どちらを動かしても左右対称に反映）
///   緑の丸     : 足の位置（同上）
/// 非再生中は変更のたびにプレビュー画面を作り直すので、ゲームビューにもすぐ反映されます。
/// </summary>
[CustomEditor(typeof(LogicModeScreen))]
public class LogicModeScreenEditor : Editor
{
    static readonly Color CircleCol   = new Color(0.3f, 0.9f, 1f, 0.9f);
    static readonly Color ObstacleCol = new Color(1f, 0.35f, 0.3f, 0.9f);
    static readonly Color CenterCol   = new Color(1f, 0.9f, 0.2f);
    static readonly Color ArmCol      = new Color(1f, 0.6f, 0.2f);
    static readonly Color LegCol      = new Color(0.4f, 1f, 0.5f);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("プレビューを再構築"))
                ((LogicModeScreen)target).RebuildPreview();
        }
        EditorGUILayout.HelpBox("シーンビューのハンドルで円の中心・半径・腕／足の位置をドラッグできます。", MessageType.Info);
    }

    void OnSceneGUI()
    {
        var s = (LogicModeScreen)target;
        var root = s.CanvasRoot;
        if (root == null) return;   // プレビュー（または再生中の画面）がまだ無い

        Vector3 W(Vector2 p) => root.TransformPoint(p);
        Vector2 L(Vector3 w) => root.InverseTransformPoint(w);

        // ---- 円の表示（経路になる部分＝水色 / 跨げない部分＝赤）----
        float lArm = 180f - s.armAngle, lLeg = 180f - s.legAngle;
        DrawArc(s, W, lArm, 180f, CircleCol);
        DrawArc(s, W, 180f, lLeg, ObstacleCol);
        DrawArc(s, W, s.armAngle, 0f, ObstacleCol);
        DrawArc(s, W, 0f, s.legAngle, CircleCol);
        DrawArc(s, W, s.armAngle, lArm, new Color(1f, 1f, 1f, 0.25f));          // 上（線なし）
        DrawArc(s, W, s.legAngle, -180f - s.legAngle, new Color(1f, 1f, 1f, 0.25f));       // 下（線なし）

        bool changed = false;

        // ---- 中心 ----
        EditorGUI.BeginChangeCheck();
        Vector3 c = Drag(W(s.circleCenter), CenterCol, Handles.RectangleHandleCap, 0.07f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(s, "円の中心を移動");
            s.circleCenter = L(c);
            changed = true;
        }

        // ---- 半径（真上）----
        EditorGUI.BeginChangeCheck();
        Vector3 r = Drag(W(s.CirclePt(90f)), CircleCol, Handles.CircleHandleCap, 0.06f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(s, "円の半径を変更");
            s.circleRadius = Mathf.Max(10f, Vector2.Distance(L(r), s.circleCenter));
            changed = true;
        }

        // ---- 腕・足（左右どちらのハンドルでも可、左右対称）----
        changed |= AngleHandle(s, W, L, s.armAngle,         false, true,  ArmCol, "右腕");
        changed |= AngleHandle(s, W, L, 180f - s.armAngle,  true,  true,  ArmCol, "左腕");
        changed |= AngleHandle(s, W, L, s.legAngle,         false, false, LegCol, "右足");
        changed |= AngleHandle(s, W, L, 180f - s.legAngle,  true,  false, LegCol, "左足");

        if (changed)
        {
            EditorUtility.SetDirty(s);
            if (!Application.isPlaying) s.RebuildPreview();
        }
    }

    static bool AngleHandle(LogicModeScreen s, System.Func<Vector2, Vector3> W, System.Func<Vector3, Vector2> L,
                            float deg, bool mirrored, bool isArm, Color col, string label)
    {
        Vector3 pos = W(s.CirclePt(deg));
        Handles.color = col;
        Handles.Label(pos + Vector3.up * HandleUtility.GetHandleSize(pos) * 0.25f, label);

        EditorGUI.BeginChangeCheck();
        Vector3 np = Drag(pos, col, Handles.CircleHandleCap, 0.06f);
        if (!EditorGUI.EndChangeCheck()) return false;

        Vector2 v = L(np) - s.circleCenter;
        float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        if (mirrored) a = 180f - a;
        a = Mathf.DeltaAngle(0f, a);   // -180〜180 に正規化

        Undo.RecordObject(s, label + "の位置を変更");
        if (isArm) s.armAngle = Mathf.Clamp(a, 0.1f, 89.9f);
        else       s.legAngle = Mathf.Clamp(a, -89.9f, -0.1f);
        return true;
    }

    static void DrawArc(LogicModeScreen s, System.Func<Vector2, Vector3> W, float from, float to, Color col)
    {
        int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(to - from) / 3f));
        var pts = new Vector3[n + 1];
        for (int i = 0; i <= n; i++) pts[i] = W(s.CirclePt(Mathf.Lerp(from, to, (float)i / n)));
        Handles.color = col;
        Handles.DrawAAPolyLine(4f, pts);
    }

    static Vector3 Drag(Vector3 pos, Color col, Handles.CapFunction cap, float scale)
    {
        Handles.color = col;
        float size = HandleUtility.GetHandleSize(pos) * scale;
#if UNITY_2022_1_OR_NEWER
        return Handles.FreeMoveHandle(pos, size, Vector3.zero, cap);
#else
        return Handles.FreeMoveHandle(pos, Quaternion.identity, size, Vector3.zero, cap);
#endif
    }
}
