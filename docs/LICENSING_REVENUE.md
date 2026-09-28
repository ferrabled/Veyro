# Licensing & revenue compliance

Analysis date: 20 Aug 2026, against the Unity Editor Software Terms (updated 30 Jun 2026) and current RevenueCat pricing. Lay reading, not legal advice.

## Verdict

Monetizing the game with RevenueCat IAP on Unity Personal is explicitly permitted. No royalties, no revenue share, no fees at hackathon scale. Money flow: player pays → store takes 15% → RevenueCat takes 0% (below $2,500 MTR/month) → rest is ours.

## Unity terms — what applies to us

1. **No cut of game revenue, ever.** §2.2: Unity Runtime distributes "without royalty, revenue share, or a runtime fee" for Unity 6 and earlier. The 2023 Runtime Fee is dead. Unity Personal costs $0.
2. **$200k tier ceiling.** Unity Personal requires Total Finances ≤ $200,000 over the trailing 12 months. For an individual this is "the amount generated in connection with your use of the Software" — game revenue counts, and conservatively Shipaton prize money for the game counts too. **Tripwire:** if winnings + revenue ever exceed $200k, §1.1 requires upgrading to Unity Pro (~$2.2k/seat/yr) *immediately* — a happy-path obligation, budgetable from the winnings. Keep records (the burden of proving eligibility is ours).
3. **Attribution** (§2.12): only required if the game shows a credits screen — then include: "[Game] was made with Unity®. Unity is a trademark or registered trademark of Unity Technologies" + the copyright line. Unity 6 Personal may disable the splash screen; keeping it is also fine.
4. **Seats** (§2.8): one person per seat, installable on two machines. Agents running the Editor in batchmode on the owner's machine act on the owner's behalf — fine. **Unity Build Server is not usable on Personal** (§2.5) — we don't use it. If we later use Unity Build Automation (cloud) for iOS, check its separate terms first.
5. **Stores are fine.** Android and iOS are Unity Supported Platforms, so shipping through Google Play and the App Store is ordinary distribution, not a "User Modification" enabling an unsupported platform — no engine changes are involved. (Standard industry practice for Unity games.)
6. **Owner's employment:** Total Finances are personal here (not providing Unity services to the employer). Separately, owner should check their employment contract's side-project/IP clauses so the game's IP is unambiguously theirs.

## RevenueCat & store economics

1. **RevenueCat cost:** free up to $2,500 monthly tracked revenue; above that, 1% of MTR. Hackathon scale ⇒ $0.
2. **Store cuts:** Google Play 15% (enroll in the reduced service-fee tier), Apple 15% via the Small Business Program (enroll once the account is set). Net to us ≈ 85%.
3. **Payouts require setup:** payments + tax profiles in each console before IAP can go live (PREREQUISITES P10). Prize money is taxable income; Devpost typically requires W-8BEN (non-US) / W-9 forms from winners.
4. **iOS under the owner's own account (D19, 28 Sep):** Apple pays the account holder, which is now the owner — iOS IAP revenue, US tax form (W-8BEN) and payouts land on the owner, same as Play. The earlier friend's-account revenue question no longer applies. Enroll in the App Store Small Business Program for the 15% rate and mark it in RevenueCat once approved.
5. **Revenue must be organic.** HAMM and Grand Prize judge *real* revenue/conversion. Self-purchases or artificial transactions violate store policies and would poison the submission. Honest small numbers with a good write-up beat inflated ones.
