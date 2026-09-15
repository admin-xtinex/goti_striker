# Verification Checklist

## Clean scene (`GameplayKit_Verification.unity`)
- [ ] New empty scene + plane (not village scene)
- [ ] Drag `PitStriker_GameplayKit` → Align → Play
- [ ] No missing scripts / null refs
- [ ] Marble aim + swipe launch works
- [ ] Pits at local Z 3.0 / 16.5 / 31.0 relative to kit root
- [ ] Detection / turn advance / HUD / audio / camera follow
- [ ] Moving/rotating kit root before Play still works
- [ ] GameplaySurface physics stable; material swappable without collider size change
