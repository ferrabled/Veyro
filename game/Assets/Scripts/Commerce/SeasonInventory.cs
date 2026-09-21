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
            // Earlier test builds awarded dyes at these same XP milestones. Keep the dyes and
            // recognize the equivalent attachment claim without granting any XP or entitlement.
            foreach(var pair in new[]{("sunset","quiver"),("sky","spellbook"),("plum","shield"),("chrome","wings")})
                if(_collected.Contains(pair.Item1))_collected.Add(pair.Item2);
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

    /// Character identity and accessory choices persist independently. Old six-slot saves migrate
    /// without losing colours or ownership; a clear marker suppresses a character's default FX.
    public sealed class CosmeticLoadout
    {
        readonly Dictionary<CosmeticSlot,string> _slots=new Dictionary<CosmeticSlot,string>();
        public string Skin { get; private set; }=CosmeticCatalog.DefaultSkinId;
        string Raw(CosmeticSlot slot) => slot==CosmeticSlot.Skin ? Skin : _slots.TryGetValue(slot,out var value) ? value : "";
        public string Item(CosmeticSlot slot) => Raw(slot)=="-" ? "" : Raw(slot);
        public void Equip(CosmeticItem item)
        {
            if(item==null)return;
            if(item.Slot==CosmeticSlot.Skin) Skin=item.Id;
            else _slots[item.Slot]=item.Id;
        }
        public void Clear(CosmeticSlot slot)
        {
            if(slot==CosmeticSlot.Skin)Skin=CosmeticCatalog.DefaultSkinId;
            else _slots[slot]="-";
        }
        public string Serialize() => string.Join("|",Enum.GetValues(typeof(CosmeticSlot)).Cast<CosmeticSlot>().Select(Raw));
        public static CosmeticLoadout Read(string encoded)
        {
            var result=new CosmeticLoadout();
            if(string.IsNullOrEmpty(encoded))return result;
            var ids=encoded.Split('|');
            if(ids.Length!=6 && ids.Length!=7)return result;
            for(int i=0;i<ids.Length;i++)
            {
                if(i>0 && ids[i]=="-"){result._slots[(CosmeticSlot)i]="-";continue;}
                var item=CosmeticCatalog.Find(ids[i]);
                if(item!=null && item.Slot==(CosmeticSlot)i)result.Equip(item);
            }
            return result;
        }
        public CosmeticLoadout Validated(Func<string,bool> owned)
        {
            var result=new CosmeticLoadout();
            if(owned(Skin))result.Equip(CosmeticCatalog.Find(Skin));
            foreach(var pair in _slots)
                if(pair.Value=="-")result._slots[pair.Key]="-";
                else if(owned(pair.Value))result.Equip(CosmeticCatalog.Find(pair.Value));
            return result;
        }
        string Resolve(CosmeticSlot slot,string fallback) => _slots.TryGetValue(slot,out var value) ? value=="-" ? "" : value : fallback;
        /// What the runner actually wears in a slot, character defaults included, so "equipped"
        /// in the menu matches what is rendered. A character id standing in for its own built-in
        /// FX (ember/frost) is not a wearable item, so only ids belonging to the slot count.
        public string Resolved(CosmeticSlot slot)
        {
            string id=slot==CosmeticSlot.Trail ? Trail : slot==CosmeticSlot.Headwear ? Headwear :
                slot==CosmeticSlot.Aura ? Aura : Item(slot);
            return CosmeticCatalog.Find(id)?.Slot==slot ? id : "";
        }
        public string Body => Item(CosmeticSlot.Body);
        public string Trail => Resolve(CosmeticSlot.Trail,Skin=="prism" ? "aurora" : Skin=="ember" ? "ember" : Skin=="frost" ? "frost" : "");
        public string Headwear => Resolve(CosmeticSlot.Headwear,Skin=="prism" ? "crown" : "");
        public string Aura => Resolve(CosmeticSlot.Aura,Skin=="prism" ? "comet" : Skin=="ember" ? "ember" : Skin=="frost" ? "frost" : "");
        public string CrashFx => Item(CosmeticSlot.CrashFx);
        public string Back => Item(CosmeticSlot.Back);
        public CosmeticItem Outfit => CosmeticCatalog.Find(Body) ?? CosmeticCatalog.Find(Skin) ?? CosmeticCatalog.Find("runner");
    }
}
