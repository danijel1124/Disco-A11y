using System.Collections.Generic;

namespace AccessibilityMod.Settings
{
    /// <summary>
    /// What each area looks like, in two sentences, for players who cannot see it.
    ///
    /// The game names an area when you enter it and shows you the rest. This table is the
    /// rest: the first sentence says what kind of place it is (room, light, condition), the
    /// second how it is laid out (exits, stairs, fixed furniture) - the part a sighted
    /// player grasps in a second and a blind one never gets at all.
    ///
    /// Every entry was written after actually looking at the place through the dev bridge
    /// (teleport in, screenshot, read it). None of it is written from memory of the game,
    /// because a description that sounds plausible and is wrong is worse than none: the
    /// player has no way to catch the error.
    ///
    /// First-impression rule: only what is permanently true of the place. No story events,
    /// no people who are only there because of a quest, no names of characters the player
    /// has not met. The descriptions are read on entering an area and can be repeated with
    /// a key - they must not spoil what happens there.
    ///
    /// This is content keyed by scene, not logic keyed by scene: the mechanism stays global
    /// ("if the current scene has a description, speak it"), and an area without an entry
    /// simply gets none.
    /// </summary>
    public static class AreaDescriptions
    {
        /// <summary>
        /// A speakable name per area. The scene names are internal ("Pawnshop-int",
        /// "Capeside-coalchamber-int") and were being read out raw, half-English and
        /// suffixed. The names avoid characters the player may not have met yet - a shack
        /// is "the shack in the yard", not somebody's shack.
        /// </summary>
        private static readonly Dictionary<string, (string En, string De)> Names = new()
        {
            ["Martinaise-ext"] = ("Martinaise", "Martinaise"),
            ["Whirling-int-f1"] = ("Whirling-in-Rags, ground floor", "Whirling-in-Rags, Erdgeschoss"),
            ["Whirling-int-f2"] = ("Whirling-in-Rags, guest floor", "Whirling-in-Rags, Gästegeschoss"),
            ["Church-int"] = ("The church", "Die Kirche"),
            ["Kiosque-int"] = ("The kiosk", "Der Kiosk"),
            ["Pawnshop-int"] = ("The pawnshop", "Das Pfandhaus"),
            ["Doomed-commerce-int-f1"] = ("Doomed commercial area, bookshop", "Verfluchter Gewerbeblock, Buchhandlung"),
            ["Doomed-commerce-int-f2"] = ("Doomed commercial area, upper floor", "Verfluchter Gewerbeblock, Obergeschoss"),
            ["Feld-int"] = ("The Feld building", "Das Feld-Gebäude"),
            ["Cunos-shack-int"] = ("The shack in the yard", "Die Bude im Hinterhof"),
            ["Secretary-int"] = ("The office", "Das Büro"),
            ["Capeside-apartments-int"] = ("Capeside apartments", "Wohnblock am Kap"),
            ["Capeside-coalchamber-int"] = ("The coal cellar", "Der Kohlenkeller"),
            ["Capeside-smoker-int"] = ("A flat in the apartments", "Eine Wohnung im Wohnblock"),
            ["Sea-fortress-int"] = ("The sea fortress", "Die Meeresfestung"),
            ["Tent-int"] = ("The tent", "Das Zelt"),
            ["FV-house-int"] = ("A house in the fishing village", "Ein Haus im Fischerdorf"),
            ["FV-shack-int"] = ("A shack in the fishing village", "Eine Bude im Fischerdorf"),
            ["Second-home-int"] = ("A hut in the fishing village", "Eine Hütte im Fischerdorf"),
            ["Crypto-garys-apt-int"] = ("A flat above the yard", "Eine Wohnung über dem Hof"),
            ["Commustudent-int"] = ("The basement room", "Der Kellerraum"),
            ["Union-boss-int"] = ("The container office", "Das Container-Büro"),
            ["Instigators-lair-int"] = ("The hideout", "Das Versteck"),
            ["Capeside-wcw-int"] = ("A family flat in the apartments", "Eine Familienwohnung im Wohnblock"),
            ["Doomed-commerce-int-s1"] = ("Behind the doomed commercial area", "Hinter dem verfluchten Gewerbeblock"),
        };

        /// <summary>The area's spoken name, or null - then the caller falls back to the scene name.</summary>
        public static string GetName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            if (!Names.TryGetValue(sceneName, out var entry)) return null;
            return Loc.IsGerman ? entry.De : entry.En;
        }

        private static readonly Dictionary<string, (string En, string De)> Table = new()
        {
            ["Martinaise-ext"] = (
                "The coastal district of Martinaise under a grey sky: wet cobblestones, battered facades, board fences and scaffolding, and a waterfront path down to the sea. It is one large, connected district - the hotel, the church, the fishing village, the harbour and the boarded-up shops all lie along the same streets.",
                "Das Küstenviertel Martinaise unter grauem Himmel: nasses Kopfsteinpflaster, angeschlagene Fassaden, Bretterzäune und Baugerüste, dazu ein Uferweg hinunter zum Meer. Es ist ein einziger großer, zusammenhängender Bezirk — Hotel, Kirche, Fischerdorf, Hafen und die verrammelte Ladenzeile liegen alle an denselben Straßen."),

            ["Whirling-int-f1"] = (
                "The cafeteria on the hotel's ground floor: chequerboard tiles, long wooden tables with benches, warm strip lights on a brick pillar. A glazed veranda full of plants runs along one side, the bar counter along the other; a stairwell and a lift lead to the floors above.",
                "Das Café im Erdgeschoss des Hotels: Schachbrettfliesen, lange Holztische mit Bänken, warmes Licht von Leuchtstreifen an einem gemauerten Pfeiler. An der einen Seite zieht sich eine verglaste Veranda voller Pflanzen entlang, an der anderen die Theke; ein Treppenhaus und ein Aufzug führen in die oberen Stockwerke."),

            ["Whirling-int-f2"] = (
                "The guest floor above the cafeteria: a curved corridor with patterned tiled walls, looking down into the room below like a gallery. The guest room doors lead off it, a staircase carries on upwards, and behind a glass front lies the balcony.",
                "Das Gästegeschoss über dem Café: ein gebogener Gang mit gemusterten Kachelwänden, der wie eine Galerie in den Gastraum hinunterblickt. Von ihm gehen die Zimmertüren ab, eine Treppe führt weiter nach oben, und hinter einer Glasfront liegt der Balkon."),

            ["Church-int"] = (
                "An abandoned wooden church, dark and draughty, its roof held up by heavy beams and scaffolding. A large crucifix hangs over the central aisle, and the far end of the hall opens into cold, deep shadow.",
                "Eine verlassene Holzkirche, dunkel und zugig, das Dach von schweren Balken und Gerüsten getragen. Über dem Mittelgang hängt ein großes Kruzifix, und das hintere Ende der Halle verliert sich in kaltem Halbdunkel."),

            ["Kiosque-int"] = (
                "A cramped corner shop, brightly lit and stacked full: shelves of bottles, a chilled display counter, newspapers, a radio. A window front with flower boxes faces the street, and the counter with the till stands at the back.",
                "Ein enger Eckladen, hell erleuchtet und vollgestellt: Regale voller Flaschen, eine gekühlte Auslage, Zeitungen, ein Radio. Zur Straße hin eine Fensterfront mit Blumenkästen, hinten der Tresen mit der Kasse."),

            ["Pawnshop-int"] = (
                "A pawnshop crammed from floor to ceiling with other people's things: radios, clocks, coats, cabinets, a stuffed animal or two. A glass counter stands in the middle, and the room is so full that there is barely a path through it.",
                "Ein Pfandhaus, vom Boden bis zur Decke mit dem Kram anderer Leute vollgestopft: Radios, Uhren, Mäntel, Vitrinen. In der Mitte steht eine Glastheke, und der Raum ist so voll, dass kaum ein Weg hindurchführt."),

            ["Doomed-commerce-int-f1"] = (
                "A bookshop with tall shop windows, shelves to the ceiling and a table of books laid out in the middle. A curved staircase leads up to a railed gallery with plants; the floor is chequerboard tile.",
                "Eine Buchhandlung mit hohen Schaufenstern, Regalen bis unter die Decke und einem Tisch voller ausgelegter Bände. Eine geschwungene Treppe führt hinauf auf eine Galerie mit Geländer und Pflanzen; der Boden ist mit Schachbrettfliesen belegt."),

            ["Doomed-commerce-int-f2"] = (
                "A makeshift gym on the floor above the shops: a weight bench, barbells, a punching bag on the wall, a mat in the middle of the plank floor. Light falls in at an angle through tall lattice windows, and a door leads back to the stairwell.",
                "Ein improvisierter Kraftraum über den Läden: eine Hantelbank, Langhanteln, ein Sandsack an der Wand, eine Matte auf dem Dielenboden. Durch hohe Sprossenfenster fällt schräges Licht, eine Tür führt zurück ins Treppenhaus."),

            ["Feld-int"] = (
                "The inside of a gutted concrete building: cracked walls, rubble underfoot, a broken staircase with a rusted handrail leading up. Pale light falls through a tall, empty arched window; otherwise it is dark and cold in here.",
                "Das Innere eines ausgeweideten Betonbaus: rissige Wände, Schutt am Boden, eine gebrochene Treppe mit rostigem Geländer, die nach oben führt. Durch ein hohes, leeres Bogenfenster fällt fahles Licht; sonst ist es hier dunkel und kalt."),

            ["Cunos-shack-int"] = (
                "A shelter nailed together from boards and pallets, with a floor of trodden sand. A sagging armchair sits raised on a platform, graffiti covers the corrugated wall, and a ladder leads up to the roof.",
                "Ein aus Brettern und Paletten zusammengenagelter Unterschlupf, der Boden aus festgetretenem Sand. Ein durchgesessener Sessel steht erhöht auf einem Podest, an der Wellblechwand prangt Graffiti, und eine Leiter führt aufs Dach."),

            ["Secretary-int"] = (
                "An office that is also a living room: green upholstered chairs, a desk with a typewriter, potted plants, a rug. A long band of windows lets in daylight, and a tiled bathroom lies off to the side.",
                "Ein Büro, das zugleich Wohnzimmer ist: grüne Polstersessel, ein Schreibtisch mit Schreibmaschine, Topfpflanzen, ein Teppich. Eine lange Fensterfront lässt Tageslicht herein, nebenan liegt ein gekacheltes Bad."),

            ["Capeside-apartments-int"] = (
                "The stairwell of an apartment block: chequerboard tiles, peeling paint, doors with signs on them - balcony, exit. Beyond them opens a flat with plank flooring and a ceiling fan, and a staircase runs on to the upper floors.",
                "Das Treppenhaus eines Wohnblocks: Schachbrettfliesen, abblätternde Farbe, Türen mit Schildern - Balkon, Ausgang. Dahinter öffnet sich eine Wohnung mit Dielenboden und Deckenventilator, und eine Treppe führt weiter in die oberen Stockwerke."),

            ["Capeside-coalchamber-int"] = (
                "A coal cellar under the building: a heap of coal, rusted barrels, a big corroded boiler. Thick pipes run across the floor, a round grate sits in the middle, and a narrow staircase leads back up.",
                "Ein Kohlenkeller unter dem Haus: ein Haufen Kohle, rostige Fässer, ein großer, zerfressener Heizkessel. Dicke Rohre laufen über den Boden, in der Mitte liegt ein rundes Gitter, und eine schmale Treppe führt wieder hinauf."),

            ["Capeside-smoker-int"] = (
                "A small, tidy flat: a bed with a canopy and green drapes, and beside it a kitchen with a sink and a pegboard of tools. A dining table stands by the window, with sunlight falling across it.",
                "Eine kleine, aufgeräumte Wohnung: ein Bett mit Baldachin und grünen Vorhängen, daneben eine Küche mit Spüle und einem Werkzeugbrett. Am Fenster steht ein Esstisch, über den die Sonne fällt."),

            ["Sea-fortress-int"] = (
                "The inside of a concrete sea fortress: damp, dark, rusted metal everywhere. A massive gear mechanism stands behind a railing, and stairs and walkways lead deeper into the structure.",
                "Das Innere einer Meeresfestung aus Beton: feucht, dunkel, überall rostiges Metall. Hinter einem Geländer steht ein mächtiges Zahnradgetriebe, Treppen und Laufstege führen tiefer in den Bau."),

            ["Tent-int"] = (
                "A tent of tarpaulins stretched over poles, lit from inside by a single work lamp. Rugs and crates cover the ground, with gas canisters and a loudspeaker among them; the entrance is just a slit in the canvas.",
                "Ein Zelt aus Planen über einem Gestänge, von einer einzelnen Arbeitsleuchte erhellt. Auf dem Boden liegen Teppiche und Kisten, dazwischen Gaskanister und ein Lautsprecher; der Eingang ist nur ein Schlitz in der Plane."),

            ["FV-house-int"] = (
                "A fisherman's cottage: plank walls, a wooden floor, shelves of preserving jars, yellow curtains at small windows. A bed stands in one corner and a lit stove in the other; the room is low and warm.",
                "Eine Fischerkate: Bretterwände, Dielenboden, Regale voller Einmachgläser, gelbe Vorhänge an kleinen Fenstern. In der einen Ecke steht ein Bett, in der anderen ein brennender Ofen; der Raum ist niedrig und warm."),

            ["FV-shack-int"] = (
                "An empty board shack, dark and bare: raw wooden walls, a single chair, one small window. Apart from the door you came through, there is nothing here.",
                "Eine leere Bretterbude, dunkel und kahl: nackte Holzwände, ein einzelner Stuhl, ein kleines Fenster. Außer der Tür, durch die du gekommen bist, gibt es hier nichts."),

            ["Second-home-int"] = (
                "A cramped hut under a sloping roof: a bed pushed under the slope, a table with two chairs, a stove with its pipe running out through the roof. Everything is timber, and roughly built.",
                "Eine enge Hütte unter einem Schrägdach: ein Bett unter die Schräge geschoben, ein Tisch mit zwei Stühlen, ein Ofen, dessen Rohr durchs Dach führt. Alles ist aus Holz und grob gezimmert."),

            ["Crypto-garys-apt-int"] = (
                "A small two-room flat: a kitchen with a chequerboard floor at the front, a blue-painted bedroom with an iron bedstead at the back. Papers and clutter are piled everywhere, and a thick ventilation duct runs through the place.",
                "Eine kleine Zweizimmerwohnung: vorne eine Küche mit Schachbrettboden, hinten ein blau gestrichenes Schlafzimmer mit eisernem Bettgestell. Überall stapeln sich Papiere und Kram, und ein dickes Lüftungsrohr zieht sich durch den Raum."),

            ["Commustudent-int"] = (
                "A basement room in a ruin, turned into a meeting place: folding chairs set out in rows, a sagging sofa, a rug over cracked concrete. Banners hang on the brick wall, and an easel stands beside them.",
                "Ein Kellerraum in einer Ruine, zum Versammlungsort gemacht: aufgestellte Klappstühle, ein durchgesessenes Sofa, ein Teppich auf rissigem Beton. An der Ziegelwand hängen Banner, daneben steht eine Staffelei."),

            ["Union-boss-int"] = (
                "An office built out of stacked shipping containers: a steel frame, red curtains for walls, folding chairs and gas bottles. The floor is bare and the room echoes; an open container door leads back outside.",
                "Ein Büro aus gestapelten Seecontainern: ein Stahlrahmen, rote Vorhänge als Wände, Klappstühle und Gasflaschen. Der Boden ist blank und der Raum hallt; eine offene Containertür führt wieder nach draußen."),

            ["Instigators-lair-int"] = (
                "A hideout under tarpaulins and wooden props, with a floor of sand. Sharp stripes of light fall through the gaps overhead; otherwise it is dark and quiet in here.",
                "Ein Versteck unter Planen und Holzstreben, der Boden aus Sand. Durch Ritzen im Dach fallen scharfe Lichtstreifen; sonst ist es hier dunkel und still."),

            ["Capeside-wcw-int"] = (
                "A family's flat: a bedroom with a bed under a window full of daylight, a kitchen with pots on the shelves, a child's room with a toy on the wall. It is small, lived-in and warm.",
                "Die Wohnung einer Familie: ein Schlafzimmer mit einem Bett unter einem Fenster voller Tageslicht, eine Küche mit Töpfen in den Regalen, ein Kinderzimmer mit Spielzeug an der Wand. Sie ist klein, bewohnt und warm."),

            ["Doomed-commerce-int-s1"] = (
                "The yard behind the shops: bare concrete, a brick wall, pipes and a rusty tank, planks and junk lying about. It is dark back here, lit only from the doorway.",
                "Der Hof hinter den Läden: nackter Beton, eine Ziegelwand, Rohre und ein rostiger Tank, dazwischen Bretter und Gerümpel. Hier hinten ist es dunkel, Licht kommt nur aus der Tür."),
        };

        /// <summary>
        /// The long, first-visit introduction: not what the room looks like, but where you
        /// ARE - what kind of place this is and how it connects to the world around it.
        ///
        /// This exists because the short description was not enough: the player woke up in
        /// "a curved corridor with patterned tiled walls" and had no way of knowing he was
        /// in a hostel above a bar until he walked downstairs and the loot names gave it
        /// away. A sighted player reads all of that off the room in seconds - numbered
        /// doors, a corridor of them, guests below. Spoken once, the first time an area is
        /// entered; repeatable any time with its own key.
        /// </summary>
        private static readonly Dictionary<string, (string En, string De)> Intros = new()
        {
            ["Whirling-int-f2"] = (
                "You are on the guest floor of the Whirling-in-Rags, a hostel in the coastal district of Martinaise: a bar room downstairs, rented rooms up here. Your room is one of them; outside it a curved corridor connects the other guest room doors, a staircase leads down into the cafeteria, and behind the glass front at the end lies a balcony over the street.",
                "Du bist im Gästegeschoss des Whirling-in-Rags, eines Gasthauses im Küstenviertel Martinaise: unten der Schankraum, hier oben die vermieteten Zimmer. Dein Zimmer ist eines davon; davor verbindet ein gebogener Flur die übrigen Zimmertüren, eine Treppe führt hinunter ins Café, und hinter der Glasfront am Ende liegt ein Balkon über der Straße."),

            ["Whirling-int-f1"] = (
                "The ground floor of the Whirling-in-Rags: a cafeteria and bar where Martinaise eats and drinks. Guests sit at the long tables, the counter is staffed, and a glazed veranda runs along the front; the stairwell leads up to the guest rooms, and the main door leads out onto the square.",
                "Das Erdgeschoss des Whirling-in-Rags: ein Café mit Bar, in dem Martinaise isst und trinkt. An den langen Tischen sitzen Gäste, die Theke ist besetzt, vorne verläuft eine verglaste Veranda; das Treppenhaus führt hinauf zu den Zimmern, die Haupttür hinaus auf den Platz."),

            ["Martinaise-ext"] = (
                "Martinaise is a run-down coastal district of the city of Revachol - once grand, long neglected: cracked facades, boarded-up shops, scaffolding nobody works on. From the square by the hostel, streets lead north towards the old church and the fishing huts by the water, east into the harbour quarter, and paths run down to the sea. Most of the district can be walked; doors and gates that matter will say so.",
                "Martinaise ist ein heruntergekommenes Küstenviertel der Stadt Revachol - einst herrschaftlich, lange vernachlässigt: rissige Fassaden, verrammelte Läden, Gerüste, an denen niemand arbeitet. Vom Platz am Gasthaus führen Straßen nach Norden zur alten Kirche und zu den Fischerhütten am Wasser, nach Osten ins Hafenviertel, und Wege hinunter ans Meer. Das meiste ist zu Fuß erreichbar; Türen und Tore, die wichtig sind, melden sich."),
        };

        /// <summary>The description for a scene, or null when we have not seen that area.</summary>
        public static string Get(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            if (!Table.TryGetValue(sceneName, out var entry)) return null;
            return Loc.IsGerman ? entry.De : entry.En;
        }

        /// <summary>The long first-visit introduction, or null when the area has none yet.</summary>
        public static string GetIntro(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            if (!Intros.TryGetValue(sceneName, out var entry)) return null;
            return Loc.IsGerman ? entry.De : entry.En;
        }

        public static bool Has(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && Table.ContainsKey(sceneName);
    }
}
