using System;
using MotionRunner.Commerce;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// Shared thumbnail tile for the reward tracks and the locker grid.
    internal sealed class CosmeticItemCard
    {
        public readonly CosmeticItem Item;
        public readonly RectTransform Rect;
        readonly CosmeticPanel _border,_surface;
        readonly Text _state;
        public CosmeticItemCard(RectTransform rect,CosmeticItem item,Action tapped)
        {
            Rect=rect;Item=item;
            _border=CosmeticUi.Surface(rect,MenuTheme.Empty,22);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=_border;
            button.onClick.AddListener(()=>tapped());
            _surface=CosmeticUi.Surface(CosmeticUi.Area("Face",rect,Vector2.zero,Vector2.one,Vector2.one*4),MenuTheme.ItemCard,18);
            _surface.raycastTarget=false;
            CosmeticUi.Thumbnail(CosmeticUi.Area("Art",rect,new Vector2(0.06f,0.32f),new Vector2(0.94f,0.97f)),item.Id);
            var title=CosmeticUi.Text("Name",CosmeticUi.Area("NameSlot",rect,new Vector2(0,0.15f),new Vector2(1,0.34f)),
                item.DisplayName,24,MenuTheme.Text,TextAnchor.MiddleCenter);
            title.resizeTextForBestFit=true;title.resizeTextMinSize=20;title.resizeTextMaxSize=24;
            _state=CosmeticUi.Text("State",CosmeticUi.Area("StateSlot",rect,Vector2.zero,new Vector2(1,0.16f)),"",19,MenuTheme.Dim,TextAnchor.MiddleCenter);
        }
        public void Refresh(bool selected,string state,bool ready=false,bool equipped=false)
        {
            _border.color=selected ? MenuTheme.Accent : equipped ? MenuTheme.Owned : MenuTheme.Empty;
            _surface.color=selected ? MenuTheme.Slot:MenuTheme.ItemCard;
            _state.text=state;_state.color=ready ? MenuTheme.Accent:MenuTheme.Dim;
        }
    }
}
