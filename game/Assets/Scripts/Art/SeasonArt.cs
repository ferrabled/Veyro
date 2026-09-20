using UnityEngine;
namespace MotionRunner.Art
{
    public sealed class SeasonArt : ScriptableObject
    {
        public GameObject Cap, TopHat, Crown;
        public Material Effects,Shadow;
        public Texture2D Spark, Star, Trace;
        static SeasonArt _cached;
        public static SeasonArt Load() => _cached!=null ? _cached : _cached=Resources.Load<SeasonArt>("Art/Season");
        public GameObject Hat(string id) => id=="cap" ? Cap : id=="tophat" ? TopHat : id=="crown" ? Crown : null;
    }
}
