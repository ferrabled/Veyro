using System.Collections.Generic;
using MotionRunner.Audio;
using MotionRunner.Commerce;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// A large focused model above a wardrobe sheet. Browsing is always separate from equipping.
    public sealed class CosmeticsPage : MenuPage
    {
        readonly List<CosmeticItemCard> _cards=new List<CosmeticItemCard>();
        readonly List<Button> _categories=new List<Button>();
        RunnerPreview _preview;
        RectTransform _content;
        ScrollRect _scroll;
        Text _name,_description,_equipLabel,_count;
        Button _equip,_all,_owned,_clear;
        string _selected;
        CosmeticSlot _slot;
        bool _listening,_onlyOwned;
        protected override void Build()
        {
            var background=CosmeticUi.Surface(CosmeticUi.Area("Backdrop",Root,Vector2.zero,Vector2.one),MenuTheme.PreviewTop,0);
            background.Gradient=true;background.Bottom=MenuTheme.PreviewBottom;
            _preview=RunnerPreview.Create(CosmeticUi.Area("Preview",Root,new Vector2(0,0.43f),new Vector2(1,0.94f)));
            CosmeticUi.Pill(CosmeticUi.Rect("Back",Root,0.025f,0.21f,44,70),"< BACK",Menu.GoHome,MenuTheme.ItemCard,26,Sfx.UiBack);
            CosmeticUi.Text("Title",CosmeticUi.Rect("Heading",Root,0.24f,0.70f,44,70),"YOUR LOCKER",36,MenuTheme.Text,TextAnchor.MiddleCenter);
            _clear=CosmeticUi.Pill(CosmeticUi.Rect("Clear",Root,0.76f,0.985f,44,70),"CLEAR SLOT",ClearSlot,MenuTheme.ItemCard,22);
            var hint=CosmeticUi.Rect("RotateHintSlot",Root,0.33f,0.67f,142,44);
            CosmeticUi.Surface(hint,MenuTheme.ItemCard,18).raycastTarget=false;
            CosmeticUi.Text("RotateHint",hint,"DRAG TO ROTATE",20,MenuTheme.Dim,TextAnchor.MiddleCenter);
            var sheet=CosmeticUi.Area("WardrobeSheet",Root,Vector2.zero,new Vector2(1,0.45f));
            CosmeticUi.Surface(sheet,MenuTheme.Card,38);
            var handle=CosmeticUi.Rect("SheetHandle",sheet,0.45f,0.55f,10,6);CosmeticUi.Surface(handle,MenuTheme.Empty,3).raycastTarget=false;
            _name=CosmeticUi.Text("Name",CosmeticUi.Rect("SelectedName",sheet,0.025f,0.65f,30,56),"",34,MenuTheme.Text);
            _description=CosmeticUi.Text("Description",CosmeticUi.Rect("SelectedDescription",sheet,0.025f,0.65f,88,36),"",22,MenuTheme.Dim);
            _equip=CosmeticUi.Pill(CosmeticUi.Rect("Equip",sheet,0.66f,0.975f,36,78),"",Activate,MenuTheme.Accent,26);
            _equipLabel=_equip.GetComponentInChildren<Text>();_equipLabel.color=MenuTheme.OnAccent;
            var names=new[]{"RUNNER","DYES","TRAILS","HATS","AURAS","CRASH","BACK"};
            var icons=new[]{"runner","mint","aurora","cap","comet","confetti","wings"};
            for(int i=0;i<names.Length;i++)
            {
                var slot=(CosmeticSlot)i;
                var rect=CosmeticUi.Rect("Category"+i,sheet,0.015f+i*0.138f,0.015f+(i+1)*0.138f,142,112);
                var button=CosmeticUi.Pill(rect,"",()=>ChooseSlot(slot),MenuTheme.Card);
                CosmeticUi.Thumbnail(CosmeticUi.Area("Icon",rect,new Vector2(0.25f,0.35f),new Vector2(0.75f,0.98f)),icons[i]);
                var label=CosmeticUi.Text("CategoryName",CosmeticUi.Area("NameSlot",rect,Vector2.zero,new Vector2(1,0.33f)),names[i],18,MenuTheme.Text,TextAnchor.MiddleCenter);
                label.horizontalOverflow=HorizontalWrapMode.Overflow;
                label.resizeTextForBestFit=true;label.resizeTextMinSize=14;label.resizeTextMaxSize=18;
                _categories.Add(button);
            }
            _all=CosmeticUi.Pill(CosmeticUi.Rect("All",sheet,0.025f,0.24f,272,52),"ALL ITEMS",()=>Filter(false),size:21);
            _owned=CosmeticUi.Pill(CosmeticUi.Rect("Owned",sheet,0.24f,0.455f,272,52),"OWNED",()=>Filter(true),size:21);
            _count=CosmeticUi.Text("Count",CosmeticUi.Rect("CountSlot",sheet,0.49f,0.97f,272,52),"",22,MenuTheme.Dim,TextAnchor.MiddleRight);
            _content=CosmeticUi.Scroll(sheet,344,out _scroll);
            _scroll.viewport.GetComponent<Image>().color=Color.clear;
            foreach(var item in CosmeticCatalog.Items)
            {
                var captured=item;
                var rect=CosmeticUi.Rect(item.Id,_content,0,1f/3,0,260);
                _cards.Add(new CosmeticItemCard(rect,item,()=>Select(captured.Id)));
            }
            Menu.Skins.Changed+=OnInventoryChanged;_listening=true;
        }
        void Filter(bool owned) { _onlyOwned=owned;ChooseSlot(_slot); }
        void LayoutCards()
        {
            int index=0;
            foreach(var card in _cards)
            {
                bool owned=Menu.Skins.IsUnlocked(card.Item.Id);
                bool visible=card.Item.Slot==_slot && (!card.Item.Legacy || owned) && (!_onlyOwned || owned);
                card.Rect.gameObject.SetActive(visible);if(!visible)continue;
                RuntimeUi.Stretch(card.Rect,new Vector2(index%3/3f,1),new Vector2((index%3+1)/3f,1),
                    new Vector2(10,0),new Vector2(-10,0));
                // Integer row indexing keeps the final, partially-filled row aligned with the others.
                int row=index/3;
                card.Rect.offsetMin=new Vector2(10,-row*280-264);card.Rect.offsetMax=new Vector2(-10,-row*280);
                index++;
            }
            _content.sizeDelta=new Vector2(0,Mathf.Ceil(index/3f)*280+10);
            _count.text=index==0 ? "NO OWNED ITEMS YET" : index+(index==1 ? " ITEM":" ITEMS");
            _scroll.StopMovement();_scroll.verticalNormalizedPosition=1;
        }
        void ChooseSlot(CosmeticSlot slot,bool immediate=false)
        {
            _slot=slot;LayoutCards();
            string equipped=Menu.Skins.Effective.Resolved(slot);
            if(!string.IsNullOrEmpty(equipped))Select(equipped,immediate);
            else
            {
                _selected=null;
                foreach(var card in _cards)if(card.Rect.gameObject.activeSelf){Select(card.Item.Id,immediate);break;}
                if(_selected==null){_preview.Show(Menu.Skins.Effective);_preview.Focus(slot,immediate);}
            }
            Refresh();
        }
        public void SelectItem(string id)
        {
            var item=CosmeticCatalog.Find(id);if(item==null)return;
            _onlyOwned=false;ChooseSlot(item.Slot);Select(id);
        }
        void Select(string id,bool immediate=false)
        {
            _selected=id;var item=CosmeticCatalog.Find(id);
            var preview=CosmeticLoadout.Read(Menu.Skins.Effective.Serialize());preview.Equip(item);
            _preview.Show(preview);_preview.Focus(item.Slot,immediate);Refresh();
        }
        void Activate()
        {
            var selected=CosmeticCatalog.Find(_selected);if(selected==null)return;
            if(Menu.Skins.IsUnlocked(_selected))
            { if(Menu.Skins.Equip(_selected)){GameAudio.Confirm();_preview.Show(Menu.Skins.Effective);Refresh();} }
            else if(selected.Rule.Kind==UnlockKind.Entitlement)Menu.Show(MenuTab.Shop);
            else Menu.ShowSeasonReward(_selected);
        }
        void ClearSlot()
        {
            Menu.Skins.Clear(_slot);_selected=null;_preview.Show(Menu.Skins.Effective);_preview.Focus(_slot);Refresh();
        }
        void OnInventoryChanged() { LayoutCards();Refresh(); }
        void Refresh()
        {
            if(_name==null)return;
            foreach(var card in _cards)
            {
                bool owned=Menu.Skins.IsUnlocked(card.Item.Id),equipped=Menu.Skins.IsEquipped(card.Item.Id);
                string state=equipped ? "EQUIPPED" : owned ? "OWNED" : card.Item.Rule.Kind==UnlockKind.Entitlement ? "IN SHOP" :
                    (card.Item.Rule.Track==SeasonTrack.Pass ? "PASS":"FREE")+" · LV "+card.Item.Rule.Level;
                card.Refresh(card.Item.Id==_selected,state,false,equipped);
            }
            for(int i=0;i<_categories.Count;i++)_categories[i].image.color=i==(int)_slot ? MenuTheme.Owned:MenuTheme.Card;
            _all.image.color=_onlyOwned ? MenuTheme.Slot:MenuTheme.Owned;_owned.image.color=_onlyOwned ? MenuTheme.Owned:MenuTheme.Slot;
            _clear.interactable=true;
            var selected=CosmeticCatalog.Find(_selected);
            if(selected==null)
            {
                _name.text="YOUR LOOK";_description.text="Choose an item to preview";
                _equipLabel.text="SELECT ITEM";_equip.interactable=false;return;
            }
            bool unlocked=Menu.Skins.IsUnlocked(_selected),wearing=Menu.Skins.IsEquipped(_selected);
            _name.text=selected.DisplayName.ToUpperInvariant();
            _description.text=CosmeticUi.SlotName(_slot)+" · "+(wearing ? "YOUR CURRENT LOOK" : unlocked ? "PREVIEW" : "LOCKED PREVIEW");
            // A character's built-in accessory is worn without being claimed, so "wearing" alone
            // must not close the claim route - an unowned item keeps its shop/reward button.
            _equip.interactable=!wearing || !unlocked;
            _equipLabel.text=wearing && unlocked ? "EQUIPPED" : unlocked ? "EQUIP" :
                selected.Rule.Kind==UnlockKind.Entitlement ? "VIEW SHOP" : "VIEW REWARD";
            _equip.image.color=unlocked ? MenuTheme.Accent:MenuTheme.Text;
        }
        public override void OnShown()
        {
            ChooseSlot(_slot,true);
        }
        void OnDestroy() { if(_listening)Menu.Skins.Changed-=OnInventoryChanged; }
    }
}
