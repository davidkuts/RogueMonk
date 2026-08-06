using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RogueMonk.EditorTools
{
    /// <summary>
    /// Ensures the MCP for Unity stdio bridge is running so external tools
    /// (Claude Code) can talk to the editor. The package defaults to HTTP
    /// transport with auto-start off, which leaves no bridge listening;
    /// this forces stdio transport and starts the bridge after each load.
    /// Also enforces the project's Enter Play Mode settings.
    /// </summary>
    [InitializeOnLoad]
    internal static class McpBridgeBootstrap
    {
        static McpBridgeBootstrap()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                ApplyEditorSettings();
                EnsureStdioBridge();
            };
        }

        private static void ApplyEditorSettings()
        {
            const EnterPlayModeOptions wanted =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

            if (!EditorSettings.enterPlayModeOptionsEnabled || EditorSettings.enterPlayModeOptions != wanted)
            {
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = wanted;
                Debug.Log("[McpBridgeBootstrap] Enter Play Mode Options set: domain reload and scene reload disabled.");
            }
        }

        private static void EnsureStdioBridge()
        {
            try
            {
                if (EditorPrefs.GetBool("MCPForUnity.UseHttpTransport", true))
                {
                    EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", false);
                    Debug.Log("[McpBridgeBootstrap] Switched MCP for Unity to stdio transport.");
                }

                Type host = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost"))
                    .FirstOrDefault(t => t != null);

                if (host == null)
                {
                    Debug.LogWarning("[McpBridgeBootstrap] StdioBridgeHost type not found; is the MCP for Unity package installed?");
                    return;
                }

                bool isRunning = (bool)host.GetProperty("IsRunning").GetValue(null);
                if (!isRunning)
                {
                    host.GetMethod("StartAutoConnect").Invoke(null, null);
                    int port = (int)host.GetMethod("GetCurrentPort").Invoke(null, null);
                    Debug.Log($"[McpBridgeBootstrap] MCP stdio bridge started on port {port}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[McpBridgeBootstrap] Failed to start MCP stdio bridge: {ex.Message}");
            }
        }
    }
}
