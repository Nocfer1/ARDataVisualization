using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Handles downloading satellite texture assets from Mapbox APIs.
/// </summary>
public static class TEMMapboxDownloader
{
    /// <summary>
    /// Fetches a satellite map texture given a fully formatted Mapbox API URL.
    /// </summary>
    public static IEnumerator DownloadSatelliteTexture(
        string mapboxUrl, 
        Action<Texture2D> onSuccess, 
        Action<string> onError)
    {
        if (string.IsNullOrEmpty(mapboxUrl))
        {
            onError?.Invoke("Mapbox URL is null or empty.");
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(mapboxUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Mapbox request failed: {request.error}");
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            texture.wrapMode = TextureWrapMode.Clamp;
            onSuccess?.Invoke(texture);
        }
    }
}