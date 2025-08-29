using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LagFreeScreenshots;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using LagFreeScreenshots.API;
using System.Linq;

using Object = UnityEngine.Object;
using PlayerSetup = ABI_RC.Core.Player.PlayerSetup;
using MetaPort = ABI_RC.Core.Savior.MetaPort;
using AuthManager = ABI_RC.Core.Networking.AuthManager;
using PortableCamera = ABI_RC.Systems.Camera.PortableCamera;
using RefFlags = System.Reflection.BindingFlags;
using Events = ABI_RC.Systems.Camera.Events;
using AudioEffects = ABI_RC.Core.AudioEffects;

[assembly:MelonInfo(typeof(LagFreeScreenshotsMod), "Lag Free Screenshots", "2.2.4", "Daky", "https://github.com/dakyneko/DakyModsCVR")]
[assembly:MelonGame(null, "ChilloutVR")]
[assembly:MelonOptionalDependencies("libwebpwrapper", "BTKUILib")]

namespace LagFreeScreenshots
{
    internal partial class LagFreeScreenshotsMod : MelonMod
    {
        private const string SettingsCategory = "LagFreeScreenshots";
        private const string SettingEnableMod = "Enabled";
        private const string SettingScreenshotResolution = "ScreenshotResolution";
        private const string SettingScreenshotFormat = "ScreenshotFormat";
        private const string SettingJpegPercent = "JpegPercent";
        private const string SettingWebpPercent = "WebpPercent";
        private const string SettingAutorotation = "Auto-rotation";
        private const string SettingMetadata = "Metadata";
        private const string SettingRecommendedMaximumFb = "RecommendedMaximumFb";
        private const string SettingCustomResolutionX = "CustomResolutionX";
        private const string SettingCustomResolutionY = "CustomResolutionY";

        internal static MelonPreferences_Entry<bool> ourEnabled;
        internal static MelonPreferences_Entry<PresetScreenshotSizes> ourResolution;
        internal static MelonPreferences_Entry<ImageFormats> ourFormat;
        internal static MelonPreferences_Entry<int> ourJpegPercent;
        internal static MelonPreferences_Entry<int> ourWebpPercent;
        internal static MelonPreferences_Entry<int> ourCustomResolutionX;
        internal static MelonPreferences_Entry<int> ourCustomResolutionY;
        internal static MelonPreferences_Entry<bool> ourAutorotation;
        internal static MelonPreferences_Entry<bool> ourMetadata;
        internal static MelonPreferences_Entry<int> ourRecommendedMaxFb;

        private static bool ourSupportsWebP = false;

        private static Thread ourMainThread;
        private static MelonLogger.Instance logger;

        public override void OnInitializeMelon()
        {
            logger = LoggerInstance;

            var category = MelonPreferences.CreateCategory(SettingsCategory, "Lag Free Screenshots");
            ourEnabled = category.CreateEntry(SettingEnableMod, true, "Enabled");
            ourResolution = category.CreateEntry(SettingScreenshotResolution, PresetScreenshotSizes.Default, "Resolution", "Overrides the game default");
            ourFormat = category.CreateEntry(SettingScreenshotFormat, ImageFormats.png, "Format: JPG/PNG/WebP", "The format/extension of the image saved to disk. Note: WebP requires installing extra dll");
            ourAutorotation = category.CreateEntry(SettingAutorotation, true, "Auto rotate", "Rotate the picture to match the camera orientation (think: auto landscape and portrait modes)");
            ourMetadata = category.CreateEntry(SettingMetadata, false, "Embed metadata", "Save metadata into picture file (EXIF / iTxT blocks)");
            ourJpegPercent = category.CreateEntry(SettingJpegPercent, 95, "Advanced: JPEG quality", "0 is lowest, 100 is highest, 85 is good");
            ourWebpPercent = category.CreateEntry(SettingWebpPercent, 85, "Advanced: WebP quality", "0 is lowest, 100 is highest, 75 is good");
            ourCustomResolutionX = category.CreateEntry(SettingCustomResolutionX, 1920, "Advanced: custom width", "Set the width when using custom resolution");
            ourCustomResolutionY = category.CreateEntry(SettingCustomResolutionY, 1080, "Advanced: custom height", "Set the height when using custom resolution");
            ourRecommendedMaxFb = category.CreateEntry(SettingRecommendedMaximumFb, 1024, "Expert: framebuffer size", "Try to keep framebuffer below (MB) by reducing MSAA");
            
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(PortableCamera).Capture()),
                new HarmonyMethod(AccessTools.Method(typeof(LagFreeScreenshotsMod), nameof(OnCapture))));
            HarmonyInstance.Patch(
                SymbolExtensions.GetMethodInfo(() => default(PortableCamera).ApplyPostMaterial(default(Material))),
                new HarmonyMethod(AccessTools.Method(typeof(LagFreeScreenshotsMod), nameof(OnApplyPostMaterial))));


            ourSupportsWebP = WebpUtils.IsWebpSupported();
            if (ourFormat.Value == ImageFormats.webp && !ourSupportsWebP) // only when if explicitely requested
            {
                logger.Warning($"WebP is not properly installed in game directory. Please provide libwebp.dll libwebpmux.dll and libwebpwrapper.dll to make this work.");
                ourFormat.Value = ImageFormats.auto;
            }

            // Check for BTKUILib and add settings UI
            if (RegisteredMelons.Any(m => m.Info.Name == "BTKUILib"))
                Daky.DakyBTKUI.AutoGenerateCategory(category);
        }

        // FIXME: Those 2 were imported from UIExpansionKit and should be refactored somewhere else?
        public override void OnUpdate()
        {
            TaskUtilities.ourMainThreadQueue.Flush();
        }

        public override void OnGUI()
        {
            TaskUtilities.ourFrameEndQueue.Flush();
        }

        private static ScreenshotRotation GetPictureAutorotation(Camera camera)
        {
            var rot = camera.transform.eulerAngles.z;
            if (rot >= 45 && rot < 135) return ScreenshotRotation.CounterClockwise90;
            if (rot >= 135 && rot < 225) return ScreenshotRotation.Clockwise180;
            if (rot >= 225 && rot < 315) return ScreenshotRotation.Clockwise90;
            return ScreenshotRotation.NoRotation;
        }

        private static FieldInfo preImageTaken = null, postImageTaken = null, applyPostMaterial = null;

        private static void CVRPreImageTaken(PortableCamera __instance)
        {
            // and raise event
            preImageTaken ??= typeof(PortableCamera).GetField(nameof(PortableCamera.PreImageTaken), RefFlags.Static | RefFlags.NonPublic);
            var f = preImageTaken?.GetValue(null) as EventHandler<Events.PreImageTakenEventArgs>;
            f?.Invoke(__instance, new Events.PreImageTakenEventArgs());
        }

        private static void CVRPostImageTaken(PortableCamera __instance)
        {
            // CVR post-capture shutter effect and raise event
            __instance._videoPlayer.enabled = true;
            __instance._videoPlayer.time = 0.0;
            __instance._videoPlayer.isLooping = false;
            __instance._videoPlayer.Play();
            AudioEffects.InterfaceAudio.Play(AudioEffects.AudioClipField.CameraShutter);

            // and raise event
            postImageTaken ??= typeof(PortableCamera).GetField(nameof(PortableCamera.PostImageTaken), RefFlags.Static | RefFlags.NonPublic);
            var f = postImageTaken?.GetValue(null) as EventHandler<Events.PostImageTakenEventArgs>;
            f?.Invoke(__instance, new Events.PostImageTakenEventArgs());
        }

        private static volatile bool catchApplyPostMaterial = false;
        private static volatile List<Material> postMaterials = new();
        private static void CVRApplyPostMaterial(PortableCamera __instance) {
            // and raise event
            applyPostMaterial ??= typeof(PortableCamera).GetField(nameof(PortableCamera.ProcessImageEvent), RefFlags.Static | RefFlags.NonPublic);
            var f = applyPostMaterial?.GetValue(null) as EventHandler<Events.ProcessImageEventArgs>;
            f?.Invoke(__instance, new Events.ProcessImageEventArgs());
        }

        private static bool OnApplyPostMaterial(PortableCamera __instance, Material mat)
        {
            if (!catchApplyPostMaterial) return true;

            postMaterials.Add(mat);
            return false;
        }

        private static bool OnCapture(PortableCamera __instance) {
            if (!ourEnabled.Value)
                return true;

            var camera = __instance._camera;
            var resX = __instance.width;
            var resY = __instance.height;
            var hasAlpha = camera.backgroundColor.a < 1 && camera.clearFlags == CameraClearFlags.SolidColor;
            
            ourMainThread = Thread.CurrentThread;

            var resFromOption = ImageResolution(ourResolution.Value);
            if (resFromOption.HasValue)
                (resX, resY) = resFromOption.Value;

            CVRPreImageTaken(__instance);

            var settings = new ImageSettings { Width = resX, Height = resY, HasAlpha = hasAlpha };
            TakeScreenshot(__instance, camera, settings).ContinueWith(async t =>
            {
                if (t.IsFaulted)
                    logger.Warning($"Free-floating task failed with exception: {t.Exception}");

                await TaskUtilities.YieldToMainThread();
                CVRPostImageTaken(__instance);

                // yield to background thread for disposes
                await Task.Delay(1).ConfigureAwait(false);
            });
            return false;
        }

        public static (int width, int height)? ImageResolution(PresetScreenshotSizes d)
        {
            return d switch
            {
                PresetScreenshotSizes.Default => null,
                PresetScreenshotSizes.Custom => (ourCustomResolutionX.Value, ourCustomResolutionY.Value),
                PresetScreenshotSizes._720p => (1280, 720),
                PresetScreenshotSizes._1080p => (1920, 1080),
                PresetScreenshotSizes._4K => (3840, 2160),
                PresetScreenshotSizes._8K => (7680, 4320),
                PresetScreenshotSizes._12K => (11520, 6480),
                PresetScreenshotSizes._16K => (15360, 8640),
                PresetScreenshotSizes.Thumbnail => (100, 100),
                PresetScreenshotSizes.Square => (1024, 1024),
                _ => throw new ArgumentOutOfRangeException(nameof(d), d, null)
            };
        }

        private static int ourLastUsedMsaaLevel = 0;
        private static int MaxMsaaCount(int w, int h)
        {
            // MSAA rendertargets store depth (24+8 bits?) and color per sample, plus one extra color sample (output color?) for levels >1
            // Unity doesn't like rendertextures over 4 gigs in size, so reduce MSAA if necessary
            var maxFbSize = (uint) ourRecommendedMaxFb.Value * 1024 * 1024;
            var colorSizePerLevel = w * (long) h * 4 * 2; // ignore no-alpha to be conservative about packing
            var maxMsaa = (maxFbSize - colorSizePerLevel / 2) / colorSizePerLevel;
            if (maxMsaa >= 8) maxMsaa = 8;
            else if (maxMsaa >= 4) maxMsaa = 4;
            else if (maxMsaa >= 2) maxMsaa = 2;
            else maxMsaa = 1;

            if (maxMsaa != ourLastUsedMsaaLevel)
            {
                logger.Msg($"Using MSAA x{maxMsaa} for screenshots (FB size {(colorSizePerLevel * maxMsaa + colorSizePerLevel / 2) / 1024 / 1024}MB)");
                ourLastUsedMsaaLevel = (int) maxMsaa;
            }

            return (int) maxMsaa;
        }

        public static (RenderTexture, ScreenshotRotation) PrepareCameraAndRender(PortableCamera __instance, Camera camera, ref ImageSettings settings)
        {
            var w = settings.Width;
            var h = settings.Height;

            // keep the camera setup so we can later restore them (we need to change them a bit temporarily)
            var oldCameraTarget = camera.targetTexture;
            var oldCameraFov = camera.fieldOfView;
            var oldAllowMsaa = camera.allowMSAA;
            var oldGraphicsMsaa = QualitySettings.antiAliasing;

            // make screenshot upside up by rotating camera just for the rendering
            var t = camera.transform;
            Quaternion? camOrigRot = null;
            var shotRotation = ScreenshotRotation.AutoRotationDisabled;
            if (ourAutorotation.Value)
            {
                shotRotation = GetPictureAutorotation(camera);
                var inverseAngle = shotRotation switch
                {
                    // we need to also compensate for upside-down texture rendering (later below)
                    ScreenshotRotation.CounterClockwise90 => 90,
                    ScreenshotRotation.Clockwise90        => -90,
                    ScreenshotRotation.NoRotation         => 180,
                    _ => 0,
                };
                if (inverseAngle != 0)
                {
                    camOrigRot = t.rotation;
                    t.rotation *= Quaternion.AngleAxis(inverseAngle, Vector3.forward); // inverse rotation
                }

                // for some rotation, we have to swap width / height
                if (shotRotation == ScreenshotRotation.Clockwise90 || shotRotation == ScreenshotRotation.CounterClockwise90)
                {
                    // we have to swap FOV to preserve the original viewport
                    camera.fieldOfView = Camera.VerticalToHorizontalFieldOfView(camera.fieldOfView, w * 1f / h);
                    // and rotate the resolution
                    (w, h) = (h, w);
                }

                settings.Width = w; // update because they may have changed just above
                settings.Height = h;
            }

            var renderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var maxMsaa = MaxMsaaCount(w, h);
            renderTexture.antiAliasing = maxMsaa;

            camera.targetTexture = renderTexture;
            camera.allowMSAA = maxMsaa > 1;
            QualitySettings.antiAliasing = maxMsaa;

            camera.Render();

            // restore the camera as it was: previous settings
            if (camOrigRot != null)
                t.rotation = camOrigRot.Value; // restore

            camera.targetTexture = oldCameraTarget;
            camera.fieldOfView = oldCameraFov;
            camera.allowMSAA = oldAllowMsaa;
            QualitySettings.antiAliasing = oldGraphicsMsaa;

            // apply PortableCamera effects mods, we need to catch their materials and we'll apply them ourself
            catchApplyPostMaterial = true;
            CVRApplyPostMaterial(__instance);
            catchApplyPostMaterial = false;
            if (postMaterials.Count > 0)
            {
                logger.Msg($"Applying {postMaterials.Count} post-processing filters: {string.Join(", ", postMaterials.Select(m => m.name))}");
                foreach (var m in postMaterials)
                    // dangerous as we use same texture in and out, but it works
                    UnityEngine.Graphics.Blit(renderTexture, renderTexture, m);
                postMaterials.Clear();
            }

            return (renderTexture, shotRotation);
        }

        async public static Task<(IntPtr, int)> CopyTextureBackToMainMemory(RenderTexture renderTexture, ImageSettings settings) {
            (IntPtr, int) data = default;

            if (SystemInfo.supportsAsyncGPUReadback)
            {
                var stopwatch = Stopwatch.StartNew();
                var request = AsyncGPUReadback.Request(renderTexture, 0,
                    settings.HasAlpha ? TextureFormat.ARGB32 : TextureFormat.RGB24, new Action<AsyncGPUReadbackRequest>(r =>
                {
                    if (r.hasError)
                        logger.Warning("Readback request finished with error (start)");

                    // TODO: those are private, we should use GetData with NativeArray
                    // TODO: probably that the else below (fallback to unsupported gpu readback is never used anyway, could be removed?)
                    data = ToBytes(r.GetDataRaw(0), r.GetLayerDataSize());
                    MelonDebug.Msg($"Bytes readback took total {stopwatch.ElapsedMilliseconds}");
                }));
                
                while (!request.done && !request.hasError && data.Item1 == IntPtr.Zero)
                    await TaskUtilities.YieldToMainThread();

                if (request.hasError)
                    logger.Warning("Readback request finished with error (in loop)");
                
                if (data.Item1 == IntPtr.Zero)
                {
                    MelonDebug.Msg("Data was null after request was done, waiting more");
                    await TaskUtilities.YieldToMainThread();
                }
            }
            else
            {
                unsafe
                {
                    logger.Msg("Does not support readback, using fallback texture read method");

                    var hasAlpha = settings.HasAlpha;
                    var w = settings.Width;
                    var h = settings.Height;

                    RenderTexture.active = renderTexture;
                    var newTexture = new Texture2D(w, h, hasAlpha ? TextureFormat.ARGB32 : TextureFormat.RGB24, false);
                    newTexture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    newTexture.Apply();
                    RenderTexture.active = null;

                    var bytes = newTexture.GetRawTextureData<byte>();
                    data = (Marshal.AllocHGlobal(bytes.Length), bytes.Length);
                    Buffer.MemoryCopy(bytes.m_Buffer, data.Item1.ToPointer(), bytes.Length, bytes.Length);

                    Object.Destroy(newTexture);
                }
            }

            return data;
        }

        public static async Task TakeScreenshot(PortableCamera __instance, Camera camera, ImageSettings settings)
        {
            await TaskUtilities.YieldToFrameEnd();

            // pass settings by reference because we modify width+height for auto rotation
            var (renderTexture, rotationQuarters) = PrepareCameraAndRender(__instance, camera, ref settings);
            
            renderTexture.ResolveAntiAliasedSurface();
            LfsApi.InvokeScreenshotTexture(renderTexture);

            var data = await CopyTextureBackToMainMemory(renderTexture, settings);

            renderTexture.Release();
            Object.Destroy(renderTexture);

            var getPath = LfsApi.MakeScreenshotFilePath ?? GetPath;
            var targetFile = getPath(settings);

            // recursively create all directory if required
            var targetDir = Path.GetDirectoryName(targetFile);
            Directory.CreateDirectory(targetDir); // won't fail if exists already

            MetadataV2 metadata = null;
            MetaPort metaport;
            GameObject localPlayerObject;
            if (ourMetadata.Value 
                && (metaport = MetaPort.Instance) != null 
                && (localPlayerObject = PlayerSetup.Instance.gameObject) != null)
            {
                metadata = new MetadataV2(
                    rotationQuarters,
                    new API.CurrentPlayerInfo
                    {
                        Uuid = metaport.ownerId,
                        Username = AuthManager.Username,
                        Transform = localPlayerObject.transform,
                    },
                    new API.CurrentInstanceInfo
                    {
                        InstanceId = metaport.CurrentInstanceId,
                        InstanceName = metaport.CurrentInstanceName,
                        WorldId = metaport.CurrentWorldId != "" ? metaport.CurrentWorldId : metaport.homeWorldGuid,
                    },
                    API.MetadataV2.GetPlayerList(camera)
                );
            }

            await EncodeAndSavePicture(targetFile, data, settings, rotationQuarters, metadata)
                .ConfigureAwait(false);
        }
        
        private static unsafe (IntPtr, int) ToBytes(IntPtr pointer, int length)
        {
            var data = Marshal.AllocHGlobal(length);
            
            Buffer.MemoryCopy((void*) pointer, (void*) data, length, length);

            return (data, length);
        }
        
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }

            return null;
        }

        private static async Task EncodeAndSavePicture(string filePath, (IntPtr, int Length) pixelsPair,
            ImageSettings settings, ScreenshotRotation rotationQuarters, MetadataV2 metadata)
        {
            if (pixelsPair.Item1 == IntPtr.Zero) return;
            
            // yield to background thread
            await Task.Delay(1).ConfigureAwait(false);

            if (Thread.CurrentThread == ourMainThread)
                logger.Error("Image encode is executed on main thread - it's a bug!");

            var hasAlpha = settings.HasAlpha;
            var w = settings.Width;
            var h = settings.Height;

            var pixelFormat = hasAlpha ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            using var bitmap = new Bitmap(w, h, pixelFormat);
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, pixelFormat);
            unsafe
            {
                // swap colors [a]rgb -> bgr[a] AND horizontal flip (unity render is 0,0 bottom left)
                if (hasAlpha) // 32 bits
                {
                    uint* input = (uint*)pixelsPair.Item1;
                    uint* output = (uint*)bitmapData.Scan0 + w - 1; // scan X-axis backward
                    for (uint y = 0; y < h; ++y, output += 2*w)
                        for (uint x = 0; x < w; ++x, ++input, --output)
                        {
                            uint v = *input;
                            // flip bit order (endianness)
                            *output = ((v >> 24) & 0xff) | ((v >> 8) & 0xff00) | ((v << 8) & 0xff0000) | ((v << 24) & 0xff000000);
                        }
                }
                else { // 24 bits
                    Int24Bits* input = (Int24Bits*)pixelsPair.Item1;
                    Int24Bits* output = (Int24Bits*)bitmapData.Scan0 + w - 1; // scan X-axis backward
                    for (uint y = 0; y < h; ++y, output += 2*w)
                        for (uint x = 0; x < w; ++x, ++input, --output)
                        {
                            // flip bit order (endianness)
                            (output->r, output->g, output->b) = (input->b, input->g, input->r);
                        }
                }
            }

            bitmap.UnlockBits(bitmapData);
            Marshal.FreeHGlobal(pixelsPair.Item1);


            var format = ourFormat.Value switch
            {
                ImageFormats.auto when ourSupportsWebP => ImageFormats.webp,
                ImageFormats.auto when hasAlpha        => ImageFormats.png,
                ImageFormats.auto                      => ImageFormats.jpeg,
                _                                      => ourFormat.Value,
            };
            var description = metadata?.ToString();

            // https://docs.microsoft.com/en-us/windows/win32/gdiplus/-gdiplus-constant-property-item-descriptions
            if (description != null)
            {
                // png description is saved as iTXt chunk manually
                if (format == ImageFormats.jpeg)
                {
                    var stringBytesCount = Encoding.Unicode.GetByteCount(description);
                    var allBytes = new byte[8 + stringBytesCount];
                    Encoding.ASCII.GetBytes("UNICODE\0", 0, 8, allBytes, 0);
                    Encoding.Unicode.GetBytes(description, 0, description.Length, allBytes, 8);

                    var pi = (PropertyItem) FormatterServices.GetUninitializedObject(typeof(PropertyItem));
                    pi.Type = 7; // PropertyTagTypeUndefined
                    pi.Id = 0x9286; // PropertyTagExifUserComment
                    pi.Value = allBytes;
                    pi.Len = pi.Value.Length;
                    bitmap.SetPropertyItem(pi);
                }
            }

            if (format == ImageFormats.jpeg)
            {
                var encoder = GetEncoder(ImageFormat.Jpeg);
                using var parameters = new EncoderParameters(1)
                {
                    Param = {[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, ourJpegPercent.Value)}
                };
                filePath = Path.ChangeExtension(filePath, ".jpg");
                bitmap.Save(filePath, encoder, parameters);
            }
            else if (format == ImageFormats.webp)
            {
                filePath = Path.ChangeExtension(filePath, ".webp");
                // we had to separate this function to make webp dll dependencies optional
                WebpUtils.SaveToFile(filePath, bitmap, description, ourWebpPercent.Value);
            }
            else
            {
                filePath = Path.ChangeExtension(filePath, ".png");
                bitmap.Save(filePath, ImageFormat.Png);
                if (description != null)
                {
                    using var pngStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);
                    var originalEndChunkBytes = new byte[12];
                    pngStream.Position = pngStream.Length - 12;
                    pngStream.Read(originalEndChunkBytes, 0, 12);
                    pngStream.Position = pngStream.Length - 12;
                    var itxtChunk = PngUtils.ProducePngDescriptionTextChunk(description);
                    pngStream.Write(itxtChunk, 0, itxtChunk.Length);
                    pngStream.Write(originalEndChunkBytes, 0, originalEndChunkBytes.Length);
                }
            }

            await TaskUtilities.YieldToMainThread();

            logger.Msg($"Image saved to {filePath}");

            // compatibility with log-reading tools
            UnityEngine.Debug.Log($"Took screenshot to: {filePath}");

            LfsApi.InvokeScreenshotSaved(filePath, w, h, metadata);
        }

        public static string GetPath(ImageSettings settings)
        {
            var dateStr = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff");
            return string.Join(@"\", new string[] {
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "ChilloutVR",
                DateTime.Now.ToString("yyyy"),
                DateTime.Now.ToString("MM"),
                $"ChilloutVR-{dateStr}.jpg" });
        }
    }

    public struct ImageSettings
    {
        public int Width;
        public int Height;
        public bool HasAlpha;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    internal struct Int24Bits
    {
        public byte r;
        public byte g;
        public byte b;
    }
}
