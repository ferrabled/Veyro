using System.Collections.Generic;
using MotionRunner.Audio;
using MotionRunner.Commerce;
using MotionRunner.Core;
using MotionRunner.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// A shared horizontal timeline: one tier column, premium above free, one large selected preview.
    public sealed class SeasonPassPage : MenuPage
    {
        const float TierWidth=236;
        readonly List<CosmeticItemCard> _cards=new List<CosmeticItemCard>();
        readonly List<Text> _levels=new List<Text>();
        Text _xp,_next,_name,_description,_actionLabel,_passLabel;
        RectTransform _fill,_railFill,_content;
        RunnerPreview _preview;
        ScrollRect _scroll;
        Button _action;
        string _selected;
        bool _listening;
        protected override void Build()
        {
            var background=CosmeticUi.Backdrop(Root,MenuTheme.PreviewTop);
            background.Gradient=true;background.Bottom=MenuTheme.PreviewBottom;
            _preview=RunnerPreview.Create(CosmeticUi.Area("Preview",Root,new Vector2(0,0.43f),new Vector2(1,0.855f)));
            CosmeticUi.Pill(CosmeticUi.Rect("Back",Root,0.025f,0.21f,44,70),"< BACK",Menu.GoHome,MenuTheme.ItemCard,26,Sfx.UiBack);
            CosmeticUi.Text("Title",CosmeticUi.Rect("Heading",Root,0.23f,0.98f,44,70),"SEASON 1  /  WORLD RUNNER",33,MenuTheme.Text);
            _xp=CosmeticUi.Text("Xp",CosmeticUi.Rect("XpSlot",Root,0.035f,0.98f,132,56),"",40,MenuTheme.Text);
            _next=CosmeticUi.Text("Next",CosmeticUi.Rect("NextSlot",Root,0.035f,0.98f,191,40),"",25,MenuTheme.Dim);
            _fill=RuntimeUi.Bar("Progress",CosmeticUi.Rect("ProgressSlot",Root,0.045f,0.955f,247,12),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,MenuTheme.Empty,MenuTheme.Accent);
            var hint=CosmeticUi.Rect("RotateHintSlot",Root,0.63f,0.975f,285,44);
            CosmeticUi.Surface(hint,MenuTheme.ItemCard,18).raycastTarget=false;
            CosmeticUi.Text("RotateHint",hint,"DRAG TO ROTATE",20,MenuTheme.Dim,TextAnchor.MiddleCenter);
            var selected=CosmeticUi.Area("SelectedReward",Root,new Vector2(0.02f,0.385f),new Vector2(0.98f,0.45f));
            _name=CosmeticUi.Text("Name",CosmeticUi.Area("NameSlot",selected,new Vector2(0,0.48f),new Vector2(0.62f,1)),"",37,MenuTheme.Text);
            _description=CosmeticUi.Text("Description",CosmeticUi.Area("DescriptionSlot",selected,Vector2.zero,new Vector2(0.62f,0.49f)),"",24,MenuTheme.Dim);
            _action=CosmeticUi.Pill(CosmeticUi.Area("RewardAction",selected,new Vector2(0.64f,0.17f),new Vector2(1,0.89f)),"",Activate,MenuTheme.Accent,27);
            _actionLabel=_action.GetComponentInChildren<Text>();_actionLabel.color=MenuTheme.OnAccent;
            var sheet=CosmeticUi.Area("RewardSheet",Root,Vector2.zero,new Vector2(1,0.38f));
            CosmeticUi.Surface(sheet,MenuTheme.Card,38);
            CosmeticUi.Text("Heading",CosmeticUi.Rect("HeadingSlot",sheet,0.03f,0.48f,22,56),"YOUR REWARDS",29,MenuTheme.Text);
            CosmeticUi.Pill(CosmeticUi.Rect("CurrentLevel",sheet,0.50f,0.84f,22,56),"MY LEVEL",JumpToProgress,size:24);
            CosmeticUi.Pill(CosmeticUi.Rect("Previous",sheet,0.84f,0.915f,22,56),"<",()=>Move(-1),size:27);
            CosmeticUi.Pill(CosmeticUi.Rect("Next",sheet,0.915f,0.995f,22,56),">",()=>Move(1),size:27);
            var trackArea=CosmeticUi.Area("TrackArea",sheet,new Vector2(0,0.13f),new Vector2(1,0.87f));
            var track=CosmeticUi.Area("Tracks",trackArea,Vector2.zero,Vector2.one);
            // A fixed design-height rail fits both short and tall portrait screens.
            var fitter=track.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=1.68f;
            var premium=CosmeticUi.Area("PremiumLabel",track,new Vector2(0,0.46f),new Vector2(0.10f,0.87f));
            CosmeticUi.Surface(premium,MenuTheme.Premium,12);
            var paidText=CosmeticUi.Text("Label",premium,"PASS",24,MenuTheme.Text,TextAnchor.MiddleCenter);
            paidText.rectTransform.localRotation=Quaternion.Euler(0,0,90);
            var free=CosmeticUi.Area("FreeLabel",track,new Vector2(0,0.035f),new Vector2(0.10f,0.445f));
            CosmeticUi.Surface(free,MenuTheme.Slot,12);
            var freeText=CosmeticUi.Text("Label",free,"FREE",24,MenuTheme.Text,TextAnchor.MiddleCenter);
            freeText.rectTransform.localRotation=Quaternion.Euler(0,0,90);
            var viewport=CosmeticUi.Area("HorizontalTimeline",track,new Vector2(0.115f,0),Vector2.one);
            viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            _scroll=viewport.gameObject.AddComponent<ScrollRect>();_scroll.viewport=viewport;_scroll.horizontal=true;_scroll.vertical=false;
            _scroll.movementType=ScrollRect.MovementType.Clamped;_scroll.scrollSensitivity=55;_scroll.decelerationRate=0.12f;
            _content=CosmeticUi.Area("Tiers",viewport,new Vector2(0,0),new Vector2(0,1));
            _content.pivot=new Vector2(0,0.5f);_content.sizeDelta=new Vector2(TierWidth*10,0);_scroll.content=_content;
            var rail=CosmeticUi.Area("Rail",_content,new Vector2(0,0.885f),new Vector2(1,0.898f));
            _railFill=RuntimeUi.Bar("Earned",rail,Vector2.zero,Vector2.one,new Vector2(TierWidth/2,0),new Vector2(-TierWidth/2,0),MenuTheme.Empty,MenuTheme.Accent);
            for(int level=1;level<=10;level++)
            {
                var column=CosmeticUi.Area("Tier"+level,_content,new Vector2((level-1)/10f,0),new Vector2(level/10f,1));
                _levels.Add(CosmeticUi.Text("Level",CosmeticUi.Area("LevelSlot",column,new Vector2(0,0.905f),Vector2.one),"",25,MenuTheme.Text,TextAnchor.MiddleCenter));
                AddReward(column,CosmeticCatalog.RewardAt(SeasonTrack.Pass,level),0.46f,0.87f);
                AddReward(column,CosmeticCatalog.RewardAt(SeasonTrack.Free,level),0.035f,0.445f);
            }
            var bar=CosmeticUi.Area("ScrollBar",sheet,new Vector2(0.14f,0.105f),new Vector2(0.97f,0.119f));
            CosmeticUi.Surface(bar,MenuTheme.Empty,8);
            var handle=CosmeticUi.Area("Handle",bar,Vector2.zero,Vector2.one);
            var thumb=CosmeticUi.Surface(handle,MenuTheme.Text,8);
            var scrollbar=bar.gameObject.AddComponent<Scrollbar>();scrollbar.direction=Scrollbar.Direction.LeftToRight;
            scrollbar.handleRect=handle;scrollbar.targetGraphic=thumb;_scroll.horizontalScrollbar=scrollbar;
            var pass=CosmeticUi.Pill(CosmeticUi.Area("PassOwnership",sheet,new Vector2(0.025f,0.025f),new Vector2(0.62f,0.088f)),"",()=>Menu.Show(MenuTab.Shop),MenuTheme.Premium,23);
            _passLabel=pass.GetComponentInChildren<Text>();
            CosmeticUi.Text("SwipeHint",CosmeticUi.Area("SwipeHintSlot",sheet,new Vector2(0.63f,0.025f),new Vector2(0.98f,0.088f)),"SWIPE TO EXPLORE  >",20,MenuTheme.Dim,TextAnchor.MiddleCenter);
            Menu.Season.Changed+=Refresh;_listening=true;
        }
        void AddReward(Transform column,CosmeticItem item,float bottom,float top)
        {
            var rect=CosmeticUi.Area(item.Id,column,new Vector2(0,bottom),new Vector2(1,top),new Vector2(7,0));
            _cards.Add(new CosmeticItemCard(rect,item,()=>Select(item.Id)));
        }
        void Select(string id,bool immediate=false)
        {
            _selected=id;var item=CosmeticCatalog.Find(id);
            var look=CosmeticLoadout.Read(Menu.Skins.Effective.Serialize());look.Equip(item);
            _preview.Show(look);_preview.Focus(item.Slot,immediate);Refresh();
        }
        void Activate()
        {
            var item=CosmeticCatalog.Find(_selected);if(item==null)return;
            if(Menu.Season.CanCollect(item.Id))
            { if(Menu.Season.Collect(item.Id))GameAudio.Confirm();else GameAudio.Deny();if(item.Slot==CosmeticSlot.CrashFx)_preview.Burst();Refresh(); }
            else if(Menu.Season.IsOwned(item.Id))Menu.ShowCosmetic(item.Id);
            else if(item.Rule.Track==SeasonTrack.Pass && !Menu.IsPassOwned())Menu.Show(MenuTab.Shop);
        }
        public void Refresh()
        {
            if(_xp==null)return;
            var season=Menu.Season;var state=season.Read(Menu.IsPassOwned());
            _xp.text="LEVEL "+state.Level+" / 10     "+season.Inventory.Xp+" XP";
            _next.text=state.IsMaxLevel ? "All levels reached · collect your rewards" :
                (season.Curve.Threshold(state.Level+1)-season.Inventory.Xp)+" XP to level "+(state.Level+1);
            if(!season.HasRecordedXp)_next.text=SeasonProgress.NotLiveNote;
            if(season.Curve.Id.StartsWith("test"))_next.text+="  ·  TEST: 1 XP / LEVEL";
            RuntimeUi.SetBarFill(_fill,state.Fraction);
            RuntimeUi.SetBarFill(_railFill,Mathf.Clamp01((state.Level-1+(state.IsMaxLevel ? 0:state.Fraction))/9f));
            _passLabel.text=Menu.IsPassOwned() ? "PREMIUM PASS  ·  OWNED" : "UNLOCK PREMIUM  >";
            for(int i=0;i<_levels.Count;i++)
            {
                bool current=i+1==state.Level;
                _levels[i].text=(current ? "YOU · ":"LV ")+(i+1)+"  /  "+season.Curve.Threshold(i+1)+" XP";
                _levels[i].color=current ? MenuTheme.Accent:MenuTheme.Dim;
                _levels[i].fontSize=22;
            }
            foreach(var card in _cards)
            {
                bool owned=season.IsOwned(card.Item.Id),ready=season.CanCollect(card.Item.Id);
                card.Refresh(card.Item.Id==_selected,owned ? "COLLECTED" : ready ? "COLLECT" : card.Item.Rule.Level>state.Level ? "LOCKED" : "PASS REQUIRED",ready);
            }
            var selected=CosmeticCatalog.Find(_selected);if(selected==null)return;
            _name.text=selected.DisplayName.ToUpperInvariant();
            _description.text=CosmeticUi.SlotName(selected.Slot)+" · "+(selected.Rule.Track==SeasonTrack.Pass ? "PREMIUM":"FREE")+" · LEVEL "+selected.Rule.Level;
            bool collected=season.IsOwned(_selected),canCollect=season.CanCollect(_selected);
            bool needsPass=selected.Rule.Track==SeasonTrack.Pass && !Menu.IsPassOwned();
            _actionLabel.text=collected ? "IN LOCKER  >" : canCollect ? "COLLECT" : needsPass ? "GET PASS" : season.Curve.Threshold(selected.Rule.Level)+" XP NEEDED";
            _action.interactable=collected || canCollect || needsPass;
            _action.image.color=canCollect ? MenuTheme.Accent:MenuTheme.Text;
        }
        void Move(int direction)
        {
            _scroll.StopMovement();float width=Mathf.Max(1,_content.rect.width-_scroll.viewport.rect.width);
            _scroll.horizontalNormalizedPosition=Mathf.Clamp01(_scroll.horizontalNormalizedPosition+direction*TierWidth*3/width);
        }
        void CenterLevel(int level)
        {
            Canvas.ForceUpdateCanvases();_scroll.StopMovement();
            float width=_scroll.viewport.rect.width;
            _scroll.horizontalNormalizedPosition=Mathf.Clamp01(((level-0.5f)*TierWidth-width/2)/Mathf.Max(1,_content.rect.width-width));
        }
        void JumpToProgress() { Select(CosmeticCatalog.RewardAt(SeasonTrack.Free,Menu.Season.Inventory.Level).Id);CenterLevel(Menu.Season.Inventory.Level); }
        public void SelectReward(string id)
        {
            var item=CosmeticCatalog.Find(id);if(item==null || item.Rule.Kind!=UnlockKind.SeasonLevel)return;
            Select(id);CenterLevel(item.Rule.Level);
        }
        public override void OnShown()
        {
            Select(CosmeticCatalog.RewardAt(SeasonTrack.Free,Menu.Season.Inventory.Level).Id,true);
            CenterLevel(Menu.Season.Inventory.Level);
        }
        void OnDestroy() { if(_listening)Menu.Season.Changed-=Refresh; }
    }
}
