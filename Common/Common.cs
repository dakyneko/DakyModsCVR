using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.Camera;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Daky
{
    public static class Dakytils
    {
        public static T NN<T>(this T go) where T : UnityEngine.Object => go == null ? null : go;

        public static Stream? StreamFromAssembly(string namespace_, string filename) {
            var stream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(namespace_ + "." + filename);
            if (stream == null || stream.Length == 0) return null;
            return stream;
        }
        public static byte[]? BytesFromAssembly(string namespace_, string filename)
        {
            using var stream = StreamFromAssembly(namespace_, filename);
            if (stream == null) return null;

            using var memStream = new MemoryStream((int)stream.Length);
            stream.CopyTo(memStream);
            return memStream.ToArray();
        }

        public static Sprite SpriteFromAssembly(string namespace_, string filename, int width = 512, int height = 512)
        {
            var bytes = BytesFromAssembly(namespace_, filename);
            var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            texture.LoadImage(bytes);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0, 0));
        }

        public static PortableCameraSettingsHeader PortableCameraAddHeader(PortableCamera cam)
        {
            var i = cam.@interface;
            // adapted from PortableCameraInterface.AddAndGetHeader
            GameObject spacerObject = UnityEngine.Object.Instantiate(cam.spacerPrefab, i.settingsContentRow);
            GameObject gameObject = UnityEngine.Object.Instantiate(cam.settingsHeadingPrefab, i.settingsContentRow);
            var component = gameObject.GetComponent<PortableCameraSettingsHeader>();
            component.OriginType = "CameraStar";
            component.HeaderText.text = "Camera*";
            component.headerObject = gameObject;
            component.spacerObject = spacerObject;
            i.headers.Add(component);
            return component;
        }

        // boiler plate woooh
        private static PortableCameraSetting NewCameraSetting(PortableCamera pcam, string name, string display,
            PortableCameraSettingType type, Type origin, object initialValue)
        {
            var s = pcam.@interface.AddAndGetSetting(type);
            s.SettingIdentifier = name;
            s.DisplayName = display;
            s.OriginType = origin.ToString();
            s.DefaultValue = initialValue;
            return s;
        }

        public static PortableCameraSetting NewCameraSetting(PortableCamera pcam, string name, string display,
            Type origin, bool initialValue, Action<bool> onChange)
        {
            var s = NewCameraSetting(pcam, name, display, PortableCameraSettingType.Bool, origin, initialValue);
            s.BoolChanged = onChange;
            s.Load();
            return s;
        }

        public static PortableCameraSetting NewCameraSetting(PortableCamera pcam, string name, string display,
            Type origin, int initialValue, Action<int> onChange, int minValue = 0, int maxValue = 100)
        {
            var s = NewCameraSetting(pcam, name, display, PortableCameraSettingType.Int, origin, initialValue);
            s.IntChanged = onChange;
            s.MinValue = minValue;
            s.MaxValue = maxValue;
            s.Load();
            return s;
        }

        public static PortableCameraSetting NewCameraSetting(PortableCamera pcam, string name, string display,
            Type origin, float initialValue, Action<float> onChange, float minValue = 0f, float maxValue = 1f)
        {
            var s = NewCameraSetting(pcam, name, display, PortableCameraSettingType.Float, origin, initialValue);
            s.FloatChanged = onChange;
            s.MinValue = minValue;
            s.MaxValue = maxValue;
            s.Load();
            // TODO: CVR has bug when values are 0 it consider null maybe and show _Value instead :(
            return s;
        }

        public static V GetWithDefault<U,V>(this Dictionary<U,V> d, U key, Func<V> makeDefault)
        {
            V v;
            if (d.TryGetValue(key, out v))
                return v;

            v = makeDefault();
            d.Add(key, v);
            return v;
        }

        public static V GetWithDefault<U,V>(this Dictionary<U,V> d, U key) where V : new()
        {
            V v;
            if (d.TryGetValue(key, out v))
                return v;

            v = new V();
            d.Add(key, v);
            return v;
        }

        public static T TryOrDefault<T>(Func<T> f)
        {
            try
            {
                return f();
            }
            catch (Exception)
            {
                return default;
            }
        }

        // returns a function that check if the value changed (returned by get) if so calls action with old and new value
        public static Action TriggerOnChange<T>(Func<T> get, Action<T, T> action)
        {
            var v = get();
            return () =>
            {
                var newV = get();
                if (!EqualityComparer<T>.Default.Equals(v, newV))
                    action(v, newV);
                v = newV;
            };
        }
        // almost the same but the action function is passed afterward
        public static Action<Action<T, T>> TriggerOnChange<T>(Func<T> get)
        {
            var v = get();
            return (action) =>
            {
                var newV = get();
                if (!EqualityComparer<T>.Default.Equals(v, newV))
                    action(v, newV);
                v = newV;
            };
        }

        public static PlayerDescriptor GetPlayerById(string id)
        {
            if (MetaPort.Instance.ownerId == id)
                return PlayerSetup.Instance.gameObject.GetComponent<PlayerDescriptor>();
            else
                return MetaPort.Instance.PlayerManager.NetworkPlayers.First(p => p.Uuid == id).PlayerDescriptor;
        }
    }
}