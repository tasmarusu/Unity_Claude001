using UnityEngine;

namespace OneTapDemolition
{
    /// <summary>
    /// 文字なしのタップ誘導UI。波紋の拡大フェードとドットの拍動でループ表示し、
    /// 初回タップで非表示にしてPlayerPrefsに記録する(以後の起動では出さない)。
    /// </summary>
    public class TutorialHintUI : MonoBehaviour
    {
        private const string SeenKey = "HasSeenTapTutorial";

        [SerializeField] private RectTransform ring;
        [SerializeField] private RectTransform dot;
        [SerializeField] private CanvasGroup ringGroup;
        [SerializeField] private float ringCycleDuration = 1.2f;
        [SerializeField] private float dotPulseDuration = 0.6f;

        private float ringTimer;
        private float dotTimer;
        private bool dismissed;

        private void Start()
        {
            if (PlayerPrefs.GetInt(SeenKey, 0) == 1)
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (dismissed)
            {
                return;
            }

            if (WasTappedThisFrame())
            {
                Dismiss();
                return;
            }

            ringTimer += Time.deltaTime;
            float ringT = Mathf.Repeat(ringTimer, ringCycleDuration) / ringCycleDuration;
            float ringScale = Mathf.Lerp(0.5f, 1.6f, ringT);
            ring.localScale = new Vector3(ringScale, ringScale, 1f);
            if (ringGroup != null)
            {
                ringGroup.alpha = 1f - ringT;
            }

            dotTimer += Time.deltaTime;
            float dotT = Mathf.PingPong(dotTimer / dotPulseDuration, 1f);
            float dotScale = Mathf.Lerp(0.85f, 1.1f, dotT);
            dot.localScale = new Vector3(dotScale, dotScale, 1f);
        }

        private bool WasTappedThisFrame()
        {
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                return true;
            }
            return Input.GetMouseButtonDown(0);
        }

        private void Dismiss()
        {
            dismissed = true;
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            gameObject.SetActive(false);
        }
    }
}
