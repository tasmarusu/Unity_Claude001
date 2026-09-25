# CLAUDE_PROGRESS.md (ゲーム開発セッションの進捗・再開用)

最終更新: 2026-09-26 / 担当: ゲーム開発セッション

## 今作っているもの
「一撃ビル解体」に **1(Smash Fest型: 狙いと制約のあるステージ)** と **2(Mob Control型: 階ごとの倍率ゲート)** を統合した試作。
(根拠: 調査レポート「ハイカジ売れ筋100本と模倣候補」の候補1+2。ワンタップ操作は維持。)

## ゲームループ
PLAY → 狙う(押している間、狙った階と巻き込まれる階がハイライトされ、予測スコアが指の上に出る) → 離して崩す →
ゲート通過で倍率(×2/×3/÷2)、保護階(KEEP)を壊すと失敗 → リザルト(星1〜3・スコア・建築史解放) → NEXT/RETRY(1タップ)。
- ステージは番号から決定的に生成(StageGenerator)。最適解を総当たりで求め、星は最適の60%/90%。
- ショットは1→2→3発へ増加。難易度は階数・ゲート数・悪ゲート・保護階が段階的に増える。
- 動画広告視聴で同ステージを+1ショットでリトライ(リワード)。

## 主要ファイル(Assets/Scripts)
新規: StageSpec.cs(FloorKind/StageSpec/ScoreRules) / StageGenerator.cs / FloorBadge.cs / StageHudUI.cs / StageResultUI.cs / UiKit.cs / UiSprites.cs / SafeAreaUtil.cs
書き換え: GameManager.cs(状態機械 Ready→Playing→Resolving→Result) / BuildingTower.cs / Floor.cs / TapDemolishController.cs(押して狙う→離して撃つ) / ScoreManager.cs
追加: ProceduralAudio(ゲート/失敗/星/ファンファーレ/クリック) / JuiceManager(PlayGate等) / ScoreFeedbackUI(ゲートバナー)
削除: RewardBonusUI.cs(リザルト内の動画ボタンに統合。シーンのRewardBonusRootも削除)
設定: ProjectSettings 画面向きをPortrait固定。

## 広告
既存のGoogle Mobile Ads(テスト広告ID)を継続使用。バナーは下部固定で、読み込み後に3Dカメラのviewportをバナー高さ分切り上げ、
UIは SafeAreaFitter(Safe Area+バナー高さ)の内側に配置。初期化失敗でもゲームは止まらない。インタースティシャルはステージ遷移3回ごと。
※ネイティブバナーはEditorでは疑似表示のみ。実機での見え方は未確認。

## アセット
- Assets/Resources/BuildingHistory/ : 建築史30+強化3アイコン(納品済み)
- Assets/Resources/StageUI/star.png : 星アイコン(モデル制作が納品。UiSprites.Starが自動で優先使用)
- 注意: Assets/UI/BuildingHistory/ に旧パスの複製アイコン(未追跡・別GUID)が再出現している。コミットしていない。モデル制作側で整理が必要。

## 確認済み(Editor Playモード、MCP経由)
コンパイルエラーなし / ステージ1〜3の生成と難易度カーブ / 成功→リザルト→NEXT / 失敗→リトライ / 複数ショット継続 /
Safe Area計算(各アスペクト比の疑似値)/ HUD・ゲート看板・リザルトの表示崩れなし。

## 未確認・残課題
- 実マウス/タッチでの「押して狙う→離して撃つ」入力とAimラベルの実操作確認(MCPからは入力を注入できず、コード経由で発火して確認)
- 実機(Android)でのバナー領域・ノッチ・各アスペクト比の見え方
- 保護階を壊した失敗時のスコア表示(失敗でもスコアは出る。ベストには記録しない)
- ドロップゾーン(Smash Fest型の落下目標)は未実装。次の拡張候補。
- 星3の閾値(最適の90%)がやや厳しい可能性。プレイテストで調整。
- 進行度(PlayerPrefs OneTapDemolition_StageIndex)を1へ戻したい場合はエディタで削除。

## 次の作業開始地点
1. 実機/エディタで押して離す入力を実際に触って感触確認(長押し遅延・指の下の予測ラベル位置)
2. 星閾値・ステージ難易度の微調整 3. ドロップゾーン等の追加は方針確認後
