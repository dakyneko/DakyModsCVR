using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using System;
using System.Drawing;
using System.Linq;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Encoder = System.Drawing.Imaging.Encoder;

namespace CameraInstants;

public class InstantsPropBuilder
{
    public static string Build(Stream templateBundle, Bitmap bitmap, string gid, float propSize = 0.6f)
    {
        var gidLen = 36;
        if (gid.Length != gidLen) throw new Exception($"gid should be {gidLen} long");

        var manager = new AssetsManager();
        // path is used for caching, so we'll provide a static name, it shouldn't change
        var bundlei = manager.LoadBundleFile(templateBundle, "cvrspawnable_00000000-0000-0000-0000-000000000000.cvrprop");
        var bundle = bundlei.file;
        var info = bundle.BlockAndDirInfo;

        var assetIndex = 0;
        var loadDeps = false; // seems not necessary
        var asseti = manager.LoadAssetsFileFromBundle(bundlei, assetIndex, loadDeps);
        asseti.name = info.DirectoryInfos[0].Name = $"CAB-{gid}"; // this is necessary!
        var asset = asseti.file;

        // detect root GameObject by name and Transform
        var go = asset
            .GetAssetsOfType(AssetClassID.GameObject)
            .First(x => manager.GetBaseField(asseti, x)["m_Name"]?.AsString?.StartsWith("CVRSpawnable_") ?? false);
        if (go == null) throw new Exception($"Couldn't find root object");
        var transform = GetFirstChildrenComponentOfType(manager, asseti, go, AssetClassID.Transform);
        if (transform == null) throw new Exception($"Couldn't find root transform");

        // change root GameObject name to match the provided gid (optional)
        SetString(manager, asseti, go, "m_Name", $"CVRSpawnable_{gid}");

        // replace image in Texture
        int width = bitmap.Width, height = bitmap.Height;
        var tex = asset.GetAssetsOfType(AssetClassID.Texture2D)?[0];
        if (tex == null) throw new Exception($"Couldn't find Texture2D asset");
        ReplaceImage(manager, asseti, tex, bitmap);

        // adjust the RenderTexture
        var rtex = asset.GetAssetsOfType(AssetClassID.CustomRenderTexture)[0];
        SetInt(manager, asseti, rtex, "m_Width", width);
        SetInt(manager, asseti, rtex, "m_Height", height);

        // fit object size to image aspect ratio: XZ
        var aspect = 1f * width / height;
        var (objectWidth, objectHeight) = aspect > 1 ?
            (propSize, propSize / aspect) :
            (propSize * aspect, propSize);
        SetVectorf(manager, asseti, transform, "m_LocalScale", new float[] { objectWidth, objectHeight, 0.1f }); // rescale
        SetVectorf(manager, asseti, transform, "m_LocalRotation", new float[] { 0, 1, 0, 0 }); // enforce orientation

        // compression: it's weird, we have to write it, read and write again
        info.DirectoryInfos[assetIndex].SetNewData(asset);
        var memory = new MemoryStream();
        var writer = new AssetsFileWriter(memory);
        bundle.Write(writer);
        bundle.Close();

        // patch the .prefab gid directly in memory, yes it's beautiful isn't it?
        // why you ask? because cvr won't load it if it doesn't match gid!
        // TODO: is there a way more convenient to do this?
        var prefabPattern = Encoding.ASCII.GetBytes("/cvrspawnable_");
        var prefabExtPattern = Encoding.ASCII.GetBytes(".prefab");
        var bundleBytes = memory.GetBuffer();
        var idx = BytePatternSearch(bundleBytes, prefabPattern);
        if (idx == -1) throw new Exception("Couldn't patch the .prefab name in bundle #1");
        var idx2 = BytePatternSearch(bundleBytes, prefabExtPattern, idx);
        if (idx2 == -1 || (idx2 - idx) > 100) throw new Exception("Couldn't patch the .prefab name in bundle #2");
        idx += prefabPattern.Length;
        Array.Copy(Encoding.ASCII.GetBytes(gid), 0, bundleBytes, idx, gidLen);

        // compression: 2nd step, reread and compress this time
        bundle = new AssetBundleFile();
        bundle.Read(new AssetsFileReader(memory));
        var tmpFile = Path.GetTempFileName();
        var output = new FileStream(tmpFile, FileMode.Truncate); //new MemoryStream();
        writer = new AssetsFileWriter(output);
        bundle.Pack(writer, AssetBundleCompressionType.LZ4);
        writer.Dispose(); // cannot dispose
        bundle.Close();
        return tmpFile;
    }

    private static AssetFileInfo GetFirstChildrenComponentOfType(AssetsManager manager, AssetsFileInstance asseti, AssetFileInfo i, AssetClassID type)
    {
        var typeStr = type.ToString();
        var fields = manager.GetBaseField(asseti, i);
        var xs = fields["m_Component.Array"];
        if (xs == null) throw new Exception("Couldn't find component array");

        foreach (var x in xs)
            foreach (var y in x)
            {
                var info = manager.GetExtAsset(asseti, y).info;
                var asset = manager.GetBaseField(asseti, info);
                if (asset.TypeName == typeStr)
                    return info;
            }
        return null;
    }

    private static void SetString(AssetsManager manager, AssetsFileInstance asseti, AssetFileInfo i, string field, string value)
    {
        var fields = manager.GetBaseField(asseti, i);
        var f = fields[field];
        if (f == null) throw new Exception($"Couldn't find field {field} in {i}");

        f.AsString = value;
        i.SetNewData(fields);
    }

    static void SetInt(AssetsManager manager, AssetsFileInstance asseti, AssetFileInfo i, string field, int value)
    {
        var fields = manager.GetBaseField(asseti, i);
        var f = fields[field];
        if (f == null) throw new Exception($"Couldn't find field {field} in {i}");

        f.AsInt = value;
        i.SetNewData(fields);
    }

    private static void SetVectorf(AssetsManager manager, AssetsFileInstance asseti, AssetFileInfo transform, string field, float[] vs)
    {
        var fields = manager.GetBaseField(asseti, transform);
        var f = fields[field];
        if (f == null) throw new Exception($"Couldn't find field {field} in {transform}");

        for (int i = 0; i < vs.Length; ++i)
            f.Children[i].AsFloat = vs[i];
        transform.SetNewData(fields);
    }

    private static unsafe void ReplaceImage(AssetsManager manager, AssetsFileInstance asseti, AssetFileInfo tex, Bitmap bitmap)
    {
        int width = bitmap.Width, height = bitmap.Height;
        var fields = manager.GetBaseField(asseti, tex);
        var t = TextureFile.ReadTextureFile(fields);

        // preparing for unsafe memory operations
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        var dataLen = width * height * 4;
        var outputBytes = new byte[dataLen];
        var outputHandle = GCHandle.Alloc(outputBytes, GCHandleType.Pinned);
        var outputPtr = outputHandle.AddrOfPinnedObject();

        // we have to copy + flip the image, unity wants upside down
        uint* input = (uint*)data.Scan0;
        uint* output = (uint*)outputPtr + width * (height - 1);
        for (uint y = 0; y < height; ++y, output -= 2 * width) // input scans up->down, output down->up
            for (uint x = 0; x < width; ++x, ++input, ++output) // normal left to right
                *output = *input;

        bitmap.UnlockBits(data);
        outputHandle.Free();
        t.SetTextureDataRaw(outputBytes, width, height); // BGRA32 format
        t.WriteTo(fields);
        fields["m_TextureFormat"].AsInt = (int)UnityEngine.TextureFormat.BGRA32;
        tex.SetNewData(fields);
    }

    // MemoryExtensions aren't available in this .net version ;(
    // so we take a simpler but slower version, thanks https://stackoverflow.com/a/38625726
    private static int BytePatternSearch(byte[] src, byte[] pattern, int start = 0)
    {
        int maxFirstCharSlot = src.Length - pattern.Length + 1;
        for (int i = start; i < maxFirstCharSlot; i++)
        {
            if (src[i] != pattern[0]) // compare only first byte
                continue;
            
            // found a match on first byte, now try to match rest of the pattern
            for (int j = pattern.Length - 1; j >= 1; j--) 
            {
               if (src[i + j] != pattern[j]) break;
               if (j == 1) return i;
            }
        }
        return -1;
    }

    public static Bitmap ResizeImage(Bitmap bitmap, int maxSize)
    {
        // make the asset thumbnail
        // resize thumb largest dimension respecting aspect ratio
        var aspect = 1f * bitmap.Width / bitmap.Height;
        var (thumbWidth, thumbHeight) = aspect > 1 ?
            (maxSize, (int)Mathf.Floor(maxSize / aspect)) :
            ((int)Mathf.Floor(maxSize * aspect), maxSize);
        return new Bitmap(bitmap, thumbWidth, thumbHeight); // nice allocations
    }

    public static string MakeThumbnail(Bitmap bitmap, ImageFormat format, int quality = 80, int maxSize = 250)
    {
        var thumb = ResizeImage(bitmap, maxSize);

        var tmpFile = Path.GetTempFileName();
        thumb.Save(tmpFile,
            ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == format.Guid), // c# libs are fun
            new EncoderParameters { Param = new EncoderParameter[] { new(Encoder.Quality, quality) } });
        return tmpFile;
    }
}
