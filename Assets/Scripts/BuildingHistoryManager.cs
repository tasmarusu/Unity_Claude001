using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// クリアごとに建築史トリビアを1つ解放し、解放数に応じた永続スコア倍率ボーナスを提供する。
    /// docs/building-history-unlocks.md の30項目テーブルに対応。アイコンはAssets/Resources/BuildingHistoryから読み込む。
    /// </summary>
    public class BuildingHistoryManager : MonoBehaviour
    {
        public struct Entry
        {
            public string IconId;
            public string Era;
            public string Name;
            public string Note;
        }

        private const string UnlockedCountKey = "OneTapDemolition_HistoryUnlocked";
        private const int TotalItems = 30;
        private const float BonusPerItem = 0.02f;
        private const float MaxBonus = 0.6f;

        public static BuildingHistoryManager Instance { get; private set; }

        public int UnlockedCount { get; private set; }
        public int TotalCount => TotalItems;

        /// <summary>
        /// 歴史解放ボーナス(永続・乗算)。解放数×0.02、最大+0.6(30/30時)。
        /// </summary>
        public float HistoryBonusMultiplier => Mathf.Min(UnlockedCount * BonusPerItem, MaxBonus);

        private static readonly Entry[] Entries =
        {
            new Entry { IconId = "01_ancient_stepped", Era = "古代", Name = "石造多層建築の誕生", Note = "高さの限界は自重との戦いだった" },
            new Entry { IconId = "02_roman_insula", Era = "紀元1世紀", Name = "ローマのインスラ", Note = "古代版マンション、5〜6階建て" },
            new Entry { IconId = "03_medieval_church", Era = "中世", Name = "石造教会・城郭建築技術", Note = "石積みアーチ構造の発展" },
            new Entry { IconId = "04_iron_bridge", Era = "1779", Name = "鋳鉄建材の実用化", Note = "金属建材時代の幕開け" },
            new Entry { IconId = "05_safety_elevator", Era = "1852", Name = "安全エレベーター発明", Note = "高層化の鍵となる発明" },
            new Entry { IconId = "06_reinforced_concrete", Era = "1867", Name = "鉄筋コンクリート発明", Note = "現代建築の基礎技術" },
            new Entry { IconId = "07_home_insurance_building", Era = "1885", Name = "ホーム・インシュランス・ビルディング", Note = "世界初の鉄骨高層ビル" },
            new Entry { IconId = "08_skyscraper_coined", Era = "1880年代", Name = "「摩天楼」という言葉の誕生", Note = "シカゴ発の建築革命" },
            new Entry { IconId = "09_eiffel_tower", Era = "1889", Name = "エッフェル塔", Note = "鉄骨建築の象徴" },
            new Entry { IconId = "10_flatiron_building", Era = "1902", Name = "フラットアイアン・ビルディング", Note = "NYの名物三角形ビル" },
            new Entry { IconId = "11_woolworth_building", Era = "1913", Name = "ウールワース・ビルディング", Note = "「商業の大聖堂」" },
            new Entry { IconId = "12_curtain_wall_tech", Era = "1920年代", Name = "カーテンウォール技術の初期実用化", Note = "軽量な壁で高層化を加速" },
            new Entry { IconId = "13_chrysler_building", Era = "1930", Name = "クライスラー・ビル", Note = "アールデコの傑作" },
            new Entry { IconId = "14_empire_state_building", Era = "1931", Name = "エンパイア・ステート・ビル(381m)", Note = "40年間世界一の高さ" },
            new Entry { IconId = "15_great_depression", Era = "1930年代", Name = "大恐慌時代の建設停滞", Note = "高層ビルブームの一時中断" },
            new Entry { IconId = "16_international_style", Era = "1950年代", Name = "国際様式(モダニズム)の普及", Note = "シンプル・機能美への転換" },
            new Entry { IconId = "17_seagram_building", Era = "1958", Name = "シーグラム・ビルディング", Note = "ミース・ファン・デル・ローエの名作" },
            new Entry { IconId = "18_base_isolation_research", Era = "1960年代", Name = "免震構造の研究開始(日本)", Note = "地震国ならではの技術発展" },
            new Entry { IconId = "19_world_trade_center", Era = "1973", Name = "ワールドトレードセンター", Note = "チューブ構造の採用" },
            new Entry { IconId = "20_sears_tower", Era = "1973", Name = "シアーズ・タワー", Note = "西側で最も高いビルへ" },
            new Entry { IconId = "21_tuned_mass_damper", Era = "1980年代", Name = "制震ダンパー技術の実用化", Note = "揺れを吸収する技術" },
            new Entry { IconId = "22_petronas_towers", Era = "1998", Name = "ペトロナスツインタワー", Note = "アジア発、初の世界一" },
            new Entry { IconId = "23_taipei_101", Era = "2004", Name = "台北101", Note = "チューンドマスダンパー搭載" },
            new Entry { IconId = "24_japan_highrise_boom", Era = "2000年代", Name = "日本の高層マンション普及期", Note = "都市部の縦への発展" },
            new Entry { IconId = "25_burj_khalifa", Era = "2010", Name = "ブルジュ・ハリファ(828m)", Note = "現在も世界最高" },
            new Entry { IconId = "26_post_311_seismic_code", Era = "2011", Name = "東日本大震災後の耐震基準見直し", Note = "安全基準の再定義" },
            new Entry { IconId = "27_shanghai_tower", Era = "2015", Name = "上海中心大厦", Note = "ねじれ構造で風荷重を軽減" },
            new Entry { IconId = "28_zeb", Era = "2010年代", Name = "ZEB(ネット・ゼロ・エネルギービル)普及", Note = "環境配慮型建築へ" },
            new Entry { IconId = "29_3d_printed_construction", Era = "2020年代", Name = "3Dプリント建築の実用化", Note = "建設の自動化・省力化" },
            new Entry { IconId = "30_merdeka_118", Era = "2023", Name = "メルデカ118", Note = "現時点で世界2位の高さ" },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<BuildingHistoryManager>() != null)
            {
                return;
            }

            GameObject go = new GameObject(nameof(BuildingHistoryManager));
            go.AddComponent<BuildingHistoryManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            UnlockedCount = Mathf.Clamp(PlayerPrefs.GetInt(UnlockedCountKey, 0), 0, TotalItems);
        }

        /// <summary>
        /// タワークリア時に呼ばれる。未解放が残っていれば次の1項目を解放して返す。全解放済みならnullを返す。
        /// </summary>
        public Entry? TryUnlockNext()
        {
            if (UnlockedCount >= TotalItems)
            {
                return null;
            }

            Entry unlocked = Entries[UnlockedCount];
            UnlockedCount++;
            PlayerPrefs.SetInt(UnlockedCountKey, UnlockedCount);
            return unlocked;
        }

        /// <summary>
        /// QAの自動プレイが進行度を元に戻すときに使う(PlayerPrefsだけでなく、メモリ上の解放数も戻す)。
        /// </summary>
        public void RestoreUnlockedCount(int count)
        {
            UnlockedCount = Mathf.Clamp(count, 0, TotalItems);
            PlayerPrefs.SetInt(UnlockedCountKey, UnlockedCount);
        }

        /// <summary>
        /// Assets/Resources/BuildingHistory/以下のPNGをSpriteとして読み込む(アンロック項目・強化アイコン共通)。
        /// </summary>
        public static Sprite LoadIcon(string iconId)
        {
            return Resources.Load<Sprite>("BuildingHistory/" + iconId);
        }
    }
}
