# ContactGlove Unity SDK V2

[English](README.md) | [ドキュメント](https://docs.melt-interface-technologies.com/ja/docs/contact/sdks/unity-v2)

DivingStationから手の追跡データ、コントローラー入力、デバイス情報を取得し、本体振動を制御するためのUnity向けC# SDKです。通信にはDivingStation Protocol v2（OSC/UDP）を使います。

## 動作要件

- Unity 6.3 LTS（6000.3系）
- DivingStation 2.0.0以降とContactGlove3 Pro
- Unity Package ManagerからGit URLで導入する場合はGit

Unity UI 2.0、Physics、IMGUI、ImageConversionはパッケージの依存宣言から解決されます。

## 導入

**Window > Package Management > Package Manager** を開き、**Install package from git URL** に次のURLを入力します。

```text
https://github.com/MeltInterfaceTechnologies/ContactGloveSDKv2Unity.git?path=/Packages/com.melt-interface-technologies.contactglove.sdkv2#v1.0.0
```

ローカルに取得したリポジトリから導入する場合は、**Install package from disk** で`Packages/com.melt-interface-technologies.contactglove.sdkv2/package.json`を選択してください。

**ContactGlove Unity SDK V2** の **Samples** から **Basic Sample** をImportし、`Assets/Samples/ContactGlove Unity SDK V2/1.0.0/Basic Sample/BasicSample.unity`を開きます。DivingStationを起動してグローブを接続し、[セットアップ](https://docs.melt-interface-technologies.com/ja/docs/contact/sdks/unity-v2/setup)に従ってSDK接続を有効にしてください。

## Basic Sample

Basic Sampleは、固定した手のルートを基準に、球と円柱で25関節のRaw／Retarget済みスケルトンを表示します。F／Jまたは画面のボタンで左右の本体振動を送信できます。

VRでの手の表示には、Unity OpenXR Plugin、XR Hands、Input Systemを導入し、Package Managerから公式 **HandVisualizer** をImportしてください。必要なruntimeと表示不具合の切り分けは[VRのセットアップ手順](https://docs.melt-interface-technologies.com/ja/docs/contact/sdks/unity-v2/setup#vr連携openxrとxr-hands)を参照してください。

## ドキュメント

- [ユーザーガイド](https://docs.melt-interface-technologies.com/ja/docs/contact/sdks/unity-v2)
- [セットアップ](https://docs.melt-interface-technologies.com/ja/docs/contact/sdks/unity-v2/setup)
- [APIリファレンス](https://docs.melt-interface-technologies.com/ja/docs/contact/sdks/unity-v2/api)
- [変更履歴](CHANGELOG.md)

## ライセンス

SDKのソースコードと自社サンプルには[Apache License 2.0](LICENSE)を適用します。Unityの構成要素、別途導入するXRパッケージ、OpenXR runtimeには各提供元の条件が適用されます。[Third Party Notices](Packages/com.melt-interface-technologies.contactglove.sdkv2/Third%20Party%20Notices.md)を参照してください。
