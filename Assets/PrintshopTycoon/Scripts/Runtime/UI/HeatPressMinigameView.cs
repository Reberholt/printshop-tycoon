using System;
using System.Collections;
using PrintshopTycoon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PrintshopTycoon
{
    /// <summary>
    /// UI for the heat press timing game. Logic lives in HeatPressMinigame (core);
    /// this view draws the bar, handles the STOP tap and plays feedback.
    /// </summary>
    public class HeatPressMinigameView : MonoBehaviour
    {
        GameManager game;
        HeatPressStation station;
        HeatPressMinigame logic;
        Action onFinished;

        RectTransform bar;
        RectTransform goodZone;
        RectTransform perfectZone;
        RectTransform indicator;
        Text resultText;
        Button stopButton;
        bool resolving;
        bool finished;

        public void Build(GameManager gameManager, HeatPressStation pressStation, RectTransform content, Action finishedCallback)
        {
            game = gameManager;
            station = pressStation;
            onFinished = finishedCallback;
            logic = new HeatPressMinigame(game.Session.Balance, Environment.TickCount);

            var title = UIFactory.Label(content, "Title", Loc.T("press.title"), 44, Palette.UiDark);
            UIFactory.Height(title, 90f);

            var barHolder = UIFactory.Rect(content, "BarHolder");
            UIFactory.Height(barHolder, 170f);
            var barImg = UIFactory.Panel(barHolder, "Bar", MaterialLibrary.Hex("#FFE5D9"), true, false);
            bar = barImg.rectTransform;
            bar.Stretch(10, 30, 10, 30);
            goodZone = UIFactory.Panel(bar, "Good", Palette.UiGreen, false, false).rectTransform;
            perfectZone = UIFactory.Panel(bar, "Perfect", MaterialLibrary.Hex("#B8F2C8"), false, false).rectTransform;
            indicator = UIFactory.Panel(bar, "Indicator", Palette.UiDark, true, false).rectTransform;
            indicator.anchorMin = new Vector2(0f, -0.2f);
            indicator.anchorMax = new Vector2(0f, 1.2f);
            indicator.sizeDelta = new Vector2(26f, 0f);

            resultText = UIFactory.Label(content, "Result", "", 64, Palette.UiAccent);
            UIFactory.Height(resultText, 110f);

            stopButton = UIFactory.Button(content, "Stop", Loc.T("press.stop"), Palette.UiRed, OnStop, 80);
            UIFactory.Height(stopButton, 230f);

            StartRound();
        }

        void StartRound()
        {
            resolving = false;
            logic.Start();
            station.SetForcedClosed(false);
            SetZone(goodZone, logic.TargetCenter, logic.GoodHalfWidth);
            SetZone(perfectZone, logic.TargetCenter, logic.PerfectHalfWidth);
            resultText.text = "";
            stopButton.interactable = true;
        }

        static void SetZone(RectTransform rt, float center, float half)
        {
            rt.anchorMin = new Vector2(center - half, 0f);
            rt.anchorMax = new Vector2(center + half, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void Update()
        {
            if (logic == null || resolving) return;
            logic.Tick(Time.unscaledDeltaTime);
            indicator.anchorMin = new Vector2(logic.Position, -0.2f);
            indicator.anchorMax = new Vector2(logic.Position, 1.2f);
            indicator.anchoredPosition = Vector2.zero;
            if (PlayerController.InteractKeyPressed()) OnStop();
        }

        void OnStop()
        {
            if (resolving || logic == null || !logic.Running) return;
            resolving = true;
            stopButton.interactable = false;
            var result = logic.Stop();
            station.SetForcedClosed(true);
            game.Audio.Play(Sfx.PressHiss, 0.8f);
            StartCoroutine(Resolve(result));
        }

        IEnumerator Resolve(PressResult result)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            var r = station.ApplyPressResult(result);
            switch (result)
            {
                case PressResult.Perfect: resultText.text = Loc.T("press.perfect"); resultText.color = Palette.UiGreen; game.Audio.Play(Sfx.Perfect); break;
                case PressResult.Good: resultText.text = Loc.T("press.good"); resultText.color = Palette.UiBlue; game.Audio.Play(Sfx.Good); break;
                default:
                    resultText.text = Loc.T(r == ActionResult.Retry ? "press.miss" : "press.low");
                    resultText.color = Palette.UiRed;
                    game.Audio.Play(Sfx.Miss);
                    break;
            }
            StartCoroutine(Pop(resultText.transform));
            yield return new WaitForSecondsRealtime(r == ActionResult.Retry ? 0.9f : 1.0f);

            if (r == ActionResult.Retry) { StartRound(); yield break; }
            finished = true;
            station.SetForcedClosed(false);
            onFinished?.Invoke();
        }

        IEnumerator Pop(Transform t)
        {
            for (float a = 0f; a < 0.25f; a += Time.unscaledDeltaTime)
            {
                t.localScale = Vector3.one * (1f + Mathf.Sin(a / 0.25f * Mathf.PI) * 0.3f);
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        /// <summary>Panel closed before the result was applied - give the press back.</summary>
        public void Abort()
        {
            if (finished) return;
            finished = true;
            StopAllCoroutines();
            station.CancelPress();
        }
    }
}
