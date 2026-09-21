using MotionRunner.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotionRunner.Art
{
    public static class ParkTheme
    {
        public static Color Hex(uint rgb) => new Color(((rgb >> 16) & 255) / 255f,
            ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        public static readonly Color Ink = Hex(0x12454C);
        public static readonly Color Leaf = Hex(0x358C7F);
        public static readonly Color Mint = Hex(0x87BEA6);
        public static readonly Color Paper = Hex(0xFBF5E9);
        public static readonly Color Stone = Hex(0xC6B69B);
        public static readonly Color Pink = Hex(0xEE3D87);
        public static readonly Color Coral = Hex(0xD86755);
        public static readonly Color Gold = Hex(0xFFC85C);

        public static void Apply(Camera camera, Light sun)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = Hex(0xD6EBDB);
            RenderSettings.skybox = ParkAssets.Load().Sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(0xCCE7E0);
            RenderSettings.ambientEquatorColor = Hex(0xA6BDB3);
            RenderSettings.ambientGroundColor = Hex(0x6E807A);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex(0xD6EBDB);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 122f;
            sun.color = Hex(0xFFF0D4);
            sun.intensity = 0.70f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        }

        public static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            if (Application.isPlaying) Object.Destroy(go.GetComponent<Collider>());
            else Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = RuntimeMaterials.Shared(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
