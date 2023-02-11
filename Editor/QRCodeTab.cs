
using System;
using System.Diagnostics;
using System.IO;
using SingularityGroup.HotReload.Newtonsoft.Json;
using SingularityGroup.HotReload.ZXing;
using SingularityGroup.HotReload.ZXing.QrCode;
using SingularityGroup.HotReload.ZXing.QrCode.Internal;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SingularityGroup.HotReload.Editor {
    internal class QRCodeTab {
        Texture2D qrCodeTexture;

        string payload;
        string payloadLabel;
        
        public const string name = "Connect Player";
        public const int size = 250;
        
        Texture2D GetTexture() {
            if (!qrCodeTexture) {
                var writer = new BarcodeWriter {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new QrCodeEncodingOptions {
                        Height = size,
                        Width = size,
                        Margin = 2,
                        CharacterSet = "UTF-8",
                        ErrorCorrection = ErrorCorrectionLevel.L,
                    },
                };
                var pixels = writer.Write(GetPayload());
                qrCodeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                qrCodeTexture.SetPixels32(pixels);
                qrCodeTexture.Apply();
            }
            return qrCodeTexture;
        }

        string GetPayload() {
            if (payload != null) {
                return payload;
            }
        
            var serverInfo = CreatePayload();
            if (serverInfo == null) {
                createPayloadFailed++;
                return payload = "";
            }
            createPayloadFailed = 0;
            payload = serverInfo.ToUriString();
            payloadLabel = "Server Info: " + JsonConvert.SerializeObject(serverInfo);
            return payload;
        }

        /// <summary>
        /// Clear the stored payload to allow retrying to create the payload
        /// </summary>
        private void ClearPayload() {
            payload = null;
            payloadLabel = null;
        }

        static PatchServerInfo CreatePayload() {
            var ip = IpHelper.GetIpAddress();
            if(string.IsNullOrEmpty(ip)) return null;
            var fullCommitHash = GitUtil.GetShortCommitHash();
            // On MacOS GetShortCommitHash() returns 7 characters, on Windows it returns 8 characters.
            // When git command produced an unexpected result, use a fallback string
            var shortCommitHash = PatchServerInfo.UnknownCommitHash;
            if (fullCommitHash.Length > 6) {
                shortCommitHash = fullCommitHash.Length < 8 ? fullCommitHash : fullCommitHash.Substring(0, 8);
            }
            var rootPath = Path.GetFullPath(".");
            //return JsonConvert.SerializeObject(new PatchServerInfo(ip, shortCommitHash, rootPath));
            return new PatchServerInfo(ip, shortCommitHash, rootPath);
        }

        private int createPayloadFailed = 0;
        private GUIStyle titleStyle;

        public void OnGUI() {
            if (titleStyle == null) {
                titleStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 24,
                    alignment = TextAnchor.UpperCenter,
                };
            }
            EditorGUILayout.LabelField("Connect Android Player", titleStyle, GUILayout.MinHeight(24 + 8));

            if (string.IsNullOrEmpty(GetPayload())) {
                EditorGUILayout.LabelField("There was an issue with retrieving the local ip address of your pc");
                var label = "Retry";
                if (createPayloadFailed > 1) {
                    // change label so user knows that retry button actually did something
                    // (otherwise you click button and nothing changes)
                    label += $" (failed {createPayloadFailed} times)";
                }
                // button to retry to get payload (user can click this after turning on wifi for example)
                if (GUILayout.Button(label)) {
                    ClearPayload();
                }
                GUILayout.Space(12f);
                return;
            }

            EditorGUILayout.LabelField("Scan this QR-code with a phone to connect to the code patcher that is running on your pc.",
                EditorStyles.wordWrappedLabel);
            var rect = GUILayoutUtility.GetRect(size, size*1.5f, size, size*1.5f, GUI.skin.box);
            rect.y += 6; // a bit of spacing between image and text
            rect.height -= 6; // a bit of spacing at the bottom of the window
            if (rect.width > rect.height) {
                // make it a square, so that texture is rendered left-aligned
                rect.width = rect.height;
            }
            var texture = GetTexture();
            EditorGUI.DrawPreviewTexture(rect, texture, null, ScaleMode.ScaleToFit);
            
            GUILayout.Space(2f);
            EditorGUILayout.LabelField(payloadLabel, EditorStyles.wordWrappedLabel);
            GUILayout.Space(2f);
        }
    }

    internal static class GitUtil {
        public static string GetShortCommitHash() {
            try {
                // Note: don't use ReadToEndAsync because waiting on that task blocks forever.
                return StartGitCommand("log", " -n 1 --pretty=format:%h").StandardOutput
                    .ReadToEnd();
            } catch (Exception e) {
                Debug.LogException(e);
                return "";
            }
        }

        static Process StartGitCommand(string command, string arguments, Action<ProcessStartInfo> modifySettings = null) {
            var startInfo = new ProcessStartInfo("git", command + " " + arguments) {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (modifySettings != null) {
                modifySettings(startInfo);
            }
            return Process.Start(startInfo);
        }
    }
}