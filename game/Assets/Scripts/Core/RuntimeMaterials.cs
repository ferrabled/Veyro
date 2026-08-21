using System.Collections.Generic;
using UnityEngine;

namespace MotionRunner.Core
{
    /// Colored materials cloned from the shipped URP-Lit base
    /// (see ProjectSetup.CreateBaseMaterial for why a Resources asset is required).
    public static class RuntimeMaterials
    {
        static Material _baseLit;
        static Material BaseLit =>
            _baseLit != null ? _baseLit : _baseLit = Resources.Load<Material>("Materials/PrimitiveLit");

        static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();

        /// A fresh material instance - use only when the caller intends to tint it later.
        public static Material Lit(Color color)
        {
            var m = new Material(BaseLit) { color = color };
            return m;
        }

        /// One shared material per color. The chunk system builds hundreds of renderers, so
        /// handing each a private material would grow memory for no visual gain (T-003 asks
        /// for a flat memory profile over a ten-minute run).
        public static Material Shared(Color color)
        {
            if (Cache.TryGetValue(color, out var cached) && cached != null) return cached;
            var m = Lit(color);
            Cache[color] = m;
            return m;
        }
    }
}
