using System;
using MotionRunner.Audio;
using MotionRunner.Commerce;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    internal static class CosmeticUi
    {
        public static RectTransform Rect(string name,Transform parent,float left,float right,float top,float height)
        {
            RuntimeUi.Element(name,parent,out var rect);
            RuntimeUi.Stretch(rect,new Vector2(left,1),new Vector2(right,1),new Vector2(12,-top-height),new Vector2(-12,-top));
            return rect;
        }
        public static Text Text(string name,Transform parent,string text,int size,Color color,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var label=RuntimeUi.Label(name,parent,Vector2.zero,Vector2.one,new Vector2(12,4),new Vector2(-12,-4),size,align,color);
            label.horizontalOverflow=HorizontalWrapMode.Wrap;
            label.text=text;return label;
        }
        public static Button Button(Transform rect,string text,Action tapped,Color? background=null,int size=30,Sfx sound=Sfx.UiTap)
        {
            var image=rect.gameObject.AddComponent<Image>();image.color=background??MenuTheme.Slot;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>tapped());
            RuntimeUi.TapSound(button,sound);
            Text("Label",rect,text,size,MenuTheme.Text,TextAnchor.MiddleCenter);return button;
        }
        public static RectTransform Scroll(Transform parent,float top,out ScrollRect scroll)
        {
            RuntimeUi.Element("Scroll",parent,out var viewport);
            RuntimeUi.Stretch(viewport,Vector2.zero,Vector2.one,new Vector2(12,12),new Vector2(-12,-top));
            viewport.gameObject.AddComponent<Image>().color=MenuTheme.Card;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.horizontal=false;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
            RuntimeUi.Element("Content",viewport,out var content);
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(0.5f,1);
            content.sizeDelta=Vector2.zero;scroll.content=content;return content;
        }
        public static string SlotName(CosmeticSlot slot) => slot==CosmeticSlot.Back ? "BACK ACCESSORY" : slot==CosmeticSlot.Skin ? "CHARACTER" : slot==CosmeticSlot.CrashFx ? "CRASH FX" : slot.ToString().ToUpperInvariant();
        public static RectTransform Area(string name,Transform parent,Vector2 min,Vector2 max,Vector2? inset=null)
        {
            RuntimeUi.Element(name,parent,out var rect);
            var pad=inset??Vector2.zero;
            RuntimeUi.Stretch(rect,min,max,pad,-pad);return rect;
        }
        public static RectTransform Fixed(string name,Transform parent,float x,float y,float width,float height)
        {
            var rect=Area(name,parent,new Vector2(0,1),new Vector2(0,1));
            rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);
            return rect;
        }
        public static CosmeticPanel Surface(Transform parent,Color color,float radius=24)
        {
            var panel=parent.gameObject.AddComponent<CosmeticPanel>();panel.color=color;panel.Radius=radius;return panel;
        }
        public static Button Pill(Transform parent,string text,Action tap,Color? color=null,int size=27,Sfx sound=Sfx.UiTap)
        {
            var surface=Surface(parent,color??MenuTheme.Slot);
            var button=parent.gameObject.AddComponent<Button>();button.targetGraphic=surface;
            button.onClick.AddListener(()=>tap());RuntimeUi.TapSound(button,sound);
            Text("Label",parent,text,size,MenuTheme.Text,TextAnchor.MiddleCenter);
            return button;
        }
        public static void Thumbnail(Transform parent,string id)
        {
            var rect=Area("Thumbnail",parent,Vector2.zero,Vector2.one);
            var image=rect.gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>("Art/CosmeticThumbnails/"+id);
            image.preserveAspect=true;image.raycastTarget=false;
            if(image.sprite==null)image.color=Color.clear;
        }
        public static CosmeticLoadout Preview(string id)
        {
            var loadout=new CosmeticLoadout();var item=CosmeticCatalog.Find(id);
            if(item!=null)loadout.Equip(item);return loadout;
        }
    }
}
