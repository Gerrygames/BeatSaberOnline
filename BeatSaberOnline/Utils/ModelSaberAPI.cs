﻿using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Logger = BeatSaberOnline.Data.Logger;

namespace BeatSaberOnline.Utils
{
    public static class ModelSaberAPI
    {
        // Sourced from https://github.com/andruzzzhka/BeatSaberMultiplayer/blob/master/BeatSaberMultiplayer/AvatarController.cs
        public static event Action<string, CustomAvatar.CustomAvatar> avatarDownloaded;

        public static Dictionary<string, CustomAvatar.CustomAvatar> cachedAvatars = new Dictionary<string, CustomAvatar.CustomAvatar>();
        public static List<string> queuedAvatars = new List<string>();

        public static IEnumerator DownloadAvatarCoroutine(string hash, Action<CustomAvatar.CustomAvatar> callback)
        {
            queuedAvatars.Add(hash);
            string downloadUrl = "";
            string avatarName = "";
            UnityWebRequest www = UnityWebRequest.Get("https://modelsaber.assistant.moe/api/v1/avatar/get.php?filter=hash:" + hash);

            www.timeout = 10;

            yield return www.SendWebRequest();


            if (www.isNetworkError || www.isHttpError)
            {
                Logger.Error(www.error);
                queuedAvatars.Remove(hash);
                callback?.Invoke(null);
                yield break;
            }
            else
            {
                JSONNode node = JSON.Parse(www.downloadHandler.text);

                if (node.Count == 0)
                {
                    Logger.Error($"Avatar with hash {hash} doesn't exist on ModelSaber!");
                    cachedAvatars.Add(hash, null);
                    queuedAvatars.Remove(hash);
                    callback?.Invoke(null);
                    yield break;
                }

                downloadUrl = node[0]["download"].Value;
                avatarName = downloadUrl.Substring(downloadUrl.LastIndexOf("/") + 1);
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                queuedAvatars.Remove(hash);
                callback?.Invoke(null);
                yield break;
            }


            bool timeout = false;
            float time = 0f;
            UnityWebRequestAsyncOperation asyncRequest;

            try
            {
                www = UnityWebRequest.Get(downloadUrl);
                www.timeout = 0;

                asyncRequest = www.SendWebRequest();
            }
            catch (Exception e)
            {
                Logger.Error(e);
                queuedAvatars.Remove(hash);
                callback?.Invoke(null);
                yield break;
            }

            while (!asyncRequest.isDone)
            {
                yield return null;

                time += Time.deltaTime;

                if ((time >= 5f && asyncRequest.progress == 0f))
                {
                    www.Abort();
                    timeout = true;
                    Logger.Error("Connection timed out!");
                }
            }


            if (www.isNetworkError || www.isHttpError || timeout)
            {
                queuedAvatars.Remove(hash);
                Logger.Error("Unable to download avatar! " + (www.isNetworkError ? $"Network error: {www.error}" : (www.isHttpError ? $"HTTP error: {www.error}" : "Unknown error")));
                callback?.Invoke(null);
            }
            else
            {
                string docPath = "";
                string customAvatarPath = "";

                byte[] data = www.downloadHandler.data;

                try
                {
                    docPath = Application.dataPath;
                    docPath = docPath.Substring(0, docPath.Length - 5);
                    docPath = docPath.Substring(0, docPath.LastIndexOf("/"));
                    customAvatarPath = docPath + "/CustomAvatars/" + avatarName;

                    File.WriteAllBytes(customAvatarPath, data);

                    CustomAvatar.CustomAvatar downloadedAvatar = Controllers.AvatarController.CreateInstance<CustomAvatar.CustomAvatar>(customAvatarPath);

                    queuedAvatars.Remove(hash);
                    cachedAvatars.Add(hash, downloadedAvatar);

                    downloadedAvatar.Load((CustomAvatar.CustomAvatar avatar, CustomAvatar.AvatarLoadResult result) => { if (result == CustomAvatar.AvatarLoadResult.Completed) { callback?.Invoke(avatar); avatarDownloaded?.Invoke(hash, avatar); } else callback?.Invoke(null); });
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    queuedAvatars.Remove(hash);
                    callback?.Invoke(null);
                    yield break;
                }
            }
        }
    }
}
