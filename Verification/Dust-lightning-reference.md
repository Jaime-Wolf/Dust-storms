# Dust electrical-discharge reference - October 7, 2026

The user requested rare visible/audible dust-static discharges after accepting the 0.1.6 appearance. This supersedes the earlier request to omit lightning. Cosmetic effects only; player exposure damage and shelter remain deferred.

Primary references read:

- Rahman, Cheng and Samtaney, Generation and sustenance of electric fields in sandstorms, Physical Review Research 3, L012008 (2021): https://journals.aps.org/prresearch/abstract/10.1103/PhysRevResearch.3.L012008 . The paper models charge separation as differently sized grains are transported by turbulent air, producing large-scale electric fields. It supports the physical inspiration, not this mod's numerical frequency or flash dimensions.
- NASA, Rover Detects Electric Sparks in Mars Dust Devils, Storms (December 2025): https://www.nasa.gov/missions/mars-2020-perseverance/perseverance-rover/nasa-rover-detects-electric-sparks-in-mars-dust-devils-storms/ . The mission recorded spark/crackle sounds and electrical signatures in Martian dust activity. NASA distinguishes well-documented terrestrial dust charging from actual discharges, which are rare on Earth. The Mars recordings motivate a restrained crackle timbre; they are not terrestrial recordings or audio assets used by this mod.

Game design: a small, short arc and mild glow within existing heavy dust, with a generated crackle. At the user's request, 0.1.9 makes strikes uncommon: forty-five seconds of eligible heavy weather plus an exponential wait with 150-second mean. It also increases playback gain and adds a short baked reverb tail. At the user's subsequent request, 0.1.10 uses a sharper lightning crack and low thunder-style roll, a 30% larger branching bolt and another playback-gain increase. This sound character is artistic tuning rather than an assertion that every terrestrial dust spark produces conventional thunder. Version0.1.11 replaces global fog/ambient whitening with a brief local soft-shadow point light and extends the thunder/reverb trail to4.4s, with another small volume increase. Version0.1.12 extends the visual pulse to1.1s and audio to6.5s, increases local light reach/strength, adds native-solar-phase night amplification, and favours the visible area by chance using rendered camera matrices. These remain artistic gameplay choices. Intervals can still outlast a storm. Frequency, arc length, colour/glow, sound/reverb and delayed playback are artistic tuning, not a calibrated electrical or atmospheric simulation.

Version0.1.14 changes eligible strike intervals to a uniform30-50 seconds at the user's request and reduces storm wind by15%, retaining the temporary25% wind dip from0.1.13. Version0.1.15 responds to odd crackles by blending fewer softer fragments into the initial thunderclap, with the deep roll and6.5s reverb retained.

Additional sound references reviewed October7,2026: [National Weather Service thunder explanation](https://www.weather.gov/safety/lightning-science-thunder), [Dan Robinson's original thunder recordings and notes](https://stormhighway.com/thunderc.php), and [Fineberg, Walters and Reiss, Advances in Thunder Sound Synthesis](https://arxiv.org/abs/2204.08026). The crack/main crash/rolling-tail descriptions informed our generated envelope. These were text/reference-note reviews; the web recordings were not auditioned or copied. Numerical comparisons validate levels, not realistic timbre; a user listening test remains necessary.

No storm explosions, damage, strikes on objects, EMP, fire, suction or camera shake. No external audio or textures downloaded/copied. N offers an explicit user preview while dust is visible; it does not force a new storm or change the random timer.



