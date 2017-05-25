using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleUI;

namespace UIMenuAndCfgPair
{
    class MenuCfgPair
    {
        public UIMenu MainMenu { get; set; }
        public string CfgFileName { get; set; }

        public UIMenuItem ItemEnablePowers { get; set; }
        public UIMenuItem ItemEnablePowersWithPed { get; set; }
        public UIMenuItem ItemAddAlly { get; set; }
        public UIMenuItem ItemAddEnemy { get; set; }

        public UIMenu ShieldSubmenu { get; set; }
        public UIMenuItem ItemSaveCurrentWeapon { get; set; }
        public UIMenuItem ItemInitialThowForce { get; set; }
        public UIMenuItem ItemPShieldDamage { get; set; }
        public UIMenuItem ItemVShieldDamage { get; set; }
        public UIMenuItem ItemQuickReturnShield { get; set; }
        public UIMenuItem ItemReturnInterval { get; set; }
        public UIMenuItem ItemAllowShieldCurve { get; set; }
        public UIMenuItem ItemCurveForce { get; set; }
        public UIMenuItem ItemBackhandThrow { get; set; }

        public UIMenu SpecialsSubmenu { get; set; }
        public UIMenuItem ItemAllowSpecialAttacks { get; set; }
        public UIMenuItem ItemPChargeDamage { get; set; }
        public UIMenuItem ItemVChargeDamage { get; set; }
        public UIMenuItem ItemStrikingPowerMultiplier { get; set; }
        public UIMenuItem ItemAllowCustomMelee { get; set; }
        public UIMenuItem ItemAllowReflect { get; set; }
        public UIMenuItem ItemAllowTank { get; set; }
        public UIMenuItem ItemMaxLiftSizeMMcubed { get; set; }
        public UIMenuItem ItemMaxHealth { get; set; }
        public UIMenuItem ItemRegenHealthAmount { get; set; }
        public UIMenuItem ItemRegenInterval { get; set; }

        public UIMenu MobilitySubmenu { get; set; }
        public UIMenuItem ItemAllowSuperRun { get; set; }
        public UIMenuItem ItemRunAnimationMultiplier { get; set; }
        public UIMenuItem ItemSuperRunningVelocity { get; set; }
        public UIMenuItem ItemSuperSprintingVelocity { get; set; }
        public UIMenuItem ItemAllowSuperJump { get; set; }
        public UIMenuItem ItemJumpForwardForce { get; set; }
        public UIMenuItem ItemJumpUpwardForce { get; set; }
        public UIMenuItem ItemSafeFallHeight { get; set; }
        public UIMenuItem ItemAllowCombatRoll { get; set; }
        public UIMenuItem ItemRollSpeed { get; set; }

        public UIMenu MiscSubmenu { get; set; }
        public UIMenuItem ItemSaveCurrentPed { get; set; }
        public UIMenuItem ItemAllowSlowMoAim { get; set; }
        public UIMenuItem ItemAllowShieldOnBack { get; set; }
        public UIMenuItem ItemBackShieldPosX { get; set; }
        public UIMenuItem ItemBackShieldPosY { get; set; }
        public UIMenuItem ItemBackShieldPosZ { get; set; }
        public UIMenuItem ItemBackShieldRotX { get; set; }
        public UIMenuItem ItemBackShieldRotY { get; set; }
        public UIMenuItem ItemBackShieldRotZ { get; set; }
        public UIMenuItem ItemFxRed { get; set; }
        public UIMenuItem ItemFxGreen { get; set; }
        public UIMenuItem ItemFxBlue { get; set; }
        public UIMenuItem ItemFixCompatibilityWithFlash { get; set; }

        public UIMenuItem ItemReloadSettings { get; set; }
        public UIMenuItem ItemSaveSettings { get; set; }

        public MenuCfgPair(UIMenu mainMenu, string cfgFilename)
        {
            MainMenu = mainMenu;
            CfgFileName = cfgFilename;
        }
    }
}
