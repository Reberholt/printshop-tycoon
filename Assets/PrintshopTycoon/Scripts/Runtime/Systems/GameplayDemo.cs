using System;
using System.Collections;
using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// A scripted ~40 second play-through for the gameplay preview video: Hue actually walks
    /// between stations (scripted joystick input) and the new v0.3 features play out in real
    /// time - order board, shelf, DTF printing, heat press minigame, pressing, packing, payout
    /// screen, level-up, factory view, upgrades and a timed special order.
    /// Used by "Printshop Tycoon > Record Gameplay Preview" (Unity Recorder) or on its own.
    /// </summary>
    public class GameplayDemo : MonoBehaviour
    {
        const string DemoSave = "printshop_tycoon_demo.json";

        /// <summary>Raised when the demo has finished (the recorder stops on this).</summary>
        public static event Action Finished;
        public static bool Running { get; private set; }

        GameManager game;
        GameSession s;

        public static void Begin()
        {
            if (Running) return;
            new GameObject("GameplayDemo").AddComponent<GameplayDemo>();
        }

        IEnumerator Start()
        {
            Running = true;
            DontDestroyOnLoad(gameObject);
            GameManager started = null;
            yield return DemoSetup.StartFreshGame(DemoSave, g => started = g);
            game = started;
            s = game.Session;
            yield return Wait(1.6f);                                   // main menu with Hue & Silke

            game.UI.CloseModal();
            yield return Wait(0.8f);

            // Order board.
            yield return Walk(new Vector3(-0.6f, 0f, -5.5f), new Vector3(-0.6f, 0f, 6.3f), game.Board.ApproachPosition);
            Face(game.Board);
            game.Board.Interact();
            yield return Wait(1.4f);
            s.Orders.Accept();
            game.UI.CloseModal();
            yield return Wait(0.3f);

            // Shelf: take the blanks.
            yield return Walk(new Vector3(-1.2f, 0f, 5.5f), game.Shelf.ApproachPosition);
            Face(game.Shelf);
            game.Shelf.Interact();
            yield return Wait(0.5f);

            // DTF printer: choose the logo and watch it print.
            yield return Walk(game.Printer.ApproachPosition);
            Face(game.Printer);
            game.Printer.Interact();
            yield return Wait(1.0f);
            game.UI.CloseModal();
            s.Orders.SelectLogo(s.Orders.Order.LogoId);
            game.Audio.Play(Sfx.Machine);
            while (!s.Orders.TransfersReady) yield return null;
            yield return Wait(0.4f);
            game.Printer.Interact();
            yield return Wait(0.3f);

            // Heat press: minigame (auto-stops on "perfect"), then the closed, steaming press.
            var press = game.Presses[0];
            yield return Walk(new Vector3(2.2f, 0f, 2.6f), press.ApproachPosition);
            Face(press);
            press.Interact();
            yield return null;
            if (game.UI.Minigame != null) game.UI.Minigame.AutoPlay = true;
            while (game.UI.MinigameOpen) yield return null;
            while (!s.Orders.PressDone) yield return null;
            yield return Wait(0.4f);
            press.Interact();
            yield return Wait(0.3f);

            // Packing, payout screen with the exact breakdown.
            yield return Walk(new Vector3(-0.6f, 0f, 2.85f), new Vector3(-0.6f, 0f, -2.3f), game.Packing.ApproachPosition);
            Face(game.Packing);
            game.Packing.Interact();
            yield return Wait(1.5f);
            while (!game.UI.ModalOpen) yield return null;
            yield return Wait(2.6f);
            game.UI.CloseModal();
            yield return Wait(0.3f);

            // Level up (shown via the normal level-up queue).
            s.Progression.AddXp(s.Progression.XpForNextLevel - s.Progression.XpIntoLevel);
            yield return Wait(0.3f);
            yield return Wait(2.2f);
            game.UI.CloseModal();
            yield return Wait(0.6f);

            // Factory overview: pan + zoom, then buy an upgrade.
            game.SetMode(CameraMode.Factory);
            yield return Wait(0.9f);
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                game.Cameras.Pan(new Vector2(-120f, 40f) * Time.deltaTime);
                game.Cameras.Zoom(1f + 0.25f * Time.deltaTime);
                yield return null;
            }
            game.UI.ShowUpgrades();
            yield return Wait(1.2f);
            game.BuyUpgrade(UpgradeDefinition.PressTiming);
            yield return Wait(1.3f);
            game.UI.CloseModal();
            game.SetMode(CameraMode.Character);
            yield return Wait(1.0f);

            // Optional timed special order.
            game.UI.ShowOrderPanel();
            yield return Wait(1.6f);
            game.AcceptSpecialOrder();
            yield return Wait(1.0f);
            yield return Walk(new Vector3(-0.6f, 0f, -1.0f));
            yield return Wait(1.2f);

            game.Player.ScriptedMove = null;
            SaveManager.FileNameOverride = null;
            Running = false;
            Finished?.Invoke();
            Destroy(gameObject);
        }

        static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        /// <summary>Walks Hue through the waypoints with scripted joystick input (real movement + animation).</summary>
        IEnumerator Walk(params Vector3[] points)
        {
            var player = game.Player;
            var cam = game.Cameras.Camera.transform;
            foreach (var target in points)
            {
                float timeout = Time.time + 6f;
                while (Time.time < timeout)
                {
                    Vector3 to = target - player.transform.position;
                    to.y = 0f;
                    if (to.magnitude < 0.25f) break;
                    Vector3 fwd = cam.forward; fwd.y = 0f; fwd.Normalize();
                    Vector3 right = cam.right; right.y = 0f; right.Normalize();
                    Vector3 dir = to.normalized;
                    float slow = Mathf.Clamp01(to.magnitude / 0.8f) * 0.6f + 0.4f;
                    player.ScriptedMove = new Vector2(Vector3.Dot(dir, right), Vector3.Dot(dir, fwd)) * slow;
                    yield return null;
                }
            }
            player.ScriptedMove = Vector2.zero;
            yield return null;
            player.ScriptedMove = null;
        }

        void Face(Interactable station)
        {
            Vector3 to = station.transform.position - game.Player.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) game.Player.transform.rotation = Quaternion.LookRotation(to);
        }
    }
}
