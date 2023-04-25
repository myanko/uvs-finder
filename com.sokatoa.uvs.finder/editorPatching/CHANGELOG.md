# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).


## [1.3.1] - 2021-09-07
- Updated readme

## [1.3.0] - 2021-09-06
- Update harmony to version 2.1.1.0

## [1.2.0] - 2021-07-07
- Fix ``OnWillEnablePatch``
- Add Open Harmony Log button when log file exists
- Remove ability to hide inactive patches
- Use static constructor to initialize patch manager

## [1.2.0-exp.1] - 2021-04-22
- Fix: ``hideInactive`` does also hide groups when all patches are inactive
- Fix: 2019.4 compatibility ``hasFocus``

## [1.2.0-exp] - 2021-04-15
- Added ``PatchManager.IsInitialized``
- Initial support for capturing harmony instances that try to bypass EditorPatching completely
- Improved EditorPatching settings window. Patches are now grouped by package/assembly names

## [1.1.1-exp.1] - 2021-04-13
- Disabling patch defaults to slow mode
- Add api to get access to persistent state
- Fix support for 2018.4
- Log warnings and errors for PatchProviders missing patches or methods

## [1.1.1-exp] - 2021-03-29
- Experimental change: ``EditorPatchProvider`` has public fields for ``PatchThreaded`` (to patch on background thread), ``SuppressExceptions`` and ``EnableException`` (contains last Exception during patching)
- ``EnablePatch`` returns if patching was successful
- ``PatchManager`` will attempt patching on main thread when it catches a ``UnityException.MainThread`` exception

## [1.1.0-pre.4] - 2021-03-24
- Lazy ``AssetDatabase.FindAssets``, only necessary when path is being accessed via API 
- Unpatch with explicit fast mode for batch unpatching
- enabling / disabling patches does not immediately trigger UI repaint (caused a lot of overhead when enabling/disabling many patches at once)
- disabling a patch is awaitable now too and runs on a background thread
- changed patch disabling to manual unpatching instead of using Harmony ``UnpatchAll``

## [1.1.0-pre.1] - 2021-03-20
- expose option to not set persistent patch state to avoid writing dynamic patches state information in settings file

## [1.1.0-pre] - 2021-03-17
- enable patches immediately
- added CanEnable to EditorPatch for delaying apply patch to allow for waiting of other requirements, e.g. when GUI is not yet loaded
- ``PatchManager.EnablePatch`` awaitable
- support Harmony.Debug and added built in patch to log harmony output to Unity console
- moved deeplink patch into submodule
- allow to update already registered ``EditorPatchProvider`` instance
- allow EditorPatchProvider to override ``Id``.
- EditorPatchProvider ``DisplayName`` and ``Description`` are now optional 
- updated Harmony plugin to 2.0.4

## [1.0.1] - 2021-03-01
- license guid conflict fix

## [1.0.0] - 2021-02-28
- initial public release