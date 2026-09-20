using System;
using System.Collections.Generic;
using System.Linq;

namespace MotionRunner.Commerce
{
    /// Cumulative thresholds, independent of earned XP. Changing the test curve never grants XP.
    public sealed class SeasonCurve
    {
        readonly int[] _thresholds;
        public string Id { get; }
        public const int Levels = 10;
        public static SeasonCurve Testing => new SeasonCurve("test-v1", new[]{0,1,2,3,4,5,6,7,8,9});
        public static SeasonCurve Production => new SeasonCurve("season1-v1", new[]{0,10,25,50,100,180,300,450,650,900});
        public static bool ProductionApproved => false;
        public SeasonCurve(string id, int[] thresholds)
        {
            if (thresholds == null || thresholds.Length != Levels || thresholds[0] != 0)
                throw new ArgumentException("Ten thresholds starting at zero are required.");
            for (int i=1;i<thresholds.Length;i++)
                if (thresholds[i] <= thresholds[i-1]) throw new ArgumentException("Thresholds must increase.");
            Id=id; _thresholds=(int[])thresholds.Clone();
        }
        public int Threshold(int level) => _thresholds[Math.Max(1,Math.Min(Levels,level))-1];
        public int Level(int xp)
        {
            int level=1;
            while(level<Levels && xp>=_thresholds[level]) level++;
            return level;
        }
    }

    public sealed class SeasonInventory
    {
        readonly HashSet<string> _collected = new HashSet<string>(StringComparer.Ordinal);
        public SeasonCurve Curve { get; }
        public int Xp { get; private set; }
        public int Level => Curve.Level(Xp);
        public SeasonInventory(SeasonCurve curve, int xp, string savedClaims="")
        {
            Curve=curve; SetRecordedXp(xp);
            foreach(string id in (savedClaims ?? "").Split(','))
                if(CosmeticCatalog.Find(id)?.Rule.Kind == UnlockKind.SeasonLevel) _collected.Add(id);
        }
        public void SetRecordedXp(int xp) => Xp=Math.Max(0,xp);
        public bool IsCollected(string id)
        {
            var item=CosmeticCatalog.Find(id);
            return item != null && (item.Rule.Kind != UnlockKind.SeasonLevel || item.Rule.Level==1 || _collected.Contains(id));
        }
        public bool IsOwned(string id, IReadOnlyCollection<string> entitlements)
        {
            var item=CosmeticCatalog.Find(id);
            return item != null && item.Rule.IsUnlocked(entitlements,Level) && IsCollected(id);
        }
        public bool CanCollect(string id, IReadOnlyCollection<string> entitlements)
        {
            var item=CosmeticCatalog.Find(id);
            return item != null && item.Rule.Kind==UnlockKind.SeasonLevel &&
                !IsCollected(id) && item.Rule.IsUnlocked(entitlements,Level);
        }
        public bool Collect(string id, IReadOnlyCollection<string> entitlements) =>
            CanCollect(id,entitlements) && _collected.Add(id);
        public string SaveClaims() => string.Join(",",_collected.OrderBy(id=>id,StringComparer.Ordinal));
    }

    /// Presets and slot choices are mutually exclusive: customizing starts with the base outfit,
    /// so the exclusive parts of Ember/Frost cannot become independently equipped rewards.
    public sealed class CosmeticLoadout
    {
        readonly Dictionary<CosmeticSlot,string> _slots=new Dictionary<CosmeticSlot,string>();
        public string Skin { get; private set; }=CosmeticCatalog.DefaultSkinId;
        public string Item(CosmeticSlot slot) => slot==CosmeticSlot.Skin ? Skin :
            _slots.TryGetValue(slot,out var value) ? value : "";
        public void Equip(CosmeticItem item)
        {
            if(item.Slot==CosmeticSlot.Skin) { Skin=item.Id; _slots.Clear(); }
            else { Skin=""; _slots[item.Slot]=item.Id; }
        }
        public void Clear(CosmeticSlot slot)
        {
            if(slot==CosmeticSlot.Skin) { Skin=CosmeticCatalog.DefaultSkinId; _slots.Clear(); }
            else { Skin=""; _slots.Remove(slot); }
        }
        public string Serialize() => string.Join("|",Enum.GetValues(typeof(CosmeticSlot)).Cast<CosmeticSlot>().Select(Item));
        public static CosmeticLoadout Read(string encoded)
        {
            var result=new CosmeticLoadout();
            if(string.IsNullOrEmpty(encoded)) return result;
            var ids=encoded.Split('|');
            if(ids.Length!=6) return result;
            result.Skin="";
            for(int i=0;i<ids.Length;i++)
            {
                var item=CosmeticCatalog.Find(ids[i]);
                if(item==null || item.Slot!=(CosmeticSlot)i) continue;
                if(i==0) { result.Equip(item); return result; }
                result._slots[(CosmeticSlot)i]=item.Id;
            }
            return result;
        }
        public CosmeticLoadout Validated(Func<string,bool> owned)
        {
            var result=new CosmeticLoadout();
            if(!string.IsNullOrEmpty(Skin))
            {
                if(owned(Skin)) result.Equip(CosmeticCatalog.Find(Skin));
                return result;
            }
            foreach(var pair in _slots)
                if(owned(pair.Value)) result.Equip(CosmeticCatalog.Find(pair.Value));
            return result;
        }
        public string Body => Item(CosmeticSlot.Body);
        public string Trail => Skin=="prism" ? "aurora" : Skin=="ember" ? "ember" : Skin=="frost" ? "frost" : Item(CosmeticSlot.Trail);
        public string Headwear => Skin=="prism" ? "crown" : Item(CosmeticSlot.Headwear);
        public string Aura => Skin=="prism" ? "comet" : Skin=="ember" ? "ember" : Skin=="frost" ? "frost" : Item(CosmeticSlot.Aura);
        public string CrashFx => Item(CosmeticSlot.CrashFx);
        public CosmeticItem Outfit => CosmeticCatalog.Find(string.IsNullOrEmpty(Skin) ? Body : Skin) ?? CosmeticCatalog.Find("runner");
    }
}
