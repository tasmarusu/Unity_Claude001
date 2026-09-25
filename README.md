# 一撃ビル解体 (One-Tap Demolition)

画面下から積み上がるビルをタップした瞬間、その真下の1階層だけを破壊する1タップゲーム。
タップした階より上の階は支えを失い連鎖的に崩落する。連鎖崩落でどこまで高得点を稼げるかを競う、ハイパーカジュアル向けプロトタイプ。

## ゲーム概要

- 押している間、狙った階と巻き込まれる階がハイライトされ、指を離すとその階から上が連鎖崩落する(ワンタップ操作)。
- 各階には種類がある: 通常 / ゲート(×2・×3・÷2、通過後の階の得点に掛かる) / 保護階(KEEP、壊すとステージ失敗)。
- ステージごとにショット数が限られ、スコアで星1〜3を評価。どこから崩すかが判断になる(進行・仕様の詳細は CLAUDE_PROGRESS.md)。
- クリアごとに建築史を1つ解放し、永続スコアボーナスが増える。
## フォルダ構成

```
Unity_Claude001/
├── Assets/
│   ├── Scripts/
│   │   ├── Floor.cs                  # 1階分のロジック(タップされたら物理演算オンで弾け飛ぶ)
│   │   ├── BuildingTower.cs          # Floorを積み上げて生成、連鎖崩落の制御
│   │   ├── TapDemolishController.cs  # メインカメラにアタッチ、タップ/クリックのレイキャスト判定
│   │   ├── ScoreManager.cs           # スコア加算・ベストスコアのPlayerPrefs保存
│   │   ├── GameManager.cs            # タワー生成・クリア後の次タワー生成ループ制御
│   │   └── JuiceManager.cs           # 破壊時の演出(カメラシェイク/パーティクル/ヒットストップ/SE/スコアポップ)を統括
│   ├── Prefabs/
│   │   ├── Floor.prefab              # Cube + Rigidbody(Kinematic) + BoxCollider + Floor.cs
│   │   └── BuildingTower.prefab      # BuildingTower.cs(Floor Prefab / Floor Count 等を設定済み)
│   └── Scenes/
│       └── Main.unity                # Main Camera / GameManager / ScoreManager / JuiceManager 配線済み
├── ProjectSettings/
│   ├── ProjectVersion.txt            # Unity Hubがプロジェクトとして認識するための最小限のバージョン情報
│   └── EditorBuildSettings.asset     # ビルド設定にMain.unityを登録済み
├── .gitignore
└── README.md
```

このリポジトリには Unity Editor が自動生成する `Library/` `Temp/` 等のフォルダは含まれません(`.gitignore` で除外)。プレハブ・シーンの実体はテキスト形式のアセットとしてコミット済みで、**Unity Editorで開けばすぐPlayできる状態**になっています。

## セットアップ手順(Unity Editor上)

### 1. プロジェクトを開く

Unity Hub で「プロジェクトを追加」からこのフォルダ(`Unity_Claude001`)を選択して開きます。`ProjectSettings/ProjectVersion.txt` で **Unity 6000.3.10f1** を指定しているため、同バージョン(または互換性のある Unity 6 系)のEditorがインストールされている必要があります。

`ProjectVersion.txt` のリビジョンハッシュ値はプレースホルダーです。Unity Hubが「エディタが見つかりません」と表示した場合は、インストール済みの 6000.3.10f1 を手動で選択して開いてください(バージョン番号が一致していれば問題なく開けます)。

初回オープン時、Unityがこのリポジトリのアセットを再インポートします。数分かかることがあります。

### 2. 内容を確認する(すでに配線済み)

`Assets/Scenes/Main.unity` を開くと以下が既に配置されています。

- **Directional Light**
- **Main Camera** — `TapDemolishController.cs` アタッチ済み(`Target Camera` = 自身)
- **GameManager** — `GameManager.cs` アタッチ済み(`Tower Prefab` = `BuildingTower.prefab`、`Next Tower Delay` = 1.5)
- **ScoreManager** — `ScoreManager.cs` アタッチ済み
- **JuiceManager** — `JuiceManager.cs` + `AudioSource` アタッチ済み(`Camera Transform` = Main Camera)

`Assets/Prefabs/Floor.prefab` は Cube(MeshFilter/MeshRenderer) + `BoxCollider` + `Rigidbody`(`Is Kinematic` = ON) + `Floor.cs` の構成、`Assets/Prefabs/BuildingTower.prefab` は `BuildingTower.cs` の `Floor Prefab` に `Floor.prefab` を設定済みです(`Floor Count` = 10、`Floor Height` = 1)。

### 3. 任意で差し替える(プレースホルダー)

- `JuiceManager` の `Debris Particle Prefab` に破壊時のパーティクル(コンクリート片・ホコリ)を設定する。
- `JuiceManager` の `Destroy Clip` / `Impact Clip` に仮のSEを割り当てる。
- 未設定でも例外は発生しません(nullチェック済み)。見た目・音を強化したい場合のみ差し替えてください。

### 4. 実行して確認する

Playモードに入り、積み上がったビルの任意の階をクリック(エディタ)/タップ(実機)すると、その階と上階が連鎖的に崩落し、スコアが加算されることを確認します。

> **Note:** プレハブ・シーンはUnity Editorを介さずテキストアセットとして直接作成しています。開いて何かエラー・警告が出た場合は、該当コンポーネントを一度削除して手動で付け直してください(スクリプト自体には影響ありません)。

## 破壊時のゲームフィール(JuiceManager)

`JuiceManager.cs` に演出ロジックを集約し、`Floor.cs` と `BuildingTower.cs` から呼び出す設計になっています。

| 演出 | 実装 | 呼び出し元 |
| --- | --- | --- |
| カメラシェイク | コルーチンでカメラのローカル位置を揺らす。連鎖数(崩落する総階数)が多いほど揺れが強くなる | `BuildingTower.RequestDemolish` → `JuiceManager.TriggerImpact` |
| ヒットストップ | `Time.timeScale` を一瞬だけ落として「間」を作る | `JuiceManager.TriggerImpact` |
| 破壊パーティクル | 階が壊れた位置に `ParticleSystem` プレハブを生成(プレースホルダー、実体は要設定) | `Floor.Demolish` → `JuiceManager.PlayFloorDestroyEffect` |
| 効果音 | `AudioSource.PlayOneShot` で再生。連鎖が進むほど `pitch` を上げて盛り上がりを演出 | `Floor.Demolish` → `JuiceManager.PlayFloorDestroyEffect` |
| スコアポップ演出フック | `UnityEvent<Vector3, int>`(`OnScorePopupRequested`)としてUI側に公開。数値ポップ等のUI実装はここに接続する | `BuildingTower.CollapseFromFloor` → `JuiceManager.ShowScorePopup` |

パーティクルとSEクリップは未設定でも例外は発生せず(nullチェック済み)、プレースホルダーとして後から差し替え可能です。

## 今後の拡張候補

- タイトル/リザルトUIの追加
- Floorの見た目バリエーション(色・素材のランダム化)
- コンボ数に応じたスコア倍率のさらなる強化
- Cinemachineへのカメラシェイク移行(現状は自作コルーチンによる簡易シェイク)
