# 一撃ビル解体 (One-Tap Demolition)

画面下から積み上がるビルをタップした瞬間、その真下の1階層だけを破壊する1タップゲーム。
タップした階より上の階は支えを失い連鎖的に崩落する。連鎖崩落でどこまで高得点を稼げるかを競う、ハイパーカジュアル向けプロトタイプ。

## ゲーム概要

- タワーは複数階の `Floor` を下から積み上げて構成される。
- プレイヤーが任意の階をタップ(モバイル)/クリック(エディタ)すると、その階と、それより上の全ての階が連鎖的に崩落する。
- 崩落した階数・連鎖数に応じてスコアが加算される。連鎖が続くほどボーナススコア・演出(カメラシェイク・SEピッチ)が強くなる。
- タワーを崩し切ると、少し間を置いて次のタワーが生成され、ループでプレイが続く。

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
│   ├── Prefabs/                      # Floor.prefab, BuildingTower.prefab などを配置(要作成)
│   └── Scenes/                       # メインシーンを配置(要作成)
├── ProjectSettings/
│   └── ProjectVersion.txt            # Unity Hubがプロジェクトとして認識するための最小限のバージョン情報
├── .gitignore
└── README.md
```

このリポジトリには Unity Editor が自動生成する `Library/` `Temp/` 等のフォルダやプレハブ・シーンの実体は含まれていません(`.gitignore` で除外、また未作成のため)。**Unity Editorで開いて以下のセットアップを行ってください。**

## セットアップ手順(Unity Editor上)

### 1. プロジェクトを開く

Unity Hub で「プロジェクトを追加」からこのフォルダ(`Unity_Claude001`)を選択して開きます。`ProjectSettings/ProjectVersion.txt` に記載のバージョン(または近いLTSバージョン)のEditorがインストールされている必要があります。異なるバージョンで開く場合はUnity Hub側でバージョン変更を許可してください。

### 2. Floorプレハブを作成する

1. Hierarchy上に立方体(Cube)などのGameObjectを作成する。
2. `Rigidbody` コンポーネントを追加し、`Is Kinematic` にチェックを入れる。
3. `Box Collider`(または任意のCollider)が付いていることを確認する。
4. `Floor.cs` をアタッチする。
5. `Assets/Prefabs/` にドラッグ&ドロップしてプレハブ化し、`Floor.prefab` として保存する。

### 3. BuildingTowerを作成する

1. 空のGameObjectを作成し、`BuildingTower.cs` をアタッチする。
2. Inspectorで `Floor Prefab` に手順2で作成した `Floor.prefab` を設定する。
3. `Floor Count`(積み上げ階数)、`Floor Height`(1階の高さ)を必要に応じて調整する。
4. これも `Assets/Prefabs/` に `BuildingTower.prefab` として保存する。

### 4. シーンをセットアップする

1. `Assets/Scenes/` に新規シーンを作成し保存する(例: `Main.unity`)。
2. Main Camera を配置し、`TapDemolishController.cs` をアタッチする。
3. 空のGameObjectに `GameManager.cs` をアタッチし、Inspectorで `Tower Prefab` に `BuildingTower.prefab` を設定する。任意で `Tower Spawn Point` を設定する。
4. 空のGameObjectに `ScoreManager.cs` をアタッチする。
5. 空のGameObjectに `JuiceManager.cs` をアタッチする。
   - `Camera Transform` にMain Cameraを設定する(未設定時は `Camera.main` を自動取得)。
   - `Audio Source` コンポーネントを同じGameObjectに追加し、`Destroy Clip` / `Impact Clip` に仮のSEを設定する(任意)。
   - `Debris Particle Prefab` に破壊時のパーティクル(コンクリート片やホコリのプレースホルダー)を設定する(任意)。
6. Floorが乗るレイヤーを作成し(例: `Floor`)、`TapDemolishController` の `Floor Layer Mask` をそのレイヤーに絞ると誤判定を防げる。

### 5. 実行して確認する

Playモードに入り、積み上がったビルの任意の階をクリック(エディタ)/タップ(実機)すると、その階と上階が連鎖的に崩落し、スコアが加算されることを確認する。

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
