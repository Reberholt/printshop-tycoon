using System.Collections.Generic;

namespace PrintshopTycoon.Core
{
    /// <summary>
    /// Minimal string table. Danish is the primary language; English is prepared.
    /// Usage: Loc.T("hud.coins"), Loc.T("toast.paid", 150).
    /// </summary>
    public static class Loc
    {
        public static string Language = "da";

        static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>
        {
            // key                         Danish                                              English
            { "game.title",               new[] { "PRINTSHOP TYCOON",                           "PRINTSHOP TYCOON" } },
            { "menu.play",                new[] { "Spil",                                       "Play" } },
            { "menu.continue",            new[] { "Fortsæt",                                    "Continue" } },
            { "menu.settings",            new[] { "Indstillinger",                              "Settings" } },
            { "menu.reset",               new[] { "Nyt spil",                                   "New game" } },
            { "menu.reset_confirm",       new[] { "Slet al fremgang? Tryk igen for at bekræfte.", "Delete all progress? Tap again to confirm." } },
            { "menu.pause",               new[] { "Pause",                                      "Paused" } },
            { "menu.resume",              new[] { "Fortsæt",                                    "Resume" } },
            { "menu.save",                new[] { "Gem spil",                                   "Save game" } },
            { "menu.main",                new[] { "Hovedmenu",                                  "Main menu" } },
            { "menu.close",               new[] { "Luk",                                        "Close" } },
            { "menu.subtitle",            new[] { "Fra lille værksted til trykimperium",        "From tiny workshop to print empire" } },
            { "settings.sound",           new[] { "Lyd: {0}",                                   "Sound: {0}" } },
            { "settings.on",              new[] { "Til",                                        "On" } },
            { "settings.off",             new[] { "Fra",                                        "Off" } },
            { "settings.language",        new[] { "Sprog: Dansk",                               "Language: English" } },

            { "hud.level",                new[] { "Niveau {0}",                                 "Level {0}" } },
            { "hud.factory",              new[] { "Fabrik",                                     "Factory" } },
            { "hud.back_to_hue",          new[] { "Tilbage",                                    "Back" } },
            { "hud.build",                new[] { "Byg",                                        "Build" } },
            { "hud.staff",                new[] { "Ansatte",                                    "Staff" } },
            { "hud.orders",               new[] { "Ordrer",                                     "Orders" } },
            { "hud.stock",                new[] { "Lager: {0}",                                 "Stock: {0}" } },
            { "hud.autojobs",             new[] { "Småordrer: {0}",                             "Small orders: {0}" } },

            { "step.0",                   new[] { "Gå til ordretavlen og tag ordren",           "Go to the order board and accept the order" } },
            { "step.1",                   new[] { "Hent {0} blanke T-shirts på hylden",         "Get {0} blank T-shirts from the shelf" } },
            { "step.2",                   new[] { "Print logoet på DTF-printeren",              "Print the logo on the DTF printer" } },
            { "step.3",                   new[] { "Pres trykket på varmepressen",               "Press the transfers on the heat press" } },
            { "step.4",                   new[] { "Tag de færdige T-shirts fra pressen",        "Take the finished T-shirts off the press" } },
            { "step.5",                   new[] { "Pak ordren ved pakkebordet",                 "Pack the order at the packing table" } },
            { "order.title",              new[] { "{0}: {1} T-shirts",                          "{0}: {1} T-shirts" } },
            { "order.reward",             new[] { "Betaling: {0} mønter",                       "Payment: {0} coins" } },
            { "order.logo",               new[] { "Logo: {0}",                                  "Logo: {0}" } },
            { "order.accept",             new[] { "Tag ordren",                                 "Accept order" } },
            { "order.in_progress",        new[] { "I gang – trin {0} af {1}",                   "In progress – step {0} of {1}" } },
            { "order.panel_title",        new[] { "Ordretavle",                                 "Order board" } },

            { "customer.bakery",          new[] { "Nibe Bageri",                                "Nibe Bakery" } },
            { "customer.handball",        new[] { "Nibe Håndbold",                              "Nibe Handball" } },
            { "customer.fishing",         new[] { "Limfjordens Lystfiskere",                    "Limfjord Anglers" } },
            { "logo.bakery",              new[] { "Kringlen",                                   "The Pretzel" } },
            { "logo.handball",            new[] { "Håndbolden",                                 "The Handball" } },
            { "logo.fishing",             new[] { "Fisken",                                     "The Fish" } },

            { "station.shelf",            new[] { "Lagerhylde",                                 "Storage shelf" } },
            { "station.printer",          new[] { "DTF-printer",                                "DTF printer" } },
            { "station.packing",          new[] { "Pakkebord",                                  "Packing table" } },
            { "station.board",            new[] { "Ordretavle",                                 "Order board" } },
            { "machine.heat_press",       new[] { "Varmepresse",                                "Heat press" } },

            { "act.accept",               new[] { "Se ordre",                                   "View order" } },
            { "act.take_blanks",          new[] { "Tag T-shirts",                               "Take T-shirts" } },
            { "act.restock",              new[] { "Køb {0} stk. ({1})",                         "Buy {0} ({1})" } },
            { "act.print",                new[] { "Print logo",                                 "Print logo" } },
            { "act.press",                new[] { "Start presse",                               "Start press" } },
            { "act.collect",              new[] { "Tag T-shirts",                               "Collect" } },
            { "act.pack",                 new[] { "Pak ordre",                                  "Pack order" } },
            { "act.none",                 new[] { "",                                           "" } },

            { "logo.select_title",        new[] { "Vælg logoet kunden har bestilt",             "Pick the logo the customer ordered" } },
            { "press.title",              new[] { "Tryk STOP i den grønne zone!",               "Hit STOP in the green zone!" } },
            { "press.stop",               new[] { "STOP!",                                      "STOP!" } },
            { "press.perfect",            new[] { "PERFEKT!",                                   "PERFECT!" } },
            { "press.good",               new[] { "GODT!",                                      "GOOD!" } },
            { "press.miss",               new[] { "Øv – prøv igen!",                            "Oops – try again!" } },
            { "press.low",                new[] { "Lidt skævt… det går nok",                    "A bit off… it'll do" } },

            { "toast.paid",               new[] { "+{0} mønter!",                               "+{0} coins!" } },
            { "toast.no_coins",           new[] { "Ikke nok mønter",                            "Not enough coins" } },
            { "toast.no_stock",           new[] { "Hylden er tom – køb flere T-shirts",         "The shelf is empty – buy more T-shirts" } },
            { "toast.wrong_logo",         new[] { "Forkert logo – tjek ordren",                 "Wrong logo – check the order" } },
            { "toast.station_busy",       new[] { "Pressen er optaget",                         "The press is busy" } },
            { "toast.wrong_press",        new[] { "T-shirtene ligger på en anden presse",       "The shirts are on another press" } },
            { "toast.invalid_place",      new[] { "Maskinen kan ikke stå her",                  "The machine can't go here" } },
            { "toast.bought",             new[] { "Købt: {0}",                                  "Bought: {0}" } },
            { "toast.hired",              new[] { "Ny medarbejder ansat!",                      "New worker hired!" } },
            { "toast.saved",              new[] { "Spillet er gemt",                            "Game saved" } },
            { "toast.save_failed",        new[] { "Kunne ikke gemme",                           "Could not save" } },
            { "toast.restocked",          new[] { "+{0} blanke T-shirts",                       "+{0} blank T-shirts" } },
            { "toast.limit",              new[] { "Maks. antal nået",                           "Limit reached" } },

            { "shop.title",               new[] { "Byg maskiner",                               "Build machines" } },
            { "shop.buy",                 new[] { "Køb ({0})",                                  "Buy ({0})" } },
            { "shop.soon",                new[] { "Flere maskiner kommer snart",                "More machines coming soon" } },
            { "place.hint",               new[] { "Træk maskinen på plads",                     "Drag the machine into place" } },
            { "place.confirm",            new[] { "Placér ({0})",                               "Place ({0})" } },
            { "place.cancel",             new[] { "Annullér",                                   "Cancel" } },
            { "place.invalid",            new[] { "Ugyldig placering",                          "Invalid position" } },

            { "staff.title",              new[] { "Ansatte",                                    "Staff" } },
            { "staff.hire",               new[] { "Ansæt medarbejder ({0})",                    "Hire worker ({0})" } },
            { "staff.worker",             new[] { "Medarbejder {0}",                            "Worker {0}" } },
            { "staff.assign",             new[] { "Skift presse",                               "Change press" } },
            { "staff.unassigned",         new[] { "Ikke tildelt",                               "Unassigned" } },
            { "staff.at",                 new[] { "Ved: {0}",                                   "At: {0}" } },
            { "staff.none",               new[] { "Ingen ansatte endnu",                        "No staff yet" } },
            { "staff.explain",            new[] { "Medarbejdere klarer småordrer helt selv, når der er T-shirts på hylden.", "Workers handle small orders on their own when the shelf has T-shirts." } },

            { "worker.idle",              new[] { "Venter",                                     "Waiting" } },
            { "worker.moving",            new[] { "På vej",                                     "Walking" } },
            { "worker.working",           new[] { "Presser",                                    "Pressing" } },
            { "worker.packing",           new[] { "Pakker",                                     "Packing" } },
            { "worker.no_jobs",           new[] { "Ingen ordrer",                               "No orders" } },
            { "worker.no_stock",          new[] { "Mangler T-shirts",                           "Needs T-shirts" } },
            { "worker.busy",              new[] { "Pressen er optaget",                         "Press busy" } },
            { "worker.unassigned",        new[] { "Ikke tildelt",                               "Unassigned" } },

            { "info.occupied_by_player",  new[] { "I brug af Hue",                              "Used by Hue" } },
            { "info.free",                new[] { "Ledig",                                      "Free" } },
            { "info.worker",              new[] { "Medarbejder: {0}",                           "Worker: {0}" } },

            { "silke.name",               new[] { "Silke",                                      "Silke" } },
            { "silke.0",                  new[] { "Hej Hue! Vores første kunde har bestilt T-shirts. Gå hen til ordretavlen!", "Hi Hue! Our first customer ordered T-shirts. Head to the order board!" } },
            { "silke.1",                  new[] { "Super! Hent blanke T-shirts på hylden. Følg den gule pil.", "Great! Grab blank T-shirts from the shelf. Follow the yellow arrow." } },
            { "silke.2",                  new[] { "Nu skal logoet printes på DTF-printeren. Vælg det rigtige!", "Now print the logo on the DTF printer. Pick the right one!" } },
            { "silke.3",                  new[] { "Varmepressen! Stop viseren i den grønne zone.", "The heat press! Stop the marker in the green zone." } },
            { "silke.4",                  new[] { "Flot! Tag T-shirtene af pressen.",           "Nice! Take the shirts off the press." } },
            { "silke.5",                  new[] { "Sidste trin: pak ordren ved pakkebordet.",   "Last step: pack the order at the packing table." } },
            { "silke.done",               new[] { "Første ordre klaret! Tryk på Fabrik og byg en ekstra varmepresse.", "First order done! Tap Factory and build a second heat press." } },
            { "silke.hire",               new[] { "Godt købt! Ansæt nu en medarbejder under Ansatte.", "Good buy! Now hire a worker under Staff." } },
            { "silke.auto",               new[] { "Medarbejderen klarer småordrer selv. Hold hylden fyldt!", "The worker handles small orders. Keep the shelf stocked!" } },
        };

        public static int LanguageIndex => Language == "en" ? 1 : 0;

        public static string T(string key)
        {
            if (key != null && table.TryGetValue(key, out var v)) return v[LanguageIndex];
            return key;
        }

        public static string T(string key, params object[] args)
        {
            string format = T(key);
            try { return string.Format(format, args); }
            catch (System.FormatException) { return format; }
        }

        public static bool Has(string key) => key != null && table.ContainsKey(key);
    }
}
