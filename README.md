# ContactGlove Unity SDK V2

[日本語](README_JA.md) | [Documentation](https://docs.melt-interface-technologies.com/en/docs/contact/sdks/unity-v2)

A managed C# SDK for receiving hand-tracking data, controller input and device information from DivingStation, and sending body-vibration commands. It uses DivingStation Protocol v2 over OSC/UDP.

## Requirements

- Unity 6.3 LTS (6000.3) or later in the 6.3 LTS series.
- DivingStation 2.0.0 or later and ContactGlove3 Pro.
- Git, when installing from a Git URL.

Unity UI 2.0, Physics, IMGUI and ImageConversion are declared package dependencies.

## Installation

To install version **1.0.0**, open **Window > Package Management > Package Manager**, select **Install package from git URL**, and enter:

```text
https://github.com/MeltInterfaceTechnologies/ContactGloveSDKv2Unity.git?path=/Packages/com.melt-interface-technologies.contactglove.sdkv2#v1.0.0
```

To install from a local copy of the repository, select **Install package from disk** and choose `Packages/com.melt-interface-technologies.contactglove.sdkv2/package.json`.

Select **ContactGlove Unity SDK V2**, import **Basic Sample** from **Samples**, and open `Assets/Samples/ContactGlove Unity SDK V2/1.0.0/Basic Sample/BasicSample.unity`. Start DivingStation, connect the glove, and follow the [setup guide](https://docs.melt-interface-technologies.com/en/docs/contact/sdks/unity-v2/setup) to enable the SDK connection.

## Basic Sample

Basic Sample displays raw and retargeted 25-joint skeletons at fixed hand roots using spheres and cylinders. Use F/J or the onscreen buttons to send left/right body vibration.

For VR hand visualization, install Unity OpenXR Plugin, XR Hands and Input System, then import Unity's official **HandVisualizer** sample through Package Manager. See the [VR setup instructions](https://docs.melt-interface-technologies.com/en/docs/contact/sdks/unity-v2/setup#vr-rendering-with-openxr-and-xr-hands) for runtime requirements and rendering troubleshooting.

## Documentation

- [User guide](https://docs.melt-interface-technologies.com/en/docs/contact/sdks/unity-v2)
- [Setup](https://docs.melt-interface-technologies.com/en/docs/contact/sdks/unity-v2/setup)
- [API reference](https://docs.melt-interface-technologies.com/en/docs/contact/sdks/unity-v2/api)
- [Changelog](CHANGELOG.md)

## License

SDK source and first-party sample assets are licensed under [Apache License 2.0](LICENSE). Unity components, separately installed XR packages and OpenXR runtimes retain their providers' terms. See [Third Party Notices](Packages/com.melt-interface-technologies.contactglove.sdkv2/Third%20Party%20Notices.md).
