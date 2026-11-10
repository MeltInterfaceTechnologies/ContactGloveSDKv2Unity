# License scope and external dependencies

The SDK source and first-party Basic Sample are licensed under Apache License 2.0;
see [LICENSE.md](LICENSE.md).

The SDK package depends on Unity UI, Physics, IMGUI and ImageConversion modules
listed in [package.json](package.json). Unity provides these dependencies and
the sample’s built-in fonts, sprites, shaders and primitive geometry under its
own terms, which continue to apply when used with the SDK.

The Basic Sample scene, UI scripts and hand-preview RenderTexture are first-party
sample assets covered by the SDK's Apache License 2.0.

The VR setup guide uses separately installed Unity OpenXR Plugin and XR Hands
packages and an OpenXR runtime such as SteamVR. Unity’s official HandVisualizer
sample, including its scene, meshes, prefabs and materials, must be imported
from XR Hands through Package Manager and retain Unity’s applicable license terms.
SteamVR runtime is supplied by Valve under its own terms.
