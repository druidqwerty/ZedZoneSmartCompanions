using System;
using UnityEngine;

namespace ZedZone.SmartCompanions.UiSmoke
{
    public sealed partial class UiSmokeRunner
    {
        // Native coordinate mapping only. Input is verified separately by OsProbeRunner.
        private void VerifyScaledInteraction()
        {
            var savedMatrix = GUI.matrix;
            try
            {
                float scale = ReadUiFloat("EffectiveUiScale");
                var matrix = Matrix4x4.identity;
                matrix.m00 = matrix.m11 = scale;
                GUI.matrix = matrix;
                var start = new Vector2(50f, 50f);
                var a = GUIUtility.GUIToScreenPoint(start);
                var b = GUIUtility.GUIToScreenPoint(start + new Vector2(20f, 30f));
                if ((b - a - new Vector2(20f, 30f) * scale).magnitude > 0.2f)
                    throw new Exception("Native GUI coordinate conversion ignored effective scale.");
                if ((GUIUtility.ScreenToGUIPoint(a) - start).magnitude > 0.2f)
                    throw new Exception("Native GUI scaled screen/local roundtrip failed.");

                // Native Event mutation does not reliably inject queued input in this IL2CPP build.
                // Do not count synthetic handler calls as OS pointer/drag verification.
                UiSmokePlugin.Logger.LogInfo($"[UiSmoke] PASS native GUI coordinate roundtrip; actual={Screen.width}x{Screen.height}, scale={scale:0.000}. Synthetic drag/scroll injection unsupported and not counted.");
            }
            finally
            {
                GUI.matrix = savedMatrix;
            }
        }

    }
}
