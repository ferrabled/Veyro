using System;
using MotionRunner.Commerce;
using MotionRunner.Progression;
using MotionRunner.Social;
using UnityEngine;

namespace MotionRunner.Core
{
    /// Cached server XP, local collection and loadout state; all scoped to the confirmed profile.
    public sealed class SeasonService : ISeasonProgress, IDisposable
    {
        const string LastProfileKey="veyro.season.last-profile";
        readonly IStore _store;
        readonly IProfileService _profile;
        public SeasonInventory Inventory { get; private set; }
        public SeasonCurve Curve { get; }
        public string ProfileId { get; private set; }
        public bool HasRecordedXp => !string.IsNullOrEmpty(ProfileId);
        public bool IsConnected => _profile?.Current != null;
        public event Action Changed;
        public string StoragePrefix => "veyro.season."+Curve.Id+"."+(ProfileId ?? "guest")+".";
        public SeasonService(IStore store, IProfileService profile, SeasonCurve curve)
        {
            _store=store; _profile=profile; Curve=curve;
            ProfileId=PlayerPrefs.GetString(LastProfileKey,"");
            LoadInventory();
            _store.EntitlementsChanged+=OnEntitlements;
            if(_profile!=null) { _profile.ProfileChanged+=OnProfile; if(_profile.Current!=null) OnProfile(); }
        }
        void LoadInventory() => Inventory=new SeasonInventory(Curve,
            PlayerPrefs.GetInt("veyro.season.xp."+ProfileId,0),PlayerPrefs.GetString(StoragePrefix+"claims",""));
        void OnProfile()
        {
            var profile=_profile.Current;
            string id=profile?.UserId ?? "";
            if(profile==null && HasRecordedXp)
            {
                PlayerPrefs.DeleteKey("veyro.season.xp."+ProfileId);
                PlayerPrefs.DeleteKey(StoragePrefix+"claims");
                PlayerPrefs.DeleteKey(StoragePrefix+"loadout");
            }
            if(id!=ProfileId) { ProfileId=id; LoadInventory(); }
            Inventory.SetRecordedXp(profile?.Xp ?? 0);
            PlayerPrefs.SetString(LastProfileKey,id);
            if(profile!=null) PlayerPrefs.SetInt("veyro.season.xp."+id,Inventory.Xp);
            PlayerPrefs.Save(); Changed?.Invoke();
        }
        void OnEntitlements() => Changed?.Invoke();
        public bool IsOwned(string id) => Inventory.IsOwned(id,_store.ActiveEntitlements);
        public bool CanCollect(string id) => Inventory.CanCollect(id,_store.ActiveEntitlements);
        public bool Collect(string id)
        {
            if(!Inventory.Collect(id,_store.ActiveEntitlements)) return false;
            PlayerPrefs.SetString(StoragePrefix+"claims",Inventory.SaveClaims());
            PlayerPrefs.Save(); Changed?.Invoke(); return true;
        }
        public SeasonSnapshot Read(bool passOwned)
        {
            int level=Inventory.Level;
            int start=Curve.Threshold(level);
            int required=level==SeasonCurve.Levels ? 0 : Curve.Threshold(level+1)-start;
            return new SeasonSnapshot(level,Inventory.Xp-start,required,true,passOwned);
        }
        public void Dispose()
        {
            _store.EntitlementsChanged-=OnEntitlements;
            if(_profile!=null) _profile.ProfileChanged-=OnProfile;
        }
    }
}
