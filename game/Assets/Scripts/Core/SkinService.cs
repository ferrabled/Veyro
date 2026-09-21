using System;
using MotionRunner.Commerce;
using UnityEngine;

namespace MotionRunner.Core
{
    /// The shop's existing skin seam now owns a complete, ownership-checked cosmetic loadout.
    public sealed class SkinService : IDisposable
    {
        readonly IStore _store;
        readonly Action<CosmeticLoadout> _apply;
        public SeasonService Season { get; }
        public CosmeticLoadout Selected { get; private set; }
        public CosmeticLoadout Effective => Selected.Validated(IsUnlocked);
        public string EquippedId => Effective.Skin;
        public event Action Changed;
        string Key => Season.StoragePrefix+"loadout";
        public SkinService(IStore store, SeasonService season, Action<CosmeticLoadout> apply)
        {
            _store=store; Season=season; _apply=apply;
            season.Changed+=Apply;
            Apply();
        }
        public bool IsUnlocked(string id) => Season.IsOwned(id);
        public bool IsEquipped(string id)
        {
            var item=CosmeticCatalog.Find(id);
            return item!=null && Effective.Resolved(item.Slot)==id;
        }
        public bool Equip(string id)
        {
            if(!IsUnlocked(id)) return false;
            Selected.Equip(CosmeticCatalog.Find(id)); Save(); return true;
        }
        public void Clear(CosmeticSlot slot) { Selected.Clear(slot); Save(); }
        void Save()
        {
            PlayerPrefs.SetString(Key,Selected.Serialize()); PlayerPrefs.Save(); Apply();
        }
        public void Apply()
        {
            string encoded=PlayerPrefs.GetString(Key,"");
            if(string.IsNullOrEmpty(encoded))
            {
                Selected=new CosmeticLoadout();
                var legacy=CosmeticCatalog.Find(PlayerPrefs.GetString("veyro.skin","runner"));
                if(legacy!=null && legacy.Slot==CosmeticSlot.Skin && IsUnlocked(legacy.Id)) Selected.Equip(legacy);
            }
            else Selected=CosmeticLoadout.Read(encoded);
            _apply?.Invoke(Effective); Changed?.Invoke();
        }
        public static Color ToColor(CosmeticColor c) => new Color(c.R,c.G,c.B);
        public void Dispose() => Season.Changed-=Apply;
    }
}
