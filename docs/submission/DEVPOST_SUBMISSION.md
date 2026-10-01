# Devpost submission: what goes in each field (iOS entry)

Rewritten 30 Sep 2026 in the owner's voice, in the order the Devpost form asks for it.
Entering: **Best Game, RevenueCat Design, HAMM, OneSignal**, and **Layers only if the events show up
in its dashboard**. Not entering: Grand Prize, #BuildInPublic, everything else. Leave those fields
empty; an empty field just means that category isn't judged.

`[BRACKETS]` = check or fill before submitting, or delete the sentence.

---

## Project overview

**Elevator pitch**
```
An endless runner you play with your body. Prop your iPhone up, step back, then step and hop to dodge. Or just tilt it in your hands. Same track for everyone, every day.
```

## Project details (the story)

```markdown
## Inspiration

You've probably seen those YouTube videos where kids run along with Subway Surfers on the TV. They jump, duck and dodge in the living room as if they were the character. I love them, but something always bugged me: the kids are pretending. The video does exactly the same thing whatever they do.

So I wanted to make the version that actually watches you. The phone already has a camera pointing at you, so no extra hardware is needed. And for when you're on the sofa or on the bus, the same game also works by tilting the phone.

## What it does

Veyro Run is a three-lane endless runner with two ways to play.

In **Tilt & Touch** you tilt the phone to change lanes and tap to jump.

In **Camera mode** (labelled beta) you lean the phone against something, step back a couple of metres, and play with your body. A step to the side changes lane, and a hop jumps. After a crash you hop to run again. If you walk out of frame the run pauses until you hop twice. You never have to go back and touch the phone.

Every day there's a new Daily Run, and it's the same track for everyone in the world, generated from the date. There are daily and all-time leaderboards. Camera runs get their own board, because otherwise it wouldn't be fair. You can also send a friend a link to race your exact track.

There's a Season 1 pass with 10 levels of cosmetic rewards that you earn by playing, a daily streak, and two extra characters in the shop. None of it changes the run itself.

## How I built it

I built it on my own in Unity 6 and C#, in about six weeks. [OPTIONAL: "I used AI coding tools a lot along the way." Keep or delete.] A few decisions ended up mattering:

- All input goes through one small interface. Tilt, touch and camera are just different adapters behind it, so the game never knows which one you're using. That saved me when my first camera approach failed.
- The camera tracking is BlazeFace, a tiny face detector (418 KB) that runs on the phone through Unity's Inference Engine. Nothing is uploaded, and no server is involved in the tracking.
- The track is generated from a seed, and tests lock that seed down, so I can't accidentally change a Daily Run people have already played.
- The backend is small. Supabase holds profiles, the leaderboard and XP, and runs are checked on the server. Cloudflare hosts the website.
- RevenueCat handles purchases, OneSignal sends the daily reminder, and Layers handles the analytics, which players have to opt into.
- I don't own a Mac or an iPhone. I export the Xcode project from Windows and check it with a script, and a friend's Mac does the archive and upload.

## Challenges I ran into

The big one: my first plan for camera mode didn't work. I started with BlazePose, the full-body model with 33 points, because it looked like the obvious choice. On my test phone (a OnePlus Nord 2) it took 118 ms for the body points and 217 ms for the detector. That's about 3 updates a second, and I needed at least 30. You can't dodge anything like that.

I tried to make it faster and there was nothing left to tune. Then I realised I didn't need a skeleton. I just needed to know where the player is, and a face detector does that in 3.7 ms on the same phone. Your face moves left, the runner moves left. Your face goes up fast, the runner jumps. It's a worse model of a person and a much better controller.

Smaller ones:
- Unity added a camera permission to the app just because some code mentioned the webcam.
- The SDKs pulled in advertising-ID libraries that I had to strip out. On iOS I stripped the tracking prompt as well, because I didn't want one at all.
- Google makes a new developer account find 12 testers for 14 days before it can publish anything, so I ran a closed test from the first week.

## Accomplishments that I'm proud of

Playing it standing in the middle of the living room, with the phone on a table, at 60 fps on a mid-range phone. Going from an empty project to the App Store in six weeks, on my own. And the fact that the Daily Run is exactly the same track on every phone, without a server generating it.

## What I learned

Measure the obvious idea before you fall in love with it. The 33-point version looked more impressive, but the simple one is the one you can actually play.

I also learned that store review, privacy forms and SDK settings take as long as the game itself. They're worth starting on day one.

## What's next for Veyro Run

Getting back to the original picture: the phone mirrored to the TV, and kids running in front of it for real. Mirroring already works through AirPlay; I still need to measure the delay. After that comes sliding (a crouch in camera mode), a second world, Season 2, and an Android release.
```

**Built with**
```
unity, c#, unity-inference-engine, blazeface, revenuecat, storekit, onesignal, layers, supabase, cloudflare-workers, ios
```

**Try it out**
```
https://apps.apple.com/app/id6816748253
https://veyro.ferrabled.com/
```

---

## Additional info

| Field | Answer |
|---|---|
| Attached a 1024×1024 icon? | Yes: `builds/submission/icon-1024.png` |
| Attached a screenshot without device frames? | Yes: `builds/submission/screenshots/` (all exactly 1179×2556) |
| First version released 1 Aug – 30 Sep? | **Yes**. It's the app's first version |
| Employee of RevenueCat or a sponsor? | No |
| Type of app | **iOS** |
| iOS URL | `https://apps.apple.com/app/id6816748253` |
| Android / Galaxy URL, Next Gen fields | leave empty |
| RevenueCat project ID | `proj6cc5c025` |

### Promo code field (short)

Use **custom offer codes**: a code name you choose, which many people can redeem through a link.
Apple supports them for non-consumable purchases.

```
Free unlocks: open on the iPhone, then open the game (SHOP → Restore Purchases if needed).
Pass: https://apps.apple.com/redeem?ctx=offercodes&id=6816748253&code=SHIPATONPASS
Skins: same link with code=SHIPATONEMBER or code=SHIPATONFROST
```

**Creating the codes** (about 5 minutes, repeated for each of the three purchases):
1. App Store Connect → Veyro Run → **In-App Purchases** → the purchase → **Offer Codes → Create Offer**.
2. Reference name: `Shipaton judges`. Eligibility: **select all options** (everyone). Countries: all.
   Choose **Free Offer**.
3. Open the offer → **Custom Codes → Create Custom Codes**. Use `SHIPATONPASS`, `SHIPATONEMBER`
   or `SHIPATONFROST` (no special characters allowed). Set the redemption limit to 100 and the
   expiration to 31 Oct.
4. The codes need up to an hour before they work. Check that the redemption URL App Store Connect
   shows matches the links above, and fix the Devpost text if it doesn't.

**Limits to know:**
- Custom codes redeem **only through the link** (or an in-app sheet the game doesn't have). They
  don't work in the App Store's "Redeem Gift Card or Code" field, which is why the field text
  gives links.
- Each code covers one product, and each customer can redeem it once.

**Test the flow:** create sandbox codes on the same Offer Codes screen (→ Sandbox Codes). Redeem one
on an iPhone (sandbox account, iOS 16.3+) to confirm that the game picks the item up after a relaunch
or a Restore Purchases.

**Fallback** if a judge still gets stuck: find their Player ID (Profile → Account → Copy) under
RevenueCat → Customers and grant the `skin_ember`, `skin_frost` and `season1` entitlements until
31 Oct.

The form says judges may skip codes, so **the video has to show a purchase and the unlocked item
within the first 3 minutes**. The friend's `Veyro Run Purchases.mp4` covers that.

### Grand Prize, #BuildInPublic (both), Peace, Catvertising, Influencer, Kotlin, Noise, Galaxy, Replit, Funnel
Leave empty.

### HAMM Award
```
Veyro Run is free and has no ads. It makes money from three one-time purchases through RevenueCat: two characters, Ember and Frost (€2.99 each), and the Season 1 pass (€4.99).

I chose one-time cosmetics on purpose. Endless runners usually sell revives, coin doublers or energy, and I think that makes the game worse: you end up paying to get around a difficulty the game created on purpose. It would also break the Daily Run, which only works if everyone plays the same track with the same rules. So everything for sale changes how you look, and nothing changes how you play.

How it's set up:
- Each product has its own entitlement in RevenueCat instead of one "premium" flag, so if I want a bundle later I can build it in the dashboard without an app update.
- The characters go straight to Apple's purchase sheet, because you can already see what you're buying. The pass goes through a RevenueCat paywall, because it has a whole ladder of rewards to explain.
- Buying the pass unlocks the level-1 reward straight away. The rest you earn with XP from your runs, so the pass is something you play through, not just a pack of items.
- Your profile ID is also your RevenueCat ID, so purchases and progress come back together if you reinstall, and Restore Purchases is always in the shop.

Results: it's early, so I don't have meaningful revenue numbers to share yet, and I'm not going to buy my own items to invent them.
```

### RevenueCat Design Award
Fix the pass paywall's fake discount in RevenueCat before submitting (STATUS 29 Sep).
```
The design problem I cared about most: how do you control a game when you're two metres away from the screen? In camera mode your body replaces every button. You step to change lanes and hop to jump. After a crash you hop to run again. If you walk out of frame, the run pauses. To resume you stand still and hop twice, which is a gesture you won't make by accident, and a 3-2-1 countdown gives you time to get ready. A small YOU · CAMERA strip shows where the game sees you, so you notice when you're drifting out of frame. Once the phone is propped up, you never have to walk back and touch it.

The setup respects the player too. The game checks whether your phone is fast enough before it asks for the camera. If it isn't, it never asks. If the camera can't keep up mid-run, it hands you back tilt controls instead of failing.

A few things I'd point you to:
- The icon and the colours. The icon is a torn ticket, because every day you get a ticket to a new track. Its paper, teal and pink run through every screen.
- The end of a run. The runner falls differently depending on what you hit, and does a little dance if you beat your best. The result card is laid out so you can send it to a friend as a challenge.
- The music follows the run. The menu has its own loop. Each run gets a song picked by the track's seed, so everyone playing today's Daily Run hears the same one. When you crash the music fades and holds, and the result screen plays a different tune for a new best than for a loss. Pausing lowers it instead of cutting it.
- The locker. It's a 3D preview of your runner that you drag to rotate, and the camera moves to the head, back or feet depending on what you're choosing.
- The podium. The top three stand on gold, silver and bronze, each with a little pixel critter generated from their name.
- The Season 1 pass paywall is built in RevenueCat's Paywall Builder.
```

### Best Game Award
```
It's a three-lane endless runner, but you play it by moving. Hold the phone and tilt to steer, then tap to jump. Or lean it against something, step back, and play with your whole body: step to the side to change lane, and hop to jump. Runs are short and restarting is instant (in camera mode you just hop).

What keeps you coming back is the Daily Run. Every day there's one new track and everybody plays the same one, so a score actually means something when you compare it with a friend's. You can also send someone a link to your exact run, and they get your score to beat.

For the look I wanted a sunny park: pale paving, teal low-poly trees, gold hurdles and coins. The menus use the icon's colours (paper, teal and pink, like a printed ticket), so going from the menu into a run feels like staying in the same place. I hand-picked the characters and the music to fit that mood, and added dodge, crash and celebration animations so the runner reacts to what you do and falls differently depending on what you hit.

Monetization is cosmetics only. In an endless runner, the moment you sell revives or coin doublers it becomes pay-to-win. With a shared Daily Run that would break the whole idea, because everyone has to run the same course with the same rules. So you can buy characters and the season pass, and they only change how you look.
```

### Keep Them Coming Back (OneSignal)
App ID:
```
1f6ba056-efe3-4bfe-a0cd-a9a26150720a
```
Before pasting, open the message report in OneSignal (Messages → the 28 Sep "Today's Daily Run is
live" send). Confirm it was sent from the dashboard to subscribers, not as a test to one device, and
copy the delivered and clicked counts.
```
Veyro Run is built around one daily habit, the same way Duolingo is. Every day there's one new track, the Daily Run, and it's the same for everyone in the world. Finishing it stamps your daily streak on the home screen, which then says "today is stamped — come back tomorrow". The best reason to open the game is that today's track is new and your streak is waiting. OneSignal is how I tell people that moment has come.

How it works:
- There's no permission popup on first launch. The game offers the reminder only after you've finished your first Daily Run, when you already know what you'd be reminded about. If you say no, nothing else happens, and you can turn it on later in Profile → Settings.
- The notification tells you the new track is out: "Today's Daily Run is live · Same track for everyone today. Set your best and see where you land on the leaderboard."
- Tapping it opens the game on the Run tab with the Daily Run selected and ready to go. It never starts the run by itself, because in camera mode you need to be standing back first, and it's ignored if you're already in the middle of a run.

The campaign: I sent it from the OneSignal dashboard on 28 Sep 2026 to all subscribed devices [N delivered, N opened].

Next, the streak goes into the notifications. Each player gets tagged with their streak and the last day they finished a Daily Run. That way people who already played today don't get pinged, and someone about to lose a long streak gets a "keep your 5-day streak alive" message instead of the generic one. After that, the reminder goes out automatically every time a new track comes out.
```

### The Growth Loop (Layers): only if you can see the events
Check: turn on PROFILE → SETTINGS → GAMEPLAY ANALYTICS on the iPhone, tap a reminder, finish a Daily
Run, then look in Layers → Events for `notification_opened`, `daily_run_started` and
`daily_run_completed`. No events → leave this field empty.
```
Yes, the Layers SDK is installed in the iOS build (Unity SDK 3.3.2). Analytics is off by default and players turn it on in Profile → Settings. The app never asks for tracking and doesn't use the advertising ID.

The loop I'm testing is the daily return: finish a Daily Run, get a reminder when the next one is out, then come back and finish that one too.

Audience: players who finished at least one Daily Run and said yes to reminders and to analytics.
Hypothesis: a reminder right when the new track appears brings people back to play that day's run.
Message and channel: a OneSignal push ("Today's Daily Run is live"), which opens the game on the Run tab.
Experiment: a first pilot with a single message. The notification carries a campaign id, and the game attaches it to the events it sends to Layers (notification_opened, daily_run_started, daily_run_completed), so I can follow one reminder all the way to a finished run.
What the signal showed: [DATE: N opened → N started → N finished].
What I learned: [one real thing]. One thing I already know: because analytics is opt-in, I only measure the people who chose to share, so the numbers are small. I'm fine with that trade.
Next: a second version of the message that mentions your streak, sent at the same time to the same group, comparing how many finish the run.
```

### Additional notes for the judges
```
Thanks for checking out Veyro Run!

- If an unlock code doesn't work for you, email ferrabled+veyro@gmail.com and I'll unlock everything on your account.
- Camera mode works best in a well-lit room, with the phone propped up around chest height and you 1.5–2 m away. Step to change lane, hop to jump.
- The how-to-play guide mentions sliding. That move isn't in the game yet; it's coming in the next update.
- Camera frames never leave the phone.
- Built by me, with a friend lending a Mac to upload the iOS build.
```

### RevenueCat Growth Fund
Your call.

---

## Gallery: order and captions

Files in `builds/submission/screenshots/`, made from the friend's 1320×2868 set and resized to
1179×2556. Upload the icon separately where the form asks for it. Optionally put
`builds/submission/gallery-cover-3x2.png` first as the thumbnail.

| # | File | Caption |
|---|---|---|
| 1 | `01-run2.png` | Tilt to change lanes, tap to jump. Today's track is the same for everyone. |
| 2 | `02-run_wall.png` | Camera mode: phone on the table, me two metres back. The strip at the top is what the camera sees. |
| 3 | `03-how_to_play4.png` | Two ways to play: hold it and tilt, or prop it up and use your body. |
| 4 | `04-run_jump.png` | Jump the gold hurdles with a tap, or with a real hop in camera mode. |
| 5 | `05-end_run.png` | Crash, check your score, run again. You can send the run to a friend as a challenge. |
| 6 | `06-home.png` | Home: your season level, your daily streak, and the two ways to play. |
| 7 | `07-season_pass2.png` | Season 1: ten levels of free and pass rewards, earned by running. Cosmetic only. |
| 8 | `08-shop.png` | The shop: two characters and the Season 1 pass. One-time purchases, no advantage in the run. |

Not used:
- `run.png` and `camera.png`, which are weaker versions of 1 and 2.
- `season_pass.png`, where everything is locked.
- `how_to_play.png`, `how_to_play2.png` and `how_to_play3.png`, because 2 and 3 advertise sliding, which doesn't exist yet.
- **`notification.png` and `notification_test.png`: never upload these.** They show other people's
  names, private messages and a phone number.
