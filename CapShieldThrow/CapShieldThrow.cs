using GTA; // This is a reference that is needed! do not edit this
using GTA.Native; // This is a reference that is needed! do not edit this
using GTA.Math;
using Control = GTA.Control;
using System; // This is a reference that is needed! do not edit this
using System.Windows.Forms; // This is a reference that is needed! do not edit this
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Drawing;
using SimpleUI;
using CfgHelper;
using UIMenuAndCfgPair;
using System.Linq;
using ScriptCommunicatorHelper;

namespace CapShieldThrow
{
    public class CapShieldThrow : Script // declare Modname as a script
    {
        enum ragdollType
        {
            Normal = 0,
            StiffBody = 1,
            NarrowLegStumble = 2,
            WideLegStuble = 3
        }

        enum ragdollBlock
        {
            WhenShot = 1,
            WhenHitByVehicle,
            WhenSetOnFire = 4
        }

        List<PoweredUser> PoweredUsers = new List<PoweredUser>();
        List<ProfileSetting> ProfileSettings = new List<ProfileSetting>();

        List<Entity> targettedEntities = new List<Entity>();
        List<Entity> reflectedProps = new List<Entity>();
        List<controlledProps> controlledReflectedProps = new List<controlledProps>();

        bool CapAbilities;
        Notification notification;
        RaycastResult RayCastCap;
        int VillianGroup;

        Ped player;
        Prop weapBackProp;

        /*Model holdWeap = 0x758A5090;
        Prop weapProp;
        WeaponHash CapShield;
        private static readonly Random rng = new Random();
        int rInt = 0;*/

        Entity grabbedEntity;
        bool hasGrabbedEntity;
        Vehicle Hand2HeadObj;

        Prop ballShield1;
        Prop ballShield2;
        Prop ballShield3;
        Prop ballShieldBack1;
        Prop ballShieldBack2;
        bool ballShieldExistsOnBack;

        Entity _TargettedEntity;
        Entity _HomedEntity;

        float tackleRotx = 0;
        float tackleRoty = 0;
        float tackleRotz = 0;
        
        bool isOnBike;
        bool TankProofON;
        bool allowRoll;
        bool wasThrownOnFoot;
        bool InitiatedSlowMo;
        bool hasAutoTarget;
        int keepTargetInterval;
        int cleanTimer;
        int ragdollRecoveryTimer;
        bool firstTimeRagdollRecovery;

        int InputTimer;
        static int InputWait = 150;

        int controlledPropTimer;

        //Model IMMRocketModel = "0xE62DC548";

        string lastThrowDir = "NADA";
        static string vehThrowDict = "veh@drivebybike@police@front@grenade";
        static string vehMeleeDict = "anim@veh@drivebybike@dirt@front@melee_1h";

        /* For testing:
        bool propIsCreated;
        */

        float offsetX = 0.23f;
        float offsetY = -0.09f;
        float offsetZ = 0.2f;
        float bloodRotX = 0;
        float bloodRotY = 0;
        float bloodRotZ = 0;

        float rotateX = -40;
        float rotateY = -8;
        float rotateZ = 70;

        float increment = 0.001f;

        //INI settings
        CultureInfo culture;
        Keys throwKey = Keys.X;
        Keys reflectKey = Keys.R;
        Keys grabKey = Keys.E;
        Keys SpecialSwitchKey = Keys.T;
        GTA.Control throwButton = GTA.Control.Cover;
        GTA.Control SpecialAttackButton = GTA.Control.Attack;
        GTA.Control reflectButton = GTA.Control.MeleeAttackLight;
        GTA.Control grabButton = GTA.Control.Talk;
        GTA.Control SpecialSwitchButton = GTA.Control.PhoneDown;

        MenuPool _menuPool;

        UIMenu capMenu;
        UIMenuItem ItemDisablePowers;
        UIMenuItem ItemDeleteAllies;
        UIMenuItem ItemDeleteEnemies;
        UIMenu ControlsMenu;
        UIMenuItem ItemReloadControlsCFG;

        public CapShieldThrow() // main function
        {
            SetPlayerAsFirstUser();
            SetupKeyboardCulture();
            CreateSCMOD();
            LoadControls();
            LoadMergerINI();
            //LoadINIProfile("1 - Current Player");
            CollectCustomSettings();
            InitMenu();

            VillianGroup = World.AddRelationshipGroup("Villians");

            Tick += OnTick;
            Tick += OnTickAI;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;

            Interval = 0;
        }

        void CreateSCMOD()
        {
            if (!File.Exists(@"scripts\CapShieldThrow.scmod"))
            {
                using (StreamWriter writer = new StreamWriter(@"scripts\CapShieldThrow.scmod"))
                {
                    writer.WriteLine("Captain America Script");
                    writer.WriteLine("Version 2.4");
                }
            }
        }

        void SetPlayerAsFirstUser()
        {
            while (Game.Player.Character == null) { Wait(250); }
            while (!Game.Player.Character.Exists()) { Wait(250); }
            PoweredUsers.Add(new PoweredUser(Game.Player.Character));
        }

        List<MenuCfgPair> CustomSettingItems = new List<MenuCfgPair>();
        static string SettingsDirectory = @"scripts\Captain America Files";

        void CollectCustomSettings()
        {
            CfgHelperClass cfghelper = new CfgHelperClass();

            cfghelper.LoadCustomSettings(SettingsDirectory, "*.ini");

            foreach (string filename in cfghelper.GetCleanFileNames())
            {
                CustomSettingItems.Add(new MenuCfgPair(new UIMenu(filename), filename));
                ProfileSettings.Add(new ProfileSetting(filename));

                LoadINIProfile(filename, ProfileSettings.Last());
            }
        }

        void InitMenu()
        {
            _menuPool = new MenuPool();
            capMenu = new UIMenu("Captain America Script");
            _menuPool.AddMenu(capMenu);

            foreach (MenuCfgPair custompair in CustomSettingItems)
            {
                _menuPool.AddSubMenu(custompair.MainMenu, capMenu, custompair.MainMenu.Title);

                InitProfileMenuItems(custompair);
            }

            ItemDisablePowers = new UIMenuItem("Disable Player Powers", null, "Disable Captain America powers for the player.");
            capMenu.AddMenuItem(ItemDisablePowers);

            ItemDeleteAllies = new UIMenuItem("Delete all allies", null, "Delete all allies.");
            capMenu.AddMenuItem(ItemDeleteAllies);

            ItemDeleteEnemies = new UIMenuItem("Delete all enemies", null, "Delete all enemies.");
            capMenu.AddMenuItem(ItemDeleteEnemies);

            ControlsMenu = new UIMenu("Controls");
            _menuPool.AddSubMenu(ControlsMenu, capMenu, "Controls");

            ItemReloadControlsCFG = new UIMenuItem("Reload Controls.CFG file");
            ControlsMenu.AddMenuItem(ItemReloadControlsCFG);
        }

        void InitProfileMenuItems(MenuCfgPair custompair)
        {
            custompair.ItemEnablePowers = new UIMenuItem("Enable Player Powers", null, "Enable Captain America powers for the player.");
            custompair.MainMenu.AddMenuItem(custompair.ItemEnablePowers);

            custompair.ItemEnablePowersWithPed = new UIMenuItem("Enable Player Powers and change Ped model", null, "Enable Captain America powers for the player.");
            custompair.MainMenu.AddMenuItem(custompair.ItemEnablePowersWithPed);

            custompair.ItemAddAlly = new UIMenuItem("Spawn Ally with this profile", null, "Spawn Ally with this profile.");
            custompair.MainMenu.AddMenuItem(custompair.ItemAddAlly);

            custompair.ItemAddEnemy = new UIMenuItem("Spawn Enemy with this profile", null, "Spawn Enemy with this profile.");
            custompair.MainMenu.AddMenuItem(custompair.ItemAddEnemy);

            custompair.ShieldSubmenu = new UIMenu("Shield Settings");
            _menuPool.AddSubMenu(custompair.ShieldSubmenu, custompair.MainMenu, "Shield Settings");

            custompair.ItemSaveCurrentWeapon = new UIMenuItem("Save Current Weapon as Main Weapon", null, "Saves the current weapon to this profile. INI changes will be saved as well.");
            custompair.ShieldSubmenu.AddMenuItem(custompair.ItemSaveCurrentWeapon);

            custompair.ItemInitialThowForce = new UIMenuItem("Force of throw", null, "Higher->Faster");
            custompair.ShieldSubmenu.AddMenuItem(custompair.ItemInitialThowForce);

            custompair.ItemPShieldDamage = new UIMenuItem("Shield Damage to pedestrians", null, "Regular peds have about 100 health");
            custompair.ShieldSubmenu.AddMenuItem(custompair.ItemPShieldDamage);

            custompair.ItemVShieldDamage = new UIMenuItem("Shield Damage to vehicles", null, "Vehicles have about 1000 health");
            custompair.ShieldSubmenu.AddMenuItem(custompair.ItemVShieldDamage);

            custompair.ItemQuickReturnShield = new UIMenuItem("Enable Quick Return", null, "Shield will return when very slow");
            custompair.ShieldSubmenu.AddMenuItem(custompair.ItemQuickReturnShield);

            custompair.ItemReturnInterval = new UIMenuItem("Auto-Return Timer in Seconds", null, "Shield will undoubtly return");
            custompair.ShieldSubmenu.AddMenuItem(custompair.ItemReturnInterval);

            custompair.ItemAllowShieldCurve = new UIMenuItem("Curve Shield Towards Target", null, "Shield will curve towards target");
            custompair.ShieldSubmenu.AddMenuItem(custompair.ItemAllowShieldCurve);

            custompair.ItemCurveForce = new UIMenuItem("Shield Curve Force", null, "Curving force");
            custompair.ShieldSubmenu.AddMenuItem(custompair.ItemCurveForce);

            custompair.ItemBackhandThrow = new UIMenuItem("Do Backhand Throw", null, "If false, do the flick throw from previous versions.");
            custompair.ShieldSubmenu.AddMenuItem(custompair.ItemBackhandThrow);

            custompair.SpecialsSubmenu = new UIMenu("Special Abilities");
            _menuPool.AddSubMenu(custompair.SpecialsSubmenu, custompair.MainMenu, "Special Abilities");

            custompair.ItemAllowSpecialAttacks = new UIMenuItem("Enable Special Attacks", null, "Do you want to use special attacks!?");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemAllowSpecialAttacks);

            custompair.ItemPChargeDamage = new UIMenuItem("Special Attacks Damage to peds", null, "Regular peds have about 100 health");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemPChargeDamage);

            custompair.ItemVChargeDamage = new UIMenuItem("Special Attacks Damage to vehicles", null, "Vehicles have about 1000 health");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemVChargeDamage);

            custompair.ItemStrikingPowerMultiplier = new UIMenuItem("Striking Power Multiplier", null, "Controls how far enemies will fly when hit by melee or thrown");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemStrikingPowerMultiplier);

            custompair.ItemAllowCustomMelee = new UIMenuItem("Enable Custom Melee Attacks", null, "Do you like combos!?");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemAllowCustomMelee);

            custompair.ItemAllowReflect = new UIMenuItem("Allow Reflect Ability", null, "Ability to deflect almost anything");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemAllowReflect);

            custompair.ItemAllowTank = new UIMenuItem("Allow Tank Ability", null, "Ability to sustain explosions and attacks");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemAllowTank);

            custompair.ItemMaxLiftSizeMMcubed = new UIMenuItem("Max Size of Lift-able Objects", null, "In millimeters cubed. Affects throw force too");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemMaxLiftSizeMMcubed);

            custompair.ItemMaxHealth = new UIMenuItem("Max Health", null, "Re-enable powers to set the changes");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemMaxHealth);

            custompair.ItemRegenHealthAmount = new UIMenuItem("Health Regeneration Amount", null, "Regen this amount of health every x seconds");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemRegenHealthAmount);

            custompair.ItemRegenInterval = new UIMenuItem("Heath Regen Interval", null, "Regen every x seconds");
            custompair.SpecialsSubmenu.AddMenuItem(custompair.ItemRegenInterval);

            custompair.MobilitySubmenu = new UIMenu("Movement Settings");
            _menuPool.AddSubMenu(custompair.MobilitySubmenu, custompair.MainMenu, "Movement Settings");

            custompair.ItemAllowSuperRun = new UIMenuItem("Allow Enhanced Run Ability", null);
            custompair.MobilitySubmenu.AddMenuItem(custompair.ItemAllowSuperRun);

            custompair.ItemRunAnimationMultiplier = new UIMenuItem("Run Animation Multiplier", null, "Gotta go fast!");
            custompair.MobilitySubmenu.AddMenuItem(custompair.ItemRunAnimationMultiplier);

            custompair.ItemSuperRunningVelocity = new UIMenuItem("Running Velocity", null, "Running = casual run");
            custompair.MobilitySubmenu.AddMenuItem(custompair.ItemSuperRunningVelocity);

            custompair.ItemSuperSprintingVelocity = new UIMenuItem("Sprinting Velocity", null, "Fastest run");
            custompair.MobilitySubmenu.AddMenuItem(custompair.ItemSuperSprintingVelocity);

            custompair.ItemAllowSuperJump = new UIMenuItem("Allow Enhanced Jump Ability", null);
            custompair.MobilitySubmenu.AddMenuItem(custompair.ItemAllowSuperJump);

            custompair.ItemJumpForwardForce = new UIMenuItem("Jump Forward Force", null, "Increase to jump further");
            custompair.MobilitySubmenu.AddMenuItem(custompair.ItemJumpForwardForce);

            custompair.ItemJumpUpwardForce = new UIMenuItem("Jump Upward Force", null, "Increase to jump higher");
            custompair.MobilitySubmenu.AddMenuItem(custompair.ItemJumpUpwardForce);

            custompair.ItemSafeFallHeight = new UIMenuItem("Max Safe Falling Height", null, "If you fall from a higher height, you will go into ragdoll");
            custompair.MobilitySubmenu.AddMenuItem(custompair.ItemSafeFallHeight);

            custompair.ItemAllowCombatRoll = new UIMenuItem("Allow Combat Roll Ability", null, "Sonic mod when?");
            custompair.MobilitySubmenu.AddMenuItem(custompair.ItemAllowCombatRoll);

            custompair.ItemRollSpeed = new UIMenuItem("Roll Speed", null, "Sonic mod when??");
            custompair.MobilitySubmenu.AddMenuItem(custompair.ItemRollSpeed);

            custompair.MiscSubmenu = new UIMenu("Misc Settings");
            _menuPool.AddSubMenu(custompair.MiscSubmenu, custompair.MainMenu, "Misc Settings");

            custompair.ItemSaveCurrentPed = new UIMenuItem("Save Current Ped", null, "Saves the current ped to this profile. INI changes will be saved as well.");
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemSaveCurrentPed);

            custompair.ItemAllowSlowMoAim = new UIMenuItem("Allow Slow Motion Aim", null, "Also works with the Ground Strike.");
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemAllowSlowMoAim);

            custompair.ItemAllowShieldOnBack = new UIMenuItem("Allow Shield on back", null);
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemAllowShieldOnBack);

            custompair.ItemBackShieldPosX = new UIMenuItem("Back Shield X Position", null, "Move the shield up/down");
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemBackShieldPosX);

            custompair.ItemBackShieldPosY = new UIMenuItem("Back Shield Y Position", null, "Move the shield forward/backward");
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemBackShieldPosY);

            custompair.ItemBackShieldPosZ = new UIMenuItem("Back Shield Z Position", null, "Move the shield left/right");
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemBackShieldPosZ);

            custompair.ItemBackShieldRotX = new UIMenuItem("Back Shield X Rotation", null);
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemBackShieldRotX);

            custompair.ItemBackShieldRotY = new UIMenuItem("Back Shield Y Rotation", null);
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemBackShieldRotY);

            custompair.ItemBackShieldRotZ = new UIMenuItem("Back Shield Z Rotation", null);
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemBackShieldRotZ);

            custompair.ItemFxRed = new UIMenuItem("FX Red Intensity", null, "From 0 - 255");
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemFxRed);

            custompair.ItemFxGreen = new UIMenuItem("FX Green Intensity", null, "From 0 - 255");
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemFxGreen);

            custompair.ItemFxBlue = new UIMenuItem("FX Blue Intensity", null, "From 0 - 255");
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemFxBlue);

            custompair.ItemFixCompatibilityWithFlash = new UIMenuItem("Fix Flash Mod Compatibility", null, "Allows flash slowmo ability with right-click");
            custompair.MiscSubmenu.AddMenuItem(custompair.ItemFixCompatibilityWithFlash);

            custompair.ItemReloadSettings = new UIMenuItem("Reload Settings from INI", null, "Reload the settings from this file.");
            custompair.MainMenu.AddMenuItem(custompair.ItemReloadSettings);

            custompair.ItemSaveSettings = new UIMenuItem("Save Settings to INI", null, "Save your settings.");
            custompair.MainMenu.AddMenuItem(custompair.ItemSaveSettings);
        }

        void SetupKeyboardCulture()
        {
            culture = new CultureInfo(System.Threading.Thread.CurrentThread.CurrentCulture.Name, true);
            culture.NumberFormat.NumberDecimalSeparator = ".";
            forceDecimal();
        }

        void forceDecimal()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        }

        Keys KeyToggle1;
        Keys KeyToggle2;
        Control buttonToggle1;
        Control buttonToggle2;
        Control buttonToggle3;

        void LoadMergerINI()
        {
            string filepath = @"scripts\ScriptCommunicator.ini";

            if (File.Exists(@"scripts\ScriptCommunicator.dll"))
            {
                ScriptSettings config = ScriptSettings.Load(filepath);

                KeyToggle1 = config.GetValue<Keys>("Keyboard Controls", "Menu Toggle Key 1", Keys.ControlKey);
                KeyToggle2 = config.GetValue<Keys>("Keyboard Controls", "Menu Toggle Key 2", Keys.I);
                buttonToggle1 = config.GetValue<Control>("Gamepad Controls", "Menu Toggle Button 1", Control.VehicleHandbrake);
                buttonToggle2 = config.GetValue<Control>("Gamepad Controls", "Menu Toggle Button 2", Control.VehicleHandbrake);
                buttonToggle3 = config.GetValue<Control>("Gamepad Controls", "Menu Toggle Button 3", Control.VehicleHorn);
            }
        }

        void LoadControls()
        {
            forceDecimal();
            string filepath = SettingsDirectory + "\\" + "Controls.cfg";

            ScriptSettings config = ScriptSettings.Load(filepath);

            KeyToggle1 = config.GetValue<Keys>("Controls", "Menu Toggle Key 1", Keys.ControlKey);
            KeyToggle2 = config.GetValue<Keys>("Controls", "Menu Toggle Key 2", Keys.I);
            throwKey = config.GetValue<Keys>("Controls", "Keyboard Throw Key", Keys.X);
            throwButton = config.GetValue<GTA.Control>("Controls", "Gamepad Throw Key", GTA.Control.Cover);
            SpecialAttackButton = config.GetValue<GTA.Control>("Controls", "Special Attack Control (Applies to both Gamepad and Keyboard)", GTA.Control.Attack);
            reflectKey = config.GetValue<Keys>("Controls", "Keyboard Reflect Key", Keys.R);
            reflectButton = config.GetValue<GTA.Control>("Controls", "Gamepad Reflect Key", GTA.Control.MeleeAttackLight);
            grabKey = config.GetValue<Keys>("Controls", "Keyboard Grab Key", Keys.E);
            grabButton = config.GetValue<GTA.Control>("Controls", "Gamepad Grab Key", GTA.Control.Talk);
            SpecialSwitchKey = config.GetValue<Keys>("Controls", "Keyboard Special Attack Switch Key", Keys.T);
            SpecialSwitchButton = config.GetValue<GTA.Control>("Controls", "Gamepad Special Attack Switch Key", GTA.Control.PhoneDown);
        }

        void LoadINIProfile(string filename, ProfileSetting profile)
        {
            forceDecimal();
            string filepath = SettingsDirectory + "\\" + filename + ".ini";

            if (File.Exists(filepath))
            {
                ScriptSettings config = ScriptSettings.Load(filepath);

                profile.ShieldName = config.GetValue<string>("Shield Throw Settings", "Name of Weapon to throw", "CAPSHIELD");
                profile.InitialThowForce = config.GetValue<float>("Shield Throw Settings", "Force of throw", 200.1f);
                profile.PShieldDamage = config.GetValue<int>("Shield Throw Settings", "Shield Damage to pedestrians", 60);
                profile.VShieldDamage = config.GetValue<int>("Shield Throw Settings", "Shield Damage to vehicles", 80);
                profile.QuickReturnShield = config.GetValue<bool>("Shield Throw Settings", "Quick Return", true);
                profile.ReturnInterval = config.GetValue<float>("Shield Throw Settings", "Auto-Return Timer in Seconds", 6) * 1000;
                profile.AllowShieldCurve = config.GetValue<bool>("Shield Throw Settings", "Curve Shield Towards Target", true);
                profile.CurveForce = config.GetValue<float>("Shield Throw Settings", "How much force used to curve the shield", 420f);
                profile.BackhandThrow = config.GetValue<bool>("Shield Throw Settings", "Do Backhand Throw", false);

                profile.AllowSpecialAttacks = config.GetValue<bool>("Special Attacks Settings", "Enable Special Attacks", true);
                profile.PChargeDamage = config.GetValue<int>("Special Attacks Settings", "Special Attacks Damage to pedestrians", 60);
                profile.VChargeDamage = config.GetValue<int>("Special Attacks Settings", "Special Attacks Damage to vehicles", 80);
                profile.StrikingPowerMultiplier = config.GetValue<float>("Special Attacks Settings", "Striking Power Multiplier", 1);

                profile.AllowCustomMelee = config.GetValue<bool>("Other Abilities Settings", "Allow Custom Melee", true);
                profile.MaxHealth = config.GetValue<int>("Other Abilities Settings", "Max Health", 4000);
                profile.RegenHealthAmount = config.GetValue<int>("Other Abilities Settings", "Health Regeneration Amount", 10);
                profile.RegenInterval = config.GetValue<float>("Other Abilities Settings", "Regenerate Health every X amount of milliseconds", 1);
                profile.AllowReflect = config.GetValue<bool>("Other Abilities Settings", "Allow Reflect Ability", true);
                profile.AllowTank = config.GetValue<bool>("Other Abilities Settings", "Allow Tank Ability", true);
                profile.AllowSuperRun = config.GetValue<bool>("Other Abilities Settings", "Allow Enhanced Run Ability", true);
                profile.RunAnimationMultiplier = config.GetValue<float>("Other Abilities Settings", "Run Animation Multiplier", 0.3f);
                profile.SuperRunningVelocity = config.GetValue<float>("Other Abilities Settings", "Running Velocity", 11.1f);
                profile.SuperSprintingVelocity = config.GetValue<float>("Other Abilities Settings", "Sprinting Velocity", 25.1f);
                profile.AllowSuperJump = config.GetValue<bool>("Other Abilities Settings", "Allow Enhanced Jump Ability", true);
                profile.JumpForwardForce = config.GetValue<float>("Other Abilities Settings", "Jump Forward Force", 25.01f);
                profile.JumpUpwardForce = config.GetValue<float>("Other Abilities Settings", "Jump Upward Force", 8.01f);
                profile.SafeFallHeight = config.GetValue<float>("Other Abilities Settings", "Max Safe Falling Height", 20f);
                profile.AllowCombatRoll = config.GetValue<bool>("Other Abilities Settings", "Allow Combat Roll Ability", true);
                profile.RollSpeed = config.GetValue<float>("Other Abilities Settings", "Roll Speed", 12.1f);
                profile.AllowLifting = config.GetValue<bool>("Other Abilities Settings", "Allow Lifting Ability", true);
                profile.MaxLiftSizeMMcubed = config.GetValue<float>("Other Abilities Settings", "Max Size of Lift-able Objects in mm cubed", 56f);

                profile.PedModelToUse = config.GetValue<string>("Other Abilities Settings", "Assigned Ped Model", Game.Player.Character.Model.Hash.ToString());
                profile.AllowSlowMoAim = config.GetValue<bool>("Other Abilities Settings", "Allow Slow Motion Aim", true);
                profile.AllowShieldOnBack = config.GetValue<bool>("Other Abilities Settings", "Allow Shield On Back", true);
                /*profile.BackShieldPos.X = config.GetValue<float>("Other Abilities Settings", "Back Shield X Position", 0.3f);
                profile.BackShieldPos.Y = config.GetValue<float>("Other Abilities Settings", "Back Shield Y Position", -0.15f);
                profile.BackShieldPos.Z = config.GetValue<float>("Other Abilities Settings", "Back Shield Z Position", -0.025f);
                profile.BackShieldRot.X = config.GetValue<float>("Other Abilities Settings", "Back Shield X Rotation", 80f);
                profile.BackShieldRot.Y = config.GetValue<float>("Other Abilities Settings", "Back Shield Y Rotation", -10f);
                profile.BackShieldRot.Z = config.GetValue<float>("Other Abilities Settings", "Back Shield Z Rotation", -10f);*/
                profile.SetBackShieldPos(config.GetValue<float>("Other Abilities Settings", "Back Shield X Position", 0.3f), 
                    config.GetValue<float>("Other Abilities Settings", "Back Shield Y Position", -0.15f), 
                    config.GetValue<float>("Other Abilities Settings", "Back Shield Z Position", -0.025f));
                profile.SetBackShieldRot(config.GetValue<float>("Other Abilities Settings", "Back Shield X Rotation", 80f), 
                    config.GetValue<float>("Other Abilities Settings", "Back Shield Y Rotation", -10f), 
                    config.GetValue<float>("Other Abilities Settings", "Back Shield Z Rotation", -10f));

                profile.FxRed = config.GetValue<float>("Shield Trail FX Settings", "Red Intensity", 0f);
                profile.FxGreen = config.GetValue<float>("Shield Trail FX Settings", "Green Intensity", 10f);
                profile.FxBlue = config.GetValue<float>("Shield Trail FX Settings", "Blue Intensity", 250f);

                profile.FixCompatibilityWithFlash = config.GetValue<bool>("Miscellaneous", "Fix Compatibility with Flash Abilities", false);
            }
            
            //DecipherAndSetCapShield(ShieldName); //must use this after loading an ini.
        }

        void DecipherAndSetCapShield(string capstring, PoweredUser user)
        {
            int result;
            bool IsInt = Int32.TryParse(capstring, out result);

            if (IsInt)
            {
                user.CapShield = (WeaponHash)result;
            }
            else
            {
                user.CapShield = (WeaponHash)Function.Call<int>(Hash.GET_HASH_KEY, "WEAPON_" + user.AssignedProfile.ShieldName);
            }

            //user.holdWeap = user.CapShield;
            //user.holdWeap.Request();
        }

        /*Model GetModelOfWeapon(WeaponHash wpnHash)
        {
            return Function.Call<Model>(Hash.GET_WEAPONTYPE_MODEL, wpnHash.ToString());
        }*/

        void SaveINIProfile(string filename, ProfileSetting profile)
        {
            forceDecimal();
            string filepath = SettingsDirectory + "\\" + filename + ".ini";
            ScriptSettings config = ScriptSettings.Load(filepath);

            /*
            config.SetValue<Keys>("Controls", "Keyboard Throw Key", throwKey);
            config.SetValue<Keys>("Controls", "Keyboard Reflect Key", reflectKey);
            config.SetValue<Keys>("Controls", "Keyboard Grab Key", grabKey);
            config.SetValue<Keys>("Controls", "Keyboard Special Attack Switch Key", SpecialSwitchKey);
            config.SetValue<string>("Controls", "Keyboard Controls can be found here", "https://msdn.microsoft.com/en-us/library/system.windows.forms.keys(v=vs.110).aspx?cs-save-lang=1&cs-lang=csharp#code-snippet-1");
            config.SetValue<GTA.Control>("Controls", "Special Attack Control (Applies to both Gamepad and Keyboard)", SpecialAttackButton);
            config.SetValue<GTA.Control>("Controls", "Gamepad Throw Key", throwButton);
            config.SetValue<GTA.Control>("Controls", "Gamepad Reflect Key", reflectButton);
            config.SetValue<GTA.Control>("Controls", "Gamepad Grab Key", grabButton);
            config.SetValue<GTA.Control>("Controls", "Gamepad Special Attack Switch Key", SpecialSwitchButton);
            config.SetValue<string>("Controls", "Gamepad Controls can be found here", "https://github.com/crosire/scripthookvdotnet/blob/157ac57f9530a1cf55afa61d81a066849b52f8ba/source/scripting/Controls.hpp#L179");*/

            config.SetValue<string>("Shield Throw Settings", "Name of Weapon to throw", profile.ShieldName);
            //UI.Notify("WEAPON_"+ShieldName);

            config.SetValue<float>("Shield Throw Settings", "Force of throw", profile.InitialThowForce);
            config.SetValue<int>("Shield Throw Settings", "Shield Damage to pedestrians", profile.PShieldDamage);
            config.SetValue<int>("Shield Throw Settings", "Shield Damage to vehicles", profile.VShieldDamage);
            config.SetValue<bool>("Shield Throw Settings", "Quick Return", profile.QuickReturnShield);
            config.SetValue<int>("Shield Throw Settings", "Auto-Return Timer in Seconds", (int)(profile.ReturnInterval / 1000));
            config.SetValue<bool>("Shield Throw Settings", "Curve Shield Towards Target", profile.AllowShieldCurve);
            config.SetValue<float>("Shield Throw Settings", "How much force used to curve the shield", profile.CurveForce);
            config.SetValue<bool>("Shield Throw Settings", "Do Backhand Throw", profile.BackhandThrow);

            config.SetValue<bool>("Special Attacks Settings", "Enable Special Attacks", profile.AllowSpecialAttacks);
            config.SetValue<int>("Special Attacks Settings", "Special Attacks Damage to pedestrians", profile.PChargeDamage);
            config.SetValue<int>("Special Attacks Settings", "Special Attacks Damage to vehicles", profile.VChargeDamage);
            config.SetValue<float>("Special Attacks Settings", "Striking Power Multiplier", profile.StrikingPowerMultiplier);

            config.SetValue<bool>("Other Abilities Settings", "Allow Custom Melee", profile.AllowCustomMelee);
            config.SetValue<int>("Other Abilities Settings", "Max Health", profile.MaxHealth);
            config.SetValue<int>("Other Abilities Settings", "Health Regeneration Amount", profile.RegenHealthAmount);
            config.SetValue<int>("Other Abilities Settings", "Regenerate Health every X amount of milliseconds", (int)profile.RegenInterval);
            config.SetValue<bool>("Other Abilities Settings", "Allow Reflect Ability", profile.AllowReflect);
            config.SetValue<bool>("Other Abilities Settings", "Allow Tank Ability", profile.AllowTank);
            config.SetValue<bool>("Other Abilities Settings", "Allow Enhanced Run Ability", profile.AllowSuperRun);
            config.SetValue<float>("Other Abilities Settings", "Run Animation Multiplier", profile.RunAnimationMultiplier);
            config.SetValue<float>("Other Abilities Settings", "Running Velocity", profile.SuperRunningVelocity);
            config.SetValue<float>("Other Abilities Settings", "Sprinting Velocity", profile.SuperSprintingVelocity);
            config.SetValue<bool>("Other Abilities Settings", "Allow Enhanced Jump Ability", profile.AllowSuperJump);
            config.SetValue<float>("Other Abilities Settings", "Jump Forward Force", profile.JumpForwardForce);
            config.SetValue<float>("Other Abilities Settings", "Jump Upward Force", profile.JumpUpwardForce);
            config.SetValue<float>("Other Abilities Settings", "Max Safe Falling Height", profile.SafeFallHeight);
            config.SetValue<bool>("Other Abilities Settings", "Allow Combat Roll Ability", profile.AllowCombatRoll);
            config.SetValue<float>("Other Abilities Settings", "Roll Speed", profile.RollSpeed);
            config.SetValue<bool>("Other Abilities Settings", "Allow Lifting Ability", profile.AllowLifting);
            config.SetValue<float>("Other Abilities Settings", "Max Size of Lift-able Objects in mm cubed", profile.MaxLiftSizeMMcubed);

            config.SetValue<string>("Other Abilities Settings", "Assigned Ped Model", profile.PedModelToUse);
            config.SetValue<bool>("Other Abilities Settings", "Allow Slow Motion Aim", profile.AllowSlowMoAim);
            config.SetValue<bool>("Other Abilities Settings", "Allow Shield On Back", profile.AllowShieldOnBack);
            config.SetValue<float>("Other Abilities Settings", "Back Shield X Position", profile.BackShieldPos.X);
            config.SetValue<float>("Other Abilities Settings", "Back Shield Y Position", profile.BackShieldPos.Y);
            config.SetValue<float>("Other Abilities Settings", "Back Shield Z Position", profile.BackShieldPos.Z);
            config.SetValue<float>("Other Abilities Settings", "Back Shield X Rotation", profile.BackShieldRot.X);
            config.SetValue<float>("Other Abilities Settings", "Back Shield Y Rotation", profile.BackShieldRot.Y);
            config.SetValue<float>("Other Abilities Settings", "Back Shield Z Rotation", profile.BackShieldRot.Z);

            config.SetValue<float>("Shield Trail FX Settings", "Red Intensity", profile.FxRed);
            config.SetValue<float>("Shield Trail FX Settings", "Green Intensity", profile.FxGreen);
            config.SetValue<float>("Shield Trail FX Settings", "Blue Intensity", profile.FxBlue);

            config.SetValue<bool>("Miscellaneous", "Fix Compatibility with Flash Abilities", profile.FixCompatibilityWithFlash);
            config.Save();
        }

        ScriptCommunicator CapCommunicator = new ScriptCommunicator("CapShieldThrow");
        public void ExternalEventHandling()
        {
            if (CapCommunicator.IsEventTriggered())
            {
                Wait(250);
                if (!_menuPool.IsAnyMenuOpen())
                {
                    _menuPool.LastUsedMenu.IsVisible = !_menuPool.LastUsedMenu.IsVisible;
                }
                else
                {
                    _menuPool.CloseAllMenus();
                }

                CapCommunicator.BlockScriptCommunicatorModMenu();
                CapCommunicator.ResetEvent();
            }
        }

        void OnTick(object sender, EventArgs e) // This is where most of your script goes
        {
            ExternalEventHandling();

            ManageMenu();

            if (Game.Player.IsAlive)
            {
                player = Game.Player.Character;
                PoweredUsers[0].PoweredPed = player;

                if (PoweredUsers[0].PoweredPed != null && PoweredUsers[0].PoweredPed.IsAlive)// && Game.Player.CanControlCharacter)
                {
                    if (CapAbilities)
                    {
                        ShieldThrowing(PoweredUsers[0]);

                        ThrownShieldAction(PoweredUsers[0]);

                        AutoReturnShield(PoweredUsers[0]);

                        PickupShield(PoweredUsers[0]);

                        IdentifyMultipleTargets();

                        TheOgSpecialAttacks(PoweredUsers[0]);

                        NewSpecialAttacks(PoweredUsers[0]);

                        SpecialAttackSelector(Game.Player.Character, PoweredUsers[0].AssignedProfile);

                        TankMode(PoweredUsers[0]);

                        ComboMeleeAbility(PoweredUsers[0]);

                        GrabAbility(PoweredUsers[0]);

                        fastJump(PoweredUsers[0]);

                        superSpeed(PoweredUsers[0]);

                        QuickRagdollRecover();

                        HealthRegen(PoweredUsers[0]);

                        controlledPropAction();

                        clearDamagedEntityListsOnTime(PoweredUsers[0]);

                        rollController(PoweredUsers[0]);

                        EnhancedAnimationSpeeds(PoweredUsers[0].PoweredPed);

                        ShieldFXRemover(PoweredUsers[0]);

                        clearDecalsFromShields();

                        shieldAttachementAutoSwitch(PoweredUsers[0]);
                    }
                }
            }
        }
        
        void OnTickAI(object sender, EventArgs e)
        {
            try
            {
                if (PoweredUsers.Count > 1 && Game.Player.IsAlive)
                {

                    foreach (PoweredUser user in PoweredUsers)
                    {
                        if (user == PoweredUsers[0])
                        {
                            continue;
                        }
                        else
                        {
                            if (user.PoweredPed.IsAlive)
                            {
                                /*Do controller stuff here*/
                                clearDamagedEntityListsOnTime(user);
                                ControlAIShieldThrow(user);
                                ControlChargingStar(user);
                                ControlGroundSmash(user);
                                ControlMeleeCombo(user);
                                ControlSpeed(user);

                                /*Do Action Decision stuff here*/
                                if (user.CanDoAction())
                                {
                                    if (ShieldIsNotInAir(user.weapProp) && !user.PoweredPed.IsRagdoll && !user.PoweredPed.IsGettingUp) //give weapon to user if not throwing it.
                                    {
                                        user.PoweredPed.Weapons.Give(user.CapShield, 1, true, true);
                                    }

                                    if (!user.PoweredPed.IsInAir && !user.PoweredPed.IsRagdoll)
                                    {
                                        user.PoweredPed.CanRagdoll = false;
                                    }

                                    if (user.IsEnemy)
                                    {
                                        SetEnemyTasks(user);
                                    }
                                    else
                                    {
                                        SetAllyTasks(user);
                                    }
                                }
                            }
                            else
                            {
                                try
                                {
                                    if (user.PoweredPed.CurrentBlip.Exists())
                                    {
                                        user.PoweredPed.CurrentBlip.Remove();
                                    }
                                    StopShieldFXTrail(user);
                                } catch { }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        void SetAllyTasks(PoweredUser user)
        {
            World.SetRelationshipBetweenGroups(Relationship.Companion, user.PoweredPed.RelationshipGroup, Game.Player.Character.RelationshipGroup);
            //user.Target = getClosestHatedPed(user.PoweredPed.Position, 10f, user.PoweredPed, true);
            Ped pedClosestToAlly = getClosestNonCompanionPed(user.PoweredPed.Position, 8f, user.PoweredPed, true);
            Ped pedClosestToPlayer = getClosestNonCompanionPed(Game.Player.Character.Position, 8f, user.PoweredPed, true);
            if (pedClosestToAlly != null && pedClosestToPlayer != null)
            {
                user.Target = pedClosestToAlly.MaxHealth >= pedClosestToPlayer.MaxHealth ? pedClosestToAlly : pedClosestToPlayer;
            }
            else if (pedClosestToAlly != null && pedClosestToPlayer == null)
            {
                user.Target = pedClosestToAlly;
            }
            else if (pedClosestToAlly == null && pedClosestToPlayer != null)
            {
                user.Target = pedClosestToPlayer;
            }
            if (user.Target == null) //get enemies around crosshair if there are no enemies nearby
            {
                RaycastResult ray = World.GetCrosshairCoordinates();
                user.Target = getClosestNonCompanionPed(ray.HitCoords, 15f, user.PoweredPed, true);
            }

            BlockPedRagdollOfType(user.PoweredPed, ragdollBlock.WhenHitByVehicle);
            BlockPedRagdollOfType(user.PoweredPed, ragdollBlock.WhenShot);
            
            if (user.PoweredPed.IsRagdoll && user.PoweredPed.HeightAboveGround <= 1.6f)
            {
                /*Do Quick Recover*/
                user.rInt = GetUniqueRandomInt(user.rInt, 0, 3, user);
                switch (user.rInt)
                {
                    case 0: SetPedRagdoll(user.PoweredPed, 10, ragdollType.StiffBody); user.SetActionWait(100); return;
                    case 1: user.PoweredPed.Task.ClearAllImmediately(); user.SetActionWait(100); return;
                    case 2: user.SetActionWait(500); return;
                }
                return;
            }

            if (user.PoweredPed.IsGettingUp && !isPlayingRollForward(user.PoweredPed) && DistanceBetween(user.PoweredPed.Position, user.Target.Position) > 5f)
            {
                playRollForward(user.PoweredPed);
                user.SetActionWait(500);
                return;
            }
            
            if (!user.PoweredPed.IsRagdoll && !user.PoweredPed.IsGettingUp)
            {
                if (PoweredUsers[0].PoweredPed.IsInVehicle())
                {
                    if (DistanceBetween(user.PoweredPed.Position, PoweredUsers[0].PoweredPed.Position) <= 3f && !user.PoweredPed.IsGettingIntoAVehicle && !user.PoweredPed.IsInVehicle())
                    {
                        user.PoweredPed.Task.EnterVehicle(Game.Player.Character.CurrentVehicle, VehicleSeat.Any, 5000);
                        user.SetActionWait(5000);
                    }
                    else if (DistanceBetween(user.PoweredPed.Position, PoweredUsers[0].PoweredPed.Position) > 3f)
                    {
                        user.PoweredPed.Task.RunTo(PoweredUsers[0].PoweredPed.Position.Around(2), false);
                        user.SetActionWait(500);
                    }
                    return;
                }

                if (DistanceBetween(user.PoweredPed.Position, PoweredUsers[0].PoweredPed.Position) <= 6f && (user.Target == null || user.Target.IsDead))
                {
                    return; //do nothing
                }

                if (DistanceBetween(user.PoweredPed.Position, PoweredUsers[0].PoweredPed.Position) > 30 || user.Target == null || user.Target.IsDead)
                {
                    user.PoweredPed.Task.RunTo(PoweredUsers[0].PoweredPed.Position.Around(2), false);
                    user.SetActionWait(500);
                    return;
                }

                float distance = DistanceBetween(user.PoweredPed.Position, user.Target.Position);
                if (distance > 6) //throw shield or get closer to target
                {
                    if (IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName))
                    {
                        user.PoweredPed.Task.ClearAll();
                        TriggerOnFootShieldThrow(user);
                        user.SetActionWait(700);
                        return;
                    }
                    else
                    {
                        user.PoweredPed.Task.RunTo(user.Target.Position.Around(2), false);
                        user.SetActionWait(500);
                        return;
                    }
                }
                else if (distance > 3)
                {
                    //fight ped special
                    if (IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName))
                    {
                        user.PoweredPed.Task.ClearAll();

                        user.rInt = GetUniqueRandomInt(user.rInt, 0, 2, user);
                        switch (user.rInt)
                        {
                            case 0:
                                {

                                    TriggerChargingStar(user);
                                    user.SetActionWait(1600); return;
                                }
                            case 1:
                                {

                                    if (!isPlayingAnim(user.PoweredPed, Animations.Shield2Ground))
                                    {
                                        playAnimation(user.PoweredPed, Animations.Shield2Ground);
                                    }
                                    user.SetActionWait(1600); return;
                                }
                        }
                        return;
                    }
                    else
                    {
                        user.PoweredPed.Task.RunTo(user.Target.Position.Around(2), false);
                        user.SetActionWait(500);
                        return;
                    }
                }
                else
                {
                    user.PoweredPed.Task.ClearAll();

                    user.rInt = GetUniqueRandomInt(user.rInt, 0, 6, user);
                    switch (user.rInt)
                    {
                        case 0:
                            {
                                playAnimation(user.PoweredPed, Animations.BackSlap);
                                user.SetActionWait(800); return;
                            }
                        case 1:
                            {
                                playAnimation(user.PoweredPed, Animations.LeftHook);
                                user.SetActionWait(800); return;
                            }
                        case 2:
                            {
                                playAnimation(user.PoweredPed, Animations.RightHook);
                                user.SetActionWait(800); return;
                            }
                        case 3:
                            {
                                playAnimation(user.PoweredPed, Animations.StrongKick);
                                user.SetActionWait(800); return;
                            }
                        case 4:
                            {
                                playAnimation(user.PoweredPed, Animations.SmackDown);
                                user.SetActionWait(800); return;
                            }
                        case 5:
                            {
                                playAnimation(user.PoweredPed, Animations.Uppercut);
                                user.SetActionWait(800); return;
                            }
                    }
                    return;
                }
            }
        }

        void SetEnemyTasks(PoweredUser user)
        {
            user.Target = Game.Player.Character;
            user.PoweredPed.RelationshipGroup = VillianGroup;
            //World.SetRelationshipBetweenGroups(Relationship.Like, user.PoweredPed.RelationshipGroup, Game.Player.Character.RelationshipGroup);
            SetRelationshipOneSided(Relationship.Like, VillianGroup, Game.Player.Character.RelationshipGroup);

            BlockPedRagdollOfType(user.PoweredPed, ragdollBlock.WhenHitByVehicle);
            BlockPedRagdollOfType(user.PoweredPed, ragdollBlock.WhenShot);

            if (user.PoweredPed.IsRagdoll && user.PoweredPed.HeightAboveGround <= 1.6f)
            {
                /*Do Quick Recover*/
                user.rInt = GetUniqueRandomInt(user.rInt, 0, 3, user);
                switch (user.rInt)
                {
                    case 0: SetPedRagdoll(user.PoweredPed, 10, ragdollType.StiffBody); user.SetActionWait(100); return;
                    case 1: user.PoweredPed.Task.ClearAllImmediately(); user.SetActionWait(100); return;
                    case 2: user.SetActionWait(500); return;
                }
                return;
            }

            if (user.PoweredPed.IsGettingUp && !isPlayingRollForward(user.PoweredPed) && DistanceBetween(user.PoweredPed.Position, user.Target.Position) > 5f)
            {
                playRollForward(user.PoweredPed);
                user.SetActionWait(500);
                return;
            }

            if (!user.PoweredPed.IsRagdoll && !user.PoweredPed.IsGettingUp)
            {
                float distance = DistanceBetween(user.PoweredPed.Position, user.Target.Position);
                if (distance > 30)
                {
                    user.PoweredPed.Task.RunTo(user.Target.Position.Around(2), false);
                    user.SetActionWait(500);
                }
                else if (distance > 10) //throw shield or get closer to target
                {
                    if (IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName))
                    {
                        user.PoweredPed.Task.ClearAll();
                        TriggerOnFootShieldThrow(user);
                        user.SetActionWait(700);
                    }
                    else
                    {
                        user.PoweredPed.Task.RunTo(user.Target.Position.Around(2), false);
                        user.SetActionWait(500);
                    }
                }
                else if (distance > 5)
                {
                    //fight ped special
                    if (IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName))
                    {
                        user.Target = getClosestNonCompanionPed(user.PoweredPed.Position, 6f, user.PoweredPed, true);
                        if (user.Target == null) { user.Target = PoweredUsers[0].PoweredPed; }

                        user.PoweredPed.Task.ClearAll();

                        user.rInt = GetUniqueRandomInt(user.rInt, 0, 2, user);
                        switch (user.rInt)
                        {
                            case 0:
                                {

                                    TriggerChargingStar(user);
                                    user.SetActionWait(1600); return;
                                }
                            case 1:
                                {

                                    if (!isPlayingAnim(user.PoweredPed, Animations.Shield2Ground))
                                    {
                                        playAnimation(user.PoweredPed, Animations.Shield2Ground);
                                    }
                                    user.SetActionWait(1600); return;
                                }
                        }
                    }
                    else
                    {

                    }
                }
                else
                {
                    user.Target = getClosestNonCompanionPed(user.PoweredPed.Position, 6f, user.PoweredPed, true);

                    if (user.Target != null)
                    {
                        if ((((Ped)user.Target).IsRagdoll && !user.Target.IsInAir) || ((Ped)user.Target).IsGettingUp)
                        {
                            //PlayPedAmbientSpeech(user.PoweredPed, "GENERIC_HOWS_IT_GOING", "Speech_Params_Standard");
                            //user.SetActionWait(1000); return;

                            TriggerChargingStar(user);
                            user.SetActionWait(1600); return;
                        }
                    }

                    user.PoweredPed.Task.ClearAll();

                    user.rInt = GetUniqueRandomInt(user.rInt, 0, 6, user);
                    switch (user.rInt)
                    {
                        case 0:
                            {
                                playAnimation(user.PoweredPed, Animations.BackSlap);
                                user.rInt = user.rng.Next(750, 1000);
                                user.SetActionWait(user.rInt); return;
                            }
                        case 1:
                            {
                                playAnimation(user.PoweredPed, Animations.LeftHook);
                                user.rInt = user.rng.Next(750, 1000);
                                user.SetActionWait(user.rInt); return;
                            }
                        case 2:
                            {
                                playAnimation(user.PoweredPed, Animations.RightHook);
                                user.rInt = user.rng.Next(750, 1000);
                                user.SetActionWait(user.rInt); return;
                            }
                        case 3:
                            {
                                playAnimation(user.PoweredPed, Animations.StrongKick);
                                user.rInt = user.rng.Next(750, 1000);
                                user.SetActionWait(user.rInt); return;
                            }
                        case 4:
                            {
                                playAnimation(user.PoweredPed, Animations.SmackDown);
                                user.rInt = user.rng.Next(750, 1000);
                                user.SetActionWait(user.rInt); return;
                            }
                        case 5:
                            {
                                playAnimation(user.PoweredPed, Animations.Uppercut);
                                user.rInt = user.rng.Next(750, 1000);
                                user.SetActionWait(user.rInt); return;
                            }
                    }
                }
            }
        }

        void ShieldThrowing(PoweredUser user)
        {
            if (user.PoweredPed.IsOnFoot)
            {
                isOnBike = false;
                if (IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName)) //If holding Cap Shield
                {
                    if (!user.AssignedProfile.FixCompatibilityWithFlash)
                    { Game.DisableControlThisFrame(2, GTA.Control.Aim); }
                    user.PoweredPed.Weapons.CurrentWeaponObject.SetNoCollision(user.PoweredPed, false);
                    if (!user.PoweredPed.IsGettingUp && !user.PoweredPed.IsRagdoll)
                    {
                        //CapShield = user.PoweredPed.Weapons.Current.Hash;

                        if (isAiming())
                        {
                            //isHoming = false;
                            //ShieldCanReturn = false;
                            //ShieldThrowLerp = 0;
                            //shieldSpin = 0;

                            if (TargetPedExists())
                            {
                                hasAutoTarget = true; SetTargetEntity(user);
                            }
                            else
                            {
                                if (keepTargetInterval < Game.GameTime)
                                { hasAutoTarget = false; }
                                else { DrawMarker(_TargettedEntity); }
                            }

                            DrawReticle();
                            blockInput();

                            if (justPressedThrowButton())
                            {
                                TriggerOnFootShieldThrow(user);
                                InitiatedSlowMo = true;
                                InputTimer = Game.GameTime + 1000;
                            }
                        }

                        if (ShieldIsNotInAir(user.weapProp) && user.holdWeap.IsInCdImage && user.holdWeap.IsValid)
                        {
                            if (isPlayingAnim(user.PoweredPed, Animations.OnFootThrowVertical) || isPlayingAnim(user.PoweredPed, Animations.OnFootThrowAcross))
                            {
                                if (ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.OnFootThrowVertical) || ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.OnFootThrowAcross)) //if can throw shield now
                                {
                                    wasThrownOnFoot = true;
                                    if (user.AssignedProfile.AllowSlowMoAim) { Game.TimeScale = 1; }
                                    createAndPushShield(true, user);
                                    //InitiatedSlowMo = false;
                                }
                                else
                                {
                                    if (Function.Call<int>(Hash.GET_FOLLOW_PED_CAM_VIEW_MODE) != 4)
                                    {
                                        PedFaceRotationExact(user.PoweredPed, GameplayCamera.Rotation, user.ShieldThrowLerp);
                                    }
                                    ControlLerp(user.ShieldThrowLerp, 1f, out user.ShieldThrowLerp);

                                    if (isHoldingThrowButton())
                                    {
                                        if (user.AssignedProfile.AllowSlowMoAim) { Game.TimeScale = 0.05f; }

                                        SetMultipleTargets();
                                    }
                                    else
                                    {
                                        if (user.AssignedProfile.AllowSlowMoAim) { Game.TimeScale = 1; }
                                    }
                                }
                            }
                            InitiatedSlowMo = false;
                        }
                    }
                    else
                    {
                        if (InitiatedSlowMo)
                        {
                            if (user.AssignedProfile.AllowSlowMoAim) { Game.TimeScale = 1; }
                            InitiatedSlowMo = false;

                        }
                    }
                }
                else
                {
                    if (/*ballShieldExistsOnHand*/BallShieldsExistOnHand())
                    {
                        deleteBallisticShieldsOnHand();
                    }
                    hasAutoTarget = false;
                }
            }
            else
            {
                if (isUsingMotorcyle())
                {
                    if (!isOnBike)
                    {
                        if (CapAbilities)
                        {
                            user.PoweredPed.Weapons.Select(WeaponHash.Unarmed);
                            isOnBike = true;
                        }
                    }
                    if (user.PoweredPed.Weapons.Current.Hash == WeaponHash.Unarmed)
                    {
                        if (ballShieldExistsOnBack)
                        {
                            if (isAiming())
                            {
                                DrawReticle();
                                Game.DisableControlThisFrame(2, GTA.Control.VehicleCinCam);

                                if (TargetPedExists())
                                {
                                    hasAutoTarget = true; SetTargetEntity(user);
                                }
                                else
                                {
                                    if (keepTargetInterval < Game.GameTime)
                                    { hasAutoTarget = false; }
                                    else { DrawMarker(_TargettedEntity); }
                                }

                                if (justPressedThrowFromMoto() && !isPlayingBikeShieldThrowFrontBack("Throw_0") && !isPlayingBikeShieldThrowFrontBack("Throw_180r") && !isPlayingBikeShieldThrowLeftRight("melee_l") && !isPlayingBikeShieldThrowLeftRight("melee_r"))
                                {
                                    lastThrowDir = MotoThrowDirection();

                                    if (!ifMotoThrowIsLeftRight())
                                    {
                                        playBikeShieldThrowFrontBack(lastThrowDir);
                                    }
                                    else
                                    {
                                        playBikeShieldThrowLeftRight(lastThrowDir);
                                    }

                                    InputTimer = Game.GameTime + 1000;
                                    user.isHoming = false;
                                    user.ShieldCanReturn = false;
                                    user.ShieldThrowLerp = 0;
                                    user.shieldSpin = 0;
                                    InitiatedSlowMo = true;
                                }
                            }

                            if (!user.shieldIsThrown)
                            {
                                if (!CanThrowShieldFrontBackFromMotoNow(lastThrowDir))
                                {
                                    if (isPlayingBikeShieldThrowFrontBack(lastThrowDir))
                                    {
                                        if (isHoldingThrowFromMoto())
                                        {
                                            if (user.AssignedProfile.AllowSlowMoAim) { Game.TimeScale = 0.15f; }
                                        }
                                        else
                                        {
                                            if (user.AssignedProfile.AllowSlowMoAim) { Game.TimeScale = 1; }
                                        }

                                    }
                                    //InitiatedSlowMo = false;
                                }
                                else
                                {
                                    wasThrownOnFoot = false;
                                    if (user.AssignedProfile.AllowSlowMoAim) { Game.TimeScale = 1; }
                                    deleteShieldsOnBack();
                                    createAndPushShield(true, user);
                                    InitiatedSlowMo = false;
                                }

                                if (!CanThrowShieldLeftRightFromMotoNow(lastThrowDir))
                                {
                                    if (isPlayingBikeShieldThrowLeftRight(lastThrowDir))
                                    {
                                        if (isHoldingThrowFromMoto())
                                        {
                                            if (user.AssignedProfile.AllowSlowMoAim) { Game.TimeScale = 0.15f; }
                                        }
                                        else
                                        {
                                            if (user.AssignedProfile.AllowSlowMoAim) { Game.TimeScale = 1; }
                                        }

                                    }
                                    //InitiatedSlowMo = false;
                                }
                                else
                                {
                                    wasThrownOnFoot = false;
                                    if (user.AssignedProfile.AllowSlowMoAim) { Game.TimeScale = 1; }
                                    if (lastThrowDir == "melee_r")
                                    {
                                        deleteShieldsOnBack();
                                        createAndPushShield(true, user);
                                        InitiatedSlowMo = false;
                                    }
                                    else
                                    {
                                        deleteShieldsOnBack();
                                        createAndPushShield(false, user);
                                        InitiatedSlowMo = false;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        void createAndPushShield(bool rightHand, PoweredUser user)
        {
            deleteBallisticShieldsOnHand();
            user.pickupTimer = Game.GameTime + 2000; //pickup timer will be 2 seconds greater than the game time.
            user.autoReturnTimer = Game.GameTime + (int)user.AssignedProfile.ReturnInterval; //Setup a wait for x seconds until shield will come back to you automatically.
            user.autoReturnTimeout = Game.GameTime + 20000; //script will wait for 20 seconds; if shield hasn't returned, it will cancel the action.
            //AvoidEntityDeletion();
            if (rightHand)
            { user.weapProp = World.CreateProp(user.holdWeap, /*boneCoord(player, "IK_R_Hand")*/ Vector3.Zero, false, false); user.weapProp.AttachTo(player, player.GetBoneIndex(Bone.IK_R_Hand)); user.weapProp.Detach(); }
            else { user.weapProp = World.CreateProp(user.holdWeap, /*boneCoord(player, "IK_L_Hand")*/Vector3.Zero, false, false); user.weapProp.AttachTo(player, player.GetBoneIndex(Bone.IK_L_Hand)); user.weapProp.Detach(); }
            SetObjectPhysicsParameters(user.weapProp, 1f, 2.2f, -1.0f, 1.0f, -1.0f, 1f, -1.0f, -1.0f, -1.0f, -1.0f, 10f);
            
            user.shieldIsThrown = true;
            addCollision(user.weapProp);
            if (!wasThrownOnFoot && player.IsInVehicle())
            { user.weapProp.SetNoCollision(player.CurrentVehicle, false); }
            SetEntityProofs(user.weapProp, true, false, true, true, true);

            if (!hasAutoTarget)
            {
                ApplyVelocity(user.weapProp, ForwardDirFromCam(user.AssignedProfile.InitialThowForce), 1f);
                //weapProp.ApplyForce(ForwardDirFromCam(initialThowForce));//, weapProp.Rotation + new Vector3(50f, 0, 0) * 300.0f);
            }
            else
            {
                ThrowAtTargetEntity(user, _TargettedEntity);
            }

            if (wasThrownOnFoot)
            {
                player.Weapons.Select(WeaponHash.Unarmed, true);
                player.Weapons.Remove(user.CapShield);
            }

            //RemovePersistance();
        }

        void TriggerOnFootShieldThrow(PoweredUser user) //for use with player or AI
        {
            if (ShieldIsNotInAir(user.weapProp))
            {
                if (user.AssignedProfile.BackhandThrow && !isPlayingAnim(user.PoweredPed, Animations.OnFootThrowAcross))
                {
                    playAnimation(user.PoweredPed, Animations.OnFootThrowAcross, true, true);
                    user.isHoming = false;
                    user.ShieldCanReturn = false;
                    user.ShieldThrowLerp = 0;
                    user.shieldSpin = 0;
                    user.shieldRotationLerp = 0;
                }
                else if (!user.AssignedProfile.BackhandThrow && !isPlayingAnim(user.PoweredPed, Animations.OnFootThrowVertical))
                {
                    playAnimation(user.PoweredPed, Animations.OnFootThrowVertical, true, true);
                    user.isHoming = false;
                    user.ShieldCanReturn = false;
                    user.ShieldThrowLerp = 0;
                    user.shieldSpin = 0;
                    user.shieldRotationLerp = 0;
                }
            }
        }

        void ControlAIShieldThrow(PoweredUser user)
        {
            try
            {
                if (ShieldIsNotInAir(user.weapProp) /*&& user.holdWeap.IsInCdImage && user.holdWeap.IsValid*/)
                {
                    if (isPlayingAnim(user.PoweredPed, Animations.OnFootThrowVertical) || isPlayingAnim(user.PoweredPed, Animations.OnFootThrowAcross))
                    {
                        if (ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.OnFootThrowVertical) || ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.OnFootThrowAcross)) //if can throw shield now
                        {
                            createAndPushShieldAI(user);
                        }
                        else
                        {
                            PedFaceRotationExact(user.PoweredPed, DirToRotTest(Vector3.Normalize(user.Target.Position - user.PoweredPed.Position)), user.ShieldThrowLerp);
                            ControlLerp(user.ShieldThrowLerp, 1f, out user.ShieldThrowLerp);
                        }
                    }
                }

                ThrownShieldAction(user);
                AutoReturnShield(user);
            } catch { }
        }

        void createAndPushShieldAI(PoweredUser user)
        {
            user.pickupTimer = Game.GameTime + 2000; //pickup timer will be 2 seconds greater than the game time.
            user.autoReturnTimer = Game.GameTime + (int)user.AssignedProfile.ReturnInterval; //Setup a wait for x seconds until shield will come back to you automatically.
            user.autoReturnTimeout = Game.GameTime + 20000; //script will wait for 20 seconds; if shield hasn't returned, it will cancel the action.
            user.weapProp = World.CreateProp(user.holdWeap, Vector3.Zero, false, false);
            user.weapProp.AttachTo(user.PoweredPed, user.PoweredPed.GetBoneIndex(Bone.IK_R_Hand));
            user.weapProp.Detach();

            user.shieldIsThrown = true;
            addCollision(user.weapProp);
            SetEntityProofs(user.weapProp, true, false, true, true, true);

            ThrowAtTargetEntity(user, user.Target);

            user.PoweredPed.Weapons.Select(WeaponHash.Unarmed, true);
            user.PoweredPed.Weapons.Remove(user.CapShield);
        }

        bool ShieldIsNotInAir(Entity shield)
        {
            return shield == null || !shield.Exists();
        }

        void ThrowAtTargetEntity(PoweredUser user, Entity target)
        {
            //if (_TargettedEntity != null)
            //{
            try
            {
                if (user == PoweredUsers[0])
                {
                    ApplyVelocity(user.weapProp, ForwardDirFromCam(user.AssignedProfile.InitialThowForce), 1f);

                    _HomedEntity = _TargettedEntity;
                    user.isHoming = true;
                    user.firstHit = false;
                }
                else
                {
                    Vector3 DirBetweenPeds = (target.Position.Around(2f) - user.weapProp.Position).Normalized;
                    ApplyVelocity(user.weapProp, DirBetweenPeds, user.AssignedProfile.InitialThowForce);

                    user.isHoming = true;
                    user.firstHit = false;
                }
            }
            catch
            {
                UI.ShowSubtitle("Failed at throwing with speed");
            }
            //}
        }

        void DecideNextHomingTarget(Entity lastDamagedEntity, PoweredUser user)
        {
            user.firstHit = true;

            if (targettedEntities.Contains(lastDamagedEntity)) { targettedEntities.Remove(lastDamagedEntity); }

            if (targettedEntities.Count == 0)
            {
                user.isHoming = false;
                user.ShieldCanReturn = true;
            }
            else
            {
                _TargettedEntity = targettedEntities[targettedEntities.Count - 1]; //last entity in the list
                user.Target = _TargettedEntity;
                _HomedEntity = _TargettedEntity;
            }
        }

        void ThrownShieldAction(PoweredUser user)
        {
            try
            {
                bool userIsPlayer = user == PoweredUsers[0];
                if (user.weapProp != null && user.weapProp.Exists() && !user.weapProp.IsAttached())
                {
                    Vector3 vel = user.weapProp.Velocity;
                    //UI.ShowSubtitle("Velocity: " + vel.ToString());
                    if (Math.Abs(vel.X) >= 5 || Math.Abs(vel.Y) >= 5 || Math.Abs(vel.Z) >= 5) //if (weapProp.IsInAir)
                    {
                        AddShieldFXTrail(user.weapProp, Vector3.Zero, user);
                        //Function.Call(Hash.SET_ENTITY_PROOFS, weapProp, true, true, true, true, true, true, true, true);
                        user.weapProp.HasCollision = true;

                        Ped nearPed = World.GetClosestPed(user.weapProp.Position, 3.5f);

                        if (nearPed != null && nearPed != user.PoweredPed)
                        {
                            Vector3 Loc = pedDamageLoc(nearPed, user.weapProp.Position);

                            if (Loc != new Vector3(0, 0, 0))
                            {
                                if (!user.shieldedPeds.Contains(nearPed))
                                {
                                    DamagePedWithShield(nearPed, Loc, user);
                                    user.shieldedPeds.Add(nearPed);
                                }
                            }
                            else
                            {
                                if (user.shieldedPeds.Count > 0)
                                { user.shieldedPeds.Clear(); }
                            }
                        }

                        Vehicle nearVeh = World.GetClosestVehicle(user.weapProp.Position, 4.0f);

                        if (nearVeh != null && nearVeh.Exists())
                        {
                            if (!user.shieldedVehs.Contains(nearVeh))
                            {
                                DamageVehicleWithShield(nearVeh, user);
                                
                            }
                        }
                        else
                        {
                            if (user.shieldedVehs.Count > 0)
                            { user.shieldedVehs.Clear(); }
                        }

                        Prop[] nearProp = World.GetNearbyProps(user.weapProp.Position, 2.0f);
                        foreach (Prop e in nearProp)
                        {
                            if (nearProp != null && e != null && e != user.weapProp)
                            {
                                DamagePropWithShield(e, user);
                            }

                        }
                        
                        PropAchieveRotation(user.weapProp, user.shieldRotationLerp, new Vector3(user.shieldRotation.X, user.shieldRotation.Y, user.shieldSpin));
                        ControlLerp(user.shieldRotationLerp, 1f, out user.shieldRotationLerp);
                        user.shieldSpin += 800f * Game.LastFrameTime;
                    }

                    HomeIntoEntity(user, user.Target);

                    if (user.ShieldCanReturn)
                    {
                        ReturnShield(user);
                    }

                    if (user.AssignedProfile.QuickReturnShield)
                    {
                        if (user.pickupTimer < Game.GameTime && user.weapProp.Velocity.X < 2f && user.weapProp.Velocity.Y < 2f && user.weapProp.Velocity.Z < 2f)
                        {
                            user.ShieldCanReturn = true;
                        }
                    }
                }
            }
            catch { }
        }

        void HomeIntoEntity(PoweredUser user, Entity target)
        {
            if (user.isHoming && !user.ShieldCanReturn && user.AssignedProfile.AllowShieldCurve)
            {
                Vector3 DirBetweenEnt = user == PoweredUsers[0] ? (target.Position - user.weapProp.Position).Normalized : (target.Position.Around(2f) - user.weapProp.Position).Normalized;
                float weapToPlayerDist = DistanceBetween(user.weapProp.Position, user.PoweredPed.Position);
                float homedEntToPlayerDist = DistanceBetween(target.Position, user.PoweredPed.Position);

                if (!user.firstHit)
                {
                    if (weapToPlayerDist <= homedEntToPlayerDist) //if the distance from the player to the shield is less than the distance from the player to the target entity. i.e. if the shield did not miss the entity.
                    { ApplyVelocity(user.weapProp, DirBetweenEnt, user.AssignedProfile.CurveForce * Game.LastFrameTime); }
                }
                else
                {
                    ApplyVelocity(user.weapProp, DirBetweenEnt, 840f * Game.LastFrameTime);
                }
            }
        }

        void ReturnShield(PoweredUser user)
        {
            bool userIsPlayer = user == PoweredUsers[0];

            user.returnDir = (boneCoord(user.PoweredPed, "IK_R_Hand") - user.weapProp.Position).Normalized;

            if (DistanceBetween(user.weapProp.Position, boneCoord(user.PoweredPed, "IK_R_Hand")) >= 2.0f)
            {
                ApplyVelocity(user.weapProp, user.returnDir, 300f * Game.LastFrameTime);
            }
            else
            {
                ApplyVelocity(user.weapProp, user.returnDir, 420f * Game.LastFrameTime);
                //SetVelocityXYZ(weapProp, returnDir, -30f);
            }

            if (DistanceBetween(user.PoweredPed.Position, user.weapProp.Position) < 0.9f)
            {
                StopShieldFXTrail(user);
                user.weapProp.MarkAsNoLongerNeeded();
                user.weapProp.Delete();
                user.ShieldCanReturn = false;
                user.PoweredPed.Weapons.Give(user.CapShield, 1, true, true);
                user.shieldIsThrown = false;

                if (userIsPlayer)
                {
                    if (user.PoweredPed.IsOnFoot)
                    { createBallisticShieldsOnHand(); }
                    if (isUsingMotorcyle())
                    { createBallisticShieldOnBack(user); }

                    if (targettedEntities.Count > 0)
                    { targettedEntities.Clear(); }

                    InitiatedSlowMo = false;
                }
            }
        }

        void AutoReturnShield(PoweredUser user)
        {
            try
            {
                if (user.weapProp != null && user.weapProp.Exists() && !user.weapProp.IsAttached() && /*!ShieldCanReturn &&*/ canDoAutoReturnNow(user))
                {
                    /*if (!user.ShieldCanReturn)
                    {
                        //UI.ShowSubtitle("Auto-Returning Shield...", 1);
                        ReturnShield(user);
                    }*/
                    user.ShieldCanReturn = true;
                    if (canWarpShieldNow(user))
                    {
                        if (user == PoweredUsers[0])
                        {
                            user.weapProp.Position = GameplayCamera.GetOffsetInWorldCoords(new Vector3(0, -1, 2));
                        }
                        else
                        {
                            user.weapProp.Position = World.GetSafeCoordForPed(user.PoweredPed.Position.Around(15));
                        }
                    }
                }
            }
            catch { }
        }

        bool canDoAutoReturnNow(PoweredUser user)
        {
            if (user.autoReturnTimer < Game.GameTime)
            {
                if (user.autoReturnTimeout >= Game.GameTime)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        bool canWarpShieldNow(PoweredUser user)
        {
            if (user.autoReturnTimer < Game.GameTime - 5000)
            {
                if (user.autoReturnTimer >= Game.GameTime - 5250)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        void PickupShield(PoweredUser user)
        {
            if (hasTappedControl(GTA.Control.Sprint, true))
            {
                Prop[] props = World.GetNearbyProps(user.PoweredPed.Position, 2.5f, user.holdWeap);

                for (int i = 0; i < props.Length; i++)
                {
                    if (props != null && props.Length > 0)
                    {
                        if (!props[i].IsAttached())
                        {
                            if (user.pickupTimer <= Game.GameTime)
                            {
                                props[i].MarkAsNoLongerNeeded();
                                props[i].Delete();
                                user.PoweredPed.Weapons.Give(user.CapShield, 1, true, true);
                                createBallisticShieldsOnHand();
                                user.isHoming = false;
                            }
                        }
                    }
                }
            }
        }

        void TheOgSpecialAttacks(PoweredUser user)
        {
            if (user.PoweredPed.IsOnFoot)
            {
                if (user.AssignedProfile.AllowSpecialAttacks)
                {
                    if (IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName))
                    {
                        if (isAiming() && IsSelectedSpecialAttack(SpecialAttack.ChargingStar))
                        {
                            Game.DisableControlThisFrame(2, SpecialAttackButton);

                            if (isHoldingTackleButton() && user.tackleCounter < 2 && !hasGrabbedEntity)
                            {
                                TriggerChargingStar(user);
                            }
                            else if (!isHoldingTackleButton())
                            {
                                user.tackleCounter = 0;
                            }
                        }
                    }

                    if (user.isTackleKey)
                    {
                        ControlChargingStar(user);
                    }
                }

                if (user.AssignedProfile.AllowReflect)
                {
                    if (IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName))
                    {
                        Game.DisableControlThisFrame(2, reflectButton);

                        if (!isPlayingReflectAnimation(user.PoweredPed) && isHoldingReflectButton())
                        {
                            user.isTackleKey = false;
                            playReflectTask(user.PoweredPed);
                        }
                    }

                    if (isPlayingReflectAnimation(user.PoweredPed))
                    {
                        try
                        {
                            reflectProps();

                            Prop currWeap = user.PoweredPed.Weapons.CurrentWeaponObject;
                            Vector3 offsetFromShield = currWeap.GetOffsetFromWorldCoords(currWeap.Position + currWeap.ForwardVector * 0f + currWeap.UpVector * 2f + currWeap.RightVector * 1.5f);
                            AddShieldFXTrail(currWeap, offsetFromShield, user);

                            user.reflectTimer = Game.GameTime + 600;
                            user.PoweredPed.IsExplosionProof = true;
                            user.PoweredPed.IsFireProof = true;
                            user.isSturdy = true;

                            if (!user.isTackleKey)
                            {
                                ReflectHitVictims(currWeap, offsetFromShield, user);
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        if (ShieldIsNotInAir(user.weapProp))
                        {
                            if (!isPlayingTackleAnimation(user.PoweredPed))
                                StopShieldFXTrail(user);
                        }

                        if (user.isSturdy)
                        {
                            if (user.reflectTimer < Game.GameTime)
                            {
                                if (user.PoweredPed.IsOnFire) { stopEntityFire(user.PoweredPed); }
                                user.PoweredPed.IsExplosionProof = false;
                                user.PoweredPed.IsFireProof = false;
                            }
                            else
                            {
                                //UI.ShowSubtitle("REFLECT!", 1);
                            }
                        }

                        if (!isPlayingTackleAnimation(user.PoweredPed))
                        {
                            if (reflectedProps.Count > 0)
                            {
                                reflectedProps.Clear();
                            }
                        }
                    }
                }
            }
        }

        void TriggerChargingStar(PoweredUser user)
        {
            if (!isPlayingTackleAnimation(user.PoweredPed))
            {
                user.isTackleKey = true;
                user.setTackleProperties = true;
                user.tackleValue = 0;
                if (!user.PoweredPed.IsFalling) { user.tackleCounter++; }
                playTackleTask(user);
            }
        }

        void ControlChargingStar(PoweredUser user)
        {
            if (isPlayingTackleAnimation(user.PoweredPed))
            {
                bool userIsPlayer = user == PoweredUsers[0];
                if (userIsPlayer)
                {
                    if (hasAutoTarget && _TargettedEntity != null)
                    {
                        if (user.setTackleProperties)
                        {
                            user.tackleDir = (_TargettedEntity.Position - user.PoweredPed.Position).Normalized;
                            //ApplyVelocity(user.PoweredPed, tackleDir, 420f * Game.LastFrameTime);
                            //setTackleProperties = false;
                        }
                    }
                    else if (!hasAutoTarget || _TargettedEntity == null)
                    {
                        if (user.setTackleProperties)
                        {
                            user.tackleDir = ForwardDirFromCam(1f);
                            //ApplyVelocity(user.PoweredPed, tackleDir, 420f * Game.LastFrameTime);
                            //setTackleProperties = false;
                        }
                    }
                }
                else
                {
                    user.tackleDir = (user.Target.Position - user.PoweredPed.Position).Normalized;
                }

                Vector3 rotFromDir = DirToRotTest(user.tackleDir);
                Vector3 tackleRotation = new Vector3(rotFromDir.X/* - 30f*/, user.PoweredPed.Rotation.Y, rotFromDir.Z);

                user.PoweredPed.Rotation = Vector3.Lerp(user.PoweredPed.Rotation, tackleRotation, user.tackleValue * 2.5f);
                user.tackleValue += Game.LastFrameTime;
                if (user.tackleValue > 1f) { user.tackleValue = 1; }

                if (user.tackleValue >= 0.3f)
                {
                    if (user.setTackleProperties)
                    {
                        PlayChargeSound(user.PoweredPed);
                        user.currentPos = user.PoweredPed.Position;
                        ApplyVelocity(user.PoweredPed, user.tackleDir, 80f);
                        user.noRagdollTimer = Game.GameTime + 1000;
                        user.noPlayerRagdoll = true;
                        user.stopTackleNow = true;
                        user.setTackleProperties = false;
                    }

                    if (userIsPlayer) { reflectProps(); }

                    AddLightWithShadow(user.PoweredPed, 1f, 1f, 1f);

                    try
                    {
                        AddShieldFXTrail(user.PoweredPed.Weapons.CurrentWeaponObject, Vector3.Zero, user);
                    }
                    catch { }
                }

                ChargingStarVictims(user);
            }

            if (user.stopTackleNow)
            {
                if (DistanceBetween(user.currentPos, user.PoweredPed.Position) > 10f || (Math.Abs(user.PoweredPed.Velocity.X) + Math.Abs(user.PoweredPed.Velocity.Y) < 5f))
                {
                    user.PoweredPed.Velocity = Vector3.Zero;
                    user.stopTackleNow = false;
                }
            }

            if (user.noPlayerRagdoll)
            {
                if (user.noRagdollTimer >= Game.GameTime)
                {
                    Function.Call(Hash.SET_PED_CAN_RAGDOLL, user.PoweredPed, false);
                }
                else
                {
                    if (user.PoweredPed.HeightAboveGround < 1.5f)
                    {
                        Function.Call(Hash.SET_PED_CAN_RAGDOLL, user.PoweredPed, true);
                        user.noPlayerRagdoll = false;
                    }
                }
            }
        }

        void clearDamagedEntityListsOnTime(PoweredUser user)
        {
            //UI.ShowSubtitle("chargedEntities: " + (chargedEntities.Count + chargedEntities.Count));
            //UI.ShowSubtitle("allowMeleeTimer: " + allowMeleeTimer);

            if (user.allowDamageTimer < Game.GameTime)
            {
                if (user.chargedEntities.Count > 0)
                {
                    user.chargedEntities.Clear();
                }
                if (user.chargedEntities.Count > 0)
                {
                    user.chargedEntities.Clear();
                }

                if (user.allowMeleeTimer < Game.GameTime)
                { ResetRagdollBlockOfType(user.PoweredPed, ragdollBlock.WhenHitByVehicle); }
            }
            if (user.allowDamageTimer >= Game.GameTime || user.allowMeleeTimer >= Game.GameTime)
            {
                BlockPedRagdollOfType(user.PoweredPed, ragdollBlock.WhenHitByVehicle);
            }
        }

        void ReflectHitVictims(Prop weapObjSource, Vector3 hitPos, PoweredUser user)
        {
            Vector3 maxRangePos = weapObjSource.GetOffsetInWorldCoords(hitPos);
            Entity[] ents = World.GetNearbyEntities(maxRangePos, 5f);
            try
            {
                foreach (Entity e in ents)
                {
                    float entToDist = DistanceBetween(e.Position, user.PoweredPed.Position);

                    float maxToDist = DistanceBetween(maxRangePos, user.PoweredPed.Position);
                    Vector3 forceDir = (e.Position - weapObjSource.Position).Normalized;

                    if (EntityIsAPed(e))
                    {
                        if (entToDist <= maxToDist)
                        {
                            Ped p = (Ped)e;
                            if (p != user.PoweredPed && !p.IsInVehicle() && !user.chargedEntities.Contains(p))
                            {
                                DamagePed(user, p, (int)Math.Round((user.AssignedProfile.PChargeDamage) / 1.5f, 0), ragdollType.WideLegStuble, 1500, false, 1, forceDir, 20f * user.AssignedProfile.StrikingPowerMultiplier, user.PoweredPed.UpVector, 0.5f);
                                StartPtfxAtCoordNonLooped(PTFX.MeleeSpark, p.Position, Vector3.Zero, 3.0f);
                                user.chargedEntities.Add(p);
                            }
                        }
                    }
                    else if (EntityIsAVehicle(e))
                    {
                        float vehBoneToDist = DistanceBetween(vehDamageLoc((Vehicle)e, maxRangePos), user.PoweredPed.Position);

                        if (vehBoneToDist <= maxToDist)
                        {
                            Vehicle closeVeh = (Vehicle)e;
                            if (!user.chargedEntities.Contains(closeVeh))
                            {
                                DamageVehicle(user, closeVeh, (int)Math.Round((user.AssignedProfile.VChargeDamage) / 1.5f, 0), 1600, 250f, false, 1, forceDir, 8f * user.AssignedProfile.StrikingPowerMultiplier, closeVeh.UpVector, 0.5f);
                                Vector3 dmgLoc = vehDamageLoc((Vehicle)e, maxRangePos);
                                AddShock(dmgLoc, e.Position.Around(0.5f), user.PoweredPed);
                                StartPtfxAtCoordNonLooped(PTFX.MeleeSpark, dmgLoc, Vector3.Zero, 5.0f);
                                user.chargedEntities.Add(closeVeh);
                            }
                        }
                    }
                    else if (EntityIsAnObject(e))
                    {
                        Prop pr = (Prop)e;
                        if (IsEntityAttachedToAnyPed(pr) && pr != weapObjSource)
                        {
                            if (entToDist <= maxToDist)
                            {
                                Ped p = (Ped)GetAttachedEntity(pr);

                                if (!user.chargedEntities.Contains(p) && p != user.PoweredPed)
                                {
                                    DamagePed(user, p, (int)Math.Round((user.AssignedProfile.PChargeDamage) / 1.5f, 0), ragdollType.WideLegStuble, 1500, false, 1, forceDir, 20f * user.AssignedProfile.StrikingPowerMultiplier, user.PoweredPed.UpVector, 0.5f);
                                    StartPtfxAtCoordNonLooped(PTFX.MeleeSpark, p.Position, Vector3.Zero, 3.0f);
                                    user.chargedEntities.Add(p);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        void ChargingStarVictims(PoweredUser user)
        {
            Prop weapObj = user.PoweredPed.Weapons.CurrentWeaponObject;
            Entity[] ents = World.GetNearbyEntities(user.PoweredPed.Position + user.PoweredPed.ForwardVector * 1.2f, 5f);
            try
            {
                foreach (Entity e in ents)
                {
                    if (EntityIsAPed(e))
                    {
                        Ped p = (Ped)e;
                        if (p != user.PoweredPed && p.IsTouching(user.PoweredPed) && !p.IsInVehicle() && user.tackleValue >= 0.3f && !user.chargedEntities.Contains(p))
                        {
                            DamagePed(user, p, user.AssignedProfile.PChargeDamage, ragdollType.WideLegStuble, 3000, true, (int)Math.Round((user.AssignedProfile.PChargeDamage) / 1.5f, 0));
                            SetVelocityXYZ(p, user.PoweredPed.Velocity, user.AssignedProfile.StrikingPowerMultiplier);
                            user.chargedEntities.Add(p);
                        }
                    }
                    else if (EntityIsAVehicle(e))
                    {
                        Vehicle closeVeh = (Vehicle)e;
                        if ((closeVeh.IsTouching(user.PoweredPed) || closeVeh.IsTouching(weapObj)) && user.tackleValue >= 0.3f)
                        {
                            if (!user.chargedEntities.Contains(closeVeh))
                            {
                                DamageVehicle(user, closeVeh, user.AssignedProfile.VChargeDamage, 1600f, 1600f, true, (int)Math.Round((user.AssignedProfile.VChargeDamage) / 1.5f, 0), user.tackleDir, 15f * user.AssignedProfile.StrikingPowerMultiplier, closeVeh.UpVector, 1f);
                                user.chargedEntities.Add(closeVeh);
                            }
                        }
                    }
                    else if (EntityIsAnObject(e))
                    {
                        Prop pr = (Prop)e;
                        if ((pr.IsTouching(user.PoweredPed) || pr.IsTouching(weapObj)) && user.tackleValue >= 0.3f)
                        {
                            if (IsEntityAttachedToAnyPed(pr) && pr != weapObj)
                            {
                                Ped p = (Ped)GetAttachedEntity(pr);

                                if (!user.chargedEntities.Contains(p) && p != user.PoweredPed)
                                {
                                    DamagePed(user, p, user.AssignedProfile.PChargeDamage, ragdollType.WideLegStuble, 3000, true, (int)Math.Round((user.AssignedProfile.PChargeDamage) / 1.5f, 0));
                                    SetVelocityXYZ(p, user.PoweredPed.Velocity, user.AssignedProfile.StrikingPowerMultiplier);
                                    user.chargedEntities.Add(p);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        //bool hasMadeImpact = false;
        void NewSpecialAttacks(PoweredUser user)
        {
            if (user.PoweredPed.IsOnFoot)
            {
                if (user.AssignedProfile.AllowSpecialAttacks)
                {
                    if (IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName))
                    {
                        if (isAiming() && IsSelectedSpecialAttack(SpecialAttack.Shield2Ground))
                        {
                            Game.DisableControlThisFrame(2, SpecialAttackButton);

                            if (isHoldingTackleButton() && !isPlayingAnim(user.PoweredPed, Animations.Shield2Ground) && !hasGrabbedEntity)
                            {
                                playAnimation(user.PoweredPed, Animations.Shield2Ground);
                            }
                        }

                        ControlGroundSmash(user);
                    }
                }
            }
        }

        void ControlGroundSmash(PoweredUser user)
        {
            if (isPlayingAnim(user.PoweredPed, Animations.Shield2Ground))
            {
                bool userIsPlayer = user == PoweredUsers[0];
                if (ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.Shield2Ground))
                {
                    if (!user.hasMadeImpact)
                    {
                        Vector3 smashPos = user.PoweredPed.Weapons.CurrentWeaponObject.Position;
                        AddDecal(smashPos, DecalTypes.bang_concrete_bang2, 10f, 10f);
                        StartPtfxAtCoordNonLooped(PTFX.GroundSmash, new Vector3(smashPos.X, smashPos.Y, GetGroundZ(smashPos)), Vector3.Zero, 4.0f);
                        PlayHeavySmashSound(smashPos);

                        Entity[] nearEnts = World.GetNearbyEntities(smashPos, 10f);

                        try
                        {
                            for (int i = 0; i < nearEnts.Length; i++)
                            {
                                Entity e = nearEnts[i];
                                Vector3 dir = (e.Position - user.PoweredPed.Position).Normalized;
                                if (!e.IsInAir && e != user.PoweredPed)
                                {
                                    try
                                    {
                                        DamagePed(user, (Ped)e, (int)Math.Round((user.AssignedProfile.PChargeDamage) / 1.5f, 0), ragdollType.WideLegStuble, 800, false, 1, dir, 0.1f * user.AssignedProfile.StrikingPowerMultiplier, user.PoweredPed.UpVector, 1.5f);
                                    }
                                    catch { }

                                    try
                                    {
                                        DamageVehicle(user, (Vehicle)e, (int)Math.Round((user.AssignedProfile.VChargeDamage) / 1.2f, 0), 800f, 2000f, false, 1, dir, 1f * user.AssignedProfile.StrikingPowerMultiplier, e.UpVector, 5f);
                                    }
                                    catch { }

                                    ApplyVelocity(e, Vector3.WorldUp, 5f);
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }
                        catch { }

                        if (user.AssignedProfile.AllowSlowMoAim && userIsPlayer) { Game.TimeScale = 1f; }
                        user.hasMadeImpact = true;
                    }
                }
                else
                {
                    user.hasMadeImpact = false;

                    if (!isHoldingTackleButton() && isAiming())
                    {
                        if (user.AssignedProfile.AllowSlowMoAim && userIsPlayer) { Game.TimeScale = 0.3f; }
                    }
                    else
                    {
                        if (user.AssignedProfile.AllowSlowMoAim && userIsPlayer) { Game.TimeScale = 1f; }
                    }
                }
            }
        }

        void DamagePed(PoweredUser attacker, Ped victim, int damageAmount, ragdollType type, int ragdollTimeMS, bool considerAttackerHeldWeapon = false, int damageAmountBareHands = 40, Vector3 forceDirection = default(Vector3), float forceDirectionMultiplier = 0f, Vector3 forceRotation = default(Vector3), float forceRotationMultiplier = 0f, float forceDirectionMultiplierBareHands = 0f, float forceRotationMultiplierBareHands = 0f)
        {
            PlayPedHitSound(victim);
            PlayPedPainSound(victim);
            SetPedRagdoll(victim, ragdollTimeMS, type);
            
            if (considerAttackerHeldWeapon)
            {
                Prop weapObj = attacker.PoweredPed.Weapons.CurrentWeaponObject;
                if (weapObj != null)
                {
                    victim.ApplyDamage(damageAmount);
                    victim.ApplyForce(forceDirection * forceDirectionMultiplier, forceRotation * forceRotationMultiplier);
                }
                else
                {
                    victim.ApplyDamage(damageAmountBareHands);
                    victim.ApplyForce(forceDirection * forceDirectionMultiplierBareHands, forceRotation * forceRotationMultiplierBareHands);
                }
            }
            else
            {
                victim.ApplyDamage(damageAmount);
                victim.ApplyForce(forceDirection * forceDirectionMultiplier, forceRotation * forceRotationMultiplier);
            }

            AddBloodToPed(victim);
            AddShock(victim.Position, Vector3.Zero, attacker.PoweredPed);

            attacker.allowDamageTimer = Game.GameTime + attacker.allowDamageWaitInterval;
        }

        void DamageVehicle(PoweredUser attacker, Vehicle victim, int damageAmount, float visualDamageAmount = 800f, float radiusOfDamage = 1600f, bool considerAttackerHeldWeapon = false, int damageAmountBareHands = 40, Vector3 forceDirection = default(Vector3), float forceDirectionMultiplier = 0f, Vector3 forceRotation = default(Vector3), float forceRotationMultiplier = 0f, float forceDirectionMultiplierBareHands = 0f, float forceRotationMultiplierBareHands = 0f)
        {
            PlayVehHitSound(victim);

            if (considerAttackerHeldWeapon)
            {
                Prop weapObj = attacker.PoweredPed.Weapons.CurrentWeaponObject;
                if (weapObj != null)
                {
                    SetVehicleDamage(victim, damageAmount);
                    SetVehicleVisualDamage(victim, weapObj.Position, visualDamageAmount, radiusOfDamage);
                    victim.ApplyForce(forceDirection * forceDirectionMultiplier, forceRotation * forceRotationMultiplier);
                }
                else
                {
                    SetVehicleDamage(victim, damageAmountBareHands);
                    SetVehicleVisualDamage(victim, attacker.PoweredPed.Position, visualDamageAmount, radiusOfDamage);
                    victim.ApplyForce(forceDirection * forceDirectionMultiplierBareHands, forceRotation * forceRotationMultiplierBareHands);
                }
            }
            else
            {
                SetVehicleDamage(victim, damageAmount);
                SetVehicleVisualDamage(victim, attacker.PoweredPed.Position, visualDamageAmount, radiusOfDamage);
                victim.ApplyForce(forceDirection * forceDirectionMultiplier, forceRotation * forceRotationMultiplier);
            }

            AddShock(victim.Position, Vector3.Zero, attacker.PoweredPed);

            if (victim.Model.IsBike || victim.Model.IsBicycle) { if (victim.Driver != null && victim.Driver.Exists()) { Function.Call(Hash.KNOCK_PED_OFF_VEHICLE, victim.Driver); } }

            attacker.allowDamageTimer = Game.GameTime + attacker.allowDamageWaitInterval;
        }

        int RecoverToTankTimer;
        int HealthDuringShield;
        void TankMode(PoweredUser user)
        {
            if (user.PoweredPed.IsOnFoot && IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName))
            {
                if (IsPedInStealth(user.PoweredPed))
                {
                    if (user.AssignedProfile.AllowTank)
                    {
                        if (!isPlayingTankAnimation(user.PoweredPed))
                        {
                            TankProofON = true;
                            playTankAnimation(user.PoweredPed);
                        }
                        else
                        {
                            user.PoweredPed.IsExplosionProof = true;

                            getExplosionDmg();

                            AutoReflectBullets(user.PoweredPed.Weapons.CurrentWeaponObject.Position, 5f, user);

                            HealthDuringShield = user.PoweredPed.Health;

                            RecoverToTankTimer = Game.GameTime + 1000;

                            if (user.PoweredPed.IsClimbing || user.PoweredPed.IsGoingIntoCover || user.PoweredPed.IsInCover() || user.PoweredPed.IsSwimming || user.PoweredPed.IsSwimmingUnderWater || user.PoweredPed.IsGettingIntoAVehicle || user.PoweredPed.IsInVehicle() || user.PoweredPed.IsTryingToEnterALockedVehicle || user.PoweredPed.IsJacking
                                || Game.IsControlPressed(2, GTA.Control.VehicleExit) || justPressedThrowButton() || isHoldingReflectButton() || isHoldingTackleButton())
                            {
                                exitTankMode(user.PoweredPed);
                            }
                            if (Game.IsControlPressed(2, GTA.Control.VehicleExit))
                            {
                                exitTankMode(user.PoweredPed);
                            }
                        }
                    }
                }
                else
                {
                    exitTankMode(user.PoweredPed);
                }

                if (RecoverToTankTimer >= Game.GameTime && user.PoweredPed.IsRagdoll)
                {
                    user.PoweredPed.Health = HealthDuringShield;
                    SetPedRagdoll(user.PoweredPed, 1, ragdollType.StiffBody);
                    Function.Call(Hash.SET_PED_STEALTH_MOVEMENT, user.PoweredPed, 0, "DEFAULT_ACTION");
                    RecoverToTankTimer = 0;
                }
            }
            else if (!user.PoweredPed.IsOnFoot || !IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName))
            { exitTankMode(user.PoweredPed); }
        }

        void exitTankMode(Ped ped)
        {
            if (TankProofON)
            {
                stopTankAnimation(ped);
                ped.IsExplosionProof = false;
                TankProofON = false;
                stopDefaultStealthTask(ped);
            }
        }

        void AutoReflectBullets(Vector3 impactPos, float forwardValue, PoweredUser user)
        {
            try
            {
                if (Function.Call<bool>(Hash.HAS_BULLET_IMPACTED_IN_AREA, impactPos.X, impactPos.Y, impactPos.Z, 0.5f, 0, 0))
                {
                    ShootBullet(impactPos, player.GetOffsetInWorldCoords(new Vector3(GetRandomFloat(user.rng, -1, 1), forwardValue, GetRandomFloat(user.rng, -1, 1))), player, WeaponHash.APPistol, 90, -1, true);
                    ShootBullet(impactPos, player.GetOffsetInWorldCoords(new Vector3(0, forwardValue, 0)), player, WeaponHash.APPistol, 90, -1, true);

                    if (isHoldingControl(GTA.Control.Aim, true) && _TargettedEntity != null)
                    {
                        ShootBullet(impactPos, _TargettedEntity.Position, player, WeaponHash.APPistol, 90, -1, true);
                    }
                }
            }
            catch { }
        }

        void ShootBullet(Vector3 sourcePosition, Vector3 targetPosition, Ped owner, Model model, int damage, float speed = -1.0f, bool isAudible = true, bool isInvisible = false, bool p7 = true)
        {
            Function.Call(Hash.SHOOT_SINGLE_BULLET_BETWEEN_COORDS, sourcePosition.X, sourcePosition.Y, sourcePosition.Z, targetPosition.X, targetPosition.Y, targetPosition.Z, damage, p7, model.Hash, owner.Handle, isAudible, isInvisible, speed);
        }

        void getExplosionDmg()
        {
            if (player.IsExplosionProof && TankProofON)
            {
                foreach (GTA.ExplosionType exploType in Enum.GetValues(typeof(GTA.ExplosionType)))
                {
                    Vector3 f_pos = player.Position + player.ForwardVector * 1.5f;
                    bool FrontExploExists = Function.Call<bool>(Hash.IS_EXPLOSION_IN_SPHERE, (int)exploType, f_pos.X, f_pos.Y, f_pos.Z, 1.5f);

                    Vector3 b_pos = player.Position + player.ForwardVector * -1.5f;
                    bool BackExploExists = Function.Call<bool>(Hash.IS_EXPLOSION_IN_SPHERE, (int)exploType, b_pos.X, b_pos.Y, b_pos.Z, 1.5f);

                    Vector3 l_pos = player.Position + player.RightVector * -1.5f;
                    bool LeftExploExists = Function.Call<bool>(Hash.IS_EXPLOSION_IN_SPHERE, (int)exploType, l_pos.X, l_pos.Y, l_pos.Z, 1.5f);

                    Vector3 r_pos = player.Position + player.RightVector * 1.5f;
                    bool RightExploExists = Function.Call<bool>(Hash.IS_EXPLOSION_IN_SPHERE, (int)exploType, r_pos.X, r_pos.Y, r_pos.Z, 1.5f);

                    if (FrontExploExists || BackExploExists || LeftExploExists || RightExploExists) {
                        //UI.Notify("ExplosionType: " + exploType);
                        PlayPedPainSound(player);

                        if (FrontExploExists)
                        {
                            player.ApplyDamage(15);
                            ApplyVelocity(player, -player.ForwardVector, 2f);
                        }
                        else if (BackExploExists)
                        {
                            player.ApplyDamage(40);
                            ApplyVelocity(player, player.ForwardVector, 4f);
                        }
                        else if (LeftExploExists)
                        {
                            player.ApplyDamage(40);
                            ApplyVelocity(player, player.RightVector, 4f);
                        }
                        else if (RightExploExists)
                        {
                            player.ApplyDamage(15);
                            ApplyVelocity(player, -player.RightVector, 2f);
                        }
                    }
                }
            }
        }

        void ComboMeleeAbility(PoweredUser user)
        {
            if (user.PoweredPed.IsOnFoot)
            {
                if (isWeaponMelee(user.PoweredPed) && !hasGrabbedEntity)
                {
                    if (user.AssignedProfile.AllowCustomMelee)
                    {
                        Game.DisableControlThisFrame(2, GTA.Control.Attack);
                        Game.DisableControlThisFrame(2, GTA.Control.MeleeAttackLight);

                        bool CanAttackWithShield = IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName) && isHoldingControl(GTA.Control.Attack, true) && !isAiming();
                        bool CanAttackWithoutShield = !IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName) && (isHoldingControl(GTA.Control.Attack, true) || isHoldingControl(GTA.Control.MeleeAttackLight));
                        if (CanAttackWithShield || CanAttackWithoutShield)
                        {
                            if (user.allowMeleeTimer < Game.GameTime)
                            {
                                user.rInt = GetUniqueRandomInt(user.rInt, 0, 6, user);
                                user.allowMeleeTimer = Game.GameTime + user.allowMeleeWaitInterval;

                                switch (user.rInt)
                                {
                                    case 0: { playAnimation(user.PoweredPed, Animations.BackSlap); return; }
                                    case 1: { playAnimation(user.PoweredPed, Animations.LeftHook); return; }
                                    case 2: { playAnimation(user.PoweredPed, Animations.RightHook); return; }
                                    case 3: { playAnimation(user.PoweredPed, Animations.StrongKick); return; }
                                    case 4: { playAnimation(user.PoweredPed, Animations.SmackDown); return; }
                                    case 5: { playAnimation(user.PoweredPed, Animations.Uppercut); return; }
                                }
                            }
                        }
                    }

                    ControlMeleeCombo(user);
                }
            }
        }

        void ControlMeleeCombo(PoweredUser user)
        {
            if (isPlayingAnim(user, Animations.BackSlap) || isPlayingAnim(user, Animations.LeftHook) || isPlayingAnim(user, Animations.RightHook) || isPlayingAnim(user, Animations.StrongKick) || isPlayingAnim(user, Animations.SmackDown) || isPlayingAnim(user, Animations.Uppercut))
            {
                try
                {
                    Vector3 source;
                    Entity e;

                    if (user == PoweredUsers[0])
                    {
                        source = user.PoweredPed.Position;
                        e = getClosest(source, getClosestNonCompanionPed(source, 6f, user.PoweredPed, true), getClosestVehicle(source, 8f, null));
                    }
                    else
                    {
                        if (DistanceBetween(user.Target.Position, user.PoweredPed.Position) <= 6)
                        {
                            e = user.Target;
                        }
                        else
                        {
                            e = null;
                        }
                    }

                    if (e != null)
                    {
                        if (ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.BackSlap) || ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.LeftHook) || ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.RightHook) || ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.StrongKick) || ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.SmackDown) || ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.Uppercut))
                        {
                            if (IsActionTimeDuringAnim(user.PoweredPed, Animations.BackSlap) || IsActionTimeDuringAnim(user.PoweredPed, Animations.LeftHook) || IsActionTimeDuringAnim(user.PoweredPed, Animations.RightHook) || IsActionTimeDuringAnim(user.PoweredPed, Animations.StrongKick) || IsActionTimeDuringAnim(user.PoweredPed, Animations.SmackDown) || IsActionTimeDuringAnim(user.PoweredPed, Animations.Uppercut))
                            {
                                if (EntityIsAPed(e))
                                {
                                    if (user.PoweredPed.IsTouching(e) || isPedCloseEnough(user.PoweredPed, (Ped)e, 1.0f))
                                    {
                                        if (!user.chargedEntities.Contains((Ped)e))
                                        {
                                            int hitDmg = (int)Math.Round((user.AssignedProfile.PChargeDamage) / 1.5f, 0);
                                            if (hitDmg < e.Health)
                                            {
                                                //DamagePed(user.PoweredPed, (Ped)e, hitDmg, ragdollType.WideLegStuble, 400, true, hitDmg - 5, user.PoweredPed.UpVector, 6.0f, user.PoweredPed.UpVector, 1.5f, 5.5f, 0.5f);

                                                DamagePed(user, (Ped)e, hitDmg, ragdollType.WideLegStuble, 400, true, hitDmg - 5, user.PoweredPed.UpVector, 0.1f * user.AssignedProfile.StrikingPowerMultiplier, user.PoweredPed.UpVector, 1.5f, 0.1f * user.AssignedProfile.StrikingPowerMultiplier, 0.5f);
                                                StartPtfxAtCoordNonLooped(PTFX.MeleeSpark, user.PoweredPed.GetBoneCoord(user.MeleeDamageBoneSource), Vector3.Zero, 3.0f);
                                                if (e.HeightAboveGround >= user.PoweredPed.HeightAboveGround) //if enemy is higher than the user.PoweredPed
                                                {
                                                    SetVelocityXYZ(e, user.PoweredPed.ForwardVector, 3f * user.AssignedProfile.StrikingPowerMultiplier, true, Vector3.WorldUp, 4.5f);
                                                }
                                                else
                                                {
                                                    SetVelocityXYZ(e, user.PoweredPed.ForwardVector, 3f * user.AssignedProfile.StrikingPowerMultiplier, true, Vector3.WorldUp, 5.5f);
                                                }
                                            }
                                            else
                                            {
                                                //DamagePed(user.PoweredPed, (Ped)e, hitDmg, ragdollType.WideLegStuble, 1200, true, hitDmg - 5, user.PoweredPed.ForwardVector, 20f, user.PoweredPed.UpVector, 2.5f, 15f, 0.5f);

                                                DamagePed(user, (Ped)e, hitDmg, ragdollType.WideLegStuble, 1200, true, hitDmg - 5, user.PoweredPed.ForwardVector, 0.1f * user.AssignedProfile.StrikingPowerMultiplier, user.PoweredPed.UpVector, 20.5f, 0.1f * user.AssignedProfile.StrikingPowerMultiplier, 10.5f);
                                                StartPtfxAtCoordNonLooped(PTFX.MeleeSpark, user.PoweredPed.GetBoneCoord(user.MeleeDamageBoneSource), Vector3.Zero, 4.0f);
                                                if (e.HeightAboveGround >= user.PoweredPed.HeightAboveGround) //if enemy is higher than the user.PoweredPed
                                                {
                                                    SetVelocityXYZ(e, user.PoweredPed.ForwardVector, 10.5f * user.AssignedProfile.StrikingPowerMultiplier, true, Vector3.WorldUp, -3.0f * user.AssignedProfile.StrikingPowerMultiplier);
                                                }
                                                else
                                                {
                                                    SetVelocityXYZ(e, user.PoweredPed.ForwardVector, 10.5f * user.AssignedProfile.StrikingPowerMultiplier, true, Vector3.WorldUp, 3.5f);
                                                }
                                            }
                                            user.chargedEntities.Add((Ped)e);
                                        }
                                    }
                                }
                                else if (EntityIsAVehicle(e))
                                {
                                    if (user.PoweredPed.IsTouching(e))
                                    {
                                        if (!user.chargedEntities.Contains((Vehicle)e))
                                        {
                                            DamageVehicle(user, (Vehicle)e, user.AssignedProfile.VChargeDamage, 800f, 1600f, false, 1, user.PoweredPed.ForwardVector, 1f, e.UpVector, 8f);
                                            StartPtfxAtCoordNonLooped(PTFX.MeleeSpark, user.PoweredPed.GetBoneCoord(user.MeleeDamageBoneSource), Vector3.Zero, 4.0f);
                                            SetVelocityXYZ(e, user.PoweredPed.ForwardVector, 2.5f * user.AssignedProfile.StrikingPowerMultiplier);
                                            user.chargedEntities.Add((Vehicle)e);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (!isPlayingTackleAnimation(user.PoweredPed))
                            {
                                Vector3 tempdir = (e.Position - user.PoweredPed.Position).Normalized;
                                user.PoweredPed.Heading = DirectionToHeading(tempdir);
                                ApproachTarget(e, user.PoweredPed, true);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        void GrabAbility(PoweredUser user)
        {
            if (user.PoweredPed.IsOnFoot)
            {
                if (isWeaponMelee(user.PoweredPed) && !hasGrabbedEntity)
                {
                    if (justPressedGrabButton() && !isPlayingAnim(user.PoweredPed, Animations.GrabPed) && !isPlayingAnim(user.PoweredPed, Animations.PickupVeh) && !isPlayingAnim(user.PoweredPed, Animations.HoldVeh))
                    {
                        Vector3 source = user.PoweredPed.Position;
                        Entity closestEnt = getClosest(source, getClosestNonCompanionPed(source, 5f, user.PoweredPed), getClosestVehicle(source, 8f, null));

                        if (EntityIsAPed(closestEnt))
                        {
                            if (user.PoweredPed.Weapons.Current.Hash != WeaponHash.Unarmed)
                            {
                                user.PoweredPed.Weapons.Select(WeaponHash.Unarmed, true);
                            }
                            playGrabPedTask(user.PoweredPed);
                        }
                        else if (EntityIsAVehicle(closestEnt))
                        {
                            if (CanVehicleBeLifted((Vehicle)closestEnt, user.AssignedProfile))
                            {
                                if (user.PoweredPed.Weapons.Current.Hash != WeaponHash.Unarmed)
                                {
                                    user.PoweredPed.Weapons.Select(WeaponHash.Unarmed, true);
                                }
                                playAnimation(user.PoweredPed, Animations.PickupVeh);
                            }
                            else
                            {
                                playAnimation(user.PoweredPed, Animations.StrongKick);
                            }
                        }
                    }

                    if (isPlayingAnim(user.PoweredPed, Animations.GrabPed))
                    {
                        try
                        {
                            Vector3 source = user.PoweredPed.Position;
                            Ped e = getClosestNonCompanionPed(source, 2f, user.PoweredPed);
                            if (ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.GrabPed)) //if can grab ped now
                            {

                                if (e.HasBone("IK_Head"))
                                {
                                    e.Detach();
                                    Wait(10);
                                    //e.Task.ClearAllImmediately();
                                    AttachInvisibleObjectToCap(user.PoweredPed);
                                    SetPedRagdoll(e, 800, ragdollType.Normal);
                                    e.SetNoCollision(user.PoweredPed, true);
                                    AttachEntityToEntityPhysically(e, Hand2HeadObj, boneIndexByID(e, 12844), boneIndexByName(Hand2HeadObj, "chassis"), Vector3.Zero, Vector3.Zero, Vector3.Zero);
                                    PlayPedPainSound(e);
                                    //AddShock(e.Position, Vector3.Zero, user.PoweredPed);
                                    e.ApplyDamage(20);
                                    grabbedEntity = e;
                                    hasGrabbedEntity = true;
                                }
                            }
                            else
                            {
                                Vector3 tempdir = (e.Position - user.PoweredPed.Position).Normalized;
                                user.PoweredPed.Heading = DirectionToHeading(tempdir);
                                ApproachTarget(e, user.PoweredPed, false);
                            }
                        }
                        catch { }
                    }
                    if (isPlayingAnim(user.PoweredPed, Animations.PickupVeh))
                    {
                        try
                        {
                            Vector3 source = user.PoweredPed.Position;
                            Vehicle e = getClosestVehicle(source, 6f, null);
                            if (ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.PickupVeh)) //if can pickup veh now
                            {
                                if (CanVehicleBeLifted(e, user.AssignedProfile))
                                {
                                    e.SetNoCollision(user.PoweredPed, true);
                                    if (e.Model.GetDimensions().Z >= e.Model.GetDimensions().X)
                                    {
                                        AttachEntityToEntity(e, user.PoweredPed, /*boneIndexByID(user.PoweredPed, 24818)*/ user.PoweredPed.GetBoneIndex(Bone.IK_R_Hand), new Vector3(user.PoweredPed.GetOffsetFromWorldCoords(e.Position).X + (e.Model.GetDimensions().X / 2) - 0.2f, 0, -0.35f), new Vector3(70f, -20f, 20f), false, false, false, 2, true);
                                    }
                                    else
                                    {
                                        //AttachEntityToEntity(e, user.PoweredPed, /*boneIndexByID(user.PoweredPed, 24818)*/ user.PoweredPed.GetBoneIndex(Bone.IK_R_Hand), new Vector3((e.Model.GetDimensions().Z / 2) - 0.15f, 0, -0.2f), new Vector3(60f, -85f, 180f), false, false, false, 2, true);
                                        AttachEntityToEntity(e, user.PoweredPed, user.PoweredPed.GetBoneIndex(Bone.IK_R_Hand), new Vector3((e.Model.GetDimensions().X / 2), 0, -0.2f), new Vector3(70f, -20f, 20f), false, false, false, 2, true);
                                    }
                                    e.Alpha = 140;
                                    grabbedEntity = e;
                                    hasGrabbedEntity = true;
                                }
                            }
                            else
                            {
                                Vector3 tempdir = (e.Position - user.PoweredPed.Position).Normalized;
                                user.PoweredPed.Heading = DirectionToHeading(tempdir);
                                ApproachTarget(e, user.PoweredPed, false);
                            }
                        }
                        catch { }
                    }
                }

                if (hasGrabbedEntity)
                {
                    Game.DisableControlThisFrame(2, GTA.Control.Attack);
                    Game.DisableControlThisFrame(2, GTA.Control.MeleeAttackLight);
                    Game.DisableControlThisFrame(2, GTA.Control.Cover);
                    Game.DisableControlThisFrame(2, GTA.Control.VehicleExit);
                    if (EntityIsAVehicle(grabbedEntity))
                    {
                        Game.DisableControlThisFrame(2, GTA.Control.Jump);
                        Game.DisableControlThisFrame(2, GTA.Control.Sprint);
                    }
                    if(EntityIsAPed(grabbedEntity))
                    {
                        if (!((Ped)grabbedEntity).IsRagdoll)
                        {
                            SetPedRagdoll((Ped)grabbedEntity, 800, ragdollType.Normal); //keep ped ragdolled if grabbed
                        }
                        if (!grabbedEntity.IsAttached())
                        {
                            AttachEntityToEntityPhysically(grabbedEntity, Hand2HeadObj, boneIndexByID((Ped)grabbedEntity, 12844), boneIndexByName(Hand2HeadObj, "chassis"), Vector3.Zero, Vector3.Zero, Vector3.Zero);
                        }
                    }
                    if (CanSwitchToHoldVehNow())
                    {
                        playAnimation(user.PoweredPed, Animations.HoldVeh);
                    }
                    else
                    {
                        if (EntityIsAVehicle(grabbedEntity) && !isPlayingAnim(user.PoweredPed, Animations.HoldVeh) && !isPlayingAnim(user.PoweredPed, Animations.ThrowPed))
                        {
                            playAnimation(user.PoweredPed, Animations.HoldVeh);
                        }
                    }
                    if (isPlayingAnim(user.PoweredPed, Animations.HoldVeh)) //freeze anim
                    {
                        if (getAnimCurrentTime(user.PoweredPed, getAnimDict(Animations.HoldVeh), getAnimName(Animations.HoldVeh)) >= 0.649f)
                        {
                            setAnimCurrentTime(user.PoweredPed, getAnimDict(Animations.HoldVeh), getAnimName(Animations.HoldVeh), 0.6490000f);
                        }
                    }
                    if (justPressedGrabButton() || justPressedThrowFromMoto() || hasTappedControl(GTA.Control.Attack, true) || hasTappedControl(GTA.Control.MeleeAttackLight, true))
                    {
                        if (!isPlayingAnim(user.PoweredPed, Animations.ThrowPed))
                        {
                            if (isPlayingAnim(user.PoweredPed, Animations.HoldVeh))
                            {
                                setAnimCurrentTime(user.PoweredPed, getAnimDict(Animations.HoldVeh), getAnimName(Animations.HoldVeh), 0.99f);
                                user.PoweredPed.Task.ClearAnimation(getAnimDict(Animations.HoldVeh), getAnimName(Animations.HoldVeh));
                            }
                            playAnimation(user.PoweredPed, Animations.ThrowPed);
                        }
                    }
                    if (isPlayingAnim(user.PoweredPed, Animations.ThrowPed))
                    {
                        if (!ActionStartTimePassedDuringAnim(user.PoweredPed, Animations.ThrowPed)) //if can throw ped now
                        {
                            //user.PoweredPedFaceCameraZRot();
                            user.PoweredPed.Heading = DirectionToHeading(GameplayCamera.Direction);
                        }
                        else
                        {
                            ThrowGrabbedEntity(user, false);
                        }
                    }
                    else if (user.PoweredPed.IsRagdoll)
                    {
                        ThrowGrabbedEntity(user, true);
                    }
                }
            }
        }

        void ThrowGrabbedEntity(PoweredUser user, bool gentle)
        {
            try
            {
                if (EntityIsAVehicle(grabbedEntity) || isPlayingAnim(user.PoweredPed, Animations.HoldVeh))
                {
                    grabbedEntity.ResetAlpha();
                    grabbedEntity.Detach();
                    if (!gentle)
                    {
                        grabbedEntity.Rotation = new Vector3(grabbedEntity.Rotation.X, grabbedEntity.Rotation.Y + 180, grabbedEntity.Rotation.Z);
                        grabbedEntity.ApplyForce(user.PoweredPed.ForwardVector * 1f, grabbedEntity.UpVector * 2f);
                        SetVelocityXYZ(grabbedEntity, ForwardDirFromCam(1f), CalculatedVehicleThrowForceFinal((Vehicle)grabbedEntity, user.AssignedProfile));
                        AddShock(grabbedEntity.Position, Vector3.Zero, user.PoweredPed);
                    }
                    grabbedEntity.SetNoCollision(user.PoweredPed, false);
                    grabbedEntity.MarkAsNoLongerNeeded();
                    grabbedEntity = null;
                    hasGrabbedEntity = false;
                }
                else if (EntityIsAPed(grabbedEntity) || !isPlayingAnim(user.PoweredPed, Animations.HoldVeh))
                {
                    grabbedEntity.Detach();
                    DeleteInvisibleObjectFromCap(user.PoweredPed);
                    if (!gentle)
                    {
                        SetVelocityXYZ(grabbedEntity, ForwardDirFromCam(1f), 15f * user.AssignedProfile.StrikingPowerMultiplier);
                        SetPedRagdoll((Ped)grabbedEntity, 3000, ragdollType.WideLegStuble);
                        PlayPedPainSound((Ped)grabbedEntity);
                        AddShock(grabbedEntity.Position, Vector3.Zero, user.PoweredPed);
                        ((Ped)grabbedEntity).ApplyDamage((int)Math.Round((user.AssignedProfile.PChargeDamage) / 1.5f, 0));
                    }
                    grabbedEntity.SetNoCollision(user.PoweredPed, false);
                    grabbedEntity.MarkAsNoLongerNeeded();
                    grabbedEntity = null;
                    hasGrabbedEntity = false;
                }
            }
            catch { }
        }

        void AttachInvisibleObjectToCap(Ped cap)
        {
            Model bike = new Model(VehicleHash.Scorcher);
            bike.Request(1000);
            Hand2HeadObj = World.CreateVehicle(bike, Vector3.Zero);

            if (Hand2HeadObj.Exists())
            {
                Hand2HeadObj.Alpha = 0;
                Hand2HeadObj.SetNoCollision(cap, true);
                Vector3 posOffset = new Vector3(0, 0, 0);
                Hand2HeadObj.AttachTo(cap, boneIndexByID(cap, 57005), posOffset, Vector3.Zero);
            }
        }

        void DeleteInvisibleObjectFromCap(Ped cap)
        {
            if (Hand2HeadObj.Exists() && Hand2HeadObj.IsAttachedTo(cap))
            {
                Hand2HeadObj.Detach();
                Hand2HeadObj.MarkAsNoLongerNeeded();
                Hand2HeadObj.Delete();
                Hand2HeadObj = null;
            }
        }

        bool PreJump;
        bool IsTooHighToLandSafely;
        void QuickRagdollRecover()
        {
            if (player.IsOnFoot)
            {
                if (player.IsRagdoll)
                {
                    if (ragdollRecoveryTimer <= Game.GameTime)
                    {
                        if (hasTappedControl(GTA.Control.Sprint, true) || hasTappedControl(GTA.Control.Jump, true))
                        {
                            /*Recover From Ragdoll*/
                            SetPedRagdoll(player, 10, ragdollType.StiffBody);
                            ragdollRecoveryTimer = Game.GameTime + 2500;
                        }

                        if (!firstTimeRagdollRecovery)
                        {
                            UI.Notify("~b~Captain America Tip:~s~~n~Tap Sprint or Jump to try recovering from ragdoll!~n~Don't try recovering too high. You can only do this every 2.5 seconds!");
                            firstTimeRagdollRecovery = true;
                        }

                        if (isHoldingControl(GTA.Control.MoveUpOnly, true))
                        {
                            Function.Call(Hash.APPLY_FORCE_TO_ENTITY, player, 1, 0f, 0f, 0.005f, 0f, -10f, 0f, 1, 1, 1, 1, 0, 1);
                        }
                        if (isHoldingControl(GTA.Control.MoveDownOnly, true))
                        {
                            Function.Call(Hash.APPLY_FORCE_TO_ENTITY, player, 1, 0f, 0f, 0.005f, 0f, 10f, 0f, 1, 1, 1, 1, 0, 1);
                        }
                        if (isHoldingControl(GTA.Control.MoveLeftOnly, true))
                        {
                            Function.Call(Hash.APPLY_FORCE_TO_ENTITY, player, 1, 0.005f, 0f, 0f, 0f, 3f, 0f, 1, 1, 1, 1, 0, 1);
                        }
                        if (isHoldingControl(GTA.Control.MoveRightOnly, true))
                        {
                            Function.Call(Hash.APPLY_FORCE_TO_ENTITY, player, 1, 0.005f, 0f, 0f, 0f, -3f, 0f, 1, 1, 1, 1, 0, 1);
                        }
                    }
                }

                if (!PreJump)
                {
                    if (player.IsInAir && player.HeightAboveGround > 7f && !player.IsRagdoll)
                    {
                        player.IsInvincible = true;
                        player.CanRagdoll = false;
                        PreJump = true;
                    }
                }
                else
                {
                    if (!player.IsInAir)
                    {
                        if (player.HeightAboveGround <= 2f)
                        {
                            if (IsTooHighToLandSafely && player.IsRagdoll)
                            {
                                player.ApplyDamage(player.MaxHealth / 4);
                            }

                            if (!player.CanRagdoll)
                            {
                                player.Task.ClearAllImmediately();
                                playRollForward(player);
                            }
                        }
                        player.IsInvincible = false;
                        player.CanRagdoll = true;
                        IsTooHighToLandSafely = false;
                        PreJump = false;
                    }
                    else
                    {
                        if (player.IsFalling && player.HeightAboveGround >= PoweredUsers[0].AssignedProfile.SafeFallHeight && player.Velocity.Z <= -5f)
                        {
                            IsTooHighToLandSafely = true;
                            player.IsInvincible = false;
                            player.CanRagdoll = true;
                        }
                    }
                }

                //UI.ShowSubtitle("Post-Jump: " + PreJump.ToString() + ", IsTooHigh: " + IsTooHighToLandSafely.ToString(), 1);
            }
        }

        void HealthRegen(PoweredUser user)
        {
            if (user.PoweredPed.Exists())
            {
                if (user.PoweredPed.Health < user.PoweredPed.MaxHealth)
                {
                    if (user.RegenTimer < Game.GameTime)
                    {
                        user.PoweredPed.Health += user.AssignedProfile.RegenHealthAmount;
                        user.RegenTimer = Game.GameTime + (int)user.AssignedProfile.RegenInterval;
                    }
                }
            }
        }

        void fastJump(PoweredUser user)
        {
            if (user.AssignedProfile.AllowSuperJump)
            {
                if (user.PoweredPed.IsJumping)
                {
                    if (!user.jumpNow)
                    {
                        user.jumpTimer = Game.GameTime + 500;
                        user.jumpNow = true;
                    }
                    if (user.jumpTimer > Game.GameTime + 400)
                    {
                        if (!user.isJumping)
                        {
                            user.PoweredPed.Velocity = new Vector3(user.PoweredPed.Velocity.X + (user.PoweredPed.ForwardVector.X * user.AssignedProfile.JumpForwardForce), user.PoweredPed.Velocity.Y + (user.PoweredPed.ForwardVector.Y * user.AssignedProfile.JumpForwardForce), user.PoweredPed.Velocity.Z + (user.PoweredPed.UpVector.Z * user.AssignedProfile.JumpUpwardForce));
                            user.isJumping = true;
                        }
                    }
                }
                else if (!user.PoweredPed.IsInAir)
                {
                    user.isJumping = false;
                    user.jumpNow = false;
                }
            }
        }

        int GamepadSprintTimer;
        //int SuperSpeedTimer;
        void superSpeed(PoweredUser user)
        {
            //UI.ShowSubtitle(RunForceLerp.ToString());
            if (user.AssignedProfile.AllowSuperRun)
            {
                ControlSpeed(user);

                float leftWeight = Game.GetControlNormal(2, GTA.Control.MoveLeftOnly);
                float rightWeight = Game.GetControlNormal(2, GTA.Control.MoveRightOnly);
                float upWeight = Game.GetControlNormal(2, GTA.Control.MoveUpOnly);
                float downWeigt = Game.GetControlNormal(2, GTA.Control.MoveDownOnly);

                if (leftWeight + rightWeight + upWeight + downWeigt == 0f || (!Game.IsControlPressed(2, GTA.Control.Sprint) && user.PoweredPed.IsWalking)) //if not holding any movement control
                {
                    /*if (RunForceLerp > 0)
                    {
                        RunForceLerp -= Game.LastFrameTime * 2;
                    }
                    else { RunForceLerp = 0; }*/
                    user.RunForceLerp = 0;

                    if (user.RunForceLerp == 0 && user.SuperSpeedTimer >= Game.GameTime && !user.PoweredPed.IsJumping && !user.PoweredPed.IsRagdoll) //stop ped right when all controls are released
                    {
                        Vector3 dir = user.PoweredPed.ForwardVector;
                        user.PoweredPed.Velocity = new Vector3(dir.X * 7, dir.Y * 7, dir.Z);
                        user.SuperSpeedTimer = 0;
                    }
                }
            }

            //UI.ShowSubtitle(EntityCurrentSpeed(user.PoweredPed).ToString(), 20);
        }

        void ControlSpeed(PoweredUser user)
        {
            if (!user.PoweredPed.IsRagdoll && !isPlayingTackleAnimation(user.PoweredPed) && !user.PoweredPed.IsSwimming && !user.PoweredPed.IsSwimmingUnderWater)
            {
                if (user.PoweredPed.IsSprinting || user.PoweredPed.IsRunning)
                {
                    if (RunningTooHigh(user.PoweredPed))
                    {
                        //Vector3 destination = user.PoweredPed.GetOffsetInWorldCoords(new Vector3(0, 3, -1));
                        Vector3 destination = user.PoweredPed.GetOffsetInWorldCoords(new Vector3(0, 0, -1));
                        Vector3 dir = (destination - user.PoweredPed.Position).Normalized;
                        //SetVelocityXYZ(user.PoweredPed, dir, 5f);
                        ApplyVelocity(user.PoweredPed, dir, 4000f * Game.LastFrameTime);
                    }
                    else
                    {
                        bool isNotRagdolled = !IsDoingNoRagdollFlinch(user.PoweredPed) || (!user.PoweredPed.CanRagdoll && user != PoweredUsers[0]);
                        if (isNotRagdolled)
                        {
                            bool IsFastJumpingStill = user.PoweredPed.IsJumping && user.PoweredPed.Velocity.Z > -3f;

                            if (!IsFastJumpingStill && !user.PoweredPed.IsGettingUp)
                            {
                                Function.Call(Hash.SET_PED_MOVE_RATE_OVERRIDE, user.PoweredPed, 1 + (user.RunForceLerp * user.AssignedProfile.RunAnimationMultiplier));
                                Function.Call(Hash.SET_PED_DESIRED_MOVE_BLEND_RATIO, user.PoweredPed, 3.0f);

                                Vector3 vel = user.PoweredPed.Velocity;
                                Vector3 fDir = user.PoweredPed.ForwardVector;


                                float RunForce;

                                ControlLerp(user.RunForceLerp, 2f, out user.RunForceLerp);

                                if (user.PoweredPed.IsRunning)
                                {
                                    bool IsGamepadSprinting = !isKeyboard() && GamepadSprintTimer >= Game.GameTime;

                                    if (!IsGamepadSprinting)
                                    {
                                        RunForce = MathUtil.Lerp(2f, user.AssignedProfile.SuperRunningVelocity, user.RunForceLerp / 1f);
                                        if (!isOnVehicle(user.PoweredPed))
                                        {
                                            Vector3 runningVelocity = new Vector3((fDir.X * RunForce), (fDir.Y * RunForce), vel.Z);
                                            user.PoweredPed.Velocity = Vector3.Lerp(user.PoweredPed.Velocity, runningVelocity, user.RunForceLerp);
                                        }
                                        else
                                        {
                                            Vector3 runningVelocity = new Vector3(vel.X + (fDir.X * 1.01f), vel.Y + (fDir.Y * 1.01f), vel.Z);
                                            user.PoweredPed.Velocity = runningVelocity;
                                        }
                                        user.SuperSpeedTimer = Game.GameTime + 1000;
                                    }
                                    else //keep using sprinting speed
                                    {
                                        RunForce = MathUtil.Lerp(2f, user.AssignedProfile.SuperSprintingVelocity, user.RunForceLerp / 1f);
                                        if (!isOnVehicle(user.PoweredPed))
                                        {
                                            Vector3 runningVelocity = new Vector3((fDir.X * RunForce), (fDir.Y * RunForce), vel.Z);
                                            user.PoweredPed.Velocity = Vector3.Lerp(user.PoweredPed.Velocity, runningVelocity, user.RunForceLerp);
                                        }
                                        else
                                        {
                                            Vector3 runningVelocity = new Vector3(vel.X + (fDir.X * 1.01f), vel.Y + (fDir.Y * 1.01f), vel.Z);
                                            user.PoweredPed.Velocity = runningVelocity;
                                        }
                                        user.SuperSpeedTimer = Game.GameTime + 1000;
                                    }
                                }
                                else if (user.PoweredPed.IsSprinting)
                                {
                                    RunForce = MathUtil.Lerp(2f, user.AssignedProfile.SuperSprintingVelocity, user.RunForceLerp / 1f);

                                    if (!isOnVehicle(user.PoweredPed))
                                    {
                                        Vector3 runningVelocity = new Vector3((fDir.X * RunForce), (fDir.Y * RunForce), vel.Z);
                                        user.PoweredPed.Velocity = Vector3.Lerp(user.PoweredPed.Velocity, runningVelocity, user.RunForceLerp);
                                    }
                                    else
                                    {
                                        Vector3 runningVelocity = new Vector3(vel.X + (fDir.X * 1.01f), vel.Y + (fDir.Y * 1.01f), vel.Z);
                                        user.PoweredPed.Velocity = runningVelocity;
                                    }
                                    if (user == PoweredUsers[0]) { GamepadSprintTimer = Game.GameTime + 800; }
                                    user.SuperSpeedTimer = Game.GameTime + 1000;
                                }
                            }
                        }
                    }
                }
            }
        }

        bool RunningTooHigh(Ped p)
        {
            bool IsHigh = p.HeightAboveGround >= 2.0f && p.HeightAboveGround < 5.0f;

            if (!p.IsJumping && IsHigh) { return true; } else { return false; }
        }

        int EntityCurrentSpeed(Entity p)
        {
            return Function.Call<int>(Hash.GET_ENTITY_SPEED, p);
        }

        void shieldAttachementAutoSwitch(PoweredUser user)
        {
            try
            {
                if (!IsHoldingShield(user.PoweredPed, user.AssignedProfile.ShieldName))
                {
                    bool AllowBackAttach = user.AssignedProfile.AllowShieldOnBack;
                    if (!ballShieldExistsOnBack && !user.shieldIsThrown && AllowBackAttach)
                    {
                        if (user.PoweredPed.IsOnFoot)
                        {
                            if (!user.PoweredPed.IsInAir)
                            {
                                Wait(250);
                                deleteBallisticShieldsOnHand();
                                createBallisticShieldOnBack(user);
                            }
                            else
                            {
                                if (isPlayerParachuting())
                                {
                                    deleteBallisticShieldsOnHand();
                                    createBallisticShieldOnBack(user);
                                }
                            }
                        }
                        else
                        {
                            Wait(250);
                            deleteBallisticShieldsOnHand();
                            createBallisticShieldOnBack(user);
                        }
                    }
                }
                else
                {
                    deleteShieldsOnBack();
                    createBallisticShieldsOnHand();
                }
            }
            catch { }
        }

        void addCollision(Entity e)
        {
            //if (!Function.Call<bool>(Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, e.Handle))
            //{
            Function.Call(Hash.REQUEST_COLLISION_AT_COORD, e.Position.X, e.Position.Y, e.Position.Z);
            //}
        }

        void ManageMenu()
        {
            _menuPool.ProcessMenus();

            if (JustPressedMenuKeys())
            {
                if (_menuPool.IsAnyMenuOpen())
                {
                    _menuPool.CloseAllMenus();
                }
                else
                {
                    if (!CapCommunicator.DoesScriptCommunicatorMenuExist())
                    {
                        _menuPool.LastUsedMenu.IsVisible = !_menuPool.LastUsedMenu.IsVisible;
                        InputTimer = Game.GameTime + InputWait;
                    }
                }
                InputTimer = Game.GameTime + InputWait;
            }

            if (_menuPool.IsAnyMenuOpen())
            {
                if (capMenu.IsVisible)
                {
                    if (capMenu.JustPressedAccept())
                    {
                        UIMenuItem si = capMenu.SelectedItem;

                        if (si == ItemDisablePowers)
                        {
                            //EnablePlayerPowers();
                            DisablePowers();
                            //capMenu.SelectedItem.Value = CapAbilities;
                        }

                        if (si == ItemDeleteAllies)
                        {
                            if (PoweredUsers.Count > 1)
                            {
                                foreach (PoweredUser user in PoweredUsers)
                                {
                                    if (user.IsEnemy == false && user.PoweredPed != Game.Player.Character)
                                    {
                                        try
                                        {
                                            user.PoweredPed.MarkAsNoLongerNeeded();
                                            user.PoweredPed.Delete();
                                            user.PoweredPed.CurrentBlip.Remove();
                                        }
                                        catch { }
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                PoweredUsers.RemoveAll(item => !item.PoweredPed.Exists());
                                UI.ShowSubtitle("Allies removed!");
                            }
                        }

                        if (si == ItemDeleteEnemies)
                        {
                            if (PoweredUsers.Count > 1)
                            {
                                foreach (PoweredUser user in PoweredUsers)
                                {
                                    if (user.IsEnemy == true && user.PoweredPed != Game.Player.Character)
                                    {
                                        try
                                        {
                                            user.PoweredPed.MarkAsNoLongerNeeded();
                                            user.PoweredPed.Delete();
                                            user.PoweredPed.CurrentBlip.Remove();
                                        }
                                        catch { }
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                PoweredUsers.RemoveAll(item => !item.PoweredPed.Exists());
                                UI.ShowSubtitle("Enemies removed!");
                            }
                        }

                        InputTimer = Game.GameTime + 300;

                        capMenu.SetInputWait();
                    }
                }

                if (ControlsMenu.IsVisible)
                {
                    if (ControlsMenu.JustPressedAccept())
                    {
                        UIMenuItem si = ControlsMenu.SelectedItem;

                        if (si == ItemReloadControlsCFG)
                        {
                            LoadControls();
                            UI.ShowSubtitle("Controls reloaded");
                        }

                        ControlsMenu.SetInputWait();
                    }
                }

                foreach (MenuCfgPair custompair in CustomSettingItems)
                {
                    foreach (ProfileSetting profile in ProfileSettings)
                    {
                        if (custompair.CfgFileName == profile.ProfileName)
                        {
                            if (custompair.MainMenu.IsVisible)
                            {
                                if (custompair.MainMenu.JustPressedAccept())
                                {
                                    UIMenuItem si = custompair.MainMenu.SelectedItem;

                                    if (si == custompair.ItemEnablePowers)
                                    {
                                        EnablePlayerPowers(custompair, false, profile);
                                    }
                                    if (si == custompair.ItemEnablePowersWithPed)
                                    {
                                        EnablePlayerPowers(custompair, true, profile);
                                    }
                                    if (si == custompair.ItemAddAlly)
                                    {
                                        SetPedAsAllyOrEnemy(true, new PoweredUser(CreateAllyOrEnemy(profile, 20f)), profile);
                                        UI.ShowSubtitle("Creating Ally...", 1000);
                                        Wait(1000);
                                    }
                                    if (si == custompair.ItemAddEnemy)
                                    {
                                        SetPedAsAllyOrEnemy(false, new PoweredUser(CreateAllyOrEnemy(profile, 60f)), profile);
                                        UI.ShowSubtitle("Creating Enemy...", 1000);
                                        Wait(1000);
                                    }
                                    if (si == custompair.ItemSaveSettings)
                                    {
                                        SaveINIProfile(custompair.CfgFileName, profile);
                                        EnablePlayerPowers(custompair, false, profile);
                                        UI.ShowSubtitle(custompair.CfgFileName + " Saved");
                                    }
                                    if (si == custompair.ItemReloadSettings)
                                    {
                                        LoadINIProfile(custompair.CfgFileName, profile);
                                        //PoweredUsers[0].AssignedProfile = profile;
                                        //DecipherAndSetCapShield(profile.ShieldName, PoweredUsers[0]);
                                        UI.ShowSubtitle(custompair.CfgFileName + " Reloaded");
                                    }

                                    InputTimer = Game.GameTime + 500;

                                    custompair.MainMenu.SetInputWait();
                                }
                            }

                            if (custompair.ShieldSubmenu.IsVisible)
                            {
                                profile.InitialThowForce = custompair.ShieldSubmenu.ControlFloatValue(custompair.ItemInitialThowForce, profile.InitialThowForce, 5f, 50f, 0);
                                profile.PShieldDamage = custompair.ShieldSubmenu.ControlIntValue(custompair.ItemPShieldDamage, profile.PShieldDamage, 5, 10);
                                profile.VShieldDamage = custompair.ShieldSubmenu.ControlIntValue(custompair.ItemVShieldDamage, profile.VShieldDamage, 5, 10);
                                profile.QuickReturnShield = custompair.ShieldSubmenu.ControlBoolValue(custompair.ItemQuickReturnShield, profile.QuickReturnShield);
                                profile.ReturnInterval = custompair.ShieldSubmenu.ControlFloatValue(custompair.ItemReturnInterval, profile.ReturnInterval / 1000, 0.2f, 1f, 1) * 1000;
                                profile.AllowShieldCurve = custompair.ShieldSubmenu.ControlBoolValue(custompair.ItemAllowShieldCurve, profile.AllowShieldCurve);
                                profile.CurveForce = custompair.ShieldSubmenu.ControlFloatValue(custompair.ItemCurveForce, profile.CurveForce, 5f, 50f);
                                profile.BackhandThrow = custompair.ShieldSubmenu.ControlBoolValue(custompair.ItemBackhandThrow, profile.BackhandThrow);

                                if (custompair.ShieldSubmenu.JustPressedAccept())
                                {
                                    UIMenuItem si = custompair.ShieldSubmenu.SelectedItem;

                                    if (si == custompair.ItemSaveCurrentWeapon)
                                    {
                                        profile.ShieldName = Game.Player.Character.Weapons.Current.Hash.ToString();
                                        SaveINIProfile(custompair.CfgFileName, profile);
                                        UI.ShowSubtitle("Weapon saved to INI.");
                                    }

                                    custompair.ShieldSubmenu.SetInputWait();
                                }
                            }

                            if (custompair.SpecialsSubmenu.IsVisible)
                            {
                                profile.AllowSpecialAttacks = custompair.SpecialsSubmenu.ControlBoolValue(custompair.ItemAllowSpecialAttacks, profile.AllowSpecialAttacks);
                                profile.PChargeDamage = custompair.SpecialsSubmenu.ControlIntValue(custompair.ItemPChargeDamage, profile.PChargeDamage, 5, 10);
                                profile.VChargeDamage = custompair.SpecialsSubmenu.ControlIntValue(custompair.ItemVChargeDamage, profile.VChargeDamage, 5, 10);
                                profile.StrikingPowerMultiplier = custompair.SpecialsSubmenu.ControlFloatValue(custompair.ItemStrikingPowerMultiplier, profile.StrikingPowerMultiplier, 0.1f, 1f, 1);
                                profile.AllowCustomMelee = custompair.SpecialsSubmenu.ControlBoolValue(custompair.ItemAllowCustomMelee, profile.AllowCustomMelee);
                                profile.AllowReflect = custompair.SpecialsSubmenu.ControlBoolValue(custompair.ItemAllowReflect, profile.AllowReflect);
                                profile.AllowTank = custompair.SpecialsSubmenu.ControlBoolValue(custompair.ItemAllowTank, profile.AllowTank);
                                profile.MaxLiftSizeMMcubed = custompair.SpecialsSubmenu.ControlFloatValue(custompair.ItemMaxLiftSizeMMcubed, profile.MaxLiftSizeMMcubed, 0.5f, 5f, 3);
                                profile.MaxHealth = custompair.SpecialsSubmenu.ControlIntValue(custompair.ItemMaxHealth, profile.MaxHealth, 100, 500);
                                profile.RegenHealthAmount = custompair.SpecialsSubmenu.ControlIntValue(custompair.ItemRegenHealthAmount, profile.RegenHealthAmount, 5, 10);
                                profile.RegenInterval = custompair.SpecialsSubmenu.ControlFloatValue(custompair.ItemRegenInterval, profile.RegenInterval / 1000, 0.2f, 1f, 1) * 1000;
                            }

                            if (custompair.MobilitySubmenu.IsVisible)
                            {
                                profile.AllowSuperRun = custompair.MobilitySubmenu.ControlBoolValue(custompair.ItemAllowSuperRun, profile.AllowSuperRun);
                                profile.RunAnimationMultiplier = custompair.MobilitySubmenu.ControlFloatValue(custompair.ItemRunAnimationMultiplier, profile.RunAnimationMultiplier, 0.1f, 1f, 1);
                                profile.SuperRunningVelocity = custompair.MobilitySubmenu.ControlFloatValue(custompair.ItemSuperRunningVelocity, profile.SuperRunningVelocity, 1f, 5f);
                                profile.SuperSprintingVelocity = custompair.MobilitySubmenu.ControlFloatValue(custompair.ItemSuperSprintingVelocity, profile.SuperSprintingVelocity, 1f, 5f);
                                profile.AllowSuperJump = custompair.MobilitySubmenu.ControlBoolValue(custompair.ItemAllowSuperJump, profile.AllowSuperJump);
                                profile.JumpForwardForce = custompair.MobilitySubmenu.ControlFloatValue(custompair.ItemJumpForwardForce, profile.JumpForwardForce, 1f, 5f);
                                profile.JumpUpwardForce = custompair.MobilitySubmenu.ControlFloatValue(custompair.ItemJumpUpwardForce, profile.JumpUpwardForce, 1f, 5f);
                                profile.SafeFallHeight = custompair.MobilitySubmenu.ControlFloatValue(custompair.ItemSafeFallHeight, profile.SafeFallHeight, 5f, 10f);
                                profile.AllowCombatRoll = custompair.MobilitySubmenu.ControlBoolValue(custompair.ItemAllowCombatRoll, profile.AllowCombatRoll);
                                profile.RollSpeed = custompair.MobilitySubmenu.ControlFloatValue(custompair.ItemRollSpeed, profile.RollSpeed, 0.1f, 1f);
                            }

                            if (custompair.MiscSubmenu.IsVisible)
                            {
                                profile.AllowSlowMoAim = custompair.MiscSubmenu.ControlBoolValue(custompair.ItemAllowSlowMoAim, profile.AllowSlowMoAim);
                                profile.AllowShieldOnBack = custompair.MiscSubmenu.ControlBoolValue(custompair.ItemAllowShieldOnBack, profile.AllowShieldOnBack);
                                /*profile.BackShieldPos.X = custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldPosX, profile.BackShieldPos.X, 0.001f, 0.01f, 3);
                                profile.BackShieldPos.Y = custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldPosY, profile.BackShieldPos.Y, 0.001f, 0.01f, 3);
                                profile.BackShieldPos.Z = custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldPosZ, profile.BackShieldPos.Z, 0.001f, 0.01f, 3);
                                profile.BackShieldRot.X = custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldRotX, profile.BackShieldRot.X, 1f, 5f);
                                profile.BackShieldRot.Y = custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldRotY, profile.BackShieldRot.Y, 1f, 5f);
                                profile.BackShieldRot.Z = custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldRotZ, profile.BackShieldRot.Z, 1f, 5f);*/
                                profile.SetBackShieldPos(custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldPosX, profile.BackShieldPos.X, 0.001f, 0.01f, 3),
                                    custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldPosY, profile.BackShieldPos.Y, 0.001f, 0.01f, 3),
                                    custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldPosZ, profile.BackShieldPos.Z, 0.001f, 0.01f, 3));
                                profile.SetBackShieldRot(custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldRotX, profile.BackShieldRot.X, 1f, 5f),
                                    custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldRotY, profile.BackShieldRot.Y, 1f, 5f),
                                    custompair.MiscSubmenu.ControlFloatValue(custompair.ItemBackShieldRotZ, profile.BackShieldRot.Z, 1f, 5f));
                                profile.FxRed = custompair.MiscSubmenu.ControlFloatValue(custompair.ItemFxRed, profile.FxRed, 1f, 10f);
                                profile.FxGreen = custompair.MiscSubmenu.ControlFloatValue(custompair.ItemFxGreen, profile.FxGreen, 1f, 10f);
                                profile.FxBlue = custompair.MiscSubmenu.ControlFloatValue(custompair.ItemFxBlue, profile.FxBlue, 1f, 10f);
                                profile.FixCompatibilityWithFlash = custompair.MiscSubmenu.ControlBoolValue(custompair.ItemFixCompatibilityWithFlash, profile.FixCompatibilityWithFlash);

                                UIMenuItem si = custompair.MiscSubmenu.SelectedItem;

                                if (si == custompair.ItemBackShieldPosX || si == custompair.ItemBackShieldPosY || si == custompair.ItemBackShieldPosZ || si == custompair.ItemBackShieldRotX || si == custompair.ItemBackShieldRotY || si == custompair.ItemBackShieldRotZ)
                                {
                                    try
                                    {
                                        AttachCapShieldToBack(weapBackProp, player, profile);
                                    }
                                    catch { UI.ShowSubtitle("Place the shield on your back!"); }
                                }

                                if (si == custompair.ItemSaveCurrentPed)
                                {
                                    if (custompair.MiscSubmenu.JustPressedAccept())
                                    {
                                        profile.PedModelToUse = Game.Player.Character.Model.Hash.ToString();
                                        SaveINIProfile(custompair.CfgFileName, profile);
                                        UI.ShowSubtitle("Ped saved to INI.");

                                        custompair.MiscSubmenu.SetInputWait();
                                    }
                                }
                            }
                        }
                    }
                }
                CapCommunicator.BlockScriptCommunicatorModMenu();
            }
            else
            {
                if (CapCommunicator.IsScriptCommunicatorMenuBlocked())
                {
                    Wait(350);
                    CapCommunicator.UnblockScriptCommunicatorModMenu();
                }
            }
        }

        void ReLoadPlayerModel(bool SamePed, string newModel = "")
        {
            int result;
            bool IsInt = Int32.TryParse(newModel, out result);
            //var characterModel = new Model(SamePed ? Game.Player.Character.Model.Hash.ToString() : (IsInt ? result.ToString() : GetHashKey(newModel).ToString()));
            Model characterModel;

            if (SamePed)
            {
                characterModel = new Model(Game.Player.Character.Model.Hash.ToString());
            }
            else
            {
                if (IsInt)
                {
                    characterModel = new Model(result);
                }
                else
                {
                    characterModel = new Model(GetHashKey(newModel));
                }
            }

            characterModel.Request(500);

            // Check the model is valid
            if (characterModel.IsInCdImage && characterModel.IsValid)
            {
                // If the model isn't loaded, wait until it is
                while (!characterModel.IsLoaded) Script.Wait(100);

                // Set the player's model
                Function.Call(Hash.SET_PLAYER_MODEL, Game.Player, characterModel.Hash);
                Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, Game.Player.Character.Handle);
            }
            characterModel.MarkAsNoLongerNeeded();
        }

        void EnablePlayerPowers(MenuCfgPair custompair, bool ChangePedModel, ProfileSetting profile)
        {
            //if (!CapAbilities)
            //{
            clearAnyBallisticShields();
            clearAnyCapShields(PoweredUsers[0]);

            LoadINIProfile(custompair.CfgFileName, profile);
            PoweredUsers[0].AssignedProfile = profile;
            DecipherAndSetCapShield(profile.ShieldName, PoweredUsers[0]);

            if (ChangePedModel)
            {
                ReLoadPlayerModel(false, profile.PedModelToUse);
            }
            else
            {
                ReLoadPlayerModel(true);
            }
            Wait(250);

            player = Game.Player.Character; //required or game will not know who player is!
            PoweredUsers[0].PoweredPed = Game.Player.Character;

            if (player.Weapons.HasWeapon(WeaponHash.Knife))
            {
                player.Weapons.Select(WeaponHash.Knife, true);
            }
            else
            {
                player.Weapons.Give(WeaponHash.Knife, 1, true, true);
            }

            Wait(0);

            player.Weapons.Give(PoweredUsers[0].CapShield, 1, true, true);
            PoweredUsers[0].holdWeap = player.Weapons.Current.Model;
            PoweredUsers[0].holdWeap.Request(2000);

            /*if (holdWeap.IsInCdImage && holdWeap.IsValid)
            {
                // If the model isn't loaded, wait until it is
                while (!holdWeap.IsLoaded) Script.Wait(100);

                player.Weapons.Give(CapShield, 1, true, true);
            }*/

            createBallisticShieldsOnHand();
            player.MaxHealth = profile.MaxHealth;
            player.Health = player.MaxHealth;
            player.Armor = 100;

            if (notification != null) { notification.Hide(); }
            notification = UI.Notify("~b~Captain America Mod Activated");

            PoweredUsers[0].weapProp = new Prop(616);
            //weapProp = World.CreateProp(CapShield, Vector3.Zero, true, false);
            //while (weapProp == null || !weapProp.Exists()) { Wait(259); }
            //weapProp.MarkAsNoLongerNeeded();
            //weapProp.Delete();

            PoweredUsers[0].shieldIsThrown = false;
            CapAbilities = true;
            //}
            /*else
            {


                clearAnyBallisticShields();
                clearAnyCapShields();

                if (notification != null) { notification.Hide(); }
                notification = UI.Notify("~rng~Captain America Mod Deactivated");

                CapAbilities = false;
            }*/
        }

        void DisablePowers()
        {
            if (CapAbilities)
            {
                clearAnyBallisticShields();
                clearAnyCapShields(PoweredUsers[0]);

                if (notification != null) { notification.Hide(); }
                notification = UI.Notify("~r~Captain America Mod Deactivated");

                Game.Player.Character.IsInvincible = false;
                Game.Player.Character.CanRagdoll = true;

                CapAbilities = false;
            }
        }

        Ped CreateAllyOrEnemy(ProfileSetting profile, float distance)
        {
            int result;
            bool IsInt = Int32.TryParse(profile.PedModelToUse, out result);
            //var characterModel = new Model(SamePed ? Game.Player.Character.Model.Hash.ToString() : (IsInt ? result.ToString() : GetHashKey(newModel).ToString()));
            Model characterModel = null;

            if (IsInt)
            {
                characterModel = new Model(result);
            }
            else
            {
                characterModel = new Model(GetHashKey(profile.PedModelToUse));
            }

            characterModel.Request(500);
            
            var newPed = World.CreatePed(characterModel, World.GetSafeCoordForPed(Game.Player.Character.Position.Around(distance), false));

            Function.Call(Hash.SET_PED_CONFIG_FLAG, newPed, 281, 1); //no writhe
            Function.Call(Hash.SET_PED_CONFIG_FLAG, newPed, 314, true); //no ped melee
            Function.Call(Hash.SET_PED_CONFIG_FLAG, newPed, 224, false); //no ped melee
            Function.Call(Hash.SET_PED_CONFIG_FLAG, newPed, 122, true); //no ped melee
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, newPed, 5, true);
            Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, newPed, 46, true);
            Function.Call(Hash.SET_PED_SUFFERS_CRITICAL_HITS, newPed, false);
            Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, newPed, true);
            Function.Call(Hash.SET_PED_CAN_PLAY_AMBIENT_BASE_ANIMS, newPed, false);
            Function.Call(Hash.SET_PED_CAN_PLAY_AMBIENT_ANIMS, newPed, false);
            Function.Call(Hash.SET_PED_CAN_PLAY_VISEME_ANIMS, newPed, false);
            Function.Call(Hash.SET_PED_CAN_PLAY_GESTURE_ANIMS, newPed, false);
            newPed.CanBeTargetted = true;
            newPed.MaxHealth = profile.MaxHealth;
            newPed.Health = newPed.MaxHealth;
            newPed.Armor = 100;

            return newPed;
        }

        void SetPedAsAllyOrEnemy(bool AsAlly, PoweredUser user, ProfileSetting profile)
        {
            int wpnresult;
            bool IswpnInt = Int32.TryParse(profile.ShieldName, out wpnresult);

            if (IswpnInt)
            {
                user.PoweredPed.Weapons.Give((WeaponHash)wpnresult, 1, true, true);
            }
            else
            {
                user.PoweredPed.Weapons.Give((WeaponHash)Function.Call<int>(Hash.GET_HASH_KEY, "WEAPON_" + profile.ShieldName), 1, true, true);
            }

            PoweredUsers.Add(user);
            PoweredUsers.Last().AssignedProfile = profile;
            PoweredUsers.Last().BlipID = PoweredUsers.Last().PoweredPed.AddBlip();
            PoweredUsers.Last().BlipID.Name = profile.ProfileName;

            if (AsAlly)
            {
                PoweredUsers.Last().BlipID.IsFriendly = true;
                PoweredUsers.Last().BlipID.Sprite = BlipSprite.Friend;
                PoweredUsers.Last().BlipID.Color = BlipColor.Blue;
                PoweredUsers.Last().IsEnemy = false;
                user.PoweredPed.RelationshipGroup = Game.Player.Character.RelationshipGroup;
            }
            else
            {
                PoweredUsers.Last().BlipID.IsFriendly = false;
                PoweredUsers.Last().BlipID.Sprite = BlipSprite.Enemy;
                PoweredUsers.Last().BlipID.Color = BlipColor.Red;
                PoweredUsers.Last().IsEnemy = true;
                user.PoweredPed.RelationshipGroup = VillianGroup;
                World.SetRelationshipBetweenGroups(Relationship.Hate, user.PoweredPed.RelationshipGroup, Game.Player.Character.RelationshipGroup);
                World.SetRelationshipBetweenGroups(Relationship.Hate, Game.Player.Character.RelationshipGroup, user.PoweredPed.RelationshipGroup);
                //SetRelationshipOneSided(Relationship.Hate, Game.Player.Character.RelationshipGroup, VillianGroup);
                //SetRelationshipOneSided(Relationship.Like, VillianGroup, Game.Player.Character.RelationshipGroup);
            }

            DecipherAndSetCapShield(profile.ShieldName, user);

            PoweredUsers.Last().holdWeap = PoweredUsers.Last().PoweredPed.Weapons.Current.Model;
            PoweredUsers.Last().holdWeap.Request(2000);

            user.weapProp = new Prop(user.rng.Next(0, 1000));
        }

        bool EntityIsAnObject(Entity e)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_AN_OBJECT, e);
        }

        bool EntityIsAPed(Entity e)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_A_PED, e);
        }

        bool EntityIsAVehicle(Entity e)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_A_VEHICLE, e);
        }

        bool IsEntityAttachedToAnyPed(Entity e)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_ATTACHED_TO_ANY_PED, e);
        }

        Entity GetAttachedEntity(Entity e)
        {
            return Function.Call<Entity>(Hash.GET_ENTITY_ATTACHED_TO, e);
        }

        bool TargetPedExists()
        {
            RayCastCap = RaycastForwardCapsule();

            if (RayCastCap.DitHitEntity)
            {
                Entity tempEnt = RayCastCap.HitEntity;

                if (!EntityIsAnObject(tempEnt))
                {
                    DrawMarker(tempEnt);
                    keepTargetInterval = Game.GameTime + 1500;
                    return RayCastCap.DitHitEntity;
                }
                else
                {
                    if (tempEnt.IsInAir)
                    {
                        DrawMarker(tempEnt);
                        keepTargetInterval = Game.GameTime + 1500;
                        return RayCastCap.DitHitEntity;
                    }
                    return false;
                }
            }
            else { return false; }
        }

        Entity TargettedEntity()
        {
            if (TargetPedExists())
            {
                return RayCastCap.HitEntity;
            }
            else { return null; }
        }

        void SetTargetEntity(PoweredUser user)
        {
            _TargettedEntity = TargettedEntity();
            user.Target = _TargettedEntity;

        }

        void SetMultipleTargets()
        {
            if (TargetPedExists() && !targettedEntities.Contains(TargettedEntity()))
            {
                if (targettedEntities.Count <= 3)
                {
                    if ((EntityIsAPed(TargettedEntity()) && TargettedEntity().Health > 0) || !EntityIsAPed(TargettedEntity()))
                    { targettedEntities.Add(TargettedEntity()); }
                }
            }
        }

        void IdentifyMultipleTargets()
        {
            if (targettedEntities.Count > 0)
            {
                foreach (Entity e in targettedEntities)
                {
                    DrawMarker(e);
                }
            }
        }

        /*void AvoidEntityDeletion()
        {
            Entity[] veryNearEnts = World.GetNearbyEntities(boneCoord(player, "IK_R_Hand"), 8f); //was 3.5f

            if (veryNearEnts != null)
            {
                foreach (Entity e in veryNearEnts)
                {
                    if (!e.IsAttached())
                    {
                        e.IsPersistent = true;
                    }
                }
            }
        }

        void RemovePersistance()
        {
            Entity[] veryNearEnts = World.GetNearbyEntities(boneCoord(player, "IK_R_Hand"), 8f);

            if (veryNearEnts != null)
            {
                foreach (Entity e in veryNearEnts)
                {
                    if (!e.IsAttached() && e != weapProp)
                    {
                        e.IsPersistent = false;
                    }
                }
            }
            //DoPersistanceFunc = false;
        }*/

        void DamagePedWithShield(Ped p, Vector3 damagedbone, PoweredUser user)
        {
            bool userIsPlayer = user == PoweredUsers[0];

            Function.Call(Hash.SET_PED_CAPSULE, p, 1f);
            PlayPedPainSound(p);
            SetPedRagdoll(p, 3000, ragdollType.Normal);

            if (!user.ShieldCanReturn)
            {
                //p.ApplyForce(ForwardDirFromCam(5), p.UpVector * 2f);
                p.ApplyForce(Vector3.WorldUp * 0.1f, Vector3.WorldUp * 2f, ForceType.ForceRotPlusForce);
                if (userIsPlayer)
                {
                    //SetVelocityXYZ(p, ForwardDirFromCam(1), 15f);
                    p.Velocity = Vector3.Divide(user.weapProp.Velocity, 5f);
                }
                else
                {
                    //SetVelocityXYZ(p, (p.Position - user.PoweredPed.Position).Normalized, 15f);
                    p.Velocity = Vector3.Divide(user.weapProp.Velocity, 5f);
                }
            }
            else
            {
                //p.ApplyForce(returnDir * 5f, p.UpVector * 2f);
                p.ApplyForce(Vector3.WorldUp * 0.1f, Vector3.WorldUp * 2f, ForceType.ForceRotPlusForce);
                //SetVelocityXYZ(p, user.returnDir, 15f);
                p.Velocity = Vector3.Divide(user.weapProp.Velocity, 5f);
            }

            p.ApplyDamage(user.AssignedProfile.PShieldDamage);

            AddBloodToPed(p);

            AddShock(damagedbone, user.weapProp.Position, user.PoweredPed);

            if (!user.ShieldCanReturn)
            {
                //weapProp.Velocity = new Vector3(0, 0, 0);
            }

            if (userIsPlayer)
            {
                DecideNextHomingTarget(p, user);
            }
            else
            {
                user.isHoming = false;
                user.ShieldCanReturn = true;
            }
        }

        void DamageVehicleWithShield(Vehicle v, PoweredUser user)
        {
            if (user.weapProp.IsTouching(v) && user.PoweredPed.CurrentVehicle != v)
            {
                bool userIsPlayer = user == PoweredUsers[0];

                PlayVehHitSound(v);
                StartPtfxAtCoordNonLooped(PTFX.MeleeSpark, user.weapProp.Position, Vector3.Zero, 5.0f);
                SetVehicleDamage(v, user.AssignedProfile.VShieldDamage);

                SetVehicleVisualDamage(v, user.weapProp.Position, 8600f, 800f);

                if (!user.ShieldCanReturn)
                {
                    if (userIsPlayer)
                    {
                        v.ApplyForce(ForwardDirFromCam(2f), v.UpVector * 0.5f);
                    }
                    else
                    {
                        v.ApplyForce((v.Position - user.PoweredPed.Position).Normalized * 2f, v.UpVector * 0.5f);
                    }
                }
                else
                {
                    v.ApplyForce(user.returnDir * 2f, v.UpVector * 0.5f);
                }

                if (!v.EngineRunning) { v.StartAlarm(); }
                if (v.Model.IsBike || v.Model.IsBicycle) { if (v.Driver != null && v.Driver.Exists()) { Function.Call(Hash.KNOCK_PED_OFF_VEHICLE, v.Driver); } }
                
                AddShock(v.Position, user.weapProp.Position, user.PoweredPed);

                if (!user.ShieldCanReturn)
                {
                    user.weapProp.Velocity = new Vector3(0, 0, 0);
                }

                if (userIsPlayer)
                {
                    DecideNextHomingTarget(v, user);
                }
                else
                {
                    user.isHoming = false;
                    user.ShieldCanReturn = true;
                }
                user.shieldedVehs.Add(v);
            }
        }

        void DamagePropWithShield(Prop pr, PoweredUser user)
        {
            bool userIsPlayer = user == PoweredUsers[0]; 
            addCollision(user.weapProp);
            if (user.weapProp.IsTouching(pr))
            {
                AddShock(pr.Position, user.weapProp.Position, user.PoweredPed);

                if (!user.ShieldCanReturn)
                {
                    user.weapProp.Velocity = new Vector3(0, 0, 0);
                }

                user.firstHit = true;

                if (userIsPlayer && targettedEntities.Count == 0)
                {
                    user.isHoming = false;
                    user.ShieldCanReturn = true;
                }
                else
                {
                    user.isHoming = false;
                    user.ShieldCanReturn = true;
                }
            }
        }

        void SetVehicleVisualDamage(Vehicle v, Vector3 worldCoord, float visualDamageAmount = 200f, float radiusOfDamage = 250f, bool p6 = true)
        {
            Vector3 offset = v.GetOffsetFromWorldCoords(worldCoord);
            Function.Call(Hash.SET_VEHICLE_DAMAGE, v, offset.X, offset.Y, offset.Z, visualDamageAmount, radiusOfDamage, p6);
        }

        void SetVehicleDamage(Vehicle v, int damageAmount)
        {
            v.Health -= damageAmount;
            v.BodyHealth -= damageAmount;
            v.EngineHealth -= damageAmount;
        }

        enum pedBone
        {
            IK_Head = 12844,
            SKEL_ROOT = 0,
            IK_Root = 56604,
            SKEL_Spine_Root = 57597,
            SKEL_Spine0 = 23553,
            SKEL_Spine1 = 24816,
            SKEL_Spine2 = 24817,
            SKEL_Spine3 = 24818,
            FACIAL_facialRoot = 65068,
            SKEL_Neck_1 = 39317,
            SKEL_Head = 31086,
            SKEL_L_Calf = 63931,
            SKEL_L_Clavicle = 64729,
            SKEL_L_Foot = 14201,
            SKEL_L_Forearm = 61163,
            SKEL_L_Hand = 18905,
            SKEL_L_Thigh = 58271,
            SKEL_L_Toe0 = 2108,
            SKEL_L_UpperArm = 45509,
            SKEL_Pelvis = 11816,
            SKEL_R_Calf = 36864,
            SKEL_R_Clavicle = 10706,
            SKEL_R_Foot = 52301,
            SKEL_R_Forearm = 28252,
            SKEL_R_Hand = 57005,
            SKEL_R_Thigh = 51826,
            SKEL_R_Toe0 = 20781,
            SKEL_R_UpperArm = 40269,
            IK_L_Hand = 36029,
            IK_L_Foot = 65245,
            IK_R_Foot = 35502,
            IK_R_Hand = 6286,
            MH_L_Elbow = 22711,
            MH_L_Knee = 46078,
            MH_R_Elbow = 2992,
            MH_R_Knee = 16335,
            PH_L_Foot = 57717,
            PH_L_Hand = 60309,
            PH_R_Foot = 24806,
            PH_R_Hand = 28422,
            RB_L_ArmRoll = 5232,
            RB_L_ForeArmRoll = 61007,
            RB_L_ThighRoll = 23639,
            RB_Neck_1 = 35731,
            RB_R_ArmRoll = 37119,
            RB_R_ForeArmRoll = 43810,
            RB_R_ThighRoll = 6442,
            /*SKEL_L_Finger00 = 26610,
            SKEL_L_Finger01 = 4089,
            SKEL_L_Finger02 = 4090,
            SKEL_L_Finger10 = 26611,
            SKEL_L_Finger11 = 4169,
            SKEL_L_Finger12 = 4170,
            SKEL_L_Finger20 = 26612,
            SKEL_L_Finger21 = 4185,
            SKEL_L_Finger22 = 4186,
            SKEL_L_Finger30 = 26613,
            SKEL_L_Finger31 = 4137,
            SKEL_L_Finger32 = 4138,
            SKEL_L_Finger40 = 26614,
            SKEL_L_Finger41 = 4153,
            SKEL_L_Finger42 = 4154,
            SKEL_R_Finger00 = 58866,
            SKEL_R_Finger01 = 64016,
            SKEL_R_Finger02 = 6,
            SKEL_R_Finger10 = 58867,
            SKEL_R_Finger11 = 64096,
            SKEL_R_Finger12 = 64097,
            SKEL_R_Finger20 = 58868,
            SKEL_R_Finger21 = 64112,
            SKEL_R_Finger22 = 64113,
            SKEL_R_Finger30 = 58869,
            SKEL_R_Finger31 = 64064,
            SKEL_R_Finger32 = 64065,
            SKEL_R_Finger40 = 58870,
            SKEL_R_Finger41 = 64080,
            SKEL_R_Finger42 = 64081,
            FB_R_Brow_Out_000 = 1356,
            FB_Brow_Centre_000 = 37193,
            FB_Jaw_000 = 46240,
            FB_L_Brow_Out_000 = 58331,
            FB_L_CheekBone_000 = 21550,
            FB_L_Eye_000 = 25260,
            FB_L_Lid_Upper_000 = 45750,
            FB_L_Lip_Bot_000 = 47419,
            FB_L_Lip_Corner_000 = 29868,
            FB_L_Lip_Top_000 = 20279,
            FB_LowerLipRoot_000 = 17188,
            FB_LowerLip_000 = 20623,
            FB_R_CheekBone_000 = 19336,
            FB_R_Eye_000 = 27474,
            FB_R_Lid_Upper_000 = 43536,
            FB_R_Lip_Bot_000 = 49979,
            FB_R_Lip_Corner_000 = 11174,
            FB_R_Lip_Top_000 = 17719,
            FB_Tongue_000 = 47495,
            FB_UpperLipRoot_000 = 20178,
            FB_UpperLip_000 = 61839,*/
        }

        Vector3 pedDamageLoc(Ped victim, Vector3 damageSource)
        {
            /*Vector3 headBone = boneCoord(victim, "IK_Head");
            Vector3 body = victim.Position;
            Vector3 pelvis = victim.GetBoneCoord(Bone.SKEL_Pelvis);
            Vector3 spine0 = victim.GetBoneCoord(Bone.SKEL_Spine0);
            Vector3 spine1 = victim.GetBoneCoord(Bone.SKEL_Spine1);
            Vector3 spine2 = victim.GetBoneCoord(Bone.SKEL_Spine2);
            Vector3 spine3 = victim.GetBoneCoord(Bone.SKEL_Spine3);
            Vector3 spineRoot = victim.GetBoneCoord(Bone.SKEL_Spine_Root);
            Vector3 Lclav = victim.GetBoneCoord(Bone.SKEL_L_Clavicle);
            Vector3 Rclav = victim.GetBoneCoord(Bone.SKEL_R_Clavicle);

            Vector3 Lhand = victim.GetBoneCoord(Bone.IK_L_Hand);
            Vector3 Rhand = victim.GetBoneCoord(Bone.IK_R_Hand);
            Vector3 Lelbow = victim.GetBoneCoord(Bone.MH_L_Elbow);
            Vector3 Relbow = victim.GetBoneCoord(Bone.MH_R_Elbow);
            Vector3 Lknee = victim.GetBoneCoord(Bone.MH_L_Knee);
            Vector3 Rknee = victim.GetBoneCoord(Bone.MH_R_Knee);
            Vector3 Lcalf = victim.GetBoneCoord(Bone.SKEL_L_Calf);
            Vector3 Rcalf = victim.GetBoneCoord(Bone.SKEL_R_Calf);
            Vector3 Lforearm = victim.GetBoneCoord(Bone.SKEL_L_Forearm);
            Vector3 Rforearm = victim.GetBoneCoord(Bone.SKEL_R_Forearm);
            Vector3 Lthigh = victim.GetBoneCoord(Bone.SKEL_L_Thigh);
            Vector3 Rthigh = victim.GetBoneCoord(Bone.SKEL_R_Thigh);
            Vector3 Lupperarm = victim.GetBoneCoord(Bone.SKEL_L_UpperArm);
            Vector3 Rupperarm = victim.GetBoneCoord(Bone.SKEL_R_UpperArm);

            float headDist = DistanceBetween(headBone, damageSource.Position);
            float bodyDist = DistanceBetween(body, damageSource.Position);
            float pelvisDist = DistanceBetween(pelvis, damageSource.Position);
            float spine0Dist = DistanceBetween(spine0, damageSource.Position);
            float spine1Dist = DistanceBetween(spine1, damageSource.Position);
            float spine2Dist = DistanceBetween(spine2, damageSource.Position);
            float spine3Dist = DistanceBetween(spine3, damageSource.Position);
            float spineRootDist = DistanceBetween(spineRoot, damageSource.Position);
            float LclavDist = DistanceBetween(Lclav, damageSource.Position);
            float RclavDist = DistanceBetween(Rclav, damageSource.Position);

            float LhandDist = DistanceBetween(Lhand, damageSource.Position);
            float RhandDist = DistanceBetween(Rhand, damageSource.Position);
            float LelbowDist = DistanceBetween(Lelbow, damageSource.Position);
            float RelbowDist = DistanceBetween(Relbow, damageSource.Position);
            float LkneeDist = DistanceBetween(Lknee, damageSource.Position);
            float RkneeDist = DistanceBetween(Rknee, damageSource.Position);
            float LcalfDist = DistanceBetween(Lcalf, damageSource.Position);
            float RcalfDist = DistanceBetween(Rcalf, damageSource.Position);
            float LforearmDist = DistanceBetween(Lforearm, damageSource.Position);
            float RforearmDist = DistanceBetween(Rforearm, damageSource.Position);
            float LthighDist = DistanceBetween(Lthigh, damageSource.Position);
            float RthighDist = DistanceBetween(Rthigh, damageSource.Position);
            float LupperarmDist = DistanceBetween(Lupperarm, damageSource.Position);
            float RupperarmDist = DistanceBetween(Rupperarm, damageSource.Position);

            if (bodyDist <= 0.82f || headDist <= 0.9f || pelvisDist <= 0.82f
                 || spine0Dist <= 0.82f || spine1Dist <= 0.82f || spine2Dist <= 0.82f
                  || spine3Dist <= 0.82f || spineRootDist <= 0.82f || LclavDist <= 0.82f
                   || RclavDist <= 0.82f || LhandDist <= 0.82f || RhandDist <= 0.82f
                    || LelbowDist <= 0.82f || RelbowDist <= 0.82f || LkneeDist <= 0.82f
                     || RkneeDist <= 0.82f || LcalfDist <= 0.82f || RcalfDist <= 0.82f
                      || LforearmDist <= 0.82f || RforearmDist <= 0.82f || LthighDist <= 0.82f
                       || RthighDist <= 0.82f || LupperarmDist <= 0.82f || RupperarmDist <= 0.82f)
            {
                //if (headDist < bodyDist && headDist < legsDist)
                //{
                //    if (headDist > 0)
                //    { return headBone; }
                //    else { return body; }
                //}
                //else if (bodyDist <= headDist && bodyDist <= legsDist)
                //{
                //    if (bodyDist > 0)
                //    { return body; }
                //    else { return legs; }
                //}
                //else if (legsDist < bodyDist && legsDist < headDist)
                //{
                //    if (legsDist > 0)
                //    { return legs; }
                //    else { return body; }
                //}
                float minDist = Math.Min(Math.Min(headDist, bodyDist), Math.Min(pelvisDist, Math.Min(spine0Dist, Math.Min(spine1Dist, Math.Min(spine2Dist, Math.Min(spine3Dist, Math.Min(spineRootDist, Math.Min(LclavDist, Math.Min(RclavDist, Math.Min(LhandDist,
                    Math.Min(RhandDist, Math.Min(LelbowDist, Math.Min(RelbowDist, Math.Min(LkneeDist, Math.Min(RkneeDist, Math.Min(LcalfDist, Math.Min(RcalfDist, Math.Min(LforearmDist, Math.Min(RforearmDist, Math.Min(LthighDist, Math.Min(RthighDist, Math.Min(LupperarmDist, RupperarmDist))))))))))))))))))))));

                if (minDist == headDist) { return headBone; }
                if (minDist == bodyDist) { return body; }
                if (minDist == pelvisDist) { return pelvis; }
                if (minDist == spine0Dist) { return spine0; }
                if (minDist == spine1Dist) { return spine1; }
                if (minDist == spine2Dist) { return spine2; }
                if (minDist == spine3Dist) { return spine3; }
                if (minDist == spineRootDist) { return spineRoot; }
                if (minDist == LclavDist) { return Lclav; }
                if (minDist == RclavDist) { return Rclav; }

                if (minDist == LhandDist) { return Lhand; }
                if (minDist == RhandDist) { return Rhand; }
                if (minDist == LelbowDist) { return Lelbow; }
                if (minDist == RelbowDist) { return Relbow; }
                if (minDist == LkneeDist) { return Lknee; }
                if (minDist == RkneeDist) { return Rknee; }
                if (minDist == LcalfDist) { return Lcalf; }
                if (minDist == RcalfDist) { return Rcalf; }
                if (minDist == LforearmDist) { return Lforearm; }
                if (minDist == RforearmDist) { return Rforearm; }
                if (minDist == LthighDist) { return Lthigh; }
                if (minDist == RthighDist) { return Rthigh; }
                if (minDist == LupperarmDist) { return Lupperarm; }
                if (minDist == RupperarmDist) { return Rupperarm; }
            }
            return new Vector3(0, 0, 0);*/


            Vector3 closestBoneLoc = Vector3.Zero;
            float closestBoneDist = 1.1f;

            foreach (pedBone bone in Enum.GetValues(typeof(pedBone)))
            {
                if (PedHasBone(victim, bone))
                {
                    Vector3 currBoneCoord = victim.GetBoneCoord((Bone)bone);
                    float distance = DistanceBetween(currBoneCoord, damageSource);

                    if (bone.ToString().Contains("Spine") || bone.ToString().Contains("Clavicle"))
                    {
                        if (distance <= 1.2f)
                        {
                            closestBoneDist = distance;
                            closestBoneLoc = currBoneCoord;
                            return closestBoneLoc;
                        }
                        else { continue; }
                    }
                    else
                    {
                        if (distance <= closestBoneDist)
                        {
                            closestBoneDist = distance;
                            closestBoneLoc = currBoneCoord;
                            return closestBoneLoc;
                        }
                        else { continue; }
                    }
                }
                else { continue; }
            }
            return closestBoneLoc;
        }

        enum vehBone
        {
            chassis,
            windscreen,
            seat_pside_r,
            seat_dside_r,
            bodyshell,
            suspension_lm,
            suspension_lr,
            attach_female,
            attach_male,
            bonnet,
            boot,
            chassis_dummy,  //Center of the dummy
            chassis_Control,    //Not found yet
            door_dside_f,   //Door left, front
            door_dside_r,   //Door left, back
            door_pside_f,   //Door right, front
            door_pside_r,   //Door right, back
            Gun_GripR,
            windscreen_f,
            platelight, //Position where the light above the numberplate is located
            VFX_Emitter,
            window_lf,  //Window left, front
            window_lr,  //Window left, back
            window_rf,  //Window right, front
            window_rr,  //Window right, back
            engine, //Position of the engine
            gun_ammo,
            ROPE_ATTATCH,   //Not misspelled. In script "finale_heist2b.c4".
            wheel_lf,   //Wheel left, front
            wheel_lr,   //Wheel left, back
            wheel_rf,   //Wheel right, front
            wheel_rr,   //Wheel right, back
            exhaust,    //Exhaust. shows only the position of the stock-exhaust
            overheat,   //A position on the engine(not exactly sure, how to name it)
            misc_e, //Not a car-bone.
            seat_dside_f,   //Driver-seat
            seat_pside_f,   //Seat next to driver
            Gun_Nuzzle,
            seat_r
        }

        Vector3 vehDamageLoc(Vehicle victim, Vector3 damageSourcePos)
        {
            Vector3 closestBoneLoc = Vector3.Zero;
            float closestBoneDist = 1000f;

            foreach (vehBone bone in Enum.GetValues(typeof(vehBone)))
            {
                if (VehicleHasBone(victim, bone))
                {
                    Vector3 currBoneCoord = victim.GetBoneCoord(victim.GetBoneIndex(bone.ToString()));
                    float distance = DistanceBetween(currBoneCoord, damageSourcePos);

                    if (distance <= closestBoneDist)
                    {
                        closestBoneDist = distance;
                        closestBoneLoc = currBoneCoord;
                        continue;
                    }
                    continue;
                }
                continue;
            }
            return closestBoneLoc;
        }

        bool VehicleHasBone(Entity e, vehBone bone)
        {
            if (Function.Call<int>(Hash.GET_ENTITY_BONE_INDEX_BY_NAME, e, bone.ToString()) != -1)
            {
                return true;
            } else { return false; }
        }

        bool PedHasBone(Ped e, pedBone bone)
        {
            if (e.GetBoneIndex((Bone)bone) != -1)
            {
                return true;
            }
            else { return false; }
        }

        void noCollisionWithPlayer(Entity ent)
        {
            ent.SetNoCollision(player, false); //false = no collision between entities
        }

        void createBallisticShieldsOnHand()
        {
            Model ballShieldMod = new Model("prop_ballistic_shield");
            ballShieldMod.Request(250);

            if (ballShieldMod.IsInCdImage && ballShieldMod.IsValid && /*!ballShieldExistsOnHand*/ !BallShieldsExistOnHand())
            {
                while (!ballShieldMod.IsLoaded) { Wait(250); }
                /*while (player.Weapons.CurrentWeaponObject == null) { Wait(250); }

                player.Weapons.CurrentWeaponObject.IsExplosionProof = true;
                player.Weapons.CurrentWeaponObject.IsFireProof = true;
                player.Weapons.CurrentWeaponObject.IsMeleeProof = true;
                player.Weapons.CurrentWeaponObject.IsBulletProof = true;
                player.Weapons.CurrentWeaponObject.IsInvincible = true;*/

                ballShield1 = World.CreateProp(ballShieldMod, Vector3.Zero, true, true);
                ballShield2 = World.CreateProp(ballShieldMod, Vector3.Zero, true, true);
                ballShield3 = World.CreateProp(ballShieldMod, Vector3.Zero, true, true);
                //ballShield4 = World.CreateProp(ballShieldMod, Vector3.Zero, true, true);

                /*AttachBallisticShieldsToRHand(ballShield1, new Vector3(-0.2f, -0.2f, 0.3f), new Vector3(-80, 0, 90));
                AttachBallisticShieldsToRHand(ballShield2, new Vector3(0.25f, -0.2f, 0.3f), new Vector3(-50, 0, 90));
                AttachBallisticShieldsToRHand(ballShield3, new Vector3(0, -0.5f, 0.3f), new Vector3(-75, 0, 0));
                AttachBallisticShieldsToRHand(ballShield4, new Vector3(0.3f, -0.5f, 0.3f), new Vector3(-75, 0, 45));*/

                AttachBallisticShieldsToRHand(ballShield1, new Vector3(0.23f, -0.17f, 0.2f), new Vector3(-40, -8, 70));
                AttachBallisticShieldsToEntity(ballShield2, ballShield1, new Vector3(-0.213f, -0.009f, -0.173f), new Vector3(169, -90, 166));
                AttachBallisticShieldsToEntity(ballShield3, ballShield1, new Vector3(0.006f, 0.051f, -0.315f), new Vector3(152, -5, 166));

                //ballShieldExistsOnHand = true;
            }
        }

        bool BallShieldsExistOnHand()
        {
            try
            {
                if (ballShield1.Exists() && ballShield2.Exists() && ballShield3.Exists() /*&& ballShield4.Exists()*/)
                {
                    return true;
                }
                return false;
            } catch { return false; }
        }

        void createBallisticShieldOnBack(PoweredUser user)
        {
            Model ballShieldMod = new Model("prop_ballistic_shield");
            ballShieldMod.Request(250);

            if (ballShieldMod.IsInCdImage && ballShieldMod.IsValid && !ballShieldExistsOnBack)
            {
                while (!ballShieldMod.IsLoaded) { Wait(250); }

                weapBackProp = World.CreateProp(user.holdWeap, Vector3.Zero, true, false);
                AttachCapShieldToBack(weapBackProp, user.PoweredPed, user.AssignedProfile);

                ballShieldBack1 = World.CreateProp(ballShieldMod, Vector3.Zero, true, true);
                ballShieldBack2 = World.CreateProp(ballShieldMod, Vector3.Zero, true, true);

                AttachBallisticShieldToBack(ballShieldBack1, new Vector3(0.25f, -0.45f, 0.08f), new Vector3(0f, 90f, 0f));
                AttachBallisticShieldToBack(ballShieldBack2, new Vector3(0.25f, -0.45f, -0.12f), new Vector3(0f, 90f, 0f));

                ballShieldExistsOnBack = true;
                //ballShieldExistsOnHand = false;
            }
        }

        void AttachCapShieldToBack(Entity shield, Ped p, ProfileSetting profile)
        {
            shield.IsExplosionProof = true;
            shield.IsInvincible = true;
            shield.IsFireProof = true;
            shield.IsMeleeProof = true;
            shield.AttachTo(p, p.GetBoneIndex(Bone.SKEL_Spine1), profile.BackShieldPos, profile.BackShieldRot);
        }

        void AttachBallisticShieldsToRHand(Prop s, Vector3 offset, Vector3 rotation)
        {
            try
            {
                //SetEntityProofs(s, true, false, true, true, true);
                s.IsExplosionProof = true;
                s.IsInvincible = true;
                s.IsFireProof = true;
                s.IsMeleeProof = true;
                s.IsPersistent = true;
                s.Alpha = 0;
                s.AttachTo(player, player.GetBoneIndex(Bone.SKEL_R_Hand), offset, rotation);
            }
            catch { }
        }

        void AttachBallisticShieldsToEntity(Prop s, Entity ent, Vector3 offset, Vector3 rotation)
        {
            try
            {
                //SetEntityProofs(s, true, false, true, true, true);
                s.IsExplosionProof = true;
                s.IsInvincible = true;
                s.IsFireProof = true;
                s.IsMeleeProof = true;
                s.IsPersistent = true;
                s.Alpha = 0;
                s.AttachTo(ent, 0, offset, rotation);
            }
            catch { }
        }

        void AttachBallisticShieldToBack(Prop s, Vector3 offset, Vector3 rotation)
        {
            try
            {
                //SetEntityProofs(s, true, false, true, true, true);
                s.IsExplosionProof = true;
                s.IsInvincible = true;
                s.IsFireProof = true;
                s.IsMeleeProof = true;
                s.IsPersistent = true;
                s.Alpha = 0;
                s.AttachTo(player, player.GetBoneIndex(Bone.SKEL_Spine1), offset, rotation);
            }
            catch { }
        }

        void deleteBallisticShieldsOnHand()
        {
            if (ballShield1 != null && ballShield1.Exists())
            {
                ballShield1.IsPersistent = false;
                ballShield1.MarkAsNoLongerNeeded();
                ballShield1.Delete();
            }
            if (ballShield2 != null && ballShield2.Exists())
            {
                ballShield2.IsPersistent = false;
                ballShield2.MarkAsNoLongerNeeded();
                ballShield2.Delete();
            }
            if (ballShield3 != null && ballShield3.Exists())
            {
                ballShield3.IsPersistent = false;
                ballShield3.MarkAsNoLongerNeeded();
                ballShield3.Delete();
            }
            /*if (ballShield4 != null && ballShield4.Exists())
            {
                ballShield4.IsPersistent = false;
                ballShield4.MarkAsNoLongerNeeded();
                ballShield4.Delete();
            }*/
            //ballShieldExistsOnHand = false;
        }

        void deleteShieldsOnBack()
        {
            if (ballShieldBack1 != null && ballShieldBack1.Exists())
            {
                ballShieldBack1.IsPersistent = false;
                ballShieldBack1.MarkAsNoLongerNeeded();
                ballShieldBack1.Delete();
            }
            if (ballShieldBack2 != null && ballShieldBack2.Exists())
            {
                ballShieldBack2.IsPersistent = false;
                ballShieldBack2.MarkAsNoLongerNeeded();
                ballShieldBack2.Delete();
            }
            if (weapBackProp != null && weapBackProp.Exists())
            {
                weapBackProp.IsPersistent = false;
                weapBackProp.MarkAsNoLongerNeeded();
                weapBackProp.Delete();
            }
            ballShieldExistsOnBack = false;
        }

        void clearDecalsFromShields()
        {
            if (cleanTimer <= Game.GameTime)
            {
                if (ballShield1 != null && ballShield1.Exists())
                {
                    Function.Call(Hash.REMOVE_DECALS_FROM_OBJECT, ballShield1);
                    if (ballShield1.IsOnFire) { stopEntityFire(ballShield1); }
                }
                if (ballShield2 != null && ballShield2.Exists())
                {
                    Function.Call(Hash.REMOVE_DECALS_FROM_OBJECT, ballShield2);
                    if (ballShield2.IsOnFire) { stopEntityFire(ballShield2); }
                }
                if (ballShield3 != null && ballShield3.Exists())
                {
                    Function.Call(Hash.REMOVE_DECALS_FROM_OBJECT, ballShield3);
                    if (ballShield3.IsOnFire) { stopEntityFire(ballShield3); }
                }
                /*if (ballShield4 != null && ballShield4.Exists())
                {
                    Function.Call(Hash.REMOVE_DECALS_FROM_OBJECT, ballShield4);
                    if (ballShield4.IsOnFire) { stopEntityFire(ballShield4); }
                }*/
                if (ballShieldBack1 != null && ballShieldBack1.Exists())
                {
                    Function.Call(Hash.REMOVE_DECALS_FROM_OBJECT, ballShieldBack1);
                    if (ballShieldBack1.IsOnFire) { stopEntityFire(ballShieldBack1); }
                }
                if (ballShieldBack2 != null && ballShieldBack2.Exists())
                {
                    Function.Call(Hash.REMOVE_DECALS_FROM_OBJECT, ballShieldBack2);
                    if (ballShieldBack2.IsOnFire) { stopEntityFire(ballShieldBack2); }
                }
                if (Hand2HeadObj != null && Hand2HeadObj.Exists())
                {
                    Hand2HeadObj.Wash();
                    Hand2HeadObj.Repair();
                    if (Hand2HeadObj.IsOnFire) { stopEntityFire(Hand2HeadObj); }
                }

                cleanTimer = Game.GameTime + 800;
            }
        }

        void clearAnyBallisticShields()
        {
            Prop[] props = World.GetNearbyProps(player.Position, 5f, "prop_ballistic_shield");

            if (props != null)
            {
                foreach (Prop p in props)
                {
                    if (p != null && p.Exists())
                    {
                        p.MarkAsNoLongerNeeded();
                        p.Delete();
                    }
                }
                //ballShieldExistsOnHand = false;
                ballShieldExistsOnBack = false;
            }
        }

        void clearAnyCapShields(PoweredUser user)
        {
            Prop[] props = World.GetNearbyProps(user.PoweredPed.Position, 5f, user.holdWeap);

            if (props != null)
            {
                foreach (Prop p in props)
                {
                    if (p != null && p.Exists())
                    {
                        p.MarkAsNoLongerNeeded();
                        p.Delete();
                    }
                }
            }
        }

        void ShieldFXRemover(PoweredUser user)
        {
            if (user.weapProp == null || !user.weapProp.Exists()) //if there is no shield that was thrown
            {
                if (!isPlayingReflectAnimation(user.PoweredPed) && !isPlayingTackleAnimation(user.PoweredPed) && !isPlayingAnim(user.PoweredPed, Animations.Shield2Ground))
                {
                    StopShieldFXTrail(user);
                }
            }
        }

        void reflectProps()
        {
            try
            {
                Prop currWeap = player.Weapons.CurrentWeaponObject;
                Entity[] ents = World.GetNearbyProps(currWeap.Position, 2.5f);
                currWeap.IsExplosionProof = true;
                currWeap.IsFireProof = true;
                if (ents != null)
                {
                    foreach (Entity e in ents)
                    {
                        if (e != null && e != player && !e.IsAttachedTo(player))
                        {
                            if (!reflectedProps.Contains(e))
                            {
                                //rInt = rng.Next(-20, 21);
                                Vector3 dir = (e.Position - player.Position).Normalized; //end - start normalized = direction from start to end position.
                                Vector3 dirZeroHeight = new Vector3(dir.X, dir.Y, 0);
                                Vector3 manualAimedDir;
                                float weapHeight = currWeap.HeightAboveGround;
                                float eHeight = e.HeightAboveGround;
                                int eSpeed = EntityCurrentSpeed(e);

                                if (eSpeed >= 1110000000) //if speed of prop is greater than 1110000000 units..
                                {
                                    if (canReflectToDirection(e, out manualAimedDir))
                                    {
                                        reflectEntityThisInstance(e, manualAimedDir, 80f, 1500);
                                    }
                                    else
                                    {
                                        if (eHeight >= weapHeight)
                                        {
                                            reflectEntityThisInstance(e, dir, 80f, 1500);
                                        }
                                        else
                                        {
                                            reflectEntityThisInstance(e, dirZeroHeight, 80f, 1500);
                                        }
                                    }
                                    PlayVehHitSound(currWeap);

                                }
                                else if (eSpeed >= 1080000000)
                                {
                                    if (canReflectToDirection(e, out manualAimedDir))
                                    {
                                        reflectEntityThisInstance(e, manualAimedDir, 60f, 1000);
                                    }
                                    else
                                    {
                                        if (eHeight >= weapHeight)
                                        {
                                            reflectEntityThisInstance(e, dir, 60f, 1000);
                                        }
                                        else
                                        {
                                            reflectEntityThisInstance(e, dirZeroHeight, 60f, 1000);
                                        }
                                    }
                                    PlayVehHitSound(currWeap);
                                }

                                //UI.ShowSubtitle(rInt.ToString());
                                //UI.ShowSubtitle(EntityCurrentSpeed(e).ToString(), 20000);
                                reflectedProps.Add(e);
                            }
                        }
                    }
                }
            }
            catch
            {
                //skip
            }
        }

        bool canReflectToDirection(Entity e, out Vector3 direction)
        {
            Vector3 fDir = player.ForwardVector;
            Vector3 fCamDir = ForwardDirFromCam(1f);

            float camDir2fDirAngle = Vector3.Angle(fCamDir, fDir);

            if (camDir2fDirAngle <= 90)
            {
                if (_TargettedEntity != null && Game.IsControlPressed(2, GTA.Control.Aim))
                {
                    Vector3 position = _TargettedEntity.Position;
                    if (EntityIsAPed(_TargettedEntity))
                    {
                        try
                        {
                            direction = (new Vector3(position.X, position.Y, GetGroundZ(position)) - e.Position).Normalized; //reflect to feet
                            return true;
                        }
                        catch
                        {
                            direction = (position - e.Position).Normalized;
                            return true;
                        }
                    }
                    else
                    {
                        direction = (position - e.Position).Normalized;
                        return true;
                    }
                }
                else
                {
                    direction = fCamDir;
                    return true;
                }
            } else {
                direction = Vector3.Zero;
                return false; }
        }

        void reflectEntityThisInstance(Entity e, Vector3 direction, float force, int millisecondsToControl)
        {
            //if (!EntityIsIronManRocket(e))
            //{
            //    ApplyVelocity(e, direction, force);
            //    e.Rotation = DirToRotTest(direction);
            //    UI.ShowSubtitle("Normal Projectile");
            //}
            //else
            //{
            //if (EntityCurrentSpeed(e) > 1100000000)
            //{
            try
            {
                controlledReflectedProps.Add(new controlledProps { _prop = e, _direction = direction, _ElapsedTime = Game.GameTime + millisecondsToControl });
                controlledPropTimer = Game.GameTime + 20000;
            }
            catch { }
            //}
            //}
        }

        void controlledPropAction()
        {
            if (controlledPropTimer <= Game.GameTime)
            {
                if (controlledReflectedProps.Count > 0)
                {
                    controlledReflectedProps.Clear();
                }
            }
            else
            {
                try
                {
                    foreach (controlledProps e in controlledReflectedProps)
                    {
                        if (e._prop.Exists())
                        {
                            if (e._ElapsedTime >= Game.GameTime)
                            {
                                SetVelocityXYZ(e._prop, e._direction, 60f);
                                e._prop.Rotation = DirToRotTest(e._direction);
                            }
                            else
                            {
                                controlledReflectedProps.Remove(e);
                            }
                        }
                        else
                        {
                            controlledReflectedProps.Remove(e);
                        }
                    }
                }
                catch { }
            }
        }

        /*bool EntityIsIronManRocket(Entity e)
        {
            //How to get model: UI.ShowSubtitle(e.Model.ToString());
            if (e.Model == IMMRocketModel)
            {
                return true;
            } else { return false; }
        }*/

        void ApplyVelocity(Entity target, Vector3 direction, float multiplier)
        {
            target.Velocity = new Vector3(target.Velocity.X + (direction.X * multiplier), target.Velocity.Y + (direction.Y * multiplier), target.Velocity.Z + (direction.Z * multiplier));
        }

        void SetVelocityXYZ(Entity target, Vector3 direction, float multiplier, bool DifferentZ = false, Vector3 DifferentZDir = default(Vector3), float ZMult = 0f)
        {
            if (!DifferentZ)
            {
                target.Velocity = new Vector3((direction.X * multiplier), (direction.Y * multiplier), (direction.Z * multiplier));
            }
            else
            {
                target.Velocity = new Vector3((direction.X * multiplier), (direction.Y * multiplier), (DifferentZDir.Z * ZMult));
            }
        }

        void ApproachTarget(Entity target, Entity approacher, bool untilTouching)
        {
            if (!untilTouching)
            { approacher.Velocity = (target.Position - approacher.Position); }
            else
            {
                if (approacher.IsTouching(target))
                {
                    approacher.Velocity = (target.Position - approacher.Position);
                }
                else
                {
                    Vector3 dir = (target.Position - approacher.Position).Normalized;
                    Vector3 vel = approacher.Velocity;
                    approacher.Velocity = new Vector3(vel.X + (dir.X * 82.5f * Game.LastFrameTime), vel.Y + (dir.Y * 82.5f * Game.LastFrameTime), vel.Z + (dir.Z * 20 * Game.LastFrameTime));
                    //approacher.Velocity = new Vector3((dir.X * 12), (dir.Y * 12), (dir.Z * 10));
                }
            }
        }

        void SetEntityProofs(Entity e, bool _bulletProof, bool _collisionProof, bool _explosionProof, bool _fireProof, bool _meleeProof)
        {
            e.IsBulletProof = _bulletProof;
            e.IsCollisionProof = _collisionProof;
            e.IsExplosionProof = _explosionProof;
            e.IsFireProof = _fireProof;
            e.IsMeleeProof = _meleeProof;
        }

        void SetObjectPhysicsParameters(Prop prop, float weight = -1.0f, float p2 = 1.2f, float p3 = -1.0f, float p4 = -1.0f, float p5 = 0.010988f, float gravity = 1.0f, float p7 = -1.0f, float p8 = -1.0f, float p9 = -1.0f, float p10 = -1.0f, float buoyancy = -1.0f)
        {
            Function.Call(Hash.SET_OBJECT_PHYSICS_PARAMS, prop, weight, p2, p3, p4, p5, gravity, p7, p8, p9, p10, buoyancy);
        }

        void SetPedRagdoll(Ped p, int ms, ragdollType type)
        {
            Function.Call(Hash.SET_PED_CAN_RAGDOLL, p, true);
            Function.Call(Hash.SET_PED_TO_RAGDOLL, p, ms, ms, (int)type, 1, 1, 0);
        }

        void BlockPedRagdollOfType(Ped p, ragdollBlock type)
        {
            Function.Call(Hash._0x26695EC767728D84, p, (int)type);
        }

        void ResetRagdollBlockOfType(Ped p, ragdollBlock type)
        {
            Function.Call(Hash._0xD86D101FCFD00A4B, p, (int)type);
        }

        bool IsDoingNoRagdollFlinch(Ped p)
        {
            return !Function.Call<bool>(Hash.CAN_PED_RAGDOLL, p) && !Function.Call<bool>(Hash.GET_PED_CONFIG_FLAG, p, 104, 1) && p.HeightAboveGround < 2f;
        }

        bool IsUpright(Entity e, float angle)
        {
            return Function.Call<bool>(Hash.IS_ENTITY_UPRIGHT, e, angle);
        }

        void AttachEntityToEntity(Entity e1, Entity e2, int boneIndexE2, Vector3 offsetPos, Vector3 rotation, bool useSoftPinning = false, bool collisionBetweenEnts = false, bool entOneIsPed = false, int vertexIndex = 0, bool fixedRot = false)
        {
            Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY, e1, e2, boneIndexE2, offsetPos.X, offsetPos.Y, offsetPos.Z, rotation.X, rotation.Y, rotation.Z, -1f, useSoftPinning, collisionBetweenEnts, entOneIsPed, vertexIndex, fixedRot);
        }

        void AttachEntityToEntityPhysically(Entity e1, Entity e2, int boneIndexE1, int boneIndexE2, Vector3 offsetPos1, Vector3 offsetPos2, Vector3 rotation, float breakForce = 10000000.0f, bool fixedRot = false, bool p15 = false, bool collision = false, bool DoNotTeleport = false, int p18 = 0)
        {
            Function.Call(Hash.ATTACH_ENTITY_TO_ENTITY_PHYSICALLY, e1, e2, boneIndexE1, boneIndexE2, offsetPos1.X, offsetPos1.Y, offsetPos1.Z, offsetPos2.X, offsetPos2.Y, offsetPos2.Z, rotation.X, rotation.Y, rotation.Z, breakForce, fixedRot, p15, collision, DoNotTeleport, p18);
        }

        void AddBloodToPed(Ped ped)
        {
            Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "core");
            Function.Call(Hash._SET_PTFX_ASSET_NEXT_CALL, "core");
            Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_ON_PED_BONE, "td_blood_shotgun", ped, 0, 0, 0, GameplayCamera.Rotation.X + 0, GameplayCamera.Rotation.Y + -90, GameplayCamera.Rotation.Z + 125, 56604, 1.5f, false, false, false);
            Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_ON_PED_BONE, "td_blood_shotgun", ped, 0, 0, 0, GameplayCamera.Rotation.X + 0, GameplayCamera.Rotation.Y + -90, GameplayCamera.Rotation.Z + -45, 56604, 1.5f, false, false, false);

            Function.Call(Hash.APPLY_PED_DAMAGE_PACK, ped, "BigRunOverByVehicle", 1.0, 1.0);
        }

        void AddShock(Vector3 targetPos, Vector3 source, Ped owner)
        {
            World.ShootBullet(source, targetPos, owner, WeaponHash.SNSPistol, 1, 1);
        }

        void AddLightWithShadow(Entity p, float range, float intensity, float shadow)
        {
            Vector3 pos = p.Position;
            Function.Call(Hash._0xF49E9A9716A04595, pos.X, pos.Y, pos.Z, 10, 10, 10, range, intensity, shadow);
            Function.Call(Hash._0xF49E9A9716A04595, pos.X, pos.Y, pos.Z, 0, 0, 100, range, intensity, shadow);
        }

        void AddShieldFXTrail(Entity e, Vector3 posOffset, PoweredUser user)
        {
            if (e != null && e.Exists())
            {
                if (Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, "scr_minigamegolf"))
                {
                    Function.Call(Hash._SET_PTFX_ASSET_NEXT_CALL, "scr_minigamegolf");

                    if (!Function.Call<bool>(Hash.DOES_PARTICLE_FX_LOOPED_EXIST, user.ShieldFX))
                    {
                        user.ShieldFX = Function.Call<int>(Hash.START_PARTICLE_FX_LOOPED_ON_ENTITY, "scr_golf_ball_trail", e, posOffset.X, posOffset.Y, posOffset.Z, 0.0, 0.0, 180.0, 3.0, false, false, false);
                        Function.Call(Hash.SET_PARTICLE_FX_LOOPED_COLOUR, user.ShieldFX, user.AssignedProfile.FxRed, user.AssignedProfile.FxGreen, user.AssignedProfile.FxBlue, false);
                    }
                } else { Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "scr_minigamegolf"); }
            }
        }

        void StopShieldFXTrail(PoweredUser user)
        {
            Function.Call(Hash.REMOVE_PARTICLE_FX, user.ShieldFX, true);
        }

        void stopEntityFire(Entity ent)
        {
            Function.Call(Hash.STOP_ENTITY_FIRE, ent);
        }

        void startDefaultStealthTask(Ped ped)
        {
            if (!IsPedInStealth(ped)) { Function.Call(Hash.SET_PED_STEALTH_MOVEMENT, ped, 1, "DEFAULT_ACTION"); }
        }

        void stopDefaultStealthTask(Ped p)
        {
            if (IsPedInStealth(p)) { Function.Call(Hash.SET_PED_STEALTH_MOVEMENT, p, 0, "DEFAULT_ACTION"); }
        }

        void PlayPedAmbientSpeech(Ped p, string speechName, string speechParam)
        {
            Function.Call(Hash._0x8E04FEDD28D42462, p, speechName, speechParam); //_PLAY_AMBIENT_SPEECH1
        }

        void PlayChargeSound(Ped ped)
        {
            //Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, "QUIT_WHOOSH", "HUD_MINI_GAME_SOUNDSET", 1);
            Function.Call(Hash.PLAY_PAIN, ped, 31, 0, 0);
        }

        void PlayHeavySmashSound(Vector3 pos)
        {
            Function.Call(Hash.PLAY_SOUND_FROM_COORD, -1, "Jet_Explosions", pos.X, pos.Y, pos.Z, "exile_1", 0, 0, 0);
        }

        void PlayVehHitSound(Entity v)
        {
            Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "CRASH", v, "PAPARAZZO_03A", 0);
        }

        void PlayPedHitSound(Ped p)
        {
            Function.Call(Hash.PLAY_SOUND_FROM_ENTITY, -1, "Architect_Fall", p, "FBI_HEIST_SOUNDSET", 0);
        }

        void PlayPedPainSound(Ped p)
        {
            Function.Call(Hash.PLAY_PAIN, p, 33, 0, 0);
        }

        enum PTFX
        {
            GroundSmash,
            MeleeSpark
        }


        Dictionary<string, Dictionary<int, string>> PtfxData = new Dictionary<string, Dictionary<int, string>>()
        {
            {
                "GroundSmash", new Dictionary<int, string>
                {
                    { 0, "core" }, //ptfxDict
                    { 1, "ent_dst_concrete_large" }, //ptfxName
                }
            },
            {
                "MeleeSpark", new Dictionary<int, string>
                {
                    { 0, "core" }, //ptfxDict
                    { 1, "mel_carmetal" }, //ptfxName
                }
            }
        };

        string getPtfxDict(PTFX fx)
        {
            return PtfxData[fx.ToString()][0];
        }

        string getPtfxName(PTFX fx)
        {
            return PtfxData[fx.ToString()][1];
        }
        
        void StartPtfxAtCoordNonLooped(PTFX ptfx, Vector3 pos, Vector3 rot, float scale)
        {
            string dict = getPtfxDict(ptfx);
            string name = getPtfxName(ptfx);

            if (!Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, dict))
            {
                Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, dict);
            }

            if (Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, dict))
            {
                Function.Call(Hash._SET_PTFX_ASSET_NEXT_CALL, dict);

                Function.Call<int>(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD, name, pos.X, pos.Y, pos.Z, rot.X, rot.Y, rot.Z, scale, false, false, false);
            }
        }

        enum Animations
        {
            OnFootThrowVertical,
            OnFootThrowAcross,
            Tackle,
            Reflect,
            Tank,
            GrabPed,
            ThrowPed,
            PickupVeh,
            HoldVeh,
            StrongKick,
            BackSlap,
            LeftHook,
            RightHook,
            SmackDown,
            Uppercut,
            Shield2Ground
        }

        Dictionary<string, Dictionary<int, string>> Animation = new Dictionary<string, Dictionary<int, string>>()
        {
            {
                "OnFootThrowVertical", new Dictionary<int, string>
                {
                    { 0, "melee@knife@streamed_core" }, //animDict
                    { 1, "plyr_knife_front_takedown" }, //animName
                    { 2, "0.135" }, //actionStartTime
                    { 3, "8.0" }, //startSpeed
                    { 4, "1.0" }, //endSpeed
                    { 5, "600" }, //duration
                    { 6, "8" }, //flag
                    { 7, "0.0" }, //startPercentage
                    { 8, "0.16" }, //actionStartTime
                }
            },
            {
                "OnFootThrowAcross", new Dictionary<int, string>
                {
                    { 0, "melee@small_wpn@streamed_core_fps" }, //animDict
                    { 1, "small_melee_wpn_short_range_0" }, //animName
                    { 2, "0.0995632" }, //actionStartTime
                    { 3, "5.0" }, //startSpeed
                    { 4, "1.0" }, //endSpeed
                    { 5, "600" }, //duration
                    { 6, "8" }, //flag
                    { 7, "0.0" }, //startPercentage
                    { 8, "0.1227416" }, //actionEndTime
                }
            },
            {
                "Tackle", new Dictionary<int, string>
                {
                    { 0, "nm@stunt_jump" },
                    { 1, "jump_intro" },
                    { 2, "0.0" }, //actionStartTime
                    { 3, "4.0" }, //startSpeed
                    { 4, "3.0" }, //endSpeed
                    { 5, "2000" }, //duration
                    { 6, "8" }, //flag
                    { 7, "0.0" }, //startPercentage
                }
            },
            {
                "Reflect", new Dictionary<int, string>
                {
                    { 0, "melee@unarmed@streamed_core" },
                    { 1, "plyr_takedown_front_elbow" },
                    { 2, "0" }, //actionStartTime
                    { 3, "8" }, //startSpeed
                    { 4, "1.5" }, //endSpeed
                    { 5, "350" }, //duration
                    { 6, "8" }, //flag
                    { 7, "0.12" }, //startPercentage
                }
            },
            {
                "Tank", new Dictionary<int, string>
                {
                    { 0, "weapons@first_person@aim_rng@generic@submachine_gun@shared@core" /*"weapons@first_person@aim_idle@generic@melee@knife@shared@core" or "move_stealth@p_m_zero@2h_short@upper"*/ },
                    { 1, "wall_block" /*"walk"*/ },
                    { 2, "0" }, //actionStartTime
                    { 3, "2" }, //startSpeed
                    { 4, "8" }, //endSpeed
                    { 5, "60000" }, //duration
                    { 6, "51" }, //flag
                    { 7, "0.0" }, //startPercentage
                }
            },
            {
                "GrabPed", new Dictionary<int, string>
                {
                    { 0, "melee@knife@streamed_core" },
                    { 1, "victim_knife_failed_takedown_rear" },
                    { 2, "0.4006077" }, //actionStartTime
                    { 3, "6" }, //startSpeed
                    { 4, "2" }, //endSpeed
                    { 5, "400" }, //duration
                    { 6, "1" }, //flag
                    { 7, "0.35" }, //startPercentage
                }
            },
            {
                "ThrowPed", new Dictionary<int, string>
                {
                    { 0, "weapons@projectile@grenade_str" },
                    { 1, "throw_m_fb_forward" },
                    { 2, "0.1702127" }, //actionStartTime
                    { 3, "6" }, //startSpeed
                    { 4, "2" }, //endSpeed
                    { 5, "600" }, //duration
                    { 6, "1" }, //flag
                    { 7, "0" }, //startPercentage
                }
            },
            {
                "PickupVeh", new Dictionary<int, string>
                {
                    { 0, "veh@aligns@bike@ds" },
                    { 1, "pickup" },
                    { 2, "0.4729632" }, //actionStartTime (pickup vehicle time. Switch to holdVeh animation time is separate.)
                    { 3, "5" }, //startSpeed
                    { 4, "2" }, //endSpeed
                    { 5, "1300" }, //duration
                    { 6, "8" }, //flag
                    { 7, "0.0" }, //startPercentage
                }
            },
            /*{
                "HoldVeh", new Dictionary<int, string>
                {
                    { 0, "missfra1mcs_2_crew_react" },
                    { 1, "handsup_standing_base" },
                    { 2, "0" }, //actionStartTime
                    { 3, "2" }, //startSpeed
                    { 4, "0.1" }, //endSpeed
                    { 5, "20000" }, //duration
                    { 6, "51" }, //flag
                    { 7, "0.8" }, //startPercentage
                }
            },*/
            {
                "HoldVeh", new Dictionary<int, string>
                {
                    { 0, "random@domestic" },
                    { 1, "balcony_fight_male" },
                    { 2, "0" }, //actionStartTime
                    { 3, "2" }, //startSpeed
                    { 4, "0.1" }, //endSpeed
                    { 5, "-1" }, //duration
                    { 6, "51" }, //flag
                    { 7, "0.648" }, //startPercentage
                }
            },
            {
                "StrongKick", new Dictionary<int, string>
                {
                    { 0, "melee@knife@streamed_core" },
                    { 1, "kick_close_a" },
                    { 2, "0.2320501" }, //actionStartTime
                    { 3, "4" }, //startSpeed
                    { 4, "2" }, //endSpeed
                    { 5, "800" }, //duration
                    { 6, "0" }, //flag
                    { 7, "0.1" }, //startPercentage
                    { 8, "0.2910531" }, //actionEndTime
                    { 9, "IK_R_Foot" }, //DamageBoneSource
                }
            },
            {
                "BackSlap", new Dictionary<int, string>
                {
                    { 0, "melee@unarmed@streamed_variations" },
                    { 1, "plyr_takedown_front_backslap" },
                    { 2, "0.2461163" }, //actionStartTime
                    { 3, "5" }, //startSpeed
                    { 4, "2" }, //endSpeed
                    { 5, "700" }, //duration
                    { 6, "0" }, //flag
                    { 7, "0.1" }, //startPercentage
                    { 8, "0.2714435" }, //actionEndTime
                    { 9, "IK_R_Hand" }, //DamageBoneSource
                }
            },
            {
                "LeftHook", new Dictionary<int, string>
                {
                    { 0, "melee@unarmed@streamed_core" },
                    { 1, "plyr_takedown_rear_lefthook" },
                    { 2, "0.3237437" }, //actionStartTime
                    { 3, "6" }, //startSpeed
                    { 4, "2" }, //endSpeed
                    { 5, "1000" }, //duration
                    { 6, "0" }, //flag
                    { 7, "0.1" }, //startPercentage
                    { 8, "0.3636589" }, //actionEndTime
                    { 9, "IK_L_Hand" }, //DamageBoneSource
                }
            },
            {
                "RightHook", new Dictionary<int, string>
                {
                    { 0, "melee@unarmed@streamed_variations" },
                    { 1, "plyr_stealth_kill_unarmed_hook_r" },
                    { 2, "0.2444275" }, //actionStartTime
                    { 3, "4" }, //startSpeed
                    { 4, "2" }, //endSpeed
                    { 5, "900" }, //duration
                    { 6, "0" }, //flag
                    { 7, "0.09" }, //startPercentage
                    { 8, "0.30" }, //actionEndTime
                    { 9, "IK_R_Hand" }, //DamageBoneSource
                }
            },
            {
                "Uppercut", new Dictionary<int, string>
                {
                    { 0, "melee@unarmed@streamed_core_fps" },
                    { 1, "plyr_takedown_front_uppercut" },
                    { 2, "0.2540199" }, //actionStartTime
                    { 3, "5" }, //startSpeed
                    { 4, "2" }, //endSpeed
                    { 5, "1000" }, //duration
                    { 6, "0" }, //flag
                    { 7, "0.1" }, //startPercentage
                    { 8, "0.30" }, //actionEndTime
                    { 9, "IK_R_Hand" }, //DamageBoneSource
                }
            },
            {
                "SmackDown", new Dictionary<int, string>
                {
                    { 0, "melee@unarmed@streamed_core_fps" },
                    { 1, "plyr_psycho_front_takedown" },
                    { 2, "0.1736747" }, //actionStartTime
                    { 3, "5" }, //startSpeed
                    { 4, "2" }, //endSpeed
                    { 5, "800" }, //duration
                    { 6, "0" }, //flag
                    { 7, "0.05" }, //startPercentage
                    { 8, "0.2121151" }, //actionEndTime
                    { 9, "IK_R_Hand" }, //DamageBoneSource
                }
            },
            {
                "Shield2Ground", new Dictionary<int, string>
                {
                    { 0, "melee@knife@streamed_core" },
                    { 1, "ground_attack_on_spot" },
                    { 2, "0.3326994" }, //actionStartTime
                    { 3, "4" }, //startSpeed 4f, 2f, 800, 1, 0.1
                    { 4, "2" }, //endSpeed
                    { 5, "800" }, //duration
                    { 6, "1" }, //flag
                    { 7, "0.1" }, //startPercentage
                }
            },
        };

        string getAnimDict(Animations action)
        {
            return Animation[action.ToString()][0];
        }

        string getAnimName(Animations action)
        {
            return Animation[action.ToString()][1];
        }

        float getAnimActionStartTime(Animations action)
        {
            return float.Parse(Animation[action.ToString()][2]);
        }

        float getAnimActionEndTime(Animations action)
        {
            float time;
            bool actionEndTimeExists = float.TryParse(Animation[action.ToString()][8], out time);
            if (actionEndTimeExists) { return time; } else { return 1.0f; }
        }

        float getAnimStartSpeed(Animations action)
        {
            return float.Parse(Animation[action.ToString()][3]);
        }

        float getAnimEndSpeed(Animations action)
        {
            return float.Parse(Animation[action.ToString()][4]);
        }

        int getAnimDuration(Animations action)
        {
            return int.Parse(Animation[action.ToString()][5]);
        }

        int getAnimFlag(Animations action)
        {
            return int.Parse(Animation[action.ToString()][6]);
        }

        float getAnimStartPercentage(Animations action)
        {
            return float.Parse(Animation[action.ToString()][7]);
        }

        string getMeleeAnimDmgBoneLocation(Animations action)
        {
            return Animation[action.ToString()][9];
        }

        float getAnimCurrentTime(Ped ped, string animDict, string animName)
        {
            return Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, ped, animDict, animName);
        }

        void setAnimCurrentTime(Ped ped, string animDict, string animName, float percentageTime)
        {
            Function.Call(Hash.SET_ENTITY_ANIM_CURRENT_TIME, ped, animDict, animName, percentageTime);
        }

        void SetAnimationSpeed(Ped p, Animations action, float speedMult)
        {
            string AnimDict = getAnimDict(action);
            string AnimName = getAnimName(action);

            if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, p, AnimDict, AnimName, 3))
            {
                Function.Call(Hash.SET_ENTITY_ANIM_SPEED, p, AnimDict, AnimName, speedMult);
            }
        }

        void EnhancedAnimationSpeeds(Ped ped)
        {
            SetAnimationSpeed(ped, Animations.OnFootThrowVertical, 1.4f);
            SetAnimationSpeed(ped, Animations.OnFootThrowAcross, 1.4f);
            SetAnimationSpeed(ped, Animations.Shield2Ground, 0.7f);
            SetAnimationSpeed(ped, Animations.LeftHook, 1.13f);
        }

        bool HasAnimDictLoaded(string dict)
        {
            return Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, dict);
        }

        void CancelAnim(Ped p)
        {
            if (!HasAnimDictLoaded("missfra1mcs_2_crew_react")) { Function.Call(Hash.REQUEST_ANIM_DICT, "missfra1mcs_2_crew_react"); }
            Function.Call(Hash.TASK_PLAY_ANIM, p, "missfra1mcs_2_crew_react", "handsup_standing_base", 2f, 8f, 20, 51, 0.8f, false, false, false);
            p.Task.ClearAll();
        }

        void playAnimation(Ped ped, Animations anim, bool AllowInAir = false, bool RestoreVelocityInAir = false)
        {
            string dict = getAnimDict(anim);
            string name = getAnimName(anim);
            float startTime = getAnimStartSpeed(anim);
            float endTime = getAnimEndSpeed(anim);
            int duration = getAnimDuration(anim);
            int flag = getAnimFlag(anim);
            float startPercent = getAnimStartPercentage(anim);

            CancelAnim(ped);
            stopDefaultStealthTask(ped);
            stopTankAnimation(ped);

            Vector3 lastVelocity = ped.Velocity;
            if (AllowInAir)
            {
                if (ped.HeightAboveGround > 2f) { ped.Task.ClearAllImmediately(); ped.Velocity = Vector3.Zero; }
            }

            if (!HasAnimDictLoaded(dict)) { Function.Call(Hash.REQUEST_ANIM_DICT, dict); }
            Function.Call(Hash.TASK_PLAY_ANIM, ped, dict, name, startTime, endTime, duration, flag, startPercent, false, false, false);

            if (RestoreVelocityInAir)
            {
                ped.Velocity = lastVelocity;
            }
        }

        bool isPlayingAnim(Ped ped, Animations anim)
        {
            string dict = getAnimDict(anim);
            string name = getAnimName(anim);

            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, dict, name, 3);

            if (!ped.IsGettingUp && !ped.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        bool isPlayingAnim(PoweredUser user, Animations anim)
        {
            string dict = getAnimDict(anim);
            string name = getAnimName(anim);

            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, user.PoweredPed, dict, name, 3);

            if (!user.PoweredPed.IsGettingUp && !user.PoweredPed.IsRagdoll)
            {
                user.MeleeDamageBoneSource = getMeleeAnimDmgBoneLocation(anim);
                return playingAnim;
            }
            return false;
        }

        bool ActionStartTimePassedDuringAnim(Ped ped, Animations anim)
        {
            string dict = getAnimDict(anim);
            string name = getAnimName(anim);
            float actionTime = getAnimActionStartTime(anim);

            if (isPlayingAnim(ped, anim))
            {
                float animCurrentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, ped, dict, name);
                if (animCurrentTime >= actionTime)
                {
                    if (!ped.IsGettingUp && !ped.IsRagdoll)
                    {
                        return true;
                    }
                    return false;
                }
                else { return false; }
            }
            return false;
        }

        bool IsActionTimeDuringAnim(Ped ped, Animations anim)
        {
            string dict = getAnimDict(anim);
            string name = getAnimName(anim);
            float actionTime = getAnimActionStartTime(anim);
            float actionEndTime = getAnimActionEndTime(anim);

            if (isPlayingAnim(ped, anim))
            {
                float animCurrentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, ped, dict, name);
                if (animCurrentTime >= actionTime && animCurrentTime <= actionEndTime)
                {
                    if (!ped.IsGettingUp && !ped.IsRagdoll)
                    {
                        return true;
                    }
                    return false;
                }
                else { return false; }
            }
            return false;
        }

        void playGrabPedTask(Ped ped)
        {
            string dict = getAnimDict(Animations.GrabPed);
            string name = getAnimName(Animations.GrabPed);

            CancelAnim(ped);
            stopDefaultStealthTask(ped);
            stopTankAnimation(ped);
            if (!HasAnimDictLoaded(dict)) { Function.Call(Hash.REQUEST_ANIM_DICT, dict); }
            Function.Call(Hash.TASK_PLAY_ANIM, player, dict, name, 6f, 2f, 400, 1, 0.35f, false, false, false);
        }

        bool CanSwitchToHoldVehNow()
        {
            string dict = getAnimDict(Animations.PickupVeh);
            string name = getAnimName(Animations.PickupVeh);

            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, dict, name, 3);

            if (playingAnim)
            {
                float animCurrentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, player, dict, name);
                if (animCurrentTime >= 0.5329632f)
                {
                    if (!player.IsGettingUp && !player.IsRagdoll)
                    {
                        return true;
                    }
                    return false;
                }
                else { return false; }
            }
            return false;
        }

        void playBikeShieldThrowFrontBack(string animName)
        {
            /*
             * animNames for vehThrowDict:
             * Throw_0 : forward throw
             * Throw_90l : left throw
             * Throw_90r : right throw
             * Throw_180l : back left throw
             * Throw_180r : back right throw
             * */
            if (!HasAnimDictLoaded(vehThrowDict)) { Function.Call(Hash.REQUEST_ANIM_DICT, vehThrowDict); }
            Function.Call(Hash.TASK_PLAY_ANIM, player, vehThrowDict, animName, 8f, 3f, 500, 8, 0.05f, false, false, false);

        }

        bool isPlayingBikeShieldThrowFrontBack(string animName)
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, vehThrowDict, animName, 3);
            return playingAnim;
        }

        bool CanThrowShieldFrontBackFromMotoNow(string animName)
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, vehThrowDict, animName, 3);

            if (playingAnim)
            {
                float animCurrentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, player, vehThrowDict, animName);
                if (animCurrentTime >= 0.20f)
                {
                    return true;
                }
                else { return false; }
            }
            return false;
        }

        void playBikeShieldThrowLeftRight(string animName)
        {
            /* animNames for vehMeleeDict:
             * melee_l : left throw
             * melee_r : right throw
             * */
            if (!HasAnimDictLoaded(vehMeleeDict)) { Function.Call(Hash.REQUEST_ANIM_DICT, vehMeleeDict); }
            Function.Call(Hash.TASK_PLAY_ANIM, player, vehMeleeDict, animName, 8f, 3f, 500, 8, 0.05f, false, false, false);

        }

        bool isPlayingBikeShieldThrowLeftRight(string animName)
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, vehMeleeDict, animName, 3);
            return playingAnim;
        }

        bool CanThrowShieldLeftRightFromMotoNow(string animName)
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, vehMeleeDict, animName, 3);

            if (playingAnim)
            {
                float animCurrentTime = Function.Call<float>(Hash.GET_ENTITY_ANIM_CURRENT_TIME, player, vehMeleeDict, animName);
                if (animCurrentTime >= 0.16f)
                {
                    return true;
                }
                else { return false; }
            }
            return false;
        }

        void playTackleTask(PoweredUser user)
        {
            if ((!user.PoweredPed.IsGettingUp && !user.PoweredPed.IsRagdoll && !user.PoweredPed.IsInAir) || user.PoweredPed.HeightAboveGround > 2f && user.tackleCounter < 2)
            {
                if (user.PoweredPed.HeightAboveGround > 2f) { user.PoweredPed.Task.ClearAllImmediately(); user.PoweredPed.Velocity = Vector3.Zero; }
                else { CancelAnim(user.PoweredPed); }

                string animDict = getAnimDict(Animations.Tackle);
                string animName = getAnimName(Animations.Tackle);

                if (!HasAnimDictLoaded(animDict)) { Function.Call(Hash.REQUEST_ANIM_DICT, animDict); }
                Function.Call(Hash.TASK_PLAY_ANIM, user.PoweredPed, animDict, animName, 4f, 3f, 2000, 8/*32*/, 0.0f, false, false, false);

                //Function.Call(Hash.REQUEST_ANIM_DICT, "melee@unarmed@streamed_core");
                //Function.Call(Hash.TASK_PLAY_ANIM, player, "melee@unarmed@streamed_core", "plyr_takedown_front_elbow", 5.0f, 4.0f, 500, 8, 0.05f, false, false, false);

                //Function.Call(Hash.TASK_PLAY_ANIM, player, "melee@unarmed@streamed_core", "heavy_finishing_punch", 5.0f, 4.0f, 500, 54, 1.0f, false, false, false);
                //Function.Call(Hash.TASK_PLAY_ANIM, Ped, "doors@2handed", "r_hand_barge", 1.0f, 1.0f, 3000, 50, 1.0f, false, false, false);
                //Function.Call(Hash.TASK_PLAY_ANIM, Ped, "doors@unarmed", "r_hand_barge", 1.0f, 1.0f, 3000, 50, 1.0f, false, false, false);
            }
        }

        bool isPlayingTackleAnimation(Ped ped)
        {
            string animDict = getAnimDict(Animations.Tackle);
            string animName = getAnimName(Animations.Tackle);

            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, animDict, animName, 3);
            //bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, "melee@unarmed@streamed_core", "plyr_takedown_front_elbow", 3);

            if (!ped.IsGettingUp && !ped.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        void playReflectTask(Ped ped)
        {
            if (!ped.IsGettingUp && !ped.IsRagdoll)
            {
                string animDict = getAnimDict(Animations.Reflect);
                string animName = getAnimName(Animations.Reflect);

                CancelAnim(ped);

                if (!HasAnimDictLoaded(animDict)) { Function.Call(Hash.REQUEST_ANIM_DICT, animDict); }
                Function.Call(Hash.TASK_PLAY_ANIM, ped, animDict, animName, 8f, 1.5f, 350, 8, 0.12f, false, false, false);
                //Function.Call(Hash.TASK_PLAY_ANIM, Ped, "doors@2handed", "r_hand_barge", 1.0f, 1.0f, 3000, 50, 1.0f, false, false, false);
                //Function.Call(Hash.TASK_PLAY_ANIM, Ped, "doors@unarmed", "r_hand_barge", 1.0f, 1.0f, 3000, 50, 1.0f, false, false, false);
            }
        }

        bool isPlayingReflectAnimation(Ped ped)
        {
            string animDict = getAnimDict(Animations.Reflect);
            string animName = getAnimName(Animations.Reflect);

            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, animDict, animName, 3);

            if (!ped.IsGettingUp && !ped.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        void playTankAnimation(Ped ped)
        {
            if (!ped.IsGettingUp && !ped.IsRagdoll)
            {
                string animDict = getAnimDict(Animations.Tank);
                string animName = getAnimName(Animations.Tank);

                CancelAnim(ped);

                if (!HasAnimDictLoaded(animDict)) { Function.Call(Hash.REQUEST_ANIM_DICT, animDict); }
                Function.Call(Hash.TASK_PLAY_ANIM, ped, animDict, animName, 2f, 8f, 60000, 51, 0.0f, false, false, false);
            }
        }

        bool isPlayingTankAnimation(Ped ped)
        {
            string animDict = getAnimDict(Animations.Tank);
            string animName = getAnimName(Animations.Tank);

            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, animDict, animName, 3);

            if (!ped.IsGettingUp && !ped.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        void stopTankAnimation(Ped ped)
        {
            string animDict = getAnimDict(Animations.Tank);
            string animName = getAnimName(Animations.Tank);

            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, animDict, animName, 3);

            if (playingAnim)
            {
                //Function.Call(Hash.TASK_PLAY_ANIM, player, animDict, animName, 8f, 8f, 1, 50, 0.0f, false, false, false);
                Function.Call(Hash.STOP_ENTITY_ANIM, ped, animName, animDict, 1);
            }
        }

        void playRollForward(Ped ped)
        {
            if (/*!player.IsGettingUp &&*/ !ped.IsRagdoll)
            {
                if (ped.IsGettingUp)
                {
                    ped.Task.ClearAllImmediately();
                }
                if (!HasAnimDictLoaded("move_strafe@roll_fps")) { Function.Call(Hash.REQUEST_ANIM_DICT, "move_strafe@roll_fps"); }
                Function.Call(Hash.TASK_PLAY_ANIM, ped, "move_strafe@roll_fps", "combatroll_fwd_p1_00", 4.0f, 3.0f, -1, 0, 0.0f, false, false, false);
            }
        }

        bool isPlayingRollForward(Ped ped)
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, "move_strafe@roll_fps", "combatroll_fwd_p1_00", 3);

            if (!ped.IsGettingUp && !ped.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        void playRollForwardLeft()
        {
            if (/*!player.IsGettingUp && */ !player.IsRagdoll)
            {
                if (player.IsGettingUp)
                {
                    player.Task.ClearAllImmediately();
                }
                if (!HasAnimDictLoaded("move_strafe@roll_fps")) { Function.Call(Hash.REQUEST_ANIM_DICT, "move_strafe@roll_fps"); }
                Function.Call(Hash.TASK_PLAY_ANIM, player, "move_strafe@roll_fps", "combatroll_fwd_p1_-45", 4.0f, 3.0f, -1, 0, 0.0f, false, false, false);
            }
        }

        bool isPlayingRollForwardLeft()
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, "move_strafe@roll_fps", "combatroll_fwd_p1_-45", 3);

            if (!player.IsGettingUp && !player.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        void playRollForwardRight()
        {
            if (/*!player.IsGettingUp && */ !player.IsRagdoll)
            {
                if (player.IsGettingUp)
                {
                    player.Task.ClearAllImmediately();
                }
                if (!HasAnimDictLoaded("move_strafe@roll_fps")) { Function.Call(Hash.REQUEST_ANIM_DICT, "move_strafe@roll_fps"); }
                Function.Call(Hash.TASK_PLAY_ANIM, player, "move_strafe@roll_fps", "combatroll_fwd_p1_45", 4.0f, 3.0f, -1, 0, 0.0f, false, false, false);
            }
        }

        bool isPlayingRollForwardRight()
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, "move_strafe@roll_fps", "combatroll_fwd_p1_45", 3);

            if (!player.IsGettingUp && !player.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        void playRollBackward()
        {
            if (/*!player.IsGettingUp && */ !player.IsRagdoll)
            {
                if (player.IsGettingUp)
                {
                    player.Task.ClearAllImmediately();
                }
                if (!HasAnimDictLoaded("move_strafe@roll_fps")) { Function.Call(Hash.REQUEST_ANIM_DICT, "move_strafe@roll_fps"); }
                Function.Call(Hash.TASK_PLAY_ANIM, player, "move_strafe@roll_fps", "combatroll_bwd_p1_180", 4.0f, 3.0f, -1, 0, 0.0f, false, false, false);
            }
        }

        bool isPlayingRollBackward()
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, "move_strafe@roll_fps", "combatroll_bwd_p1_180", 3);

            if (!player.IsGettingUp && !player.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        void playRollBackwardLeft()
        {
            if (/*!player.IsGettingUp && */ !player.IsRagdoll)
            {
                if (player.IsGettingUp)
                {
                    player.Task.ClearAllImmediately();
                }
                if (!HasAnimDictLoaded("move_strafe@roll_fps")) { Function.Call(Hash.REQUEST_ANIM_DICT, "move_strafe@roll_fps"); }
                Function.Call(Hash.TASK_PLAY_ANIM, player, "move_strafe@roll_fps", "combatroll_bwd_p1_-135", 4.0f, 3.0f, -1, 0, 0.0f, false, false, false);
            }
        }

        bool isPlayingRollBackwardLeft()
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, "move_strafe@roll_fps", "combatroll_bwd_p1_-135", 3);

            if (!player.IsGettingUp && !player.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        void playRollBackwardRight()
        {
            if (/*!player.IsGettingUp && */ !player.IsRagdoll)
            {
                if (player.IsGettingUp)
                {
                    player.Task.ClearAllImmediately();
                }
                if (!HasAnimDictLoaded("move_strafe@roll_fps")) { Function.Call(Hash.REQUEST_ANIM_DICT, "move_strafe@roll_fps"); }
                Function.Call(Hash.TASK_PLAY_ANIM, player, "move_strafe@roll_fps", "combatroll_bwd_p1_135", 4.0f, 3.0f, -1, 0, 0.0f, false, false, false);
            }
        }

        bool isPlayingRollBackwardRight()
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, "move_strafe@roll_fps", "combatroll_bwd_p1_135", 3);

            if (!player.IsGettingUp && !player.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        void playRollRight()
        {
            if (/*!player.IsGettingUp && */ !player.IsRagdoll)
            {
                if (player.IsGettingUp)
                {
                    player.Task.ClearAllImmediately();
                }
                if (!HasAnimDictLoaded("move_strafe@roll_fps")) { Function.Call(Hash.REQUEST_ANIM_DICT, "move_strafe@roll_fps"); }
                Function.Call(Hash.TASK_PLAY_ANIM, player, "move_strafe@roll_fps", "combatroll_fwd_p1_90", 4.0f, 3.0f, -1, 0, 0.0f, false, false, false);
            }
        }

        bool isPlayingRollRight()
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, "move_strafe@roll_fps", "combatroll_fwd_p1_90", 3);

            if (!player.IsGettingUp && !player.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        void playRollLeft()
        {
            if (/*!player.IsGettingUp && */ !player.IsRagdoll)
            {
                if (player.IsGettingUp)
                {
                    player.Task.ClearAllImmediately();
                }
                if (!HasAnimDictLoaded("move_strafe@roll_fps")) { Function.Call(Hash.REQUEST_ANIM_DICT, "move_strafe@roll_fps"); }
                Function.Call(Hash.TASK_PLAY_ANIM, player, "move_strafe@roll_fps", "combatroll_bwd_p1_-90", 4.0f, 3.0f, -1, 0, 0.0f, false, false, false);
            }
        }

        bool isPlayingRollLeft()
        {
            bool playingAnim = Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, player, "move_strafe@roll_fps", "combatroll_bwd_p1_-90", 3);

            if (!player.IsGettingUp && !player.IsRagdoll)
            {
                return playingAnim;
            }
            return false;
        }

        bool isDoingRoll()
        {
            if (isPlayingRollForward(player) || isPlayingRollForwardLeft() || isPlayingRollForwardRight() || isPlayingRollLeft() || isPlayingRollRight() || isPlayingRollBackward() || isPlayingRollBackwardLeft() || isPlayingRollBackwardRight())
            {
                return true;
            }
            return false;
        }

        void PedFaceRotationExact(Ped ped, Vector3 endRotation, float lerpFloat)
        {
            Vector3 endVector = new Vector3(endRotation.X, endRotation.Y, MathUtil.Wrap(endRotation.Z, -180, 180));
            ped.Rotation = Vector3.Lerp(ped.Rotation, endVector, lerpFloat / 1f);
        }

        void PlayerFaceCameraZRot(float lerpFloat)
        {
            if (Function.Call<int>(Hash.GET_FOLLOW_PED_CAM_VIEW_MODE) != 4)
            {
                float Wrapped = MathUtil.Wrap(DirectionToHeading(GameplayCamera.Direction), -0f, 360f);
                player.Heading = MathUtil.Lerp(player.Heading, Wrapped, lerpFloat / 1f);
            }
        }

        void PropAchieveRotation(Prop prop, float lerpFloat, Vector3 endRotation)
        {
            prop.Rotation = Vector3.Lerp(prop.Rotation, endRotation, lerpFloat);
        }

        void ControlLerp(float lerpFloat, float divisor, out float lerpedValue)
        {
            lerpFloat += Game.LastFrameTime / divisor;
            if (lerpFloat > 1f) { lerpFloat = 1; }
            lerpedValue = lerpFloat;
        }

        float DistanceBetween(Vector3 origin, Vector3 destination)
        {
            return World.GetDistance(origin, destination);
        }

        bool isOnVehicle(Ped p)
        {
            return Function.Call<bool>(Hash.IS_PED_ON_VEHICLE, p);
        }

        bool isUsingMotorcyle()
        {
            if (player.IsInVehicle())
            {
                if (player.CurrentVehicle.Model.IsBike || player.CurrentVehicle.Model.IsBicycle)
                {
                    return true;
                }
                else { return false; }
            }
            return false;
        }

        bool isPlayerParachuting()
        {
            if (Function.Call<int>(Hash.GET_PED_PARACHUTE_STATE, player) >= 0)
            {
                return true;
            }
            return false;
        }

        bool IsPedInStealth(Ped p)
        {
            return Function.Call<bool>(Hash.GET_PED_STEALTH_MOVEMENT, p);
        }

        bool IsHoldingShield(Ped ped, string shieldname)
        {
            if (ped.Weapons.Current != null)
            {
                int shieldhash;
                Int32.TryParse(shieldname, out shieldhash);
                if ((Function.Call<int>(Hash.GET_SELECTED_PED_WEAPON, ped) == GetHashKey("WEAPON_" + shieldname)) || ((Function.Call<int>(Hash.GET_SELECTED_PED_WEAPON, ped) == shieldhash))) //if Cap Shield
                {
                    return true;
                }
            }
            return false;
        }

        bool isWeaponMelee(Ped ped)
        {
            if (Function.Call<bool>(Hash.IS_PED_ARMED, ped, 1) || ped.Weapons.Current.Hash == WeaponHash.Unarmed)
            {
                return true;
            }
            return false;
        }

        bool isAiming()
        {
            if (player.IsOnFoot) { return Game.IsControlPressed(2, GTA.Control.Aim); }
            else { return Game.IsControlPressed(2, GTA.Control.VehicleAim); }
        }

        bool justPressedThrowButton()
        {
            if (isKeyboard())
            {
                if (InputTimer < Game.GameTime)
                {
                    return Game.IsKeyPressed(throwKey);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return hasTappedControl(throwButton);
            }
        }

        bool isHoldingThrowButton()
        {
            if (isKeyboard())
            {
                return Game.IsKeyPressed(throwKey);
            }
            else
            {
                return Game.IsControlPressed(2, throwButton);
            }
        }

        bool justPressedThrowFromMoto()
        {
            if (isKeyboard())
            {
                return hasTappedControl(GTA.Control.PhoneSelect, true);
            }
            else
            {
                return hasTappedControl(GTA.Control.VehicleHandbrake);
            }
        }

        bool isHoldingThrowFromMoto()
        {
            if (isKeyboard())
            {
                return Game.IsControlPressed(2, GTA.Control.PhoneSelect);
            }
            else
            {
                return Game.IsControlPressed(2, GTA.Control.VehicleHandbrake);
            }
        }

        bool isHoldingReflectButton()
        {
            if (isKeyboard())
            {
                return Game.IsKeyPressed(reflectKey);
            }
            else
            {
                return Game.IsControlPressed(2, reflectButton);
            }
        }

        bool isHoldingTackleButton()
        {
            /*if (isKeyboard())
            {
                return Game.IsKeyPressed(tackleKey);
            }
            else
            {
                return Game.IsControlPressed(2, SpecialAttackButton);
            }*/

            return Game.IsControlPressed(2, SpecialAttackButton);
        }

        bool justPressedGrabButton()
        {
            if (isKeyboard())
            {
                if (InputTimer < Game.GameTime)
                {
                    return Game.IsKeyPressed(grabKey);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return hasTappedControl(grabButton);
            }
        }

        bool JustPressedMenuKeys()
        {
            if (InputTimer < Game.GameTime)
            {
                if (isKeyboard() && KeyPressed(KeyToggle1) && KeyPressed(KeyToggle2))
                {
                    return true;
                }
                else if (isHoldingControl(buttonToggle1) && isHoldingControl(buttonToggle2) && isHoldingControl(buttonToggle3))
                {
                    return true;
                }
            }
            return false;
        }

        bool hasTappedControl(GTA.Control control, bool acknowledgeKeyboard = false)
        {
            if (Game.IsControlJustPressed(2, control))
            {
                if (acknowledgeKeyboard)
                {
                    return true;
                }
                else
                {
                    if (!isKeyboard())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        bool isHoldingControl(GTA.Control control, bool acknowledgeKeyboard = false)
        {
            if (Game.IsControlPressed(2, control))
            {
                if (acknowledgeKeyboard)
                {
                    return true;
                }
                else
                {
                    if (!isKeyboard())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        bool KeyPressed(Keys key)
        {
            return isKeyboard() && Game.IsKeyPressed(key);
        }

        bool JustPressed(Keys key)
        {
            if (InputTimer < Game.GameTime)
            {
                if (Game.IsKeyPressed(key))
                {
                    return true;
                }
                return false;
            } return false;
        }

        void blockInput()
        {
            if (!isKeyboard())
            {
                Game.DisableControlThisFrame(2, throwButton);
                Game.DisableControlThisFrame(2, reflectButton);
                Game.DisableControlThisFrame(2, GTA.Control.Jump);
            }
        }

        int GetHashKey(string thing)
        {
            return Function.Call<int>(Hash.GET_HASH_KEY, thing);
        }

        void SetRelationshipOneSided(Relationship relationship, int feelingGroup, int receiverGroup)
        {
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, (int)relationship, feelingGroup, receiverGroup);
        }

        bool isPedCloseEnough(Ped attacker, Ped victim, float distance)
        {
            if (attacker.HasBone("IK_R_Hand"))
            {
                Vector3 damageSourceLoc = boneCoord(attacker, "IK_R_Hand");
                Vector3 damagedLoc = pedDamageLoc(victim, damageSourceLoc);
                float dist = DistanceBetween(damageSourceLoc, damagedLoc);

                if (damagedLoc != Vector3.Zero && dist <= distance)
                {
                    return true;
                }
                return false;
            }
            else if (attacker.HasBone("IK_L_Hand"))
            {
                Vector3 damageSourceLoc = boneCoord(attacker, "IK_L_Hand");
                Vector3 damagedLoc = pedDamageLoc(victim, damageSourceLoc);
                float dist = DistanceBetween(damageSourceLoc, damagedLoc);

                if (damagedLoc != Vector3.Zero && dist <= distance)
                {
                    return true;
                }
                return false;
            }
            else if (attacker.HasBone("IK_R_Foot"))
            {
                Vector3 damageSourceLoc = boneCoord(attacker, "IK_R_Foot");
                Vector3 damagedLoc = pedDamageLoc(victim, damageSourceLoc);
                float dist = DistanceBetween(damageSourceLoc, damagedLoc);

                if (damagedLoc != Vector3.Zero && dist <= distance)
                {
                    return true;
                }
                return false;
            }
            return false;
        }

        Ped getClosestPed(Vector3 coord, float radius, Ped PedToIgnore, bool ignoreDead = false)
        {
            Ped[] cPeds = World.GetNearbyPeds(coord, radius);
            Ped cPed = null;
            float minDist = 50f;

            if (cPeds != null && cPeds.Length > 0)
            {
                try
                {
                    foreach (Ped p in cPeds)
                    {
                        if (p == PedToIgnore)
                        {
                            continue; //if ped is a ped you want to ignore, skip it.
                        }
                        else
                        {
                            float dist = DistanceBetween(coord, p.Position);
                            if (dist <= radius && dist < minDist)
                            {
                                if (ignoreDead)
                                {
                                    if (p.IsDead)
                                    {
                                        if (cPed != null && cPed.Exists())
                                        {
                                            if (!cPed.IsDead)
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                cPed = p; //return the closest ped if it is not a ped you want to ignore and is not dead.
                                            }
                                        }
                                        else
                                        {
                                            cPed = p; //return the closest ped if it is not a ped you want to ignore and is not dead.
                                        }
                                    }
                                    else
                                    {
                                        minDist = dist;
                                        cPed = p; //return the closest ped if it is not a ped you want to ignore and is not dead.
                                    }
                                }
                                else
                                {
                                    minDist = dist;
                                    cPed = p; //return the closest ped if it is not a ped you want to ignore.
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return cPed; //return null if there are no peds.
        }

        Ped getClosestNonCompanionPed(Vector3 coord, float radius, Ped attacker, bool ignoreDead = false)
        {
            Ped[] cPeds = World.GetNearbyPeds(coord, radius);
            Ped cPed = null;
            float minDist = 50f;

            if (cPeds != null && cPeds.Length > 0)
            {
                try
                {
                    foreach (Ped p in cPeds)
                    {
                        Relationship relationship = World.GetRelationshipBetweenGroups(p.RelationshipGroup, attacker.RelationshipGroup);
                        if (p == attacker || relationship == Relationship.Companion/* || relationship == Relationship.Like || relationship == Relationship.Respect || relationship == Relationship.Pedestrians*/)
                        {
                            continue; //if ped is a ped you want to ignore, or is a companion/bodyguard, skip it.
                        }
                        else //only return a ped that is not the attacker, obviously
                        {
                            float dist = DistanceBetween(coord, p.Position);
                            if (dist <= radius && dist < minDist)
                            {
                                if (ignoreDead)
                                {
                                    if (p.IsDead)
                                    {
                                        if (cPed != null && cPed.Exists())
                                        {
                                            if (!cPed.IsDead) //if last closest ped is alive, don't return the dead ped
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                cPed = p; //return the closest ped if there are no alive peds around and the last closest ped was dead
                                            }
                                        }
                                        else
                                        {
                                            cPed = p; //return the closest ped if it is dead and if there was no closest ped selected in an earlier iteration
                                        }
                                    }
                                    else
                                    {
                                        minDist = dist;
                                        cPed = p; //return the closest ped if it is not a ped you want to ignore and is not dead.
                                    }
                                }
                                else
                                {
                                    minDist = dist;
                                    cPed = p; //return the closest ped if it is not a ped you want to ignore.
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return cPed; //return null if there are no peds.
        }

        Ped getClosestNonCompanionNonPedestrian(Vector3 coord, float radius, Ped attacker, bool ignoreDead = false)
        {
            Ped[] cPeds = World.GetNearbyPeds(coord, radius);
            Ped cPed = null;
            float minDist = 50f;

            if (cPeds != null && cPeds.Length > 0)
            {
                try
                {
                    foreach (Ped p in cPeds)
                    {
                        Relationship relationship = World.GetRelationshipBetweenGroups(attacker.RelationshipGroup, p.RelationshipGroup);
                        if (p == attacker || relationship == Relationship.Companion || relationship == Relationship.Pedestrians)
                        {
                            continue; //if ped is a ped you want to ignore, or is a companion/bodyguard, skip it.
                        }
                        else //only return a ped that is not the attacker, obviously
                        {
                            float dist = DistanceBetween(coord, p.Position);
                            if (dist <= radius && dist < minDist)
                            {
                                if (ignoreDead)
                                {
                                    if (p.IsDead)
                                    {
                                        if (cPed != null && cPed.Exists())
                                        {
                                            if (!cPed.IsDead) //if last closest ped is alive, don't return the dead ped
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                cPed = p; //return the closest ped if there are no alive peds around and the last closest ped was dead
                                            }
                                        }
                                        else
                                        {
                                            cPed = p; //return the closest ped if it is dead and if there was no closest ped selected in an earlier iteration
                                        }
                                    }
                                    else
                                    {
                                        minDist = dist;
                                        cPed = p; //return the closest ped if it is not a ped you want to ignore and is not dead.
                                    }
                                }
                                else
                                {
                                    minDist = dist;
                                    cPed = p; //return the closest ped if it is not a ped you want to ignore.
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return cPed; //return null if there are no peds.
        }

        Ped getClosestHatedPed(Vector3 coord, float radius, Ped attacker, bool ignoreDead = false)
        {
            Ped[] cPeds = World.GetNearbyPeds(coord, radius);
            Ped cPed = null;
            float minDist = 50f;

            if (cPeds != null && cPeds.Length > 0)
            {
                try
                {
                    foreach (Ped p in cPeds)
                    {
                        Relationship relationship = World.GetRelationshipBetweenGroups(p.RelationshipGroup, attacker.RelationshipGroup);
                        Relationship relationship2 = World.GetRelationshipBetweenGroups(attacker.RelationshipGroup, p.RelationshipGroup);
                        if (p == attacker || (relationship != Relationship.Hate && relationship != Relationship.Dislike && relationship2 != Relationship.Hate && relationship2 != Relationship.Dislike))
                        {
                            continue; //if ped is a ped you want to ignore, or the attacker does not hate the ped, skip it.
                        }
                        else //only return a ped that is not the attacker, obviously
                        {
                            float dist = DistanceBetween(coord, p.Position);
                            if (dist <= radius && dist < minDist)
                            {
                                if (ignoreDead)
                                {
                                    if (p.IsDead)
                                    {
                                        if (cPed != null && cPed.Exists())
                                        {
                                            if (!cPed.IsDead) //if last closest ped is alive, don't return the dead ped
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                cPed = p; //return the closest ped if there are no alive peds around and the last closest ped was dead
                                            }
                                        }
                                        else
                                        {
                                            cPed = p; //return the closest ped if it is dead and if there was no closest ped selected in an earlier iteration
                                        }
                                    }
                                    else
                                    {
                                        minDist = dist;
                                        cPed = p; //return the closest ped if it is not a ped you want to ignore and is not dead.
                                    }
                                }
                                else
                                {
                                    minDist = dist;
                                    cPed = p; //return the closest ped if it is not a ped you want to ignore.
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return cPed; //return null if there are no peds.
        }

        Vehicle getClosestVehicle(Vector3 pos, float radius, Vehicle VehToIgnore)
        {
            Vehicle[] cVehs = World.GetNearbyVehicles(pos, radius);
            Vehicle cVeh = null;
            float minDist = 100f;

            try
            {
                foreach (Vehicle v in cVehs)
                {
                    if (v.Exists() && v != VehToIgnore)
                    {
                        float dist = DistanceBetween(pos, v.Position);
                        if (dist <= radius && dist < minDist)
                        {
                            minDist = dist;
                            cVeh = v;
                        }
                    } else { continue; }
                }
            }
            catch { }
            return cVeh;
        }

        Entity getClosestInList(Vector3 pos, Entity[] EntListToSearch)
        {
            return World.GetClosest(pos, EntListToSearch);
        }

        Entity getClosest(Vector3 pos, Entity ent1, Entity ent2)
        {
            try
            {
                if (ent1 != null && ent1.Exists() && ent2 == null)
                {
                    return ent1;
                }
                else if (ent2 != null && ent2.Exists() && ent1 == null)
                {
                    return ent2;
                }
                else if (ent1 != null && ent2 != null && ent1.Exists() && ent2.Exists())
                {
                    if (DistanceBetween(pos, ent1.Position) < DistanceBetween(pos, ent2.Position))
                    {
                        return ent1;
                    }
                    else { return ent2; }
                }
            }
            catch { UI.ShowSubtitle("Nothin' to grab"); }
            return null;
        }

        Entity GetClosestObject(Vector3 pos, float radius)
        {
            Entity[] objects = World.GetNearbyProps(pos, radius);
            Entity closestObj = null;
            float minDist = 100f;
            
            foreach (Entity o in objects)
            {
                if (o.Exists())
                {
                    float dist = World.GetDistance(pos, o.Position);
                    if (dist <= radius && dist < minDist)
                    {
                        minDist = dist;
                        closestObj = o;
                    }
                    else
                    {
                        continue; //continue to next iteration
                    }
                }
                else { continue; }
            }
            return closestObj;
        }

        float GetModelVolume(Entity entity)
        {
            Vector3 dim = entity.Model.GetDimensions();
            float width = dim.X;
            float length = dim.Y;
            float height = dim.Z;
            /*units seem to be in millimeters
             So Volume would be in millimeters cubed*/
            return width * length * height;
        }

        float GetHeliVolume(Entity entity)
        {
            /*Small Helicopters have a width of about 2 mm*/
            Vector3 dim = entity.Model.GetDimensions();
            float width = 2f;
            float length = dim.Y;
            float height = dim.Z;

            return width * length * height;
        }

        bool CanVehicleBeLifted(Vehicle v, ProfileSetting profile)
        {
            /*INSURGENT volume is 56.06747 mm3, let's make that the smallest vehicle that Cap cannot throw, excluding Train and Helicopter class vehicles.*/

            switch (v.ClassType)
            {
                case VehicleClass.Trains: { return false; }
                case VehicleClass.Helicopters:
                    {
                        float volume = GetHeliVolume(v);
                        if (volume <= profile.MaxLiftSizeMMcubed)
                        {
                            return true;
                        }
                        else { return false; }
                    }
                default:
                    {
                        float volume = GetModelVolume(v);
                        if (volume <= profile.MaxLiftSizeMMcubed)
                        {
                            return true;
                        } else { return false; }
                    }
            }
        }

        float CalculatedVehicleThrowForceFinal(Vehicle v, ProfileSetting profile)
        {
            switch (v.ClassType)
            {
                case VehicleClass.Helicopters:
                    {
                        float volume = GetHeliVolume(v);
                        return CalculatedVehicleThrowForce(volume, profile);
                    }
                default:
                    {
                        float volume = GetModelVolume(v);
                        return CalculatedVehicleThrowForce(volume, profile);
                    }
            }
        }

        float CalculatedVehicleThrowForce(float volume, ProfileSetting profile)
        {
            float MaxSize = profile.MaxLiftSizeMMcubed;

            if (volume < MaxSize * 0.1f)
            {
                return 12f;
            }
            else if (volume < MaxSize * 0.2f)
            {
                return 11f;
            }
            else if (volume < MaxSize * 0.3f)
            {
                return 10f;
            }
            else if (volume < MaxSize * 0.4f)
            {
                return 9f;
            }
            else if (volume < MaxSize * 0.5f)
            {
                return 8f;
            }
            else if (volume < MaxSize * 0.6f)
            {
                return 7f;
            }
            else if (volume < MaxSize * 0.7f)
            {
                return 6f;
            }
            else if (volume < MaxSize * 0.8f)
            {
                return 5f;
            }
            else if (volume < MaxSize * 0.9f)
            {
                return 4f;
            }
            else if (volume < MaxSize)
            {
                return 3f;
            }
            else
            {
                return 2f;
            }
        }

        void rollController(PoweredUser user)
        {
            if (user.AssignedProfile.AllowCombatRoll)
            {
                if (!user.PoweredPed.IsInVehicle())
                {
                    if (user.PoweredPed.HeightAboveGround < 1.1f && !allowRoll)
                    {
                        allowRoll = true;
                    }

                    if (isDoingRoll())
                    {
                        Vector3 vel = user.PoweredPed.Velocity;
                        Vector3 fDir = user.PoweredPed.ForwardVector;
                        Vector3 rDir = user.PoweredPed.RightVector;
                        //bool SetSpeed = EntityCurrentSpeed(user.PoweredPed) <= 1094818202;

                        //if (SetSpeed)
                        //{
                            if (isPlayingRollForward(player))
                            {
                                user.PoweredPed.Velocity = new Vector3((fDir.X * user.AssignedProfile.RollSpeed), (fDir.Y * user.AssignedProfile.RollSpeed), vel.Z);
                            }
                            if (isPlayingRollBackward())
                            {
                                user.PoweredPed.Velocity = new Vector3((fDir.X * -user.AssignedProfile.RollSpeed), (fDir.Y * -user.AssignedProfile.RollSpeed), vel.Z);
                            }
                            if (isPlayingRollLeft())
                            {
                                user.PoweredPed.Velocity = new Vector3((rDir.X * -user.AssignedProfile.RollSpeed), (rDir.Y * -user.AssignedProfile.RollSpeed), vel.Z);
                            }
                            if (isPlayingRollRight())
                            {
                                user.PoweredPed.Velocity = new Vector3((rDir.X * user.AssignedProfile.RollSpeed), (rDir.Y * user.AssignedProfile.RollSpeed), vel.Z);
                            }
                            if (isPlayingRollForwardLeft())
                            {
                                Vector3 dir = VectorBetween(fDir, -rDir);
                                user.PoweredPed.Velocity = new Vector3((dir.X * user.AssignedProfile.RollSpeed), (dir.Y * user.AssignedProfile.RollSpeed), vel.Z);
                            }
                            if (isPlayingRollForwardRight())
                            {
                                Vector3 dir = VectorBetween(fDir, rDir);
                                user.PoweredPed.Velocity = new Vector3((dir.X * user.AssignedProfile.RollSpeed), (dir.Y * user.AssignedProfile.RollSpeed), vel.Z);
                            }
                            if (isPlayingRollBackwardLeft())
                            {
                                Vector3 dir = VectorBetween(-fDir, -rDir);
                                user.PoweredPed.Velocity = new Vector3((dir.X * user.AssignedProfile.RollSpeed), (dir.Y * user.AssignedProfile.RollSpeed), vel.Z);
                            }
                            if (isPlayingRollBackwardRight())
                            {
                                Vector3 dir = VectorBetween(-fDir, rDir);
                                user.PoweredPed.Velocity = new Vector3((dir.X * user.AssignedProfile.RollSpeed), (dir.Y * user.AssignedProfile.RollSpeed), vel.Z);
                            }
                        //}
                    }

                    if (Game.IsControlPressed(2, GTA.Control.Jump) && isAiming())
                    {
                        string input = MovementInput();

                        switch (input)
                        {
                            case "ForwardLeft":
                                {
                                    if (!isDoingRoll() && allowRoll) { playRollForwardLeft(); allowRoll = false; }
                                    return;
                                }
                            case "BackwardLeft":
                                {
                                    if (!isDoingRoll() && allowRoll) { playRollBackwardLeft(); allowRoll = false; }
                                    return;
                                }
                            case "ForwardRight":
                                {
                                    if (!isDoingRoll() && allowRoll) { playRollForwardRight(); allowRoll = false; }
                                    return;
                                }
                            case "BackwardRight":
                                {
                                    if (!isDoingRoll() && allowRoll) { playRollBackwardRight(); allowRoll = false; }
                                    return;
                                }
                            case "Left":
                                {
                                    if (!isDoingRoll() && allowRoll) { playRollLeft(); allowRoll = false; }
                                    return;
                                }
                            case "Right":
                                {
                                    if (!isDoingRoll() && allowRoll) { playRollRight(); allowRoll = false; }
                                    return;
                                }
                            case "Forward":
                                {
                                    if (!isDoingRoll() && allowRoll) { playRollForward(player); allowRoll = false; }
                                    return;
                                }
                            case "Backward":
                                {
                                    if (!isDoingRoll() && allowRoll) { playRollBackward(); allowRoll = false; }
                                    return;
                                }
                        }
                    }
                }
            }
        }

        string MovementInput()
        {
            float leftWeight = Game.GetControlNormal(2, GTA.Control.MoveLeftOnly);
            float rightWeight = Game.GetControlNormal(2, GTA.Control.MoveRightOnly);
            float upWeight = Game.GetControlNormal(2, GTA.Control.MoveUpOnly);
            float downWeigt = Game.GetControlNormal(2, GTA.Control.MoveDownOnly);

            if (leftWeight >= 0.7f)
            {
                if (upWeight >= 0.5f)
                {
                    return "ForwardLeft";
                }
                else if (downWeigt >= 0.5f)
                {
                    return "BackwardLeft";
                }
                else
                {
                    return "Left";
                }
            }

            if (rightWeight >= 0.7f)
            {
                if (upWeight >= 0.5f)
                {
                    return "ForwardRight";
                }
                else if (downWeigt >= 0.5f)
                {
                    return "BackwardRight";
                }
                else
                {
                    return "Right";
                }
            }

            if (upWeight >= 0.7f)
            {
                if (leftWeight >= 0.5f)
                {
                    return "ForwardLeft";
                }
                else if (rightWeight >= 0.5f)
                {
                    return "ForwardRight";
                }
                else
                {
                    return "Forward";
                }
            }

            if (downWeigt >= 0.7f)
            {
                if (leftWeight >= 0.5f)
                {
                    return "BackwardLeft";
                }
                else if (rightWeight >= 0.5f)
                {
                    return "BackwardRight";
                }
                else
                {
                    return "Backward";
                }
            }
            return "None";
        }

        bool isKeyboard()
        {
            return Game.CurrentInputMode == InputMode.MouseAndKeyboard;
        }

        void DrawReticle()
        {
            if (isAiming())
            {
                UI.ShowHudComponentThisFrame(HudComponent.Reticle);
            }
        }

        void DrawMarker(Entity entity)
        {
            float height = entity.Model.GetDimensions().Z;
            Vector3 markerPos = EntityIsAPed(entity) ? entity.Position + new Vector3(0, 0, (height / 2) + 0.2f) : entity.Position + new Vector3(0, 0, height);

            World.DrawMarker(MarkerType.UpsideDownCone, markerPos, Vector3.WorldUp, new Vector3(90, 0, 0), new Vector3(0.50f, 0.50f, 0.50f), Color.Crimson);
        }

        void DrawCustomText(string Message, float FontSize, int FontType, int Red, int Green, int Blue, int Alpha, float XPos, float YPos)
        {
            Function.Call(Hash.SET_TEXT_SCALE, 0.4f, FontSize);
            Function.Call(Hash.SET_TEXT_FONT, FontType);
            Function.Call(Hash.SET_TEXT_COLOUR, Red, Green, Blue, Alpha);
            Function.Call(Hash.SET_TEXT_DROPSHADOW, 0, 0, 0, 0, 0); //I don't know if this is needed, probably not.
            Function.Call(Hash._SET_TEXT_ENTRY, "STRING"); //Required, don't change this!
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, Message);
            Function.Call(Hash._DRAW_TEXT, XPos, YPos);
        }

        int boneIndexByName(Entity e, string b)
        {
            return Function.Call<int>(Hash.GET_ENTITY_BONE_INDEX_BY_NAME, e, b);
        }

        int boneIndexByID(Ped p, int ID)
        {
            return Function.Call<int>(Hash.GET_PED_BONE_INDEX, p, ID);
        }

        Vector3 boneCoord(Ped p, string b)
        {
            return p.GetBoneCoord(b);
        }

        string MotoThrowDirection()
        {
            Vector3 fDir = player.ForwardVector;
            Vector3 rDir = player.RightVector;

            Vector3 fCamDir = ForwardDirFromCam(1f);

            float camToFront = Vector3.Angle(fCamDir, fDir);
            float camToBack = Vector3.Angle(fCamDir, -fDir);
            float camToLeft = Vector3.Angle(fCamDir, -rDir);
            float camToRight = Vector3.Angle(fCamDir, rDir);

            float min = Math.Min(Math.Min(camToFront, camToBack), Math.Min(camToLeft, camToRight));

            /*
             * animNames for vehThrowDict:
             * Throw_0 : forward throw
             * Throw_90l : left throw
             * Throw_90r : right throw
             * Throw_180l : back left throw
             * Throw_180r : back right throw
             * animNames for vehMeleeDict:
             * melee_l : left throw
             * melee_r : right throw
             * */

            if (camToFront == min) { return "Throw_0"; }
            if (camToBack == min) { return "Throw_180r"; }
            if (camToLeft == min) { return "melee_l"; }
            if (camToRight == min) { return "melee_r"; }
            return "Throw_0";
        }

        bool ifMotoThrowIsLeftRight()
        {
            if (MotoThrowDirection() == "melee_l" || MotoThrowDirection() == "melee_r")
            {
                return true;
            }
            return false;
        }

        enum SpecialAttack
        {
            ChargingStar = 0,
            Shield2Ground
        }
        int SpecialAttackCounter = 0;

        void SpecialAttackSelector(Ped ped, ProfileSetting profile)
        {
            if (profile.AllowSpecialAttacks && IsHoldingShield(ped, profile.ShieldName))
            {
                if (hasTappedControl(SpecialSwitchButton) || JustPressed(SpecialSwitchKey))
                {
                    InputTimer = Game.GameTime + 250;

                    int length = Enum.GetNames(typeof(SpecialAttack)).Length;
                    if (SpecialAttackCounter < length - 1)
                    {
                        SpecialAttackCounter++;
                    }
                    else
                    {
                        SpecialAttackCounter = 0;
                    }

                    switch (SpecialAttackCounter)
                    {
                        case 0: { UI.ShowSubtitle("Charging Star"); return; }
                        case 1: { UI.ShowSubtitle("Ground Strike"); return; }
                    }
                }
            }
        }

        bool IsSelectedSpecialAttack(SpecialAttack specialAttack)
        {
            if (SpecialAttackCounter == (int)specialAttack)
            {
                return true;
            }
            return false;
        }

        enum DecalTypes //thanks to JulioNIB!
        {
            splatters_blood = 1010,
            splatters_blood_dir = 1015,
            splatters_blood_mist = 1017,
            splatters_mud = 1020,
            splatters_paint = 1030,
            splatters_water = 1040,
            splatters_water_hydrant = 1050,
            splatters_blood2 = 1110,
            weapImpact_metal = 4010,
            weapImpact_concrete = 4020,
            weapImpact_mattress = 4030,
            weapImpact_mud = 4032,
            weapImpact_wood = 4050,
            weapImpact_sand = 4053,
            weapImpact_cardboard = 4040,
            weapImpact_melee_glass = 4100,
            weapImpact_glass_blood = 4102,
            weapImpact_glass_blood2 = 4104,
            weapImpact_shotgun_paper = 4200,
            weapImpact_shotgun_mattress,
            weapImpact_shotgun_metal,
            weapImpact_shotgun_wood,
            weapImpact_shotgun_dirt,
            weapImpact_shotgun_tvscreen,
            weapImpact_shotgun_tvscreen2,
            weapImpact_shotgun_tvscreen3,
            weapImpact_melee_concrete = 4310,
            weapImpact_melee_wood = 4312,
            weapImpact_melee_metal = 4314,
            burn1 = 4421,
            burn2,
            burn3,
            burn4,
            burn5,
            bang_concrete_bang = 5000,
            bang_concrete_bang2,
            bang_bullet_bang,
            bang_bullet_bang2 = 5004,
            bang_glass = 5031,
            bang_glass2,
            solidPool_water = 9000,
            solidPool_blood,
            solidPool_oil,
            solidPool_petrol,
            solidPool_mud,
            porousPool_water,
            porousPool_blood,
            porousPool_oil,
            porousPool_petrol,
            porousPool_mud,
            porousPool_water_ped_drip,
            liquidTrail_water = 9050
        }

        void AddDecal(Vector3 pos, DecalTypes decalType, float width = 1.0f, float height = 1.0f, float rCoef = 0.1f, float gCoef = 0.1f, float bCoef = 0.1f, float opacity = 1.0f, float timeout = 20.0f)
        {
            Function.Call<int>(Hash.ADD_DECAL, (int)decalType, pos.X, pos.Y, GetGroundZ(pos), 0, 0, -1.0, 0, 1.0, 0, width, height, rCoef, gCoef, bCoef, opacity, timeout, 0, 0, 0);
        }

        float GetGroundZ(Vector3 pos) //thanks Jitnaught!
        {
            OutputArgument outArg = new OutputArgument();
            Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, pos.X, pos.Y, pos.Z, outArg, false);

            return outArg.GetResult<float>();
        }

        int GetUniqueRandomInt(int input, int rangeMin, int rangeMax, PoweredUser user)
        {
            int tempInt = user.rng.Next(rangeMin, rangeMax);
            if (input == tempInt)
            {
                return GetUniqueRandomInt(input, rangeMin, rangeMax, user);
            }
            else
            {
                return tempInt;
            }
        }

        static float GetRandomFloat(Random random, int minValue, int maxValue)
        {
            double mantissa = (random.NextDouble() * 2.0) - 1.0;
            double exponent = Math.Pow(2.0, random.Next(minValue, maxValue));
            return (float)(mantissa * exponent);
        }

        RaycastResult RaycastForwardFromCam()
        {
            try
            {
                Vector3 multiplied = new Vector3(RotationToDirection(getCamRot()).X * 4000.0f, RotationToDirection(getCamRot()).Y * 4000.0f, RotationToDirection(getCamRot()).Z * 4000.0f);
                RaycastResult ray = World.Raycast(getCamPos(), getCamPos() + multiplied, IntersectOptions.Everything, player);
                return ray;
            }
            catch
            {
                return new RaycastResult();
            }
        }

        RaycastResult RaycastForwardCapsule()
        {
            try
            {
                Vector3 multiplied = new Vector3(RotationToDirection(getCamRot()).X * 150.0f, RotationToDirection(getCamRot()).Y * 150.0f, RotationToDirection(getCamRot()).Z * 150.0f);
                //RaycastResult ray = World.Raycast(getCamPos(), getCamPos() + multiplied, IntersectOptions.Everything, player);
                RaycastResult ray = World.RaycastCapsule(getCamPos(), getCamPos() + multiplied, 5.0f, IntersectOptions.Everything, EntityToIgnoreInRay());
                return ray;
            }
            catch
            {
                return new RaycastResult();
            }
        }

        Entity EntityToIgnoreInRay()
        {
            if (player.IsInVehicle())
            {
                return player.CurrentVehicle;
            }
            else
            {
                return player;
            }
        }

        Vector3 ForwardDirFromCam(float multiplier)
        {
            Vector3 multiplied = new Vector3(RotationToDirection(getCamRot()).X * multiplier, RotationToDirection(getCamRot()).Y * multiplier, RotationToDirection(getCamRot()).Z * multiplier);
            return multiplied;
        }

        Vector3 RotationToDirection(Vector3 Rot)
        {
            try
            {
                float z = Rot.Z;
                float retz = z * 0.0174532924F;
                float x = Rot.X;
                float retx = x * 0.0174532924F;
                float absx = (float)System.Math.Abs(System.Math.Cos(retx));
                return new Vector3((float)-System.Math.Sin(retz) * absx, (float)System.Math.Cos(retz) * absx, (float)System.Math.Sin(retx));
            }
            catch
            {
                return new Vector3(0, 0, 0);
            }

        }

        Vector3 DirToRotTest(Vector3 Dir)
        {
            try
            {
                /*
                This is rotation to direction:
                rotx = 0.2
                retx = 0.2 *  0.0174532924 = 0.00349065848 //radians to degrees
                Cos(0.2 *  0.0174532924) = 0.9999939076578741
                absx = Abs(Cos(0.2 *  0.0174532924)) = 0.9999939076578741

                rotz = 0.3
                retz = 0.3 * 0.0174532924 = 0.00523598772 //radians to degrees

                dirx = -Sin(retz) * absx =  -0.005235963795437085 * 0.9999939076578741 = -0.0052359318961542843713968514009985 //-0.0052359318961542846 if rounded
                diry = Cos(retz) * absx = 0.9999862922476151 * 0.9999939076578741 = 0.999980199989
                dirz = Sin(retx) = 0.0052359637954370846088922220789420743196847079456760

                now this is direction to rotation:
                dirz = Sin(retx) =  0.0052359637954370846088922220789420743196847079456760
                num1 = retx = Asin(dirz)
                rotx = num1 / 0.0174532924

                num2 = absx = Abs(Cos(num1)
                num3 = diry / num2 = Cos(retz)
                num4 = retz = Acos(num3)
                rotz = num4 / 0.0174532924

                roty?


                */
                
                float trueRotZ;

                float dirz = Dir.Z;
                float num1 = (float)Math.Asin(dirz);
                float rotx = num1 / 0.0174532924f;

                float dirx = Dir.X;
                float num2 = (float)Math.Cos(num1);
                float num3 = dirx / num2;
                float num4 = (float)Math.Asin(-num3);
                float rotz1 = num4 / 0.0174532924f;

                float diry = Dir.Y;
                float num5 = (float)Math.Cos(num1);
                float num6 = diry / num5;
                float num7 = (float)Math.Acos(num6);
                float rotz2 = num7 / 0.0174532924f;

                if (rotz1 > 0)
                {
                    if (rotz2 < 90)
                    {
                        trueRotZ = rotz1;
                    }
                    else
                    {
                        trueRotZ = rotz2;
                    }
                }
                else
                {
                    if (rotz2 < 90)
                    {
                        trueRotZ = rotz1;
                    }
                    else
                    {
                        trueRotZ = -rotz2;
                    }
                }

                return new Vector3(rotx, 0, trueRotZ);
            }
            catch
            {
                return Vector3.Zero;
            }
        }

        Vector3 DirectionToRotation(Vector3 dir, float roll)
        {
            dir = Vector3.Normalize(dir);
            Vector3 rotval;
            rotval.Z = -MathUtil.RadiansToDegrees((float)Math.Atan2(dir.X, dir.Y));
            Vector3 rotpos = Vector3.Normalize(new Vector3(dir.Z, new Vector3(dir.X, dir.Y, 0.0f).Length(), 0.0f));
            rotval.X = MathUtil.RadiansToDegrees((float)Math.Atan2(rotpos.X, rotpos.Y));
            rotval.Y = roll;
            return rotval;
        }

        Vector3 VectorBetween(Vector3 vec1, Vector3 vec2)
        {
            return (vec1 + vec2).Normalized;
        }

        float Deg2Rad(float _deg)
        {
            //https://forums.gta5-mods.com/post/32103 Thanks Lee!
            double Radian = (_deg * Math.PI / 180);
            return (float)Radian;
        }

        float RadianToDegree(float angle)
        {
            //http://www.vcskicks.com/csharp_net_angles.php
            return (float)(angle * (180.0 / Math.PI));
        }

        float DirectionToHeading(Vector3 dir)
        {
            //https://tohjo.eu/dapkcuf/citizenmp/blob/c8710f0a3cf076c7f2a8fcbb22ed2902116f4f4c/client/clrcore/Math/GameMath.cs
            dir.Z = 0.0f;
            dir.Normalize();
            return RadianToDegree((float)-Math.Atan2(dir.X, dir.Y));
        }

        Vector3 getCamRot()
        {
            try
            {
                return Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_ROT, 0);
            }
            catch
            {

            }
            return new Vector3(0, 0, 0);
        }

        Vector3 getCamPos()
        {
            try
            {
                return Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_COORD);
            }
            catch
            {

            }
            return new Vector3(0, 0, 0);
        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
        }

        void OnKeyUp(object sender, KeyEventArgs e)
        {
        }

        void checkBonesTest()
        {
            if (Game.IsControlPressed(2, GTA.Control.Jump))
            {
                //foreach (Bone b in ExtremitiesBoneList)
                //{
                //    //Vector3 pos = boneCoord(player, b);
                //    Vector3 pos = player.GetBoneCoord("IK_R_Hand").Around(1f);
                //    Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "scr_carsteal4");
                //    Function.Call(Hash._SET_PTFX_ASSET_NEXT_CALL, "scr_carsteal4");
                //    Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_AT_COORD, "scr_carsteal5_car_muzzle_flash", pos.X, pos.Y, pos.Z, 0, 0, 0, 0.5f, false, false, false);
                //}

                /*if (!propIsCreated)
                {
                    weapProp = World.CreateProp(holdWeap, boneCoord(player, "IK_R_Hand"), false, false);
                    propIsCreated = true;
                }
                else
                {
                    weapProp.Rotation = new Vector3(player.Rotation.X + rotateX, player.Rotation.Y + rotateY, player.Rotation.Z + rotateZ);

                    //weapProp.AttachTo(player, boneIndexByName(player, Bone.IK_Head), new Vector3(0, 0, 0), new Vector3(player.Rotation.X + 90f, player.Rotation.Y + 10f, player.Heading + 90f));

                    //Vector3 fDir = player.ForwardVector;
                    //weapProp.AttachTo(player, boneIndexByName(player, Bone.IK_Head), new Vector3(0, 0, 0), fDir + new Vector3(90, 180, 0));
                }
                */
                /*choosePropVector();

                try
                {
                    AttachBallisticShieldsToRHand(ballShield1, new Vector3(offsetX, offsetY, offsetZ), new Vector3(rotateX, rotateY, rotateZ));
                }
                catch { }*/
            }
            else
            {
                //propIsCreated = false;
            }
        }

        void choosePropVector()
        {
            UI.ShowSubtitle(offsetX + ", " + offsetY + ", " + offsetZ + " - " + rotateX + ", " + rotateY + ", " + rotateZ, 20000);
            if (Game.IsKeyPressed(Keys.NumPad5))
            {
                if (Game.IsKeyPressed(Keys.NumPad4))
                {
                    offsetX = (float)Math.Round(offsetX + increment, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad6))
                {
                    offsetX = (float)Math.Round(offsetX - increment, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad2))
                {
                    offsetY = (float)Math.Round(offsetY + increment, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad8))
                {
                    offsetY = (float)Math.Round(offsetY - increment, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad1))
                {
                    offsetZ = (float)Math.Round(offsetZ + increment, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad7))
                {
                    offsetZ = (float)Math.Round(offsetZ - increment, 3);
                }
            }
            else
            {
                if (Game.IsKeyPressed(Keys.NumPad4))
                {
                    rotateX = (float)Math.Round(rotateX + 1, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad6))
                {
                    rotateX = (float)Math.Round(rotateX - 1, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad2))
                {
                    rotateY = (float)Math.Round(rotateY + 1, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad8))
                {
                    rotateY = (float)Math.Round(rotateY - 1, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad1))
                {
                    rotateZ = (float)Math.Round(rotateZ + 1, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad7))
                {
                    rotateZ = (float)Math.Round(rotateZ - 1, 3);
                }
            }
        }

        void chooseBloodFXVector()
        {
            UI.ShowSubtitle(offsetX + ", " + offsetY + ", " + offsetZ + " - " + bloodRotX + ", " + bloodRotY + ", " + bloodRotZ, 20000);
            if (Game.IsKeyPressed(Keys.NumPad5))
            {
                if (Game.IsKeyPressed(Keys.NumPad4))
                {
                    offsetX = (float)Math.Round(offsetX + increment, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad6))
                {
                    offsetX = (float)Math.Round(offsetX - increment, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad2))
                {
                    offsetY = (float)Math.Round(offsetY + increment, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad8))
                {
                    offsetY = (float)Math.Round(offsetY - increment, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad1))
                {
                    offsetZ = (float)Math.Round(offsetZ + increment, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad7))
                {
                    offsetZ = (float)Math.Round(offsetZ - increment, 3);
                }
            }
            else
            {
                if (Game.IsKeyPressed(Keys.NumPad4))
                {
                    bloodRotX = (float)Math.Round(bloodRotX + 1, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad6))
                {
                    bloodRotX = (float)Math.Round(bloodRotX - 1, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad2))
                {
                    bloodRotY = (float)Math.Round(bloodRotY + 1, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad8))
                {
                    bloodRotY = (float)Math.Round(bloodRotY - 1, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad1))
                {
                    bloodRotZ = (float)Math.Round(bloodRotZ + 1, 3);
                }
                if (Game.IsKeyPressed(Keys.NumPad7))
                {
                    bloodRotZ = (float)Math.Round(bloodRotZ - 1, 3);
                }
            }
        }
    }

    public class ProfileSetting
    {
        public string ProfileName { get; set; }
        public string ShieldName { get; set; }
        public float InitialThowForce { get; set; }
        public int PShieldDamage { get; set; }
        public int VShieldDamage { get; set; }
        public bool QuickReturnShield { get; set; }
        public float ReturnInterval { get; set; }
        public bool AllowShieldCurve { get; set; }
        public float CurveForce { get; set; }
        public bool BackhandThrow { get; set; }

        public bool AllowSpecialAttacks { get; set; }
        public int PChargeDamage { get; set; }
        public int VChargeDamage { get; set; }
        public float StrikingPowerMultiplier { get; set; }

        public bool AllowCustomMelee { get; set; }
        public int MaxHealth { get; set; }
        public int RegenHealthAmount { get; set; }
        public float RegenInterval { get; set; }
        public bool AllowReflect { get; set; }
        public bool AllowTank { get; set; }
        public bool AllowSuperRun { get; set; }
        public float RunAnimationMultiplier { get; set; }
        public float SuperRunningVelocity { get; set; }
        public float SuperSprintingVelocity { get; set; }
        public bool AllowSuperJump { get; set; }
        public float JumpForwardForce { get; set; }
        public float JumpUpwardForce { get; set; }
        public float SafeFallHeight { get; set; }
        public bool AllowCombatRoll { get; set; }
        public float RollSpeed { get; set; }
        public bool AllowLifting { get; set; }
        public float MaxLiftSizeMMcubed { get; set; }

        public string PedModelToUse { get; set; }
        public bool AllowSlowMoAim { get; set; }
        public bool AllowShieldOnBack { get; set; }
        public Vector3 BackShieldPos { get; set; }
        public Vector3 BackShieldRot { get; set; }
        public float FxRed { get; set; }
        public float FxGreen { get; set; }
        public float FxBlue { get; set; }
        public bool FixCompatibilityWithFlash { get; set; }

        public ProfileSetting(string filename)
        {
            ProfileName = filename;
        }

        public void SetBackShieldPos(float x, float y, float z)
        {
            BackShieldPos = new Vector3(x, y, z);
        }

        public void SetBackShieldRot(float x, float y, float z)
        {
            BackShieldRot = new Vector3(x, y, z);
        }
    }

    class PoweredUser
    {
        public ProfileSetting AssignedProfile;
        public Ped PoweredPed;
        public Entity Target;
        public bool IsEnemy;
        public Blip BlipID;
        int ActionTime = 0;

        public List<Entity> chargedEntities = new List<Entity>();
        public List<Ped> shieldedPeds = new List<Ped>();
        public List<Vehicle> shieldedVehs = new List<Vehicle>();

        public Model holdWeap;
        public Prop weapProp;
        public WeaponHash CapShield;
        public Random rng = new Random();
        public int rInt { get; set; }

        public Vector3 returnDir;
        public Vector3 shieldRotation = new Vector3(-10, -10, 0);
        public float shieldRotationLerp;
        public Vector3 tackleDir;
        public Vector3 currentPos;
        public bool shieldIsThrown;
        public bool isHoming;
        public bool firstHit;
        public bool ShieldCanReturn;
        public int pickupTimer;
        public int autoReturnTimer;
        public int autoReturnTimeout;
        public int reflectTimer;
        public int noRagdollTimer;
        public int allowDamageTimer; //will need to make on of these for each Cap ped when adding support for AI.
        public int allowDamageWaitInterval = 500; //how long ped must wait after damaging something before clearing damaged entity lists (which will allow the same entities to be damaged again).
        public int allowMeleeTimer;
        public int allowMeleeWaitInterval = 750;
        public float ShieldThrowLerp;
        public float RunForceLerp;
        public float tackleValue;
        public float shieldSpin = 0;
        public int jumpTimer;
        public bool isJumping;
        public bool jumpNow;
        public bool isSturdy;
        public bool isTackleKey;
        public bool setTackleProperties;
        public bool stopTackleNow;
        public int tackleCounter;
        public bool noPlayerRagdoll;
        public int ShieldFX;
        public int RegenTimer;
        public bool hasMadeImpact;
        public int SuperSpeedTimer;
        public string MeleeDamageBoneSource;

        public PoweredUser(Ped poweredPed)
        {
            PoweredPed = poweredPed;
        }

        public bool CanDoAction()
        {
            return ActionTime < Game.GameTime;
        }

        public void SetActionWait(int ms)
        {
            ActionTime = Game.GameTime + ms;
        }

        public void SetReturnDir(float x, float y, float z)
        {
            returnDir = new Vector3(x, y, z);
        }

        public void SetShieldRotation(float x, float y, float z)
        {
            shieldRotation = new Vector3(x, y, z);
        }

        public void SetTackleDir(float x, float y, float z)
        {
            tackleDir = new Vector3(x, y, z);
        }

        public void SetCurrentPos(float x, float y, float z)
        {
            currentPos = new Vector3(x, y, z);
        }
    }
}