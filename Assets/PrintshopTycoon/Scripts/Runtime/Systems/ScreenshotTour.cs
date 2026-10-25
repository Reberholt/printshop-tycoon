using System.Collections;
using System.IO;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Plays through the main screens and saves PNGs (Screenshots/ next to Assets/).
    /// Start it in Play Mode from the menu "Printshop Tycoon > Capture Screenshot Tour".
    /// Set the Game view to 1080x1920 first. Uses a throwaway save so real progress is untouched.
    /// </summary>
    public class ScreenshotTour : MonoBehaviour
    {
        const string TourSave = "printshop_tycoon_screenshot_tour.json";
        GameManager game;
        string folder;

        public static void Begin()
        {
            if (FindFirstObjectByType<ScreenshotTour>() != null) return;
            new GameObject("ScreenshotTour").AddComponent<ScreenshotTour>();
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(folder);

            // Fresh game on a throwaway save.
            SaveManager.FileNameOverride = TourSave;
            var old = FindFirstObjectByType<GameManager>();
            if (old != null) Destroy(old.gameObject);
            foreach (var name in new[] { "Workshop", "Dynamic" })
            {
                var go = GameObject.Find(name);
                if (go != null) Destroy(go);
            }
            yield return null;
            new SaveManager().Delete();
            game = new GameObject("PrintshopTycoon").AddComponent<GameManager>();
            yield return Wait(1f);
            var s = game.Session;

            yield return Shot("01_main_menu");
            game.UI.CloseModal();
            yield return Wait(1.5f);
            yield return Shot("02_workshop");

            game.UI.ShowOrderPanel();
            yield return Wait(0.6f);
            yield return Shot("03_order_board");
            s.Orders.Accept();
            game.UI.CloseModal();

            game.Player.Teleport(game.Shelf.ApproachPosition, 0f);
            game.Shelf.Interact();
            game.Player.Teleport(game.Printer.ApproachPosition + Vector3.back * 0.3f, 0f);
            yield return Wait(1f);
            yield return Shot("04_carrying_blanks");

            s.Orders.SelectLogo(s.Orders.Order.LogoId);
            yield return Wait(s.Upgrades.PrintSeconds * 0.5f);
            yield return Shot("05_printing");
            yield return Wait(s.Upgrades.PrintSeconds * 0.6f);
            game.Printer.Interact();

            var press = game.Presses[0];
            game.Player.Teleport(press.ApproachPosition, 0f);
            press.Interact();
            yield return Wait(0.7f);
            yield return Shot("06_heat_press_minigame");
            press.ApplyPressResult(PressResult.Perfect);
            game.UI.CloseModal();
            yield return Wait(0.8f);
            yield return Shot("07_pressing");
            yield return Wait(s.Orders.PressRemaining + 0.3f);
            press.Interact();

            game.Player.Teleport(game.Packing.ApproachPosition, -90f);
            yield return Wait(0.5f);
            yield return Shot("08_carrying_finished");
            game.Packing.Interact();
            yield return Wait(1.8f);
            yield return Shot("09_order_complete");
            game.UI.CloseModal();

            game.SetMode(CameraMode.Factory);
            yield return Wait(1.5f);
            yield return Shot("10_factory_overview");
            game.UI.ShowUpgrades();
            yield return Wait(0.6f);
            yield return Shot("11_upgrades");
            game.UI.CloseModal();
            game.SetMode(CameraMode.Character);
            yield return Wait(1f);

            s.Progression.AddXp(s.Progression.XpForNextLevel - s.Progression.XpIntoLevel);
            yield return Wait(1f);
            yield return Shot("12_level_up");
            game.UI.CloseModal();
            yield return Wait(0.5f);

            game.UI.ShowOrderPanel();
            yield return Wait(0.6f);
            yield return Shot("13_special_order_offer");
            game.AcceptSpecialOrder();
            yield return Wait(1f);
            yield return Shot("14_special_order_running");

            Debug.Log("[PrintshopTycoon] Screenshot tour saved to " + folder);
            SaveManager.FileNameOverride = null;
            Destroy(gameObject);
        }

        static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(folder, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[PrintshopTycoon] Screenshot " + path + " (" + Screen.width + "x" + Screen.height + ")");
            yield return null;
        }
    }
}
