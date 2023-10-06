#nullable enable

using UnityEngine;

namespace LagFreeScreenshots.API;

public static class LfsApi
{
    /// <summary>
    /// Called to generate the image filepath where the screenshot will be written
    /// Note: Use .png as the file extension as it will be replaced by LFS
    /// </summary>
    public static MakeScreenshotFilePathType? MakeScreenshotFilePath;
    public delegate string MakeScreenshotFilePathType(ImageSettings settings);

    /// <summary>
    /// Called after a screenshot is taken and written to disk
    /// </summary>
    public static event ScreenshotSavedEventV2? OnScreenshotSavedV2;
    public delegate void ScreenshotSavedEventV2(string filePath, int width, int height, MetadataV2? metadata);
    internal static void InvokeScreenshotSaved(string filePath, int width, int height, MetadataV2? metadataV2) =>
        OnScreenshotSavedV2?.Invoke(filePath, width, height, metadataV2);


    /// <summary>
    /// Called just after the camera rendered to this RenderTexture (just before it will be destroyed)
    /// It's right time to make a GPU copy with Graphics.CopyTexture for example
    /// </summary>
    public static event ScreenshotTextureEvent? OnScreenshotTexture;
    public delegate void ScreenshotTextureEvent(RenderTexture texture);
    internal static void InvokeScreenshotTexture(RenderTexture texture) =>
        OnScreenshotTexture?.Invoke(texture);
}