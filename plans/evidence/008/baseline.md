# Plan 008 baseline (2026-10-16, before any 008 change)

- HEAD `dc8b55a` ("Keep the burn marker at flame size while it flickers"); `git diff --stat dc8b55a..HEAD -- "game/The Last Fortress/Assets/TheLastFortress"` was empty, so the baseline is HEAD plus the uncommitted plan 007 work.
- Uncommitted at start (kept, not reset): modified `MvpContent.asset`, `MvpSetup.cs`, `CameraRig.cs`, `CaptureSequence.cs`, `HudView.Mvp.cs`, `HudView.cs`, `PlayerController.cs`, `PrototypeBootstrap.cs`, `PrototypeContent.cs`, `SaveStore.cs`, `WorldView.cs`, `MvpReplayAndSaveTests.cs`, `MvpSceneTests.cs`, `PrototypeSceneTests.cs`, `EditorBuildSettings.asset`, `EditorSettings.asset`, `ProjectSettings.asset`, game `README.md`, `plans/README.md`; untracked `Audio/`, `UI/`, `PresentationSetup.cs`, `AudioContent.cs`, `AudioDirector.cs`, `OnboardingState.cs`, plans 007–009 and the 007 evidence folders.
- Code size: 13,898 lines of C# under `Assets/TheLastFortress` (Core simulation ~2,900).
- No Unity Editor was open (only the MCP bridge processes); every check used closed-project batch commands.
- Fresh baseline EditMode run: **92/92 passed** in 41 s (`baseline-EditMode.xml`). PlayMode was not rerun before changes; 007 reported 16/16.
- `ProjectSettings.asset`'s only difference from HEAD at the end of this session was Unity's define-symbol reorder, which every batch run rewrites; it was restored to HEAD's order after the last run (as 007 did). `EditorSettings.asset`'s pre-existing change (`m_EnterPlayModeOptions: 1`) was left as found.
