using UnityEngine;
namespace MotionRunner.Art
{
    public sealed class CharacterArt : ScriptableObject
    {
        public GameObject Ember,Frost;
        public Material EmberMaterial,FrostMaterial;
        public GameObject Wings,Quiver,Shield,Spellbook;
        static CharacterArt _cached;
        public static CharacterArt Load()=>_cached!=null ? _cached : _cached=Resources.Load<CharacterArt>("Art/Characters");
        public GameObject Character(string id)=>id=="ember" ? Ember : id=="frost" ? Frost : ParkAssets.Load().Runner;
        public Material Outfit(string id)=>id=="ember" ? EmberMaterial : id=="frost" ? FrostMaterial : ParkAssets.Load().RunnerMaterial;
        public GameObject Attachment(string id)=>id=="wings" ? Wings : id=="quiver" ? Quiver : id=="shield" ? Shield : id=="spellbook" ? Spellbook : null;
    }
}
