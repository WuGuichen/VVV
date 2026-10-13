# UPM-P2 Unity MCP Validation

> Date: 2026-08-26
>
> Candidate: local dirty working tree based on `d9c74a7edd12ff18bf228631d82ab882a96a589d`
>
> Package tree: `c60be19db8342fd8c2621a809853429f6968f7c23d06d7df7508fd95fce65281`

## Compile gate

- MCP instance: `WGameFramework@98dec1bb66281d37`
- Unity: `6000.5.0f1`
- Custom tool: `validate_unity_scripts`
- Latest job: `68d58622bc534846a037b3af18f66ed3`
- Result: passed
- Observed: refresh completed, compilation started and finished, domain reload observed, Editor ready, Console error count 0.

## Dragon14 PlayMode gate

- MCP test job: `4bc6bb9f7a7b4c848db25dbd65250f4e`
- Mode: PlayMode in the graphical Unity Editor
- Summary: 2 total / 2 passed / 0 failed / 0 skipped
- Duration: 6.3061634 seconds
- Tests:
  - `MxFramework.Tests.PlayMode.CharacterTest.CharacterTestDragon14AnimatedAppendageSoftContactPlayModeTests.FormalDragon14_HeadTailMidAndTailTipProduceBoundedSoftContactWithoutDamage`
  - `MxFramework.Tests.PlayMode.CharacterTest.CharacterTestDragon14AnimatedAppendageSoftContactPlayModeTests.FormalDragon14_MovingTurningHighSpeedWallCornerAndSeveredStayBounded`
- Result XML: `C:/Users/WGH/AppData/LocalLow/MxFramework/WGameFramework/TestResults.xml`
- Result XML SHA-256: `bb3d43ad8e5c5d3f9e05812581d229464510c1ee86f1e488a87e4065c22e73bb`
- Screenshot: `Docs/Evidence/LCC/LCC-M5-T3/LCC-M5-T3-Dragon14-Appendage-GameView.png`
- Screenshot size: 1,324,607 bytes
- Screenshot SHA-256: `e4954fade501c745f6197484a4550051a2f84299578a666d94d5057de08d53e2`
- Screenshot generated at: 2026-08-26 17:47:14 +08:00

The test output records bounded head, tail-mid and tail-tip contact, moving/turning/high-speed cases, wall-corner resolution, and severed-tail isolation. Two temporary `InitTestScene*.unity` assets created by this MCP run were deleted through Unity MCP after the result was captured.

## Initial full-suite failure

- MCP test job: `58196aaf868b4f368828b5b6d4847704`
- Assembly: `MxFramework.Tests.PlayMode.CharacterTest`
- Summary: 39 total / 30 passed / 9 failed
- Result XML SHA-256: `03a327ad46ad471b5899b8e9a9e0aa733590d5aa12b2aefb35557b70af2f3520`
- Failed tests:
  1. `CharacterProfileSlotLocomotionPlayModeTests.FormalPackages_ProfileSlotLocomotionPlaysThroughUnityPlayables`
  2. `CharacterTestCharacterDistanceM3PlayableTests.NetworkRollback_RestoresReservationHashAndNextTickCommands`
  3. `CharacterTestDragon14NaturalAttackPlayModeTests.BakedSocketCapsulesDamageTrainingTargetWithPerEventDeduplication`
  4. `CharacterTestDragon14NaturalAttackPlayModeTests.NaturalActionInputsResolveDragon14BindingsInPlayMode`
  5. `CharacterTestLandingActionPlayModeTests.HeavyLanding_StartsHeavyAction`
  6. `CharacterTestLandingActionPlayModeTests.NormalLanding_StartsLightAction`
  7. `CharacterTestLargeCharacterPresencePlayModeTests.FourLayerDiagnosticsAppendageExclusionAndNaturalAttackRegression_AreVisible`
  8. `PlayerGuidancePresentationPlayModeTests.PublishedPackage_RendersGuidanceVariantsAndCapturesEvidence`
  9. `RuntimeAiLabBehaviorCardConnectionPlayModeTests.PublishedRecipeTimelineCreatesBranchEventAndUndo`

The MCP result confirmed these were real graphical Editor failures and P2 remained `In Progress` until they were closed.

## Full-suite closure

- Original failure-set job: `abc805ad1511456d890787d7d7f57474`
- Original failure-set result: 9 total / 9 passed / 0 failed
- Full assembly job: `266644a21f0442b8962a2987723ccac3`
- Assembly: `MxFramework.Tests.PlayMode.CharacterTest`
- Full result: 39 total / 39 passed / 0 failed / 0 skipped
- Duration: 61.0257991 seconds
- Result XML SHA-256: `44807e0df6a6895c828551db9b6352cf5d5dd75237e62824a7ba83204506bec6`
- Focused authority/cache/config EditMode job: `0135874183c444f0b4d0a9ac76a2e31f`
- Focused EditMode result: 5 total / 5 passed / 0 failed

The fixes aligned stale tests with numeric CharacterAction/Profile Slot and unified-timeline contracts, restored the generated LandingProfile link, projected animation playback speed, rebuilt rollback-derived geometry/classification at the last completed frame, excluded process-local revision witnesses from the cross-branch authority hash, fixed FairyGUI package name/id cleanup, and updated the P2-moved embedded-action generator path.

## Scope and promotion status

The Dragon14 focused gate and full CharacterTest PlayMode gate are closed, so P2 is locally `Validated`. The full aggregate BatchMode native ParticleSystem crash remains separately recorded as a baseline-compatible residual. All local results are E1/non-promotable and do not replace the required same-remote-full-SHA PinnedGit E2 matrix.
