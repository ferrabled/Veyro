using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MotionRunner.Commerce;
using MotionRunner.Social;
using MotionRunner.Track;
using NUnit.Framework;
using UnityEngine;

namespace MotionRunner.Tests
{
    public sealed class SeasonIntegrationTests
    {
        const string Last="veyro.season.last-profile";
        readonly string _user="season-tests-"+Guid.NewGuid().ToString("N");
        string _savedLast;
        bool _hadLast;
        FakeStore _store;
        FakeProfileService _profile;
        object _season,_skins;
        Type _seasonType,_skinType;
        static Type Runtime(string name) => Type.GetType("MotionRunner."+name+", Assembly-CSharp",true);
        [SetUp] public void Setup()
        {
            _hadLast=PlayerPrefs.HasKey(Last);_savedLast=PlayerPrefs.GetString(Last,"");PlayerPrefs.SetString(Last,_user);
            _store=FakeStore.WithDefaultCatalog();_store.IsReady=true;
            _profile=new FakeProfileService();_profile.BecomeReady(new Profile(_user,"TEST",0));
            _seasonType=Runtime("Core.SeasonService");_skinType=Runtime("Core.SkinService");
            _season=Activator.CreateInstance(_seasonType,_store,_profile,SeasonCurve.Testing);
            _skins=Activator.CreateInstance(_skinType,_store,_season,(Action<CosmeticLoadout>)(_=>{}));
        }
        [TearDown] public void Teardown()
        {
            ((IDisposable)_skins).Dispose();((IDisposable)_season).Dispose();
            foreach(string id in new[]{_user,_user+"other"})
            {
                PlayerPrefs.DeleteKey("veyro.season.xp."+id);
                PlayerPrefs.DeleteKey("veyro.season.test-v1."+id+".claims");
                PlayerPrefs.DeleteKey("veyro.season.test-v1."+id+".loadout");
            }
            if(_hadLast)PlayerPrefs.SetString(Last,_savedLast);else PlayerPrefs.DeleteKey(Last);
        }
        object Season(string method,params object[] args)=>_seasonType.GetMethod(method).Invoke(_season,args);
        object Skin(string method,params object[] args)=>_skinType.GetMethod(method).Invoke(_skins,args);
        [Test] public void ClaimEquipPersistRevokeRestoreAndProfileSwitchUseActualServices()
        {
            _store.SetEntitlement(Entitlements.Season1,true);
            Assert.IsFalse((bool)Season("Collect","crown"));
            _profile.BecomeReady(new Profile(_user,"TEST",6));
            Assert.IsTrue((bool)Season("Collect","crown"));Assert.IsTrue((bool)Skin("Equip","crown"));
            Skin("Apply");Assert.IsTrue((bool)Skin("IsEquipped","crown"));
            _store.SetEntitlement(Entitlements.Season1,false);Assert.IsFalse((bool)Skin("IsEquipped","crown"));
            _store.SetEntitlement(Entitlements.Season1,true);Assert.IsTrue((bool)Skin("IsEquipped","crown"));
            _profile.BecomeReady(new Profile(_user+"other","OTHER",6));
            Assert.IsFalse((bool)Skin("IsEquipped","crown"));Assert.IsFalse((bool)Season("IsOwned","crown"));
        }
        [Test] public void DeletionClearsCachedSeasonIdentity()
        {
            _profile.BecomeReady(new Profile(_user,"TEST",9));Season("Collect","cap");Skin("Equip","cap");
            _profile.DeleteAccount(null);
            Assert.AreEqual("",PlayerPrefs.GetString(Last));Assert.IsFalse((bool)Season("IsOwned","cap"));
            Assert.IsFalse(PlayerPrefs.HasKey("veyro.season.test-v1."+_user+".claims"));
        }
        [Test] public void DetailScreensDoNotAddTabsAndShopDoesNotSellRewardRows()
        {
            var menuType=Runtime("Menu.MainMenu");var tabType=Runtime("Menu.MenuTab");
            var menu=(Component)menuType.GetMethod("Create").Invoke(null,new[]{(object)_store,_skins,_profile,Enum.ToObject(tabType,1)});
            try
            {
                Assert.AreEqual(3,Enum.GetValues(tabType).Length);
                Assert.AreEqual(6,menu.GetComponentsInChildren<Canvas>(true).Length,
                    "Shell plus five independently batched pages must retain one shared navigation shell.");
                menuType.GetMethod("ShowSeason").Invoke(menu,null);
                Assert.IsTrue((bool)menuType.GetProperty("IsAwayFromHome").GetValue(menu));
                menuType.GetMethod("ShowCosmetics").Invoke(menu,null);
                menuType.GetMethod("GoHome").Invoke(menu,null);
                Assert.IsFalse((bool)menuType.GetProperty("IsAwayFromHome").GetValue(menu));
                var catalog=menuType.GetProperty("Catalog").GetValue(menu);
                var rows=(System.Collections.ICollection)catalog.GetType().GetField("_rows",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(catalog);
                Assert.AreEqual(2,rows.Count,"The shop presents only Ember and Frost below the season pass.");
            }
            finally { UnityEngine.Object.DestroyImmediate(menu.gameObject); }
        }
        [Test] public void CrashRevealKeepsInvisibleResultButtonsInactiveAndResetsForNextRun()
        {
            var type=Runtime("Gameplay.RunHud");
            var hud=(Component)type.GetMethod("Create").Invoke(null,null);
            try
            {
                type.GetMethod("ShowResult").Invoke(hud,new object[]{new RunSummary(),true});
                var group=hud.GetComponentInChildren<CanvasGroup>();
                Assert.AreEqual(0,group.alpha);Assert.IsFalse(group.interactable);
                Assert.IsTrue(group.blocksRaycasts,"An invisible result must not pass taps through to the run.");
                type.GetField("_resultRevealAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(hud,Time.unscaledTime-1);
                type.GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
                Assert.AreEqual(1,group.alpha);Assert.IsTrue(group.interactable);
                type.GetMethod("HideResult").Invoke(hud,null);
                type.GetMethod("ShowResult").Invoke(hud,new object[]{new RunSummary(),false});
                Assert.AreEqual(1,group.alpha);Assert.IsTrue(group.interactable);
            }
            finally { UnityEngine.Object.DestroyImmediate(hud.gameObject); }
        }
        [Test] public void HorizontalRewardSelectionPreviewsWithoutGrantingAndCollectionStillChecksXp()
        {
            var menuType=Runtime("Menu.MainMenu");var tabType=Runtime("Menu.MenuTab");
            var menu=(Component)menuType.GetMethod("Create").Invoke(null,new[]{(object)_store,_skins,_profile,Enum.ToObject(tabType,1)});
            try
            {
                menuType.GetMethod("ShowSeasonReward").Invoke(menu,new object[]{"cap"});
                var page=menu.GetComponentInChildren(Runtime("Menu.SeasonPassPage"));
                var scrollType=Type.GetType("UnityEngine.UI.ScrollRect, UnityEngine.UI",true);
                var scroll=page.GetComponentInChildren(scrollType);
                Assert.IsTrue((bool)scrollType.GetProperty("horizontal").GetValue(scroll));
                Assert.IsFalse((bool)scrollType.GetProperty("vertical").GetValue(scroll));
                Assert.IsFalse((bool)Season("IsOwned","cap"));Assert.IsFalse((bool)Skin("IsEquipped","cap"));
                var action=NamedButton(page,"RewardAction");
                Assert.IsFalse((bool)action.GetType().GetProperty("interactable").GetValue(action));
                _profile.BecomeReady(new Profile(_user,"TEST",4));
                Assert.IsTrue((bool)action.GetType().GetProperty("interactable").GetValue(action));
                Tap(action);
                Assert.IsTrue((bool)Season("IsOwned","cap"));Assert.IsFalse((bool)Skin("IsEquipped","cap"),"Collect must not silently equip.");
                Tap(action); // Opens the exact collected item in its locker category.
                var locker=menu.GetComponentInChildren(Runtime("Menu.CosmeticsPage"));
                Assert.IsNotNull(locker);Tap(NamedButton(locker,"Equip"));
                Assert.IsTrue((bool)Skin("IsEquipped","cap"));
                menuType.GetMethod("ShowCosmetic").Invoke(menu,new object[]{"crown"});
                Assert.IsFalse((bool)Season("IsOwned","crown"));Assert.IsTrue((bool)Skin("IsEquipped","cap"),"Locked preview must leave the outfit unchanged.");
                Assert.IsFalse(menu.transform.Find("Header").gameObject.activeSelf);
                menuType.GetMethod("GoHome").Invoke(menu,null);
                Assert.IsTrue(menu.transform.Find("Header").gameObject.activeSelf);
            }
            finally { UnityEngine.Object.DestroyImmediate(menu.gameObject); }
        }
        static Component NamedButton(Component page,string name)
        {
            var target=page.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
            return target.GetComponent(Type.GetType("UnityEngine.UI.Button, UnityEngine.UI",true));
        }
        static void Tap(Component button)
        {
            var click=button.GetType().GetProperty("onClick").GetValue(button);
            click.GetType().GetMethod("Invoke").Invoke(click,null);
        }
        [TestCase("runner")][TestCase("ember")][TestCase("frost")] public void HatFocusMagnifiesAccessoriesWithoutCroppingAndHiddenPreviewStopsRendering(string character)
        {
            var host=new GameObject("PreviewTest",typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta=new Vector2(800,1000);
            var type=Runtime("Menu.RunnerPreview");
            var preview=(Component)type.GetMethod("Create").Invoke(null,new object[]{host.transform});
            var camera=(Camera)type.GetProperty("PreviewCamera").GetValue(preview);
            var model=(Transform)type.GetProperty("Model").GetValue(preview);
            var animator=model.GetComponentInChildren<Animator>();animator.Play("idle",0,0);animator.Update(0);
            try
            {
                foreach(string id in new[]{"cap","tophat","crown"})
                {
                    var loadout=new CosmeticLoadout();loadout.Equip(CosmeticCatalog.Find(character));loadout.Equip(CosmeticCatalog.Find(id));
                    type.GetMethod("Show").Invoke(preview,new object[]{loadout});
                    type.GetMethod("Focus").Invoke(preview,new object[]{CosmeticSlot.Skin,true});
                    float fullSize=camera.orthographicSize;
                    type.GetMethod("Focus").Invoke(preview,new object[]{CosmeticSlot.Headwear,true});
                    Assert.Less(camera.orthographicSize,fullSize);
                    var hat=model.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.StartsWith("Cosmetic "));
                    var vertices=hat.GetComponent<MeshFilter>().sharedMesh.vertices;
                    foreach(float yaw in new[]{0f,45f,90f,135f,180f,225f,270f,315f})
                    {
                        model.localRotation=Quaternion.Euler(0,yaw,0);
                        // Project the visible geometry. A rotated renderer AABB contains empty
                        // corners beyond the circular brim and is not a crop of the item itself.
                        var min=Vector2.one;var max=Vector2.zero;
                        foreach(var vertex in vertices)
                        {
                            var screen=(Vector2)camera.WorldToViewportPoint(hat.transform.TransformPoint(vertex));
                            min=Vector2.Min(min,screen);max=Vector2.Max(max,screen);
                        }
                        Assert.That(min.x,Is.GreaterThanOrEqualTo(0),id+" left crop at "+yaw);
                        Assert.That(max.x,Is.LessThanOrEqualTo(1),id+" right crop at "+yaw);
                        Assert.That(min.y,Is.GreaterThanOrEqualTo(0),id+" bottom crop at "+yaw);
                        Assert.That(max.y,Is.LessThanOrEqualTo(1),id+" top crop at "+yaw);
                    }
                }
                // EditMode does not run player-loop callbacks for this runtime MonoBehaviour.
                host.SetActive(false);type.GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(preview,null);
                Assert.IsFalse(camera.gameObject.activeInHierarchy);
                host.SetActive(true);type.GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(preview,null);
                Assert.IsTrue(camera.gameObject.activeInHierarchy);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
        [TestCase("ember")][TestCase("frost")]
        public void PremiumCharactersAnimateAndWearEveryAttachment(string id)
        {
            var root=new GameObject("Character compatibility");root.transform.position=Vector3.up*0.5f;
            var visualType=Runtime("Art.RunnerVisual");var visual=visualType.GetMethod("Create").Invoke(null,new object[]{root.transform});
            var defaultMesh=root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            try
            {
                var look=new CosmeticLoadout();look.Equip(CosmeticCatalog.Find(id));
                foreach(string item in new[]{"cap","wings","shield","quiver","spellbook","mint","white","firefly","confetti"})
                {
                    look.Equip(CosmeticCatalog.Find(item));visualType.GetMethod("ApplyLoadout").Invoke(visual,new object[]{look});
                    var animator=root.GetComponentInChildren<Animator>();Assert.IsTrue(animator.avatar.isHuman && animator.avatar.isValid);
                    Assert.AreNotSame(defaultMesh,root.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh,id+" must use distinct geometry");
                    Assert.IsFalse(animator.applyRootMotion);Assert.IsEmpty(root.GetComponentsInChildren<Collider>());
                    var rig=root.GetComponentInChildren(Runtime("Art.CharacterRig"));
                    var hats=(Renderer[])rig.GetType().GetField("StockHeadwear").GetValue(rig);
                    foreach(var hat in hats)Assert.IsFalse(hat.enabled,"Pass headwear replaces native headwear");
                    foreach(string state in new[]{"run","jump","dodgeLeft","dodgeRight"})
                    {
                        animator.Play(state,0,0);animator.Update(0);
                        var thigh=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var before=thigh.localRotation;
                        animator.Update(0.12f);Assert.Greater(Quaternion.Angle(before,thigh.localRotation),0.1f,id+" "+state);
                    }
                    Assert.AreEqual(Vector3.up*0.5f,root.transform.position);
                    foreach(var r in root.GetComponentsInChildren<Renderer>())Assert.IsTrue(r.sharedMaterial.shader.isSupported);
                }
                look.Clear(CosmeticSlot.Headwear);look.Clear(CosmeticSlot.Back);
                visualType.GetMethod("ApplyLoadout").Invoke(visual,new object[]{look});
                var restored=root.GetComponentInChildren(Runtime("Art.CharacterRig"));
                foreach(var hat in (Renderer[])restored.GetType().GetField("StockHeadwear").GetValue(restored))Assert.IsTrue(hat.enabled);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void ShopPutsPassFirstAndDirectPurchasesKeepCollectedAccessories()
        {
            _profile.BecomeReady(new Profile(_user,"TEST",4));Season("Collect","cap");Skin("Equip","cap");
            var menuType=Runtime("Menu.MainMenu");var tabType=Runtime("Menu.MenuTab");
            var menu=(Component)menuType.GetMethod("Create").Invoke(null,new[]{(object)_store,_skins,_profile,Enum.ToObject(tabType,0)});
            try
            {
                var shop=menu.GetComponentInChildren(Runtime("Menu.ShopPage"));
                var transforms=shop.GetComponentsInChildren<RectTransform>();
                Assert.Greater(transforms.Single(t=>t.name=="SeasonPassCard").anchoredPosition.y,transforms.Single(t=>t.name=="emberCard").anchoredPosition.y);
                _store.NextPurchaseOutcome=PurchaseOutcome.Cancelled();Tap(NamedButton(shop,"Buyember"));
                Assert.IsFalse((bool)Skin("IsUnlocked","ember"));Assert.IsTrue((bool)Skin("IsEquipped","cap"));
                Tap(NamedButton(shop,"Buyember"));Assert.IsTrue((bool)Skin("IsEquipped","ember"));Assert.IsTrue((bool)Skin("IsEquipped","cap"));
                Tap(NamedButton(shop,"BuyPass"));Assert.IsFalse((bool)Season("IsOwned","wings"),"Cancelling a paywall grants nothing");
                Tap(NamedButton(shop,"ViewRewards"));Assert.IsNotNull(menu.GetComponentInChildren(Runtime("Menu.SeasonPassPage")));
            }
            finally{UnityEngine.Object.DestroyImmediate(menu.gameObject);}
        }
        [Test] public void ShopRecoversFromInitialUnavailableStateAndRefreshesLocalizedPrices()
        {
            _store.IsReady=false;
            var menuType=Runtime("Menu.MainMenu");var tabType=Runtime("Menu.MenuTab");
            var menu=(Component)menuType.GetMethod("Create").Invoke(null,new[]{(object)_store,_skins,_profile,Enum.ToObject(tabType,0)});
            try
            {
                var shop=menu.GetComponentInChildren(Runtime("Menu.ShopPage"));
                var textType=Type.GetType("UnityEngine.UI.Text, UnityEngine.UI",true);
                var status=shop.GetComponentsInChildren(textType,true).Single(t=>t.name=="Status");
                string Read(Component text)=>(string)textType.GetProperty("text").GetValue(text);
                StringAssert.Contains("unavailable",Read(status));
                _store.IsReady=true;_store.RaiseEntitlementsChanged();
                Assert.IsEmpty(Read(status));
                var price=NamedButton(shop,"Buyfrost").GetComponentInChildren(textType);
                Assert.AreEqual("€2.99",Read(price));
                _store.NextPurchaseOutcome=PurchaseOutcome.Failed(new StoreError("test","declined"));Tap(NamedButton(shop,"Buyfrost"));
                _store.RaiseEntitlementsChanged();StringAssert.Contains("declined",Read(status),"Readiness refresh must retain actionable purchase errors.");
            }
            finally{UnityEngine.Object.DestroyImmediate(menu.gameObject);}
        }
        [Test] public void EveryCatalogItemHasAnActualThumbnail()
        {
            foreach(var item in CosmeticCatalog.Items)
                Assert.IsNotNull(Resources.Load<Sprite>("Art/CosmeticThumbnails/"+item.Id),item.Id);
        }
        [Test] public void AllRewardsRenderWithNoGameplayCollidersAndBoundedEffects()
        {
            var root=new GameObject("CosmeticTest");root.transform.position=Vector3.up*0.5f;
            var visualType=Runtime("Art.RunnerVisual");
            var visual=visualType.GetMethod("Create").Invoke(null,new object[]{root.transform});
            var animator=root.GetComponentInChildren<Animator>();animator.Play("idle",0,0);animator.Update(0);
            try
            {
                foreach(var item in CosmeticCatalog.Items)
                {
                    var loadout=new CosmeticLoadout();loadout.Equip(item);
                    visualType.GetMethod("ApplyLoadout").Invoke(visual,new object[]{loadout});
                    Assert.IsEmpty(root.GetComponentsInChildren<Collider>(),item.Id);
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                        Assert.IsTrue(renderer.sharedMaterial!=null && renderer.sharedMaterial.shader.isSupported,item.Id+" material");
                    foreach(var particle in root.GetComponentsInChildren<ParticleSystem>())Assert.LessOrEqual(particle.main.maxParticles,36);
                    if(item.Slot==CosmeticSlot.Headwear)
                    {
                        var hat=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.StartsWith("Cosmetic "));
                        Assert.Greater(hat.bounds.center.y,1.1f,item.Id+" should sit on the head");
                        Assert.Less(hat.bounds.max.y,2.2f,item.Id+" must fit the character");
                    }
                    root.SetActive(false);root.SetActive(true);
                    visualType.GetMethod("ResetPose").Invoke(visual,null);
                    foreach(var particle in root.GetComponentsInChildren<ParticleSystem>())
                        if(particle.name=="Cosmetic aura")Assert.IsTrue(particle.isPlaying,item.Id+" aura must resume after returning from the menu.");
                    Assert.AreEqual(Vector3.up*0.5f,root.transform.position);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
