using Ror2Common;

namespace Samus
{
    // Melee characters' entries in the pause menu's controls window
    // (ror2-common PauseControls). Pad names match the buttons the native sim
    // reads (MeleePad), not RoR2's rebindable skill slots.
    internal static class MeleeSheets
    {
        public static ControlSheet SamusSheet()
        {
            var sheet = new ControlSheet
            {
                Subtitle = "Intergalactic Bounty Hunter",
                Footer = "X is also RoR2's interact: with a prompt up it goes to that, not the grapple.",
            };

            sheet.Pad("Moving")
                .Add("Left stick", "Walk, run")
                .Add("A", "Jump")
                .Add("Left stick click", "Sprint")
                .Add("X", "Aim the 20m grapple");
            sheet.Pad("Attacks")
                .Add("RT", "Charge Shot: tap again to fire, or store")
                .Add("LT", "Contextual A; LS + LT picks the attack, a flick smashes")
                .Add("RB", "Missile; a quick direction fires a Super Missile")
                .Add("LB", "Screw Attack")
                .Add("B", "Bomb, or the Shield / Air Feint alternate");
            sheet.Pad("Captured enemies")
                .Add("LS + LT / RT", "Throw them")
                .Add("Neutral LT / RT", "Pummel")
                .Add("X + LT, or A", "Let go; bosses pull Samus instead");

            sheet.Keys("Moving")
                .Add("Move", "Walk (Sprint: run)")
                .Add("Jump", "Jump")
                .Add("Interact", "Aim the 20m grapple");
            sheet.Keys("Attacks")
                .Add("Primary", "Charge Shot: tap again to fire, or store")
                .Add("Secondary", "Contextual A")
                .Add("Utility", "Missile; a quick direction fires a Super Missile")
                .Add("Special", "Screw Attack")
                .Add("C", "Bomb, or the Shield / Air Feint alternate");
            sheet.Keys("Captured enemies")
                .Add("Move + Secondary / Primary", "Throw them")
                .Add("Secondary / Primary", "Pummel")
                .Add("Interact + Secondary, or Jump", "Let go; bosses pull Samus instead");
            return sheet;
        }

        public static ControlSheet FoxSheet()
        {
            var sheet = new ControlSheet
            {
                Subtitle = "Leader of Star Fox",
                Footer = "RT is a straight laser with no charge; B reflects shots while it is out.",
            };

            sheet.Pad("Moving")
                .Add("Left stick", "Walk, run")
                .Add("A", "Jump")
                .Add("Left stick click", "Sprint");
            sheet.Pad("Attacks")
                .Add("RT", "Blaster: fire a straight laser")
                .Add("LT", "Contextual A; LS + LT picks the attack, a flick smashes")
                .Add("RB", "Fox Illusion: a quick horizontal dash")
                .Add("LB", "Fire Fox: charge and launch upward")
                .Add("B", "Reflector, or the Shield / Air Feint alternate");

            sheet.Keys("Moving")
                .Add("Move", "Walk (Sprint: run)")
                .Add("Jump", "Jump");
            sheet.Keys("Attacks")
                .Add("Primary", "Blaster: fire a straight laser")
                .Add("Secondary", "Contextual A")
                .Add("Utility", "Fox Illusion: a quick horizontal dash")
                .Add("Special", "Fire Fox: charge and launch upward")
                .Add("C", "Reflector, or the Shield / Air Feint alternate");
            return sheet;
        }

        public static ControlSheet LuigiSheet()
        {
            var sheet = new ControlSheet
            {
                Subtitle = "The Eternal Understudy",
                Footer = "RT is a bouncing fireball; B spins into the Luigi Cyclone.",
            };

            sheet.Pad("Moving")
                .Add("Left stick", "Walk, run")
                .Add("A", "Jump")
                .Add("Left stick click", "Sprint");
            sheet.Pad("Attacks")
                .Add("RT", "Fireball: fire a bouncing green fireball")
                .Add("LT", "Contextual A; LS + LT picks the attack, a flick smashes")
                .Add("RB", "Green Missile: a charged horizontal launch")
                .Add("LB", "Super Jump Punch: launch upward")
                .Add("B", "Luigi Cyclone, or the Shield / Air Feint alternate");

            sheet.Keys("Moving")
                .Add("Move", "Walk (Sprint: run)")
                .Add("Jump", "Jump");
            sheet.Keys("Attacks")
                .Add("Primary", "Fireball: fire a bouncing green fireball")
                .Add("Secondary", "Contextual A")
                .Add("Utility", "Green Missile: a charged horizontal launch")
                .Add("Special", "Super Jump Punch: launch upward")
                .Add("C", "Luigi Cyclone, or the Shield / Air Feint alternate");
            return sheet;
        }

        public static ControlSheet CaptainSheet()
        {
            var sheet = new ControlSheet
            {
                Subtitle = "F-Zero Racer",
                Footer = "RT is a slow flaming Falcon Punch; B kicks forward.",
            };

            sheet.Pad("Moving")
                .Add("Left stick", "Walk, run")
                .Add("A", "Jump")
                .Add("Left stick click", "Sprint");
            sheet.Pad("Attacks")
                .Add("RT", "Falcon Punch: a slow devastating punch")
                .Add("LT", "Contextual A; LS + LT picks the attack, a flick smashes")
                .Add("RB", "Raptor Boost: a quick horizontal lunge")
                .Add("LB", "Falcon Dive: launch upward")
                .Add("B", "Falcon Kick, or the Shield / Air Feint alternate");

            sheet.Keys("Moving")
                .Add("Move", "Walk (Sprint: run)")
                .Add("Jump", "Jump");
            sheet.Keys("Attacks")
                .Add("Primary", "Falcon Punch: a slow devastating punch")
                .Add("Secondary", "Contextual A")
                .Add("Utility", "Raptor Boost: a quick horizontal lunge")
                .Add("Special", "Falcon Dive: launch upward")
                .Add("C", "Falcon Kick, or the Shield / Air Feint alternate");
            return sheet;
        }

        public static ControlSheet LinkSheet()
        {
            var sheet = new ControlSheet
            {
                Subtitle = "Hero of Hyrule",
                Footer = "RT holds to draw the bow and release to fire; B lobs a bomb.",
            };

            sheet.Pad("Moving")
                .Add("Left stick", "Walk, run")
                .Add("A", "Jump")
                .Add("Left stick click", "Sprint");
            sheet.Pad("Attacks")
                .Add("RT", "Hero's Bow: hold to draw, release to fire")
                .Add("LT", "Contextual A; LS + LT picks the attack, a flick smashes")
                .Add("RB", "Boomerang: throw it forward")
                .Add("LB", "Spin Attack: launch upward")
                .Add("B", "Bomb, or the Shield / Air Feint alternate");

            sheet.Keys("Moving")
                .Add("Move", "Walk (Sprint: run)")
                .Add("Jump", "Jump");
            sheet.Keys("Attacks")
                .Add("Primary", "Hero's Bow: hold to draw, release to fire")
                .Add("Secondary", "Contextual A")
                .Add("Utility", "Boomerang: throw it forward")
                .Add("Special", "Spin Attack: launch upward")
                .Add("C", "Bomb, or the Shield / Air Feint alternate");
            return sheet;
        }

        public static ControlSheet PikachuSheet()
        {
            var sheet = new ControlSheet
            {
                Subtitle = "The Electric Mouse Pokemon",
                Footer = "Quick Attack homes on your lock-on (the enemy nearest your crosshair) and arrives in two zips; Thunder strikes Pikachu himself.",
            };

            sheet.Pad("Moving")
                .Add("Left stick", "Walk, run")
                .Add("A", "Jump")
                .Add("Left stick click", "Sprint");
            sheet.Pad("Attacks")
                .Add("RT", "Thunder Shock: a bouncing electric orb along your crosshair")
                .Add("LT", "Contextual A; LS + LT picks the attack, a flick smashes")
                .Add("RB", "Thunder: a lightning bolt on yourself")
                .Add("LB", "Quick Attack: two zips to your lock-on")
                .Add("B", "Skull Bash (hold to charge), or the Shield / Air Feint alternate");

            sheet.Keys("Moving")
                .Add("Move", "Walk (Sprint: run)")
                .Add("Jump", "Jump");
            sheet.Keys("Attacks")
                .Add("Primary", "Thunder Shock: a bouncing electric orb along your crosshair")
                .Add("Secondary", "Contextual A")
                .Add("Utility", "Thunder: a lightning bolt on yourself")
                .Add("Special", "Quick Attack: two zips to your lock-on")
                .Add("C", "Skull Bash (hold to charge), or the Shield / Air Feint alternate");
            return sheet;
        }
    }
}
